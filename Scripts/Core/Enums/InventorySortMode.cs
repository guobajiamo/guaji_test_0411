namespace Test00_0410.Core.Enums;

/// <summary>
/// 背包排序模式。
/// </summary>
public enum InventorySortMode
{
    /// <summary>
    /// 按“先入袋先显示”顺序。
    /// </summary>
    ArrivalOrder = 0,

    /// <summary>
    /// 按“类型 + 定义顺序”重排后的顺序。
    /// </summary>
    AutoByTypeThenDefinition = 1
}
