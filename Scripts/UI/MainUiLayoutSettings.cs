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
    public int OuterMargin { get; set; } = 19;

    [Export]
    public int PanelSpacing { get; set; } = 13;

    [Export]
    public int LeftColumnMinWidth { get; set; } = 1146;

    [Export]
    public int RightColumnMinWidth { get; set; } = 426;

    [Export]
    public int EventColumnMinWidth { get; set; } = 346;

    [Export]
    public int EventColumnSpacing { get; set; } = 13;

    [Export]
    public int HeaderFontSize { get; set; } = 29;

    [Export]
    public int SectionHeaderFontSize { get; set; } = 24;

    [Export]
    public int BodyFontSize { get; set; } = 21;

    [Export]
    public int EventButtonMinHeight { get; set; } = 72;

    [Export]
    public int SpecialButtonMinHeight { get; set; } = 61;

    [Export]
    public int SpecialButtonMinWidth { get; set; } = 187;

    [Export]
    public int FooterButtonSpacing { get; set; } = 13;

    [Export]
    public int CollapsedLogHeight { get; set; } = 227;

    [Export]
    public int ExpandedLogMargin { get; set; } = 32;

    [Export]
    public int StatusCategoryFontSize { get; set; } = 27;

    [Export]
    public int StatusItemFontSize { get; set; } = 21;

    [Export]
    public int DialogMinWidth { get; set; } = 746;

    [Export]
    public int WindowBaseWidth { get; set; } = 2560;

    [Export]
    public int WindowBaseHeight { get; set; } = 1440;

    [Export]
    public float RefreshIntervalSeconds { get; set; } = 0.2f;
}
