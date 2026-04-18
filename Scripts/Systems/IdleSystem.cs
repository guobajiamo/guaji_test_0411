using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// Idle loop system.
/// Handles progress ticking, offline settlement, resource payouts and exp grants.
/// </summary>
public partial class IdleSystem : Node
{
    public readonly record struct GatheringNodeView(int AvailableAmount, int Capacity, double SecondsToFull);

    private PlayerProfile? _profile;
    private EventRegistry? _eventRegistry;
    private SkillRegistry? _skillRegistry;
    private ItemRegistry? _itemRegistry;
    private ValueSettlementService? _settlementService;
    private SignalBus? _signalBus;

    public void Configure(
        PlayerProfile profile,
        EventRegistry eventRegistry,
        SkillRegistry skillRegistry,
        ItemRegistry itemRegistry,
        ValueSettlementService settlementService)
    {
        _profile = profile;
        _eventRegistry = eventRegistry;
        _skillRegistry = skillRegistry;
        _itemRegistry = itemRegistry;
        _settlementService = settlementService;
        _signalBus ??= GetNodeOrNull<SignalBus>("/root/SignalBus");
    }

    public bool StartIdleEvent(string eventId)
    {
        if (_profile == null || !CanStartIdleEvent(eventId))
        {
            return false;
        }

        PlayerIdleState idleState = _profile.IdleState;
        idleState.ActiveEventId = eventId;
        idleState.IsRunning = true;
        idleState.IsWaitingForGatheringRecovery = false;
        idleState.AccumulatedProgressSeconds = 0.0;
        idleState.PendingOutputFraction = 0.0;
        idleState.LastProgressUnixSeconds = GetNowUnixSeconds();
        if (_eventRegistry?.GetEvent(eventId) is EventDefinition definition
            && definition.HasResourceCap
            && GetAvailableGatheringAmount(definition) <= 0)
        {
            idleState.IsWaitingForGatheringRecovery = true;
        }

        EmitIdleEventChanged(eventId);
        return true;
    }

    public void StopIdleEvent()
    {
        if (_profile != null)
        {
            PlayerIdleState idleState = _profile.IdleState;
            idleState.ActiveEventId = string.Empty;
            idleState.IsRunning = false;
            idleState.IsWaitingForGatheringRecovery = false;
            idleState.AccumulatedProgressSeconds = 0.0;
            idleState.PendingOutputFraction = 0.0;
            idleState.LastProgressUnixSeconds = GetNowUnixSeconds();
        }

        EmitIdleEventChanged(string.Empty);
    }

    public override void _Process(double delta)
    {
        if (_profile == null || !_profile.IdleState.IsRunning || string.IsNullOrWhiteSpace(_profile.IdleState.ActiveEventId))
        {
            return;
        }

        PlayerIdleState idleState = _profile.IdleState;
        idleState.AccumulatedProgressSeconds += delta;
        idleState.LastProgressUnixSeconds = GetNowUnixSeconds();
        TickIdleProgress();
    }

    public void ApplyOfflineProgress(long nowUnixSeconds)
    {
        if (_profile == null)
        {
            return;
        }

        PlayerIdleState idleState = _profile.IdleState;
        if (!idleState.IsRunning || string.IsNullOrWhiteSpace(idleState.ActiveEventId))
        {
            return;
        }

        long elapsedSeconds = Math.Max(0, nowUnixSeconds - idleState.LastProgressUnixSeconds);
        long cappedSeconds = Math.Min(elapsedSeconds, idleState.OfflineSettlementCapSeconds);
        idleState.AccumulatedProgressSeconds += cappedSeconds;
        idleState.LastProgressUnixSeconds = nowUnixSeconds;
        TickIdleProgress();
    }

    public bool IsRunningEvent(string eventId)
    {
        return _profile != null
            && _profile.IdleState.IsRunning
            && string.Equals(_profile.IdleState.ActiveEventId, eventId, StringComparison.Ordinal);
    }

    public bool IsWaitingForGatheringRecovery(string eventId)
    {
        return _profile != null
            && _profile.IdleState.IsRunning
            && _profile.IdleState.IsWaitingForGatheringRecovery
            && string.Equals(_profile.IdleState.ActiveEventId, eventId, StringComparison.Ordinal);
    }

