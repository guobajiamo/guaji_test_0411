namespace Test00_0410.UI;

/// <summary>
/// 事件按钮显示数据。
/// 之所以单独抽一个类，是为了让“UI 长什么样”和“系统怎么执行逻辑”分开。
/// </summary>
public class EventButtonViewData
{
    public string EventId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string TooltipText { get; set; } = string.Empty;

    public string StyleVariant { get; set; } = "event_click";

    public bool IsDisabled { get; set; }

    public double ProgressRatio { get; set; }

    public bool ShowProgressBar { get; set; }
}
