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
    /// 玩家手动指定的 UI 显示顺序。
    /// 如果为空，说明仍然沿用静态定义顺序。
    /// </summary>
    public int? PlayerDisplayOrder { get; set; }
}
