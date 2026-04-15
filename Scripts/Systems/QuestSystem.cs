using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

public sealed class QuestStepProgressInfo
{
    public QuestStepDefinition Step { get; init; } = new();

    public string DisplayTitle { get; init; } = string.Empty;

    public string DisplayDescription { get; init; } = string.Empty;
}

public sealed class QuestProgressInfo
{
    public QuestDefinition Definition { get; init; } = new();

    public PlayerQuestState State { get; init; } = new();

    public IReadOnlyList<QuestStepProgressInfo> CompletedSteps { get; init; } = Array.Empty<QuestStepProgressInfo>();

    public QuestStepProgressInfo? CurrentStep { get; init; }

    public bool HasRewards { get; init; }

    public string RewardSummary { get; init; } = string.Empty;

    public bool CanClaimReward => State.IsCompleted && HasRewards && !State.IsRewardClaimed;
}

/// <summary>
/// 任务系统。
/// 负责读取任务链定义、刷新解锁/完成状态，并提供任务界面与领奖逻辑所需的数据接口。
/// </summary>
public partial class QuestSystem : Node
{
    private readonly List<QuestDefinition> _definitions = new();
    private readonly Dictionary<string, QuestDefinition> _definitionMap = new(StringComparer.Ordinal);

    private GameManager? _gameManager;
    private PlayerProfile? _profile;
    private ValueSettlementService? _settlementService;

