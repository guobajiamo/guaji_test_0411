using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 通用条件判定器。
/// 供事件、区域显示、后续商店或剧情解锁等系统复用。
/// </summary>
public static class ConditionEvaluator
{
    public static bool AreAllConditionsMet(PlayerProfile? profile, IEnumerable<EventConditionEntry> conditions)
    {
        return conditions.All(condition => IsConditionMet(profile, condition));
    }

    public static bool IsConditionMet(PlayerProfile? profile, EventConditionEntry condition)
    {
        if (profile == null)
        {
            return false;
        }

        int requiredValue = (int)condition.RequiredValue;

        return condition.ConditionType switch
        {
            ConditionType.None => true,
            ConditionType.HasItem => profile.Inventory.HasItem(condition.TargetId, requiredValue),
            ConditionType.HasGold => profile.Economy.Gold >= requiredValue,
            ConditionType.SkillLevel => profile.GetOrCreateSkillState(condition.TargetId).Level >= requiredValue,
            ConditionType.FactionReputation => profile.GetOrCreateFactionState(condition.TargetId).Reputation >= requiredValue,
            ConditionType.ZoneCleared => profile.GetOrCreateZoneState(condition.TargetId).ClearCount >= requiredValue,
            ConditionType.EventCompleted => profile.CompletedEventIds.Contains(condition.TargetId),
            ConditionType.QuestCompleted => profile.CompletedQuestIds.Contains(condition.TargetId),
            _ => false
        };
    }
}
