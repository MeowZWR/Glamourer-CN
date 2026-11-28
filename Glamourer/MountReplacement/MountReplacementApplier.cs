using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Glamourer.Interop.Penumbra;
using Luna;
using Lumina.Excel.Sheets;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Penumbra.Api.Wrappers;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Glamourer.MountReplacement;

public sealed unsafe class MountReplacementApplier : IRequiredService, IDisposable
{
    private const uint CompanyChocoboMountId = 1;
    private const int  TemporaryModPriority  = 99;
    private const string TemporaryModTag = "GlamourerMountReplacement";

    private const string SetRideBgmSig =
        "48 8B 05 ?? ?? ?? ?? 48 85 C0 74 ?? 83 78 08 06 76 ?? 48 8B 80 C0 00 00 00 66 89 88 4C 02 00 00";

    private static readonly string[] RiderPapNames =
    [
        "mount.pap", "mount01.pap", "mount02.pap", "mount03.pap", "mount04.pap", "mount05.pap", "mount06.pap", "mount07.pap", "mount08.pap",
    ];

    private readonly record struct RiderPap(string Race, string Name, string Path);

    private readonly MountReplacementManager _manager;
    private readonly PenumbraSubscriber      _penumbra;
    private readonly ActorManager            _actors;
    private readonly ActorObjectManager      _objects;
    private readonly IDataManager       _gameData;
    private readonly IClientState       _clientState;
    private readonly IFramework         _framework;
    private readonly HookManager        _hooks;
    private readonly AddTemporaryMod    _addTemporaryMod;
    private readonly RemoveTemporaryMod _removeTemporaryMod;
    private readonly EventSubscriber    _penumbraInitialized;
    private readonly Task<Hook<SetRideBgm>> _rideBgmHook;

    private readonly Dictionary<(byte Type, ushort Model), RiderPap[]> _riderPaps = [];
    private readonly PapLoadTable?                                     _papLoadTable;

    private Guid  _modCollection;
    private bool  _hasSwaps;
    private nint   _scaledObject;
    private float  _baseScale;
    private float  _writtenScale;
    private ushort _baseSkeleton;
    private ushort _swapSkeleton;
    private int    _refreshCooldown;
    private uint   _shownTarget;

    private delegate nint SetRideBgm(ushort bgmId);

    public MountReplacementApplier(MountReplacementManager manager, PenumbraSubscriber penumbra, ActorManager actors,
        ActorObjectManager objects, IDataManager gameData, IClientState clientState, IFramework framework, HookManager hooks,
        IDalamudPluginInterface pluginInterface)
    {
        _manager     = manager;
        _penumbra    = penumbra;
        _actors      = actors;
        _objects     = objects;
        _gameData    = gameData;
        _clientState = clientState;
        _framework   = framework;
        _hooks       = hooks;
        _rideBgmHook = hooks.CreateHook<SetRideBgm>("MountReplacementRideBgm", SetRideBgmSig, SetRideBgmDetour, true)!;
        _papLoadTable        = PapLoadTable.Read(gameData.GetFile("chara/xls/animation/papLoadTable.plt")?.Data);
        _addTemporaryMod     = new AddTemporaryMod(pluginInterface);
        _removeTemporaryMod  = new RemoveTemporaryMod(pluginInterface);
        _penumbraInitialized = Initialized.Subscriber(pluginInterface, UpdateAnimationSwaps);
        _penumbra.GameState.CreatingCharacterBase += OnCreatingCharacterBase;
        _manager.Changed                          += OnChanged;
        _clientState.Login                        += UpdateAnimationSwaps;
        _framework.Update                         += OnFramework;
        if (TryGetDrawnSkeleton(out var skeleton))
            _swapSkeleton = skeleton;
        UpdateAnimationSwaps();
        _shownTarget = CurrentShownTarget();
    }

    public void Dispose()
    {
        _penumbra.GameState.CreatingCharacterBase -= OnCreatingCharacterBase;
        _manager.Changed                          -= OnChanged;
        _clientState.Login                        -= UpdateAnimationSwaps;
        _framework.Update                         -= OnFramework;
        _hooks.DisposeHook("MountReplacementRideBgm");
        _penumbraInitialized.Dispose();
        ClearAnimationSwaps();
    }