    public void Configure(GameManager gameManager, PlayerProfile profile, ValueSettlementService settlementService, IEnumerable<QuestDefinition> definitions)
    {
        _gameManager = gameManager;
        _profile = profile;
        _settlementService = settlementService;

        _definitions.Clear();
        _definitionMap.Clear();

        foreach (QuestDefinition definition in definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Id))
            .OrderBy(definition => definition.Category)
            .ThenBy(definition => definition.ChainId, StringComparer.Ordinal)
            .ThenBy(definition => definition.ChainOrder)
            .ThenBy(definition => definition.SourceFileOrder)
            .ThenBy(definition => definition.SourceEntryOrder))
        {
            _definitions.Add(definition);
            _definitionMap[definition.Id] = definition;
        }

        foreach (QuestDefinition definition in _definitions)
        {
            PlayerQuestState state = profile.GetOrCreateQuestState(definition.Id);
            state.QuestId = definition.Id;
            if (definition.RewardEffects.Count == 0 && state.IsCompleted)
            {
                state.IsRewardClaimed = true;
            }
        }

        RefreshQuestState();
    }

    public void RefreshQuestState()
    {
        if (_profile == null)
        {
            return;
        }

        foreach (QuestDefinition definition in _definitions)
        {
            PlayerQuestState state = _profile.GetOrCreateQuestState(definition.Id);
            state.QuestId = definition.Id;

            if (_profile.CompletedQuestIds.Contains(definition.Id))
            {
                state.IsUnlocked = true;
                state.IsCompleted = true;
            }

            if (!state.IsUnlocked && CanUnlock(definition))
            {
                state.IsUnlocked = true;
                _gameManager?.AddGameLog($"任务已解锁：{definition.Title}");
            }

            if (!state.IsUnlocked || state.IsCompleted)
            {
                if (definition.RewardEffects.Count == 0 && state.IsCompleted)
                {
                    state.IsRewardClaimed = true;
                }

                continue;
            }

            if (GetConsecutiveCompletedStepCount(definition) >= definition.Steps.Count && definition.Steps.Count > 0)
            {
                state.IsCompleted = true;
                _profile.CompletedQuestIds.Add(definition.Id);
                if (definition.RewardEffects.Count == 0)
                {
                    state.IsRewardClaimed = true;
                }

                _gameManager?.AddGameLog($"任务已完成：{definition.Title}");
            }
        }
    }

    public IReadOnlyList<QuestProgressInfo> GetVisibleQuests(QuestCategory category)
    {
        if (_profile == null)
        {
            return Array.Empty<QuestProgressInfo>();
        }

        return _definitions
            .Where(definition => definition.Category == category)
            .Select(CreateProgressInfo)
            .Where(info => info.State.IsUnlocked)
            .ToList();
    }

    public QuestProgressInfo? GetProgressInfo(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId) || !_definitionMap.TryGetValue(questId, out QuestDefinition? definition))
        {
            return null;
        }

        return CreateProgressInfo(definition);
    }

    public QuestProgressInfo? GetCurrentMainQuest()
    {
        List<QuestProgressInfo> visibleMainQuests = GetVisibleQuests(QuestCategory.Main).ToList();
        return visibleMainQuests.FirstOrDefault(info => !info.State.IsCompleted)
            ?? visibleMainQuests.LastOrDefault();
    }

    public string GetCurrentMainQuestLabel()
    {
        QuestProgressInfo? currentQuest = GetCurrentMainQuest();
        if (currentQuest == null)
        {
            return "暂无主线任务";
        }

        return currentQuest.State.IsCompleted
            ? $"{currentQuest.Definition.Title}（已完成）"
            : currentQuest.Definition.Title;
    }

    public bool TryClaimReward(string questId)
    {
        if (_profile == null)
        {
            return false;
        }

        QuestProgressInfo? info = GetProgressInfo(questId);
        if (info == null || !info.CanClaimReward)
        {
            return false;
        }

        foreach (EventEffectEntry effect in info.Definition.RewardEffects)
        {
            ApplyRewardEffect(effect);
        }

        info.State.IsRewardClaimed = true;
        _gameManager?.AddGameLog($"已领取任务奖励：{info.Definition.Title}");
        RefreshQuestState();
        return true;
    }

    private bool CanUnlock(QuestDefinition definition)
    {
        if (_profile == null)
        {
            return false;
        }

        if (definition.RequiredCompletedQuestIds.Any(requiredQuestId => !_profile.CompletedQuestIds.Contains(requiredQuestId)))
        {
            return false;
        }

        if (GetEarlierChainDefinitions(definition).Any(previous => !_profile.CompletedQuestIds.Contains(previous.Id)))
        {
            return false;
        }

        if (definition.UnlockConditions.Count == 0)
        {
            return true;
        }

        return ConditionEvaluator.AreAllConditionsMet(_profile, definition.UnlockConditions);
    }

    private IEnumerable<QuestDefinition> GetEarlierChainDefinitions(QuestDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ChainId))
        {
            return Enumerable.Empty<QuestDefinition>();
        }

        return _definitions.Where(candidate =>
            candidate.Category == definition.Category
            && string.Equals(candidate.ChainId, definition.ChainId, StringComparison.Ordinal)
            && candidate.ChainOrder < definition.ChainOrder);
    }

    private int GetConsecutiveCompletedStepCount(QuestDefinition definition)
    {
        if (_profile == null)
        {
            return 0;
        }

        int count = 0;
        foreach (QuestStepDefinition step in definition.Steps)
        {
            if (!_profile.CompletedEventIds.Contains(step.EventId))
            {
                break;
            }

            count++;
        }

        return count;
    }

    private QuestProgressInfo CreateProgressInfo(QuestDefinition definition)
    {
        PlayerQuestState state = _profile?.GetOrCreateQuestState(definition.Id) ?? new PlayerQuestState { QuestId = definition.Id };
        int completedStepCount = GetConsecutiveCompletedStepCount(definition);
        List<QuestStepProgressInfo> completedSteps = definition.Steps
            .Take(completedStepCount)
            .Select(CreateStepProgress)
            .ToList();

        QuestStepProgressInfo? currentStep = completedStepCount < definition.Steps.Count
            ? CreateStepProgress(definition.Steps[completedStepCount])
            : null;

        return new QuestProgressInfo
        {
            Definition = definition,
            State = state,
            CompletedSteps = completedSteps,
            CurrentStep = currentStep,
            HasRewards = definition.RewardEffects.Count > 0,
            RewardSummary = BuildRewardSummary(definition.RewardEffects)
        };
    }

    private QuestStepProgressInfo CreateStepProgress(QuestStepDefinition step)
    {
        string title = !string.IsNullOrWhiteSpace(step.Title)
            ? step.Title
            : _gameManager?.GetEventDisplayName(step.EventId) ?? step.EventId;

        string description = !string.IsNullOrWhiteSpace(step.Description)
            ? step.Description
            : title;

        return new QuestStepProgressInfo
        {
            Step = step,
            DisplayTitle = title,
            DisplayDescription = description
        };
    }

    private void ApplyRewardEffect(EventEffectEntry effect)
    {
        if (_profile == null)
        {
            return;
        }

        switch (effect.EffectType)
        {
            case EventEffectType.GrantItem:
            case EventEffectType.RemoveItem:
            case EventEffectType.GrantGold:
            case EventEffectType.RemoveGold:
            case EventEffectType.GrantSkillExp:
                _settlementService?.ApplyEconomyAndInventoryEffect(effect);
                break;
            case EventEffectType.AddFactionReputation:
                _gameManager?.FactionSystem?.AddReputation(effect.TargetId, effect.IntValue);
                break;
            case EventEffectType.UnlockZone:
                _gameManager?.ZoneSystem?.UnlockZone(effect.TargetId);
                break;
            case EventEffectType.CompleteEvent:
                _profile.CompletedEventIds.Add(effect.TargetId);
                break;
            case EventEffectType.CompleteQuest:
                _profile.CompletedQuestIds.Add(effect.TargetId);
                PlayerQuestState questState = _profile.GetOrCreateQuestState(effect.TargetId);
                questState.IsUnlocked = true;
                questState.IsCompleted = true;
                break;
            case EventEffectType.StartBattle:
                _gameManager?.BattleSystem?.StartBattle(effect.TargetId);
                break;
            case EventEffectType.UnlockAchievement:
                _gameManager?.AchievementSystem?.UnlockAchievement(effect.TargetId);
                break;
            case EventEffectType.LearnSkill:
                _gameManager?.EnsureSkillLearned(effect.TargetId, effect.IntValue <= 0 ? 1 : effect.IntValue, true);
                break;
        }
    }

    private string BuildRewardSummary(IEnumerable<EventEffectEntry> effects)
    {
        List<string> parts = new();
        foreach (EventEffectEntry effect in effects)
        {
            switch (effect.EffectType)
            {
                case EventEffectType.GrantItem:
                    parts.Add($"{GetItemName(effect.TargetId)} x{effect.IntValue}");
                    break;
                case EventEffectType.RemoveItem:
                    parts.Add($"移除 {GetItemName(effect.TargetId)} x{effect.IntValue}");
                    break;
                case EventEffectType.GrantGold:
                    parts.Add($"金币 x{effect.IntValue}");
                    break;
                case EventEffectType.RemoveGold:
                    parts.Add($"扣除金币 x{effect.IntValue}");
                    break;
                case EventEffectType.GrantSkillExp:
                    parts.Add($"{GetSkillName(effect.TargetId)}经验 +{effect.IntValue}");
                    break;
                case EventEffectType.AddFactionReputation:
                    parts.Add($"势力声望 {effect.TargetId} +{effect.IntValue}");
                    break;
                case EventEffectType.UnlockZone:
                    parts.Add($"解锁区域：{effect.TargetId}");
                    break;
                case EventEffectType.CompleteEvent:
                    parts.Add($"自动完成事件：{effect.TargetId}");
                    break;
                case EventEffectType.CompleteQuest:
                    parts.Add($"自动完成任务：{effect.TargetId}");
                    break;
                case EventEffectType.StartBattle:
                    parts.Add($"触发战斗：{effect.TargetId}");
                    break;
                case EventEffectType.UnlockAchievement:
                    parts.Add($"解锁成就：{effect.TargetId}");
                    break;
                case EventEffectType.LearnSkill:
                    parts.Add($"习得技能：{GetSkillName(effect.TargetId)}");
                    break;
            }
        }

        return string.Join("，", parts);
    }

    private string GetItemName(string itemId)
    {
        return _gameManager?.GetItemDisplayName(itemId) ?? itemId;
    }

    private string GetSkillName(string skillId)
    {
        SkillDefinition? skill = _gameManager?.SkillRegistry.GetSkill(skillId);
        if (skill == null)
        {
            return skillId;
        }

        return _gameManager?.TranslateText(skill.NameKey) ?? skill.NameKey;
    }

    public string BuildQuestHoverContent(QuestProgressInfo info, Func<string, string>? stepLocationResolver = null, bool includeHistory = false)
    {
        List<string> lines = new()
        {
            $"任务描述：{info.Definition.Description}"
        };

        if (info.CurrentStep != null)
        {
            string location = stepLocationResolver?.Invoke(info.CurrentStep.Step.EventId) ?? string.Empty;
            string progressText = string.IsNullOrWhiteSpace(location)
                ? info.CurrentStep.DisplayDescription
                : $"{info.CurrentStep.DisplayDescription}\n目标位置：{location}";
            lines.Add($"当前进度：{progressText}");
        }
        else
        {
            lines.Add("当前进度：任务链已完成。");
        }

        if (info.HasRewards)
        {
            string rewardStatus = info.CanClaimReward
                ? "可在任务页手动领取奖励。"
                : info.State.IsRewardClaimed
                    ? "奖励已领取。"
                    : "完成后可领取奖励。";
            lines.Add($"任务奖励：{info.RewardSummary}");
            lines.Add($"奖励状态：{rewardStatus}");
        }

        if (includeHistory)
        {
            if (info.CompletedSteps.Count == 0)
            {
                lines.Add("已完成关键事件：暂无。");
            }
            else
            {
                lines.Add("已完成关键事件：");
                for (int index = 0; index < info.CompletedSteps.Count; index++)
                {
                    QuestStepProgressInfo step = info.CompletedSteps[index];
                    string location = stepLocationResolver?.Invoke(step.Step.EventId) ?? string.Empty;
                    string suffix = string.IsNullOrWhiteSpace(location) ? string.Empty : $"（{location}）";
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "{0}. {1}{2}", index + 1, step.DisplayTitle, suffix));
                }
            }
        }
        else
        {
            lines.Add("按住 Shift 可展开查看已完成的关键事件链。");
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }
}
