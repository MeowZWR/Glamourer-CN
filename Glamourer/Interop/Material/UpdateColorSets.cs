using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Luna;
using Penumbra.GameData;
using Penumbra.GameData.Interop;

namespace Glamourer.Interop.Material;

public sealed unsafe class CreateNewModel : FastHook<CreateNewModel.Delegate>
{
    public delegate nint Delegate(CharacterBase* characterBase, uint slot);

    private readonly ThreadLocal<Model> _updatingModel = new(() => Model.Null);

    public CreateNewModel(HookManager hooks)
        => Task = hooks.CreateHook<Delegate>("Create New Model", Sigs.CreateNewModel, Detour, true);

    private nint Detour(CharacterBase* characterBase, uint modelSlot)
    {
        _updatingModel.Value = characterBase;
        var ret = Task.Result!.Original(characterBase, modelSlot);
        _updatingModel.Value = Model.Null;
        return ret;
    }

    public Model Get()
        => _updatingModel.Value;
}
