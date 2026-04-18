using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家界面运行态。
/// 只存放“当前界面怎么展示”的轻量状态，
/// 不承担真实战斗/Buff/背包物品数据本体。
/// </summary>
public sealed class PlayerUiState
{
    /// <summary>
    /// 当前选中的二级区域。
    /// </summary>
    public string SelectedAreaId { get; set; } = string.Empty;

    /// <summary>
    /// 当前中间区域选中的标签页。
    /// </summary>
    public string SelectedTabId { get; set; } = "current_region";

    /// <summary>
    /// 左侧栏是否处于“技能模式”。
    /// false = 区域树，true = 技能树。
    /// </summary>
    public bool IsSkillSidebarMode { get; set; }

    /// <summary>
    /// 当前选中的技能。
    /// 在左侧技能树与“技能相关”聚合页之间共享。
    /// </summary>
    public string SelectedSkillId { get; set; } = string.Empty;

    /// <summary>
    /// 当前 UI 主题模式。
    /// </summary>
    public string UiThemeMode { get; set; } = "stitch";

    /// <summary>
    /// 背包排序模式。
    /// </summary>
    public InventorySortMode InventorySortMode { get; set; } = InventorySortMode.ArrivalOrder;

    /// <summary>
    /// 旧版背包标签过滤值。
    /// 当前主要作为兼容字段保留。
    /// </summary>
    public ItemTag InventoryTagFilter { get; set; } = ItemTag.None;

    /// <summary>
    /// 新版背包顶部筛选页签。
    /// </summary>
    public InventoryFilterTab InventoryFilterTab { get; set; } = InventoryFilterTab.All;

    /// <summary>
    /// 背包是否使用大图标模式。
    /// </summary>
    public bool InventoryUseLargeIcons { get; set; } = true;

    /// <summary>
    /// 收藏的子场景集合。
    /// 当前只承担“收藏/未收藏”的二值语义，不记录收藏顺序。
    /// </summary>
    public HashSet<string> FavoriteSceneIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 已被 UI 首次识别过的互动事件。
    /// 用于给新出现的按钮挂 New 提示，避免重复闪烁。
    /// </summary>
    public HashSet<string> ProcessedInteractableEventIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 当前应该显示 New 标记的二级区域。
    /// </summary>
    public HashSet<string> AreaIdsWithNewMarker { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 战斗页当前选中的二级区域。
    /// </summary>
    public string SelectedBattleAreaId { get; set; } = string.Empty;

    /// <summary>
    /// 战斗页当前选中的子场景。
    /// </summary>
    public string SelectedBattleSceneId { get; set; } = string.Empty;

    /// <summary>
    /// 战斗页当前绑定的点击事件 id。
    /// 一般对应“开始战斗/选择敌人”的入口事件。
    /// </summary>
    public string SelectedBattleEventId { get; set; } = string.Empty;

    /// <summary>
    /// 战斗页当前选中的敌人/遭遇 id。
    /// </summary>
    public string SelectedBattleEncounterId { get; set; } = string.Empty;

    /// <summary>
    /// 本次战斗页选敌是否来自左侧区域入口。
    /// 用于区分“手动打开战斗页”和“场景按钮跳转进战斗待机”的状态。
    /// </summary>
    public bool BattleSelectionFromSceneEntry { get; set; }

    /// <summary>
    /// 战斗页当前选中的主食。
    /// 这里只记 UI 选择，不等同于真实 Buff 生效状态。
    /// </summary>
    public string SelectedStapleItemId { get; set; } = string.Empty;

    /// <summary>
    /// 战斗页当前展示中的零食快捷选择。
    /// 顺序有意义，后续可与“最多 5 个零食 Buff”UI 复用。
    /// </summary>
    public List<string> SelectedSnackItemIds { get; } = new();

    public bool IsSceneFavorited(string sceneId)
    {
        return !string.IsNullOrWhiteSpace(sceneId) && FavoriteSceneIds.Contains(sceneId);
    }

    public void SetSceneFavorited(string sceneId, bool isFavorited)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
        {
            return;
        }

        if (isFavorited)
        {
            FavoriteSceneIds.Add(sceneId);
            return;
        }

        FavoriteSceneIds.Remove(sceneId);
    }

    public int GetFavoriteSortWeight(string sceneId)
    {
        return IsSceneFavorited(sceneId) ? 1 : -1;
    }

    public bool HasProcessedInteractableEvent(string eventId)
    {
        return !string.IsNullOrWhiteSpace(eventId) && ProcessedInteractableEventIds.Contains(eventId);
    }

    public void MarkInteractableEventProcessed(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        ProcessedInteractableEventIds.Add(eventId);
    }

    public IReadOnlyList<string> GetSortedProcessedInteractableEventIds()
    {
        return ProcessedInteractableEventIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    public bool HasNewMarker(string areaId)
    {
        return !string.IsNullOrWhiteSpace(areaId) && AreaIdsWithNewMarker.Contains(areaId);
    }

    public void AddNewMarker(string areaId)
    {
        if (string.IsNullOrWhiteSpace(areaId))
        {
            return;
        }

        AreaIdsWithNewMarker.Add(areaId);
    }

    public void ClearNewMarker(string areaId)
    {
        if (string.IsNullOrWhiteSpace(areaId))
        {
            return;
        }

        AreaIdsWithNewMarker.Remove(areaId);
    }

    public IReadOnlyList<string> GetSortedAreaIdsWithNewMarker()
    {
        return AreaIdsWithNewMarker
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    public void SetBattleSelection(
        string areaId,
        string sceneId,
        string battleEventId,
        string encounterId,
        bool fromSceneEntry)
    {
        SelectedBattleAreaId = areaId?.Trim() ?? string.Empty;
        SelectedBattleSceneId = sceneId?.Trim() ?? string.Empty;
        SelectedBattleEventId = battleEventId?.Trim() ?? string.Empty;
        SelectedBattleEncounterId = encounterId?.Trim() ?? string.Empty;
        BattleSelectionFromSceneEntry = fromSceneEntry;
    }

    public void ClearBattleSelection()
    {
        SelectedBattleAreaId = string.Empty;
        SelectedBattleSceneId = string.Empty;
        SelectedBattleEventId = string.Empty;
        SelectedBattleEncounterId = string.Empty;
        BattleSelectionFromSceneEntry = false;
    }

    public void SetSelectedStapleItem(string itemId)
    {
        SelectedStapleItemId = itemId?.Trim() ?? string.Empty;
    }

    public void SetSelectedSnackItems(IEnumerable<string>? itemIds, int maxCount = 5)
    {
        SelectedSnackItemIds.Clear();
        if (itemIds == null)
        {
            return;
        }

        foreach (string itemId in itemIds.Where(static id => !string.IsNullOrWhiteSpace(id)).Take(Math.Max(0, maxCount)))
        {
            SelectedSnackItemIds.Add(itemId.Trim());
        }
    }
}
