using Godot;

namespace Test00_0410.UI;

/// <summary>
/// 主界面布局设置资源。
/// 你后面如果想改 UI，优先改这个资源对应的参数，
/// 而不是直接去改业务逻辑代码。
/// </summary>
[GlobalClass]
public partial class MainUiLayoutSettings : Resource
{
    [Export]
    public int OuterMargin { get; set; } = 14;

    [Export]
    public int PanelSpacing { get; set; } = 10;

    [Export]
    public int LeftColumnMinWidth { get; set; } = 860;

    [Export]
    public int RightColumnMinWidth { get; set; } = 320;

    [Export]
    public int EventColumnMinWidth { get; set; } = 260;

    [Export]
    public int EventColumnSpacing { get; set; } = 10;

    [Export]
    public int HeaderFontSize { get; set; } = 22;

    [Export]
    public int SectionHeaderFontSize { get; set; } = 18;

    [Export]
    public int BodyFontSize { get; set; } = 16;

    [Export]
    public int EventButtonMinHeight { get; set; } = 54;

    [Export]
    public int SpecialButtonMinHeight { get; set; } = 46;

    [Export]
    public int SpecialButtonMinWidth { get; set; } = 140;

    [Export]
    public int FooterButtonSpacing { get; set; } = 10;

    [Export]
    public int CollapsedLogHeight { get; set; } = 170;

    [Export]
    public int ExpandedLogMargin { get; set; } = 24;

    [Export]
    public int StatusCategoryFontSize { get; set; } = 20;

    [Export]
    public int StatusItemFontSize { get; set; } = 16;

    [Export]
    public int DialogMinWidth { get; set; } = 560;

    [Export]
    public int WindowBaseWidth { get; set; } = 1920;

    [Export]
    public int WindowBaseHeight { get; set; } = 1080;

    [Export]
    public float RefreshIntervalSeconds { get; set; } = 0.2f;
}