    public bool ShouldShowEvent(string eventId)
    {
        if (_profile == null || _eventRegistry == null || _skillRegistry == null)
        {
            return false;
        }

        EventDefinition? definition = _eventRegistry.GetEvent(eventId);
        if (definition == null || definition.Type != EventType.IdleLoop)
        {
            return false;
        }

        bool hasBoundSkill = !string.IsNullOrWhiteSpace(definition.LinkedSkillId)
            && _skillRegistry.GetSkill(definition.LinkedSkillId) != null;
        bool hasGenericIdleConfig = string.IsNullOrWhiteSpace(definition.LinkedSkillId)
            && definition.BaseIntervalSeconds > 0.0;
        return (hasBoundSkill || hasGenericIdleConfig)
            && EventAvailabilityEvaluator.ShouldShowButton(_profile, definition);
    }

    public bool CanStartIdleEvent(string eventId)
    {
        if (!TryResolveIdleContext(eventId, out EventDefinition? eventDefinition, out _, out _, out _)
            || eventDefinition == null)
        {
            return false;
        }

        bool hasCostsForAtLeastOneCycle = GetAffordableCycleCount(eventDefinition, 1) > 0;
        return EventAvailabilityEvaluator.CanInteractButton(_profile, eventDefinition)
            && hasCostsForAtLeastOneCycle;
    }

    public double GetProgressRatio(string eventId)
    {
        if (_profile == null || !IsRunningEvent(eventId))
        {
            return 0.0;
        }

        if (IsWaitingForGatheringRecovery(eventId))
        {
            return 0.0;
        }

        if (!TryResolveIdleContext(eventId, out EventDefinition? definition, out SkillDefinition? skillDefinition, out SkillLevelEntry? levelEntry, out ItemDefinition? tool)
            || definition == null)
        {
            return 0.0;
        }

        double interval = GetEffectiveIntervalSeconds(definition, skillDefinition, levelEntry, tool);
        if (interval <= 0.0)
        {
            return 0.0;
        }

        return Math.Max(0.0, Math.Min(1.0, _profile.IdleState.AccumulatedProgressSeconds / interval));
    }

    public bool TryGetGatheringNodeView(string eventId, out GatheringNodeView view)
    {
        view = default;
        if (_profile == null || _eventRegistry == null || string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        EventDefinition? definition = _eventRegistry.GetEvent(eventId);
        if (definition == null || definition.Type != EventType.IdleLoop || !definition.HasResourceCap)
        {
            return false;
        }

        GatheringNodeState state = GetOrCreateGatheringNodeState(definition);
        double nowUnixSeconds = GetNowUnixSecondsDouble();
        RefreshGatheringNodeRecovery(definition, state, nowUnixSeconds);

        double secondsToFull = 0.0;
        if (state.AvailableAmount < definition.ResourceCap)
        {
            double elapsedSinceLastRecover = Math.Max(0.0, nowUnixSeconds - state.LastRecoverUnixSeconds);
            double leftInCurrentTick = Math.Max(0.0, definition.ResourceRecoverSecondsPerPoint - elapsedSinceLastRecover);
            int missingAmount = definition.ResourceCap - state.AvailableAmount;
            secondsToFull = missingAmount <= 0
                ? 0.0
                : leftInCurrentTick + Math.Max(0, missingAmount - 1) * definition.ResourceRecoverSecondsPerPoint;
        }

        view = new GatheringNodeView(state.AvailableAmount, definition.ResourceCap, secondsToFull);
        return true;
    }

    private void TickIdleProgress()
    {
        if (_profile == null || _settlementService == null)
        {
            return;
        }

        PlayerIdleState idleState = _profile.IdleState;
        if (!idleState.IsRunning || string.IsNullOrWhiteSpace(idleState.ActiveEventId))
        {
            return;
        }

        if (!TryResolveIdleContext(idleState.ActiveEventId, out EventDefinition? eventDefinition, out SkillDefinition? skillDefinition, out SkillLevelEntry? levelEntry, out ItemDefinition? tool))
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog("Idle stopped: context became invalid.");
            return;
        }

        if (eventDefinition == null)
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog("Idle stopped: context was incomplete.");
            return;
        }

        EventDefinition resolvedEventDefinition = eventDefinition;
        SkillDefinition? resolvedSkillDefinition = skillDefinition;
        SkillLevelEntry? resolvedLevelEntry = levelEntry;

