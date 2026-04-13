using System.Collections.Generic;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 游戏内界面的轻量运行态。
/// 用来记录当前选中的区域、当前标签页，以及场景收藏顺序。
/// </summary>
public class PlayerUiState
{
    public string SelectedAreaId { get; set; } = string.Empty;

    public string SelectedTabId { get; set; } = "current_region";

    public List<string> FavoriteSceneIds { get; } = new();

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
}
