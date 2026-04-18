namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家对某个物品的个性化状态。
/// 例如是否获得过、是否收藏、UI 排序是否被手动改过等。
/// </summary>
public class PlayerItemState
{
    public string ItemId { get; set; } = string.Empty;

    public bool IsAcquired { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsJunkMarked { get; set; }

    /// <summary>
    /// 物品首次入袋后的序号。
    /// 用于恢复“先来先占位”的默认顺序。
    /// </summary>
    public int? AcquiredSequence { get; set; }

    /// <summary>
    /// 最近一次获得该物品时的 UTC Unix 时间戳（秒）。
    /// 当前用于装备页“按获取时间排序”以及后续战利品来源追踪接口。
    /// </summary>
    public long? LatestAcquiredUnixSeconds { get; set; }

    /// <summary>
    /// 玩家手动指定的 UI 显示顺序。
    /// 如果为空，说明仍然沿用静态定义顺序。
    /// </summary>
    public int? PlayerDisplayOrder { get; set; }
}
