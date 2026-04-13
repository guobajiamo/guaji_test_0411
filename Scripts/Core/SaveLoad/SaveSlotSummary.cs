namespace Test00_0410.Core.SaveLoad;

/// <summary>
/// 单个存档位摘要。
/// 给 UI 用来展示“这个槽位有没有存档、是什么剧本、最后保存时间”等信息。
/// </summary>
public class SaveSlotSummary
{
    public int SlotIndex { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public bool Exists { get; set; }

    public string ScenarioId { get; set; } = string.Empty;

    public string ScenarioDisplayName { get; set; } = string.Empty;

    public long SavedAtUnixSeconds { get; set; }
}
