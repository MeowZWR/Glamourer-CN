using Glamourer.Gui;
using Glamourer.Services;
using Luna;
using Luna.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Structs;
using System.Text;
using System.Text.Json;

namespace Glamourer.Config;

public sealed partial class UiConfig : ConfigurationFile<FilenameService>, IDisposable
{
    [JsonIgnore]
    public readonly ColorCache<ColorId, ColorIdData> ColorCache;

    private readonly ActorManager _actors;

    public UiConfig(SaveService saveService, MessageService messageService, ActorManager actors)
        : base(saveService, messageService, TimeSpan.FromMinutes(5))
    {
        _actors    = actors;
        ColorCache = new ColorCache<ColorId, ColorIdData>(Colors);
        Load();
        Gui.Colors.SetCache(ColorCache);
    }

    public readonly ColorDictionary<ColorId, ColorIdData> Colors = new();

    [ConfigProperty]
    private TwoPanelWidth _actorsTabScale = new(250, ScalingMode.Absolute);

    [ConfigProperty]
    private TwoPanelWidth _designsTabScale = new(0.3f, ScalingMode.Percentage);

    [ConfigProperty]
    private TwoPanelWidth _automationTabScale = new(0.3f, ScalingMode.Percentage);

    [ConfigProperty]
    private TwoPanelWidth _npcTabScale = new(250, ScalingMode.Absolute);

    [ConfigProperty]
    private NpcId _selectedNpc = 0;

    [ConfigProperty]
    private int _selectedAutomationIndex = -1;

    [ConfigProperty]
    private ActorIdentifier _selectedActor = ActorIdentifier.Invalid;

    public override int CurrentVersion
        => 1;

    protected override void AddData(Utf8JsonWriter j)
    {
        j.WritePropertyName("Colors"u8);
        Colors.Serialize(j, false);
        ActorsTabScale.WriteJson(j, "ActorsTab"u8);
        DesignsTabScale.WriteJson(j, "DesignsTab"u8);
        AutomationTabScale.WriteJson(j, "AutomationTab"u8);
        NpcTabScale.WriteJson(j, "NpcTab"u8);
        j.WriteUnsignedIfNot("SelectedNpc"u8, _selectedNpc, NpcId.Zero);
        j.WriteSignedIfNot("SelectedAutomationIndex"u8, _selectedAutomationIndex, -1);
        if (_selectedActor.IsValid)
        {
            // TODO
            j.WritePropertyName("SelectedActor"u8);
            j.WriteRawValue(_selectedActor.ToJson().ToString(Formatting.Indented));
        }
    }

    protected override void LoadData(in JsonElement j)
    {
        _actorsTabScale          = TwoPanelWidth.ReadJson(j, "ActorsTab"u8,     new TwoPanelWidth(250,  ScalingMode.Absolute));
        _designsTabScale         = TwoPanelWidth.ReadJson(j, "DesignsTab"u8,    new TwoPanelWidth(0.3f, ScalingMode.Percentage));
        _automationTabScale      = TwoPanelWidth.ReadJson(j, "AutomationTab"u8, new TwoPanelWidth(0.3f, ScalingMode.Percentage));
        _npcTabScale             = TwoPanelWidth.ReadJson(j, "NpcTab"u8,        new TwoPanelWidth(250,  ScalingMode.Absolute));
        _selectedNpc             = (NpcId)j.PropertyOrDefault("SelectedNpc"u8, 0u);
        _selectedAutomationIndex = j.PropertyOrDefault("SelectedAutomationIndex"u8, -1);
        _selectedActor           = j.TryReadObject("SelectedActor"u8, out var actor)
            ? _actors.FromJson(JObject.Parse(actor.GetRawText()))
            : ActorIdentifier.Invalid;

        if (j.TryGetProperty("Colors"u8, out var colorsElement)
         && colorsElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            var bytes  = Encoding.UTF8.GetBytes(colorsElement.GetRawText());
            var reader = new Utf8JsonReader(bytes, JsonFunctions.ReaderOptions);
            if (reader.Read())
            {
                var colors = ColorDictionary<ColorId, ColorIdData>.Deserialize(Messager, ref reader, true, true, true);
                Colors.Apply(colors, true);
            }
        }
        else
        {
            Colors.ResetToDefault();
        }
    }

    public override string ToFilePath(FilenameService fileNames)
        => fileNames.UiConfigurationFile;

    public void Dispose()
    {
        Gui.Colors.SetCache(null!);
        ColorCache.Dispose();
    }
}
