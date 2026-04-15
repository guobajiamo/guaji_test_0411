using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 游戏内界面的轻量运行态。
/// 用来记录当前选中的区域、当前标签页，以及场景收藏顺序。
/// </summary>
public class PlayerUiState
{
    public string SelectedAreaId { get; set; } = string.Empty;

    public string SelectedTabId { get; set; } = "current_region";

    /// <summary>
    /// 左侧栏显示模式：
    /// false = 区域模式，true = 技能模式。
    /// </summary>
    public bool IsSkillSidebarMode { get; set; }

    /// <summary>
    /// 技能模式下当前选中的技能 ID。
    /// </summary>
    public string SelectedSkillId { get; set; } = string.Empty;

    /// <summary>
    /// 当前 UI 视觉主题模式。
    /// 默认使用 stitch 风格，可在系统页切换到 legacy。
    /// </summary>
    public string UiThemeMode { get; set; } = "stitch";

    /// <summary>
    /// 背包当前排序模式。
    /// </summary>
    public InventorySortMode InventorySortMode { get; set; } = InventorySortMode.ArrivalOrder;

    /// <summary>
    /// 背包标签筛选。
    /// None 表示显示全部。
    /// </summary>
    public ItemTag InventoryTagFilter { get; set; } = ItemTag.None;

    /// <summary>
    /// 背包导航栏筛选页签。
    /// </summary>
    public InventoryFilterTab InventoryFilterTab { get; set; } = InventoryFilterTab.All;

    /// <summary>
    /// 背包格子尺寸模式。
    /// true = 大图标（默认），false = 小图标。
    /// </summary>
    public bool InventoryUseLargeIcons { get; set; } = true;

    public List<string> FavoriteSceneIds { get; } = new();

    public HashSet<string> ProcessedInteractableEventIds { get; } = new();

    public HashSet<string> AreaIdsWithNewMarker { get; } = new();

    public bool IsSceneFavorited(string sceneId)
    {
        return FavoriteSceneIds.Contains(sceneId);
    }

    public void SetSceneFavorited(string sceneId, bool isFavorited)
    {
        FavoriteSceneIds.Remove(sceneId);
        if (isFavorited)
        {
            FavoriteSceneIds.Add(sceneId);
        }
    }

    public int GetFavoriteSortWeight(string sceneId)
    {
        int index = FavoriteSceneIds.IndexOf(sceneId);
        return index < 0 ? -1 : index;
    }

    public bool HasProcessedInteractableEvent(string eventId)
    {
        return ProcessedInteractableEventIds.Contains(eventId);
    }

    public void MarkInteractableEventProcessed(string eventId)
    {
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            ProcessedInteractableEventIds.Add(eventId);
        }
    }

    public bool HasNewMarker(string areaId)
    {
        return AreaIdsWithNewMarker.Contains(areaId);
    }

    public void AddNewMarker(string areaId)
    {
        if (!string.IsNullOrWhiteSpace(areaId))
        {
            AreaIdsWithNewMarker.Add(areaId);
        }
    }

    public void ClearNewMarker(string areaId)
    {
        if (!string.IsNullOrWhiteSpace(areaId))
        {
            AreaIdsWithNewMarker.Remove(areaId);
        }
    }

    public List<string> GetSortedProcessedInteractableEventIds()
    {
        return ProcessedInteractableEventIds.OrderBy(id => id).ToList();
    }

    public List<string> GetSortedAreaIdsWithNewMarker()
    {
        return AreaIdsWithNewMarker.OrderBy(id => id).ToList();
    }
}
