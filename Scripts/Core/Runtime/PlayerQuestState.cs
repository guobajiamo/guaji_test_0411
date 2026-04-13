namespace Test00_0410.Core.Runtime;

/// <summary>
/// 单个任务的运行时状态。
/// 这里主要保存“是否已解锁、是否已完成、奖励是否已领取”这类不能丢的状态。
/// </summary>
public class PlayerQuestState
{
    public string QuestId { get; set; } = string.Empty;

    public bool IsUnlocked { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsRewardClaimed { get; set; }
}
