using Glamourer.Config;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs;

public sealed class IncognitoButton(Configuration config) : BaseIconButton<AwesomeIcon>, IUiService
{
    public override AwesomeIcon Icon
        => config.Ephemeral.IncognitoMode
            ? LunaStyle.IncognitoOn
            : LunaStyle.IncognitoOff;

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
    {
        var hold = config.IncognitoModifier.IsActive();
        Im.Text(config.Ephemeral.IncognitoMode ? "关闭匿名模式。"u8 : "开启匿名模式。"u8);
        if (!hold)
            Im.Text($"\n按住 {config.IncognitoModifier} 键并单击以切换。");
    }

    public override void OnClick()
    {
        if (config.IncognitoModifier.IsActive())
            config.Ephemeral.IncognitoMode = !config.Ephemeral.IncognitoMode;
    }
}