    private void OnCreatingCharacterBase(in CreatingCharacterBaseArguments args)
    {
        if (!_manager.Enabled)
            return;

        var identifier = ((Actor)args.GameObject).GetIdentifier(_actors);
        if (!_manager.IsLocalPlayerMount(identifier))
            return;

        if (!_manager.TryGetTarget(identifier.DataId.Id, out var target))
        {
            _shownTarget = 0;
            return;
        }

        _shownTarget         = target.Id;
        *(uint*)args.ModelId = target.ModelChara;
        _manager.Mounts.TryGetValue(identifier.DataId.Id, out var source);
        if (source != null)
            ApplyMountScale(args.GameObject, source, target);
        var equip = (CharacterArmor*)args.EquipData;
        if (target.Id is CompanyChocoboMountId)
        {
            var (head, body, legs) = GetBarding();
            equip[0] = head;
            equip[1] = body;
            equip[3] = target.Legs.With((StainId)UIState.Instance()->Buddy.CompanionInfo.CurrentColorStainId);
            equip[4] = legs;
        }
        else if (target.HasEquipment || source == null || source.ModelChara != target.ModelChara)
        {
            equip[0] = target.Head;
            equip[1] = target.Body;
            equip[3] = target.Legs;
            equip[4] = target.Feet;
        }
    }

    private nint SetRideBgmDetour(ushort bgmId)
    {
        if (_manager.Enabled)
        {
            var player = _objects.Player;
            if (player.IsCharacter)
            {
                var mountId = (uint)player.AsCharacter->Mount.MountId;
                if (mountId != 0
                 && _manager.Mounts.TryGetValue(mountId, out var source)
                 && bgmId == source.RideBgmId
                 && _manager.TryGetTarget(mountId, out var target)
                 && target.RideBgmId != 0
                 && target.RideBgmId != bgmId)
                    bgmId = target.RideBgmId;
            }
        }

        return _rideBgmHook.Result.Original(bgmId);
    }

    private (CharacterArmor Head, CharacterArmor Body, CharacterArmor Legs) GetBarding()
    {
        var ids   = UIState.Instance()->Buddy.CompanionInfo.BuddyEquipRowIds;
        var sheet = _gameData.GetExcelSheet<BuddyEquip>();
        var head  = sheet.TryGetRow(ids[0], out var headRow) ? headRow.ModelTop : 0;
        var body  = sheet.TryGetRow(ids[1], out var bodyRow) ? bodyRow.ModelBody : 0;
        var legs  = sheet.TryGetRow(ids[2], out var legsRow) ? legsRow.ModelLegs : 0;
        return (MountReplacementManager.ToArmor(head), MountReplacementManager.ToArmor(body), MountReplacementManager.ToArmor(legs));
    }

    private void OnChanged()
    {
        UpdateAnimationSwaps();
        if (!TryGetCurrentMount(out var mount, out var mountId))
            return;

        var next = _manager.TryGetTarget(mountId, out var target) ? target.Id : 0;
        if (next == _shownTarget)
            return;

        _penumbra.Redraw(mount.Index.Index);
        var player = _objects.Player;
        if (player.Valid)
            _penumbra.Redraw(player.Index.Index);
        _shownTarget = next;
    }

    private void OnFramework(IFramework framework)
    {
        if (_refreshCooldown > 0)
        {
            --_refreshCooldown;
            return;
        }

        if (!_manager.Enabled || !TryGetDrawnSkeleton(out var skeleton) || skeleton == _swapSkeleton)
            return;

        var refresh = _swapSkeleton != 0;
        _swapSkeleton = skeleton;
        UpdateAnimationSwaps();
        if (!refresh || !TryGetCurrentMount(out var mount, out _))
            return;

        _refreshCooldown = 30;
        _penumbra.Redraw(mount.Index.Index);
        var player = _objects.Player;
        if (player.Valid)
            _penumbra.Redraw(player.Index.Index);
    }

    private uint CurrentShownTarget()
        => TryGetCurrentMount(out _, out var mountId) && _manager.TryGetTarget(mountId, out var target) ? target.Id : 0;

    private bool TryGetCurrentMount(out Actor mount, out uint mountId)
    {
        mount   = _objects.Objects[1];
        mountId = 0;
        if (!mount.Valid)
            return false;

        var identifier = mount.GetIdentifier(_actors);
        if (!_manager.IsLocalPlayerMount(identifier))
            return false;

        mountId = identifier.DataId.Id;
        return true;
    }

