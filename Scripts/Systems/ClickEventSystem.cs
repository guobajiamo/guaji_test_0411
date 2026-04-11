using Godot;
using System;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 点击事件系统。
/// 负责处理一次性按钮和可重复按钮的点击执行。
/// </summary>
public partial class ClickEventSystem : Node
{
    private PlayerProfile? _profile;
    private EventRegistry? _eventRegistry;
    private SkillSystem? _skillSystem;
    private FactionSystem? _factionSystem;
    private ZoneSystem? _zoneSystem;

    public void Configure(
        PlayerProfile profile,
        EventRegistry eventRegistry,
        SkillSystem? skillSystem = null,
        FactionSystem? factionSystem = null,
        ZoneSystem? zoneSystem = null)
    {
        _profile = profile;
        _eventRegistry = eventRegistry;
        _skillSystem = skillSystem;
        _factionSystem = factionSystem;
        _zoneSystem = zoneSystem;
    }

    public bool TryTriggerEvent(string eventId)
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

        if (IsAlreadyConsumed(definition))
        {
            return false;
        }

        if (!ArePrerequisitesMet(definition))
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

    /// <summary>
    /// 供 UI 判断“这个事件按钮现在该不该显示”。
    /// 当前首版规则比较简单：
    /// 1. 一次性事件如果做完了，就隐藏
    /// 2. 前置条件没满足，也先隐藏
    /// </summary>
    public bool ShouldShowEvent(string eventId)
    {
        if (_eventRegistry == null)
        {
            return false;
        }

        EventDefinition? definition = _eventRegistry.GetEvent(eventId);
        if (definition == null)
        {
            return false;
        }

        if (IsAlreadyConsumed(definition))
        {
            return false;
        }

        return ArePrerequisitesMet(definition);
    }

    /// <summary>
    /// 供 UI 判断“按钮亮了以后现在能不能点”。
    /// 当前主要是看是否有足够的消耗品。
    /// </summary>
    public bool CanTriggerEvent(string eventId)
    {
        if (_eventRegistry == null)
        {
            return false;
        }

        EventDefinition? definition = _eventRegistry.GetEvent(eventId);
        if (definition == null || IsAlreadyConsumed(definition) || !ArePrerequisitesMet(definition))
        {
            return false;
        }

        return CanPayCosts(definition);
    }

    /// <summary>
    /// 供“剧情分支弹窗”使用。
    /// 源事件本身只负责弹框，真正奖励写在分支目标事件里。
    /// </summary>
    public bool TryTriggerDialogChoice(string sourceEventId, string targetEventId, bool consumeSourceEventOnChoice = true)
    {
        if (_profile == null || _eventRegistry == null)
        {
            return false;
        }

        EventDefinition? sourceDefinition = _eventRegistry.GetEvent(sourceEventId);
        if (sourceDefinition == null || IsAlreadyConsumed(sourceDefinition) || !ArePrerequisitesMet(sourceDefinition))
        {
            return false;
        }

        bool sourceAdded = false;
        if (consumeSourceEventOnChoice && (sourceDefinition.Type == EventType.OneshotClick || sourceDefinition.RemoveAfterTriggered))
        {
            sourceAdded = _profile.CompletedEventIds.Add(sourceDefinition.Id);
        }

        bool success = TryTriggerEvent(targetEventId);
        if (!success && sourceAdded)
        {
            _profile.CompletedEventIds.Remove(sourceDefinition.Id);
        }

        return success;
    }

    /// <summary>
    /// 一次性事件和“触发后移除”的事件，需要在系统层再做一次防重复保护。
    /// 这样即使 UI 没及时隐藏按钮，也不会被重复触发。
    /// </summary>
    private bool IsAlreadyConsumed(EventDefinition definition)
    {
        if (_profile == null)
        {
            return false;
        }

        if (definition.Type != EventType.OneshotClick && !definition.RemoveAfterTriggered)
        {
            return false;
        }

        return _profile.CompletedEventIds.Contains(definition.Id);
    }

    private bool ArePrerequisitesMet(EventDefinition definition)
    {
        foreach (EventConditionEntry condition in definition.Prerequisites)
        {
            if (!IsConditionMet(condition))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsConditionMet(EventConditionEntry condition)
    {
        if (_profile == null)
        {
            return false;
        }

        int requiredValue = (int)condition.RequiredValue;

        return condition.ConditionType switch
        {
            ConditionType.None => true,
            ConditionType.HasItem => _profile.Inventory.HasItem(condition.TargetId, requiredValue),
            ConditionType.HasGold => _profile.Economy.Gold >= requiredValue,
            ConditionType.SkillLevel => _profile.GetOrCreateSkillState(condition.TargetId).Level >= requiredValue,
            ConditionType.FactionReputation => _profile.GetOrCreateFactionState(condition.TargetId).Reputation >= requiredValue,
            ConditionType.ZoneCleared => _profile.GetOrCreateZoneState(condition.TargetId).ClearCount >= requiredValue,
            ConditionType.EventCompleted => _profile.CompletedEventIds.Contains(condition.TargetId),
            ConditionType.QuestCompleted => _profile.CompletedQuestIds.Contains(condition.TargetId),
            _ => false
        };
    }

    private bool TryPayCosts(EventDefinition definition)
    {
        if (_profile == null)
        {
            return false;
        }

        if (!CanPayCosts(definition))
        {
            return false;
        }

        foreach (ItemCostEntry cost in definition.Costs)
        {
            _profile.Inventory.TryRemoveItem(cost.ItemId, cost.Amount);
        }

        return true;
    }

    private bool CanPayCosts(EventDefinition definition)
    {
        if (_profile == null)
        {
            return false;
        }

        foreach (ItemCostEntry cost in definition.Costs)
        {
            if (!_profile.Inventory.HasItem(cost.ItemId, cost.Amount))
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyLegacyRewards(EventDefinition definition)
    {
        if (_profile == null)
        {
            return;
        }

        foreach (EventRewardEntry reward in definition.Rewards)
        {
            double dropChance = Math.Clamp(reward.DropChance, 0.0, 1.0);
            if (dropChance <= 0.0)
            {
                continue;
            }

            if (dropChance < 1.0 && Random.Shared.NextDouble() > dropChance)
            {
                continue;
            }

            _profile.Inventory.AddItem(reward.ItemId, reward.Amount);
        }
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
                _profile.Inventory.AddItem(effect.TargetId, effect.IntValue);
                break;
            case EventEffectType.RemoveItem:
                _profile.Inventory.TryRemoveItem(effect.TargetId, effect.IntValue);
                break;
            case EventEffectType.GrantGold:
                _profile.Economy.AddGold(effect.IntValue);
                break;
            case EventEffectType.RemoveGold:
                _profile.Economy.TrySpendGold(effect.IntValue);
                break;
            case EventEffectType.GrantSkillExp:
                _skillSystem?.AddExp(effect.TargetId, effect.IntValue);
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
                // 这里后续会转交给 BattleSystem。
                break;
        }
    }
}
