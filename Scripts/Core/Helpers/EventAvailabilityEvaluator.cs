using System.Collections.Generic;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 统一处理事件按钮的“显示 / 可互动 / 隐藏 / 已消耗”判定。
/// 这样 ClickEventSystem、IdleSystem 和 UI 可以共用同一套规则。
/// </summary>
public static class EventAvailabilityEvaluator
{
    public static bool IsConsumed(PlayerProfile? profile, EventDefinition definition)
    {
        if (profile == null)
        {
            return false;
        }

        if (definition.Type != Core.Enums.EventType.OneshotClick && !definition.RemoveAfterTriggered)
        {
            return false;
        }

        return profile.CompletedEventIds.Contains(definition.Id);
    }

    public static bool AreDisplayConditionsMet(PlayerProfile? profile, EventDefinition definition)
    {
        return ConditionEvaluator.AreAllConditionsMet(profile, definition.DisplayConditions);
    }

    public static bool AreInteractionConditionsMet(PlayerProfile? profile, EventDefinition definition)
    {
        return ConditionEvaluator.AreAllConditionsMet(profile, definition.InteractionConditions);
    }

    public static bool AreHideConditionsMet(PlayerProfile? profile, EventDefinition definition)
    {
        return definition.HideConditions.Count > 0
            && ConditionEvaluator.AreAllConditionsMet(profile, definition.HideConditions);
    }

    public static bool ShouldShowButton(PlayerProfile? profile, EventDefinition definition)
    {
        return profile != null
            && !IsConsumed(profile, definition)
            && !AreHideConditionsMet(profile, definition)
            && AreDisplayConditionsMet(profile, definition);
    }

    public static bool CanInteractButton(PlayerProfile? profile, EventDefinition definition)
    {
        return ShouldShowButton(profile, definition)
            && AreInteractionConditionsMet(profile, definition);
    }

    public static IReadOnlyList<EventConditionEntry> GetMissingInteractionConditions(PlayerProfile? profile, EventDefinition definition)
    {
        return definition.InteractionConditions.FindAll(condition => !ConditionEvaluator.IsConditionMet(profile, condition));
    }
}
