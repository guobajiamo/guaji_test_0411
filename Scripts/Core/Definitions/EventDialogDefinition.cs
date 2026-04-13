using System.Collections.Generic;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 事件弹窗定义。
/// 你可以把它理解成“这个事件在真正执行前，要不要先弹一个说明框”。
/// </summary>
public class EventDialogDefinition
{
    /// <summary>
    /// 弹窗正文。
    /// 这里支持直接写中文，后期也可以改成多语言 key。
    /// </summary>
    public string BodyTextKey { get; set; } = string.Empty;

    /// <summary>
    /// 单按钮弹窗时，按钮上显示的文字。
    /// 例如“我知道了”“无奈接受”等。
    /// </summary>
    public string ConfirmButtonText { get; set; } = string.Empty;

    /// <summary>
    /// 是否显示“我再想想”取消按钮。
    /// true = 玩家可以先关闭弹窗不处理。
    /// false = 玩家必须确认或二选一，适合紧急事态类事件。
    /// </summary>
    public bool ShowCancelButton { get; set; } = true;

    /// <summary>
    /// 双选项弹窗时使用的按钮列表。
    /// 当前按需求限制为最多两个按钮。
    /// </summary>
    public List<EventDialogChoiceDefinition> Choices { get; } = new();

    /// <summary>
    /// 选择分支按钮后，是否将“弹窗源事件”本身记为已完成。
    /// 通常剧情分支都会设为 true，这样玩家做过一次选择后就不会重复弹了。
    /// </summary>
    public bool ConsumeSourceEventOnChoice { get; set; } = true;

    public bool HasConfirmButton => !string.IsNullOrWhiteSpace(ConfirmButtonText);

    public bool HasChoices => Choices.Count > 0;
}

/// <summary>
/// 弹窗分支按钮定义。
/// 每个按钮都可以连接到一个真正执行奖励/后果的事件。
/// </summary>
public class EventDialogChoiceDefinition
{
    public string ButtonText { get; set; } = string.Empty;

    public string TargetEventId { get; set; } = string.Empty;
}