    private void UpdateAnimationSwaps()
    {
        if (TryGetDrawnSkeleton(out var drawn))
            _swapSkeleton = drawn;
        else if (_swapSkeleton == 0 && TryGetPlayerSkeleton(out var fallback))
            _swapSkeleton = fallback;

        var swaps = new Dictionary<string, string>();
        if (_manager.Enabled)
        {
            foreach (var rule in _manager.Rules)
            {
                if (!rule.Enabled || rule.Source != 0 || !_manager.Mounts.TryGetValue(rule.Target, out var target))
                    continue;

                foreach (var source in _manager.Mounts.Values)
                {
                    if (source.Id != target.Id)
                {
                    AddAnimationSwaps(swaps, source, target);
                    AddSoundSwaps(swaps, source, target);
                }
                }
            }

            foreach (var rule in _manager.Rules)
            {
                if (!rule.Enabled || rule.Source == 0
                 || !_manager.Mounts.TryGetValue(rule.Source, out var source)
                 || !_manager.Mounts.TryGetValue(rule.Target, out var target)
                 || source.Id == target.Id)
                    continue;

                AddAnimationSwaps(swaps, source, target);
                AddSoundSwaps(swaps, source, target);
            }
        }

        SetAnimationSwaps(swaps);
    }

    private void AddAnimationSwaps(Dictionary<string, string> swaps, MountInfo source, MountInfo target)
    {
        if (_papLoadTable == null || _swapSkeleton == 0)
            return;

        var skeleton = _swapSkeleton;

        var sourcePaps = RiderPaps(source);
        var targetPaps = RiderPaps(target);
        if (sourcePaps.Length == 0 || targetPaps.Length == 0)
            return;

        foreach (var name in RiderPapNames)
        {
            if (!HasPap(sourcePaps, name))
                continue;

            var targetName = HasPap(targetPaps, name) ? name
                : HasPap(targetPaps, "mount.pap") ? "mount.pap"
                : name == "mount.pap" && HasPap(targetPaps, "mount01.pap") ? "mount01.pap"
                : null;
            if (targetName == null)
                continue;

            var (sourceSkeleton, sourceDir) = _papLoadTable.Resolve(skeleton, AnimationDirectory(source), name);
            var sourcePath = PapPath(sourceSkeleton, sourceDir, name);
            if (!_gameData.FileExists(sourcePath))
                continue;

            var (targetSkeleton, targetDir) = _papLoadTable.Resolve(skeleton, AnimationDirectory(target), targetName);
            var dest = ExistingPap(targetSkeleton, targetDir, targetName);
            if (dest != null && dest != sourcePath)
                swaps[sourcePath] = dest;
        }
    }

    private string? ExistingPap(ushort skeleton, string directory, string name)
    {
        var path = PapPath(skeleton, directory, name);
        if (_gameData.FileExists(path))
            return path;

        if (name != "mount.pap")
        {
            path = PapPath(skeleton, directory, "mount.pap");
            if (_gameData.FileExists(path))
                return path;
        }
        else
        {
            path = PapPath(skeleton, directory, "mount01.pap");
            if (_gameData.FileExists(path))
                return path;
        }

        return null;
    }

    private void ApplyMountScale(nint gameObject, MountInfo source, MountInfo target)
    {
        var skeleton = _swapSkeleton;
        if (skeleton == 0 && !TryGetPlayerSkeleton(out skeleton))
            return;

        var index = skeleton / 100 - 1;
        if ((uint)index >= (uint)source.RaceScales.Length || index >= target.RaceScales.Length)
            return;

        var targetFactor = (float)target.RaceScales[index];
        var mount        = (GameObject*)gameObject;
        if (_baseSkeleton == 0 || _scaledObject != gameObject || Math.Abs(mount->Scale - _writtenScale) > 0.0001f)
        {
            _baseScale     = mount->Scale;
            _baseSkeleton  = skeleton;
            _scaledObject  = gameObject;
        }

        var baseIndex = _baseSkeleton / 100 - 1;
        if ((uint)baseIndex >= (uint)source.RaceScales.Length)
            return;

        var baseFactor = (float)source.RaceScales[baseIndex];
        if (baseFactor <= 0 || targetFactor <= 0)
            return;

        var next = _baseScale * (targetFactor / baseFactor);
        if (Math.Abs(mount->Scale - next) <= 0.0001f)
        {
            _writtenScale = mount->Scale;
            return;
        }

        mount->Scale  = next;
        _writtenScale = next;
    }

    private bool TryGetDrawnSkeleton(out ushort skeleton)
    {
        skeleton = 0;
        var player = _objects.Player;
        if (!player.IsCharacter || !player.Model.IsHuman)
            return false;

        var customize = player.Model.AsHuman->Customize;
        skeleton = SkeletonOf(customize.Race, customize.Tribe, customize.Sex);
        return true;
    }

