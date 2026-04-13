using System.Collections.Generic;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 任务静态定义。
/// 负责描述任务属于哪条链、通过哪些关键事件推进，以及完成后的可领取奖励。
/// </summary>
public class QuestDefinition
{
    public string Id { get; set; } = string.Empty;

    public string SourceFilePath { get; set; } = string.Empty;

    public int SourceFileOrder { get; set; }

    public int SourceEntryOrder { get; set; }

    public QuestCategory Category { get; set; } = QuestCategory.Main;

    public string ChainId { get; set; } = string.Empty;

    public int ChainOrder { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int UnlockConditionCount { get; set; }

    public List<EventConditionEntry> UnlockConditions { get; } = new();

    public List<string> RequiredCompletedQuestIds { get; } = new();

    public int StepCount { get; set; }

    public List<QuestStepDefinition> Steps { get; } = new();

    public List<EventEffectEntry> RewardEffects { get; } = new();
}

/// <summary>
/// 单个任务步骤。
/// 当前把“关键一次性事件”作为推进点，因此每一步都会指向一个目标事件 ID。
/// </summary>
public class QuestStepDefinition
{
    public string Id { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