        if (!EventAvailabilityEvaluator.CanInteractButton(_profile, resolvedEventDefinition))
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog("Idle stopped: interaction conditions no longer met.");
            return;
        }

        double interval = GetEffectiveIntervalSeconds(resolvedEventDefinition, resolvedSkillDefinition, resolvedLevelEntry, tool);
        if (interval <= 0.0)
        {
            return;
        }

        if (resolvedEventDefinition.HasResourceCap)
        {
            int availableGatheringAmountNow = GetAvailableGatheringAmount(resolvedEventDefinition);
            if (availableGatheringAmountNow <= 0)
            {
                EnterGatheringRecoveryWait(resolvedEventDefinition);
                return;
            }

            ExitGatheringRecoveryWaitIfNeeded();
        }
        else
        {
            ExitGatheringRecoveryWaitIfNeeded();
        }

        int completedCycles = (int)Math.Floor(idleState.AccumulatedProgressSeconds / interval);
        if (completedCycles <= 0)
        {
            return;
        }

        int affordableCycles = GetAffordableCycleCount(resolvedEventDefinition, completedCycles);
        bool wasLimitedByResourceCap = false;
        if (resolvedEventDefinition.HasResourceCap)
        {
            int availableGatheringAmount = GetAvailableGatheringAmount(resolvedEventDefinition);
            if (availableGatheringAmount < affordableCycles)
            {
                affordableCycles = availableGatheringAmount;
                wasLimitedByResourceCap = true;
            }
        }
        if (affordableCycles <= 0)
        {
            if (resolvedEventDefinition.HasResourceCap && GetAvailableGatheringAmount(resolvedEventDefinition) <= 0)
            {
                EnterGatheringRecoveryWait(resolvedEventDefinition);
                return;
            }

            StopIdleEvent();
            string eventName = GameManager.Instance?.TranslateText(resolvedEventDefinition.NameKey) ?? resolvedEventDefinition.NameKey;
            string reason = wasLimitedByResourceCap
                ? "gathering resource cap reached"
                : "not enough cost items";
            GameManager.Instance?.AddGameLog($"Idle stopped: {reason} for {eventName}.");
            return;
        }

        idleState.AccumulatedProgressSeconds -= affordableCycles * interval;
        if (!ConsumeCycleCosts(resolvedEventDefinition, affordableCycles))
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog("Idle stopped: failed to settle cycle costs.");
            return;
        }

        if (resolvedEventDefinition.HasResourceCap)
        {
            ConsumeGatheringAmount(resolvedEventDefinition, affordableCycles);
        }

        bool hasPrimaryOutputRewardOverride = resolvedSkillDefinition != null
            && !string.IsNullOrWhiteSpace(resolvedSkillDefinition.PrimaryOutputItemId)
            && resolvedEventDefinition.Rewards.Any(reward =>
                string.Equals(reward.ItemId, resolvedSkillDefinition.PrimaryOutputItemId, StringComparison.Ordinal)
                && reward.DropChance >= 0.999999);
        int wholeOutput = 0;
        if (!hasPrimaryOutputRewardOverride)
        {
            double toolYieldMultiplier = tool?.ToolBonuses.LogYieldMultiplier ?? 1.0;
            double baseOutput = resolvedSkillDefinition != null && resolvedLevelEntry != null
                ? affordableCycles * resolvedLevelEntry.Output
                : affordableCycles * Math.Max(1.0, resolvedEventDefinition.BaseOutputAmount);
            double settledOutput = resolvedSkillDefinition != null
                ? _settlementService.ResolveIdleOutput(
                    resolvedSkillDefinition.Id,
                    baseOutput,
                    toolYieldMultiplier)
                : baseOutput * Math.Max(0.0, toolYieldMultiplier);
            double outputWithFraction = settledOutput + idleState.PendingOutputFraction;
            wholeOutput = (int)Math.Floor(outputWithFraction);
            idleState.PendingOutputFraction = outputWithFraction - wholeOutput;

            if (wholeOutput > 0 && resolvedSkillDefinition != null && !string.IsNullOrWhiteSpace(resolvedSkillDefinition.PrimaryOutputItemId))
            {
                _settlementService.AddItem(resolvedSkillDefinition.PrimaryOutputItemId, wholeOutput);
            }
        }
        else
        {
            idleState.PendingOutputFraction = 0.0;
        }

        Dictionary<string, int> rewardGrantSummary = ApplyEventRewardsForCycles(resolvedEventDefinition, resolvedSkillDefinition, affordableCycles);

        double settledTotalExp = 0.0;
        if (resolvedSkillDefinition != null && resolvedLevelEntry != null)
        {
            int expPerCycle = Math.Max(1, (int)Math.Round(resolvedLevelEntry.Interval, MidpointRounding.AwayFromZero));
            double baseTotalExp = affordableCycles * expPerCycle;
            settledTotalExp = _settlementService.ResolveGrantedSkillExp(resolvedSkillDefinition.Id, baseTotalExp);
            _settlementService.GrantSkillExp(resolvedSkillDefinition.Id, baseTotalExp);
        }

        string resolvedEventName = GameManager.Instance?.TranslateText(resolvedEventDefinition.NameKey) ?? resolvedEventDefinition.NameKey;
        string outputName = resolvedSkillDefinition == null || string.IsNullOrWhiteSpace(resolvedSkillDefinition.PrimaryOutputItemId)
            ? "无"
            : GameManager.Instance?.GetItemDisplayName(resolvedSkillDefinition.PrimaryOutputItemId) ?? resolvedSkillDefinition.PrimaryOutputItemId;
        string rewardSummary = rewardGrantSummary.Count == 0
            ? string.Empty
            : $", rewards: {string.Join(", ", rewardGrantSummary.Select(pair => $"+{pair.Value} {(GameManager.Instance?.GetItemDisplayName(pair.Key) ?? pair.Key)}"))}";
        string expSummary = settledTotalExp > 0.0 ? $", exp +{settledTotalExp:0}" : string.Empty;
        GameManager.Instance?.AddGameLog($"Idle settled: {resolvedEventName} x{affordableCycles}, item +{wholeOutput} {outputName}{rewardSummary}{expSummary}.");

        if (affordableCycles < completedCycles)
        {
            if (resolvedEventDefinition.HasResourceCap && GetAvailableGatheringAmount(resolvedEventDefinition) <= 0)
            {
                EnterGatheringRecoveryWait(resolvedEventDefinition);
            }
            else
            {
                StopIdleEvent();
                GameManager.Instance?.AddGameLog($"Idle stopped: costs depleted for {resolvedEventName}.");
            }
        }
    }

    private void EnterGatheringRecoveryWait(EventDefinition definition)
    {
        if (_profile == null)
        {
            return;
        }

        PlayerIdleState idleState = _profile.IdleState;
        idleState.AccumulatedProgressSeconds = 0.0;
        idleState.LastProgressUnixSeconds = GetNowUnixSeconds();
        if (idleState.IsWaitingForGatheringRecovery)
        {
            return;
        }

        idleState.IsWaitingForGatheringRecovery = true;
        EmitIdleEventChanged(idleState.ActiveEventId);

        string eventName = GameManager.Instance?.TranslateText(definition.NameKey) ?? definition.NameKey;
        GameManager.Instance?.AddGameLog($"Idle waiting: gathering resource depleted for {eventName}.");
    }

    private void ExitGatheringRecoveryWaitIfNeeded()
    {
        if (_profile == null || !_profile.IdleState.IsWaitingForGatheringRecovery)
        {
            return;
        }

        _profile.IdleState.IsWaitingForGatheringRecovery = false;
        EmitIdleEventChanged(_profile.IdleState.ActiveEventId);
    }

    private bool TryResolveIdleContext(
        string eventId,
        out EventDefinition? eventDefinition,
        out SkillDefinition? skillDefinition,
        out SkillLevelEntry? levelEntry,
        out ItemDefinition? tool)
    {
        eventDefinition = null;
        skillDefinition = null;
        levelEntry = null;
        tool = null;

        if (_profile == null || _eventRegistry == null || _skillRegistry == null || _itemRegistry == null)
        {
            return false;
        }

        eventDefinition = _eventRegistry.GetEvent(eventId);
        if (eventDefinition == null || eventDefinition.Type != EventType.IdleLoop)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(eventDefinition.LinkedSkillId))
        {
            return eventDefinition.BaseIntervalSeconds > 0.0;
        }

        skillDefinition = _skillRegistry.GetSkill(eventDefinition.LinkedSkillId);
        if (skillDefinition == null)
        {
            return false;
        }

        PlayerSkillState skillState = _profile.GetOrCreateSkillState(skillDefinition.Id);
        levelEntry = skillDefinition.GetLevelEntry(skillState.Level);
        if (levelEntry == null)
        {
            return false;
        }

        if (skillDefinition.RequiredToolTag != ItemTag.None)
        {
            tool = FindBestOwnedTool(skillDefinition.RequiredToolTag);
            if (tool == null)
            {
                return false;
            }
        }

        return true;
    }

    private ItemDefinition? FindBestOwnedTool(ItemTag requiredTag)
    {
        if (_profile == null || _itemRegistry == null)
        {
            return null;
        }

        return _itemRegistry.Items.Values
            .Where(item => item.HasTag(requiredTag) && _profile.Inventory.HasItem(item.Id))
            .OrderByDescending(item => item.ToolBonuses.LogYieldMultiplier)
            .ThenByDescending(item => item.ToolBonuses.ChopSpeedMultiplier)
            .ThenByDescending(item => item.BaseRarity)
            .ThenBy(item => item.DefinitionOrder)
            .FirstOrDefault();
    }

    private double GetEffectiveIntervalSeconds(EventDefinition definition, SkillDefinition? skillDefinition, SkillLevelEntry? levelEntry, ItemDefinition? tool)
    {
        double speedMultiplier = tool?.ToolBonuses.ChopSpeedMultiplier ?? 1.0;
        if (speedMultiplier <= 0.0)
        {
            speedMultiplier = 1.0;
        }

        if (skillDefinition == null || levelEntry == null)
        {
            if (_settlementService == null)
            {
                return Math.Max(0.2, definition.BaseIntervalSeconds / speedMultiplier);
            }

            return _settlementService.ResolveGlobalIdleIntervalSeconds(definition.BaseIntervalSeconds, speedMultiplier);
        }

        if (_settlementService == null)
        {
            return Math.Max(0.2, levelEntry.Interval / speedMultiplier);
        }

        return _settlementService.ResolveEffectiveIdleIntervalSeconds(skillDefinition.Id, levelEntry.Interval, speedMultiplier);
    }

    private int GetAffordableCycleCount(EventDefinition definition, int requestedCycles)
    {
        if (_profile == null || requestedCycles <= 0)
        {
            return 0;
        }

        int affordableCycles = requestedCycles;
        var groupedCosts = definition.Costs
            .Where(cost => cost.Amount > 0)
            .GroupBy(cost => cost.ItemId, StringComparer.Ordinal);
        foreach (var groupedCost in groupedCosts)
        {
            int totalAmountPerCycle = groupedCost.Sum(cost => cost.Amount);
            int ownedAmount = _profile.Inventory.GetItemAmount(groupedCost.Key);
            int cyclesByThisCost = totalAmountPerCycle <= 0 ? requestedCycles : ownedAmount / totalAmountPerCycle;
            affordableCycles = Math.Min(affordableCycles, cyclesByThisCost);
        }

        return affordableCycles;
    }

    private bool ConsumeCycleCosts(EventDefinition definition, int cycleCount)
    {
        if (_settlementService == null || cycleCount <= 0)
        {
            return false;
        }

        return _settlementService.TryPayItemCosts(definition.Costs, cycleCount);
    }

    private Dictionary<string, int> ApplyEventRewardsForCycles(EventDefinition definition, SkillDefinition? skillDefinition, int cycleCount)
    {
        Dictionary<string, int> summary = new(StringComparer.Ordinal);
        if (_settlementService == null || cycleCount <= 0 || definition.Rewards.Count == 0)
        {
            return summary;
        }

        double rewardAmountMultiplier = skillDefinition == null
            ? _settlementService.ResolveGlobalIdleOutput(1.0, 1.0)
            : _settlementService.ResolveIdleOutput(skillDefinition.Id, 1.0, 1.0);
        foreach (EventRewardEntry reward in definition.Rewards)
        {
            if (reward.Amount <= 0)
            {
                continue;
            }

            double dropChance = _settlementService.ResolveRewardDropChance(reward.ItemId, reward.DropChance, _itemRegistry);
            if (dropChance <= 0.0)
            {
                continue;
            }

            for (int cycleIndex = 0; cycleIndex < cycleCount; cycleIndex++)
            {
                if (dropChance < 1.0 && Random.Shared.NextDouble() > dropChance)
                {
                    continue;
                }

                int finalAmount = (int)Math.Round(reward.Amount * rewardAmountMultiplier, MidpointRounding.AwayFromZero);
                if (finalAmount <= 0)
                {
                    continue;
                }

                _settlementService.AddItem(reward.ItemId, finalAmount);
                summary.TryGetValue(reward.ItemId, out int existingAmount);
                summary[reward.ItemId] = existingAmount + finalAmount;
            }
        }

        return summary;
    }

    private int GetAvailableGatheringAmount(EventDefinition definition)
    {
        if (!definition.HasResourceCap)
        {
            return int.MaxValue;
        }

        GatheringNodeState state = GetOrCreateGatheringNodeState(definition);
        RefreshGatheringNodeRecovery(definition, state, GetNowUnixSecondsDouble());
        return state.AvailableAmount;
    }

    private void ConsumeGatheringAmount(EventDefinition definition, int amount)
    {
        if (!definition.HasResourceCap || _profile == null || amount <= 0)
        {
            return;
        }

        GatheringNodeState state = GetOrCreateGatheringNodeState(definition);
        double nowUnixSeconds = GetNowUnixSecondsDouble();
        RefreshGatheringNodeRecovery(definition, state, nowUnixSeconds);
        bool wasBelowCapBeforeConsume = state.AvailableAmount < definition.ResourceCap;
        state.AvailableAmount = Math.Max(0, state.AvailableAmount - amount);
        // Recovery should run continuously while below cap.
        // Only when dropping from full -> below full do we start the recovery timeline at "now".
        if (!wasBelowCapBeforeConsume && state.AvailableAmount < definition.ResourceCap)
        {
            state.LastRecoverUnixSeconds = nowUnixSeconds;
        }

        EmitGatheringNodeStateChanged(definition.Id);
    }

    private GatheringNodeState GetOrCreateGatheringNodeState(EventDefinition definition)
    {
        if (_profile == null)
        {
            return new GatheringNodeState();
        }

        if (!_profile.IdleState.GatheringNodeStates.TryGetValue(definition.Id, out GatheringNodeState? state))
        {
            state = new GatheringNodeState
            {
                EventId = definition.Id,
                AvailableAmount = definition.ResourceCap,
                LastRecoverUnixSeconds = GetNowUnixSecondsDouble()
            };
            _profile.IdleState.GatheringNodeStates[definition.Id] = state;
            return state;
        }

        state.EventId = definition.Id;
        if (state.AvailableAmount <= 0 && definition.ResourceCap > 0)
        {
            state.AvailableAmount = Math.Max(0, state.AvailableAmount);
        }

        if (!double.IsFinite(state.LastRecoverUnixSeconds) || state.LastRecoverUnixSeconds <= 0.0)
        {
            state.LastRecoverUnixSeconds = GetNowUnixSecondsDouble();
        }

        if (definition.ResourceCap > 0)
        {
            state.AvailableAmount = Math.Clamp(state.AvailableAmount, 0, definition.ResourceCap);
        }

        return state;
    }

    private void RefreshGatheringNodeRecovery(EventDefinition definition, GatheringNodeState state, double nowUnixSeconds)
    {
        if (!definition.HasResourceCap || definition.ResourceRecoverSecondsPerPoint <= 0.0)
        {
            return;
        }

        if (state.AvailableAmount >= definition.ResourceCap)
        {
            state.AvailableAmount = definition.ResourceCap;
            state.LastRecoverUnixSeconds = nowUnixSeconds;
            return;
        }

        double elapsedSeconds = Math.Max(0.0, nowUnixSeconds - state.LastRecoverUnixSeconds);
        if (elapsedSeconds < definition.ResourceRecoverSecondsPerPoint)
        {
            return;
        }

        int recoveredAmount = (int)Math.Floor(elapsedSeconds / definition.ResourceRecoverSecondsPerPoint);
        if (recoveredAmount <= 0)
        {
            return;
        }

        state.AvailableAmount = Math.Min(definition.ResourceCap, state.AvailableAmount + recoveredAmount);
        state.LastRecoverUnixSeconds += recoveredAmount * definition.ResourceRecoverSecondsPerPoint;
        if (state.AvailableAmount >= definition.ResourceCap)
        {
            state.LastRecoverUnixSeconds = nowUnixSeconds;
        }

        EmitGatheringNodeStateChanged(definition.Id);
    }

    private static long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static double GetNowUnixSecondsDouble()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    private void EmitIdleEventChanged(string eventId)
    {
        _signalBus ??= GetNodeOrNull<SignalBus>("/root/SignalBus");
        _signalBus?.EmitSignal(SignalBus.SignalName.ActiveIdleEventChanged, eventId);
    }

    private void EmitGatheringNodeStateChanged(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        _signalBus ??= GetNodeOrNull<SignalBus>("/root/SignalBus");
        _signalBus?.EmitSignal(SignalBus.SignalName.GatheringNodeStateChanged, eventId);
    }
}