    private bool TryGetPlayerSkeleton(out ushort skeleton)
    {
        if (TryGetDrawnSkeleton(out skeleton))
            return true;

        skeleton = 0;
        var player = _objects.Player;
        if (!player.IsCharacter)
            return false;

        var customize = player.AsCharacter->DrawData.CustomizeData;
        skeleton = SkeletonOf(customize.Race, customize.Tribe, customize.Sex);
        return true;
    }

    private static ushort SkeletonOf(byte race, byte tribe, byte sex)
    {
        var female = sex != 0;
        return race switch
        {
            1 => (ushort)(tribe == 2 ? (female ? 401 : 301) : (female ? 201 : 101)),
            2 => (ushort)(female ? 601 : 501),
            3 => (ushort)(female ? 1201 : 1101),
            4 => (ushort)(female ? 801 : 701),
            5 => (ushort)(female ? 1001 : 901),
            6 => (ushort)(female ? 1401 : 1301),
            7 => (ushort)(female ? 1601 : 1501),
            8 => (ushort)(female ? 1801 : 1701),
            _ => 101,
        };
    }

    private static bool HasPap(RiderPap[] paps, string name)
    {
        foreach (var pap in paps)
        {
            if (pap.Name == name)
                return true;
        }

        return false;
    }

    private static string AnimationDirectory(MountInfo mount)
        => $"mt_{(mount.ModelType == 2 ? 'd' : 'm')}{mount.Model:D4}";

    private static string PapPath(ushort skeleton, string directory, string name)
        => $"chara/human/c{skeleton:D4}/animation/a0001/{directory}/resident/{name}";

    private void AddSoundSwaps(Dictionary<string, string> swaps, MountInfo source, MountInfo target)
    {
        if (source.SoundPack == 0 || target.SoundPack == 0 || source.SoundPack == target.SoundPack)
            return;

        var from = $"sound/battle/mon/{source.SoundPack}.scd";
        var to   = $"sound/battle/mon/{target.SoundPack}.scd";
        if (from != to && _gameData.FileExists(from) && _gameData.FileExists(to))
            swaps[from] = to;
    }

    private RiderPap[] RiderPaps(MountInfo mount)
    {
        if (mount.ModelType is not (2 or 3))
            return [];

        var key = (mount.ModelType, mount.Model);
        if (_riderPaps.TryGetValue(key, out var cached))
            return cached;

        var kind = mount.ModelType == 2 ? 'd' : 'm';
        var list = new List<RiderPap>();
        for (var race = 1; race <= 18; ++race)
        {
            var raceCode = $"c{race:D2}01";
            foreach (var name in RiderPapNames)
            {
                var path = $"chara/human/{raceCode}/animation/a0001/mt_{kind}{mount.Model:D4}/resident/{name}";
                if (_gameData.FileExists(path))
                    list.Add(new RiderPap(raceCode, name, path));
            }
        }

        return _riderPaps[key] = list.ToArray();
    }

    private void SetAnimationSwaps(Dictionary<string, string> swaps)
    {
        if (!_penumbra.Available)
            return;

        try
        {
            ClearAnimationSwaps();
            if (swaps.Count == 0)
                return;

            var player = _objects.Player;
            var collection = player.Valid
                ? _penumbra.Collections.ObjectCollectionId(player.Index.Index).Identifier
                : _penumbra.Collections.PlayerCollectionId.Identifier;
            if (collection == Guid.Empty)
                return;

            var ec = _addTemporaryMod.Invoke(TemporaryModTag, collection, swaps, "", TemporaryModPriority);
            _hasSwaps      = ec == PenumbraApiEc.Success;
            _modCollection = collection;
            Glamourer.Log.Debug($"[MountReplacement] Mount file swaps {swaps.Count}: {ec}.");
        }
        catch (Exception e)
        {
            Glamourer.Log.Warning($"[MountReplacement] Failed to set rider animation swaps: {e.Message}");
        }
    }

    private void ClearAnimationSwaps()
    {
        if (!_hasSwaps)
            return;

        try
        {
            _removeTemporaryMod.Invoke(TemporaryModTag, _modCollection, TemporaryModPriority);
        }
        catch (Exception e)
        {
            Glamourer.Log.Warning($"[MountReplacement] Failed to remove rider animation swaps: {e.Message}");
        }

        _hasSwaps = false;
    }
}
