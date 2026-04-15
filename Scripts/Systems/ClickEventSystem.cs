using Godot;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 点击事件系统。
/// 负责处理一次性/可重复事件按钮的触发执行。
/// </summary>
public partial class ClickEventSystem : Node
{
    private PlayerProfile? _profile;
    private EventRegistry? _eventRegistry;
    private ValueSettlementService? _settlementService;
    private FactionSystem? _factionSystem;
    private ZoneSystem? _zoneSystem;
    private SkillSystem? _skillSystem;

    public void Configure(
        PlayerProfile profile,
        EventRegistry eventRegistry,
        ValueSettlementService settlementService,
        FactionSystem? factionSystem = null,
        ZoneSystem? zoneSystem = null,
        SkillSystem? skillSystem = null)
    {
        _profile = profile;
        _eventRegistry = eventRegistry;
        _settlementService = settlementService;
        _factionSystem = factionSystem;
        _zoneSystem = zoneSystem;
        _skillSystem = skillSystem;
    }

    public bool TryTriggerEvent(string eventId)
    {
        return TryTriggerEventInternal(eventId, true);
    }

    /// <summary>
    /// 系统驱动触发入口（例如技能升级钩子）。
    /// 会做正常成本/条件检查，但不强制要求按钮可见。
    /// </summary>
    public bool TryTriggerEventFromSystem(string eventId)
    {
        return TryTriggerEventInternal(eventId, false);
    }

    private bool TryTriggerEventInternal(string eventId, bool requireVisibleButton)
    {
        if (_profile == null || _eventRegistry == null)
        {
            return false;
        }

        EventDefinition? definition = _eventRegistry.GetEvent(eventId);
        if (definition == null)
        {
            return false;
        }

        if (requireVisibleButton && !ShouldShowEvent(definition.Id))
        {
            return false;
        }

        if (IsAlreadyConsumed(definition) || IsHiddenByConditions(definition))
        {
            return false;
        }

        if (!AreInteractionConditionsMet(definition))
        {
            return false;
        }

        if (!TryPayCosts(definition))
        {
            return false;
        }

        ApplyLegacyRewards(definition);
        ApplyEffects(definition);

        if (definition.Type == EventType.OneshotClick || definition.RemoveAfterTriggered)
        {
            _profile.CompletedEventIds.Add(definition.Id);
        }

        return true;
    }

    public bool ShouldShowEvent(string eventId)
    {
        if (_eventRegistry == null || _profile == null)
        {
            return false;
        }

        EventDefinition? definition = _eventRegistry.GetEvent(eventId);
        if (definition == null)
        {
            return false;
        }

        return EventAvailabilityEvaluator.ShouldShowButton(_profile, definition);
    }

    public bool CanTriggerEvent(string eventId)
    {
        if (_eventRegistry == null || _profile == null)
        {
            return false;
        }

        EventDefinition? definition = _eventRegistry.GetEvent(eventId);
        if (definition == null || !EventAvailabilityEvaluator.CanInteractButton(_profile, definition))
        {
            return false;
        }

        return CanPayCosts(definition);
    }

    /// <summary>
    /// 供“剧情分支弹窗”使用。
    /// 源事件只负责弹窗，实际奖励由分支目标事件处理。
    /// </summary>
    public bool TryTriggerDialogChoice(string sourceEventId, string targetEventId, bool consumeSourceEventOnChoice = true)
    {
        if (_profile == null || _eventRegistry == null)
        {
            return false;
        }

        EventDefinition? sourceDefinition = _eventRegistry.GetEvent(sourceEventId);
        if (sourceDefinition == null || !CanTriggerEvent(sourceEventId))
        {
            return false;
        }

        bool sourceAdded = false;
        if (consumeSourceEventOnChoice && (sourceDefinition.Type == EventType.OneshotClick || sourceDefinition.RemoveAfterTriggered))
        {
            sourceAdded = _profile.CompletedEventIds.Add(sourceDefinition.Id);
        }

        bool success = TryTriggerEventInternal(targetEventId, false);
        if (!success && sourceAdded)
        {
            _profile.CompletedEventIds.Remove(sourceDefinition.Id);
        }

        return success;
    }

    private bool IsAlreadyConsumed(EventDefinition definition)
    {
        return EventAvailabilityEvaluator.IsConsumed(_profile, definition);
    }

    private bool AreInteractionConditionsMet(EventDefinition definition)
    {
        return EventAvailabilityEvaluator.AreInteractionConditionsMet(_profile, definition);
    }

    private bool IsHiddenByConditions(EventDefinition definition)
    {
        return EventAvailabilityEvaluator.AreHideConditionsMet(_profile, definition);
    }

    private bool TryPayCosts(EventDefinition definition)
    {
        if (_profile == null || _settlementService == null)
        {
            return false;
        }

        if (!CanPayCosts(definition))
        {
            return false;
        }

        return _settlementService.TryPayItemCosts(definition.Costs);
    }

    private bool CanPayCosts(EventDefinition definition)
    {
        if (_profile == null || _settlementService == null)
        {
            return false;
        }

        return _settlementService.CanPayItemCosts(definition.Costs);
    }

    private void ApplyLegacyRewards(EventDefinition definition)
    {
        if (_settlementService == null)
        {
            return;
        }

        _settlementService.ApplyLegacyEventRewards(definition.Rewards);
    }

    private void ApplyEffects(EventDefinition definition)
    {
        foreach (EventEffectEntry effect in definition.Effects)
        {
            ApplyEffect(effect);
        }
    }

    private void ApplyEffect(EventEffectEntry effect)
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
                _factionSystem?.AddReputation(effect.TargetId, effect.IntValue);
                break;
            case EventEffectType.UnlockZone:
                _zoneSystem?.UnlockZone(effect.TargetId);
                break;
            case EventEffectType.CompleteEvent:
                _profile.CompletedEventIds.Add(effect.TargetId);
                break;
            case EventEffectType.CompleteQuest:
                _profile.CompletedQuestIds.Add(effect.TargetId);
                break;
            case EventEffectType.StartBattle:
                // 后续交给 BattleSystem 处理。
                break;
            case EventEffectType.UnlockAchievement:
                GameManager.Instance?.AchievementSystem?.UnlockAchievement(effect.TargetId);
                break;
            case EventEffectType.LearnSkill:
                int learnLevel = effect.IntValue <= 0 ? 1 : effect.IntValue;
                _skillSystem?.TryLearnSkill(effect.TargetId, learnLevel);
                break;
        }
    }
}
