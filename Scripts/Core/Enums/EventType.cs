namespace Test00_0410.Core.Enums;

/// <summary>
/// 事件类型。
/// 用来区分“一次性事件”、“可重复点击事件”和“挂机循环事件”。
/// </summary>
public enum EventType
{
    OneshotClick,
    RepeatableClick,
    IdleLoop
}
