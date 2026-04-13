using Godot;
using System;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 挂机循环系统。
/// 负责计时、读条、结算资源、发放技能经验，并在条件失效或成本耗尽时自动停止。
/// </summary>
public partial class IdleSystem : Node
{
    private PlayerProfile? _profile;
    private EventRegistry? _eventRegistry;
    private SkillRegistry? _skillRegistry;
    private ItemRegistry? _itemRegistry;
    private SignalBus? _signalBus;

    public void Configure(PlayerProfile profile, EventRegistry eventRegistry, SkillRegistry skillRegistry, ItemRegistry itemRegistry)
    {
        _profile = profile;
        _eventRegistry = eventRegistry;
        _skillRegistry = skillRegistry;
        _itemRegistry = itemRegistry;
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
        idleState.AccumulatedProgressSeconds = 0.0;
        idleState.PendingOutputFraction = 0.0;
        idleState.LastProgressUnixSeconds = GetNowUnixSeconds();
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

    /// <summary>
    /// 读档后调用这个方法，可以把离线这段时间补算进去。
    /// </summary>
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

        return !string.IsNullOrWhiteSpace(definition.LinkedSkillId)
            && _skillRegistry.GetSkill(definition.LinkedSkillId) != null
            && EventAvailabilityEvaluator.ShouldShowButton(_profile, definition);
    }

    public bool CanStartIdleEvent(string eventId)
    {
        if (!TryResolveIdleContext(eventId, out EventDefinition? eventDefinition, out _, out _, out _)
            || eventDefinition == null)
        {
            return false;
        }

        return EventAvailabilityEvaluator.CanInteractButton(_profile, eventDefinition)
            && GetAffordableCycleCount(eventDefinition, 1) > 0;
    }

    public double GetProgressRatio(string eventId)
    {
        if (_profile == null || !IsRunningEvent(eventId))
        {
            return 0.0;
        }

        if (!TryResolveIdleContext(eventId, out _, out _, out SkillLevelEntry? levelEntry, out ItemDefinition? tool))
        {
            return 0.0;
        }

        if (levelEntry == null)
        {
            return 0.0;
        }

        double interval = GetEffectiveIntervalSeconds(levelEntry, tool);
        if (interval <= 0.0)
        {
            return 0.0;
        }

        return Math.Max(0.0, Math.Min(1.0, _profile.IdleState.AccumulatedProgressSeconds / interval));
    }

    private void TickIdleProgress()
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

        if (!TryResolveIdleContext(idleState.ActiveEventId, out EventDefinition? eventDefinition, out SkillDefinition? skillDefinition, out SkillLevelEntry? levelEntry, out ItemDefinition? tool))
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog("当前挂机条件已失效，系统已自动停止挂机。");
            return;
        }

        if (eventDefinition == null || skillDefinition == null || levelEntry == null)
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog("挂机上下文解析不完整，系统已自动停止挂机。");
            return;
        }

        if (!EventAvailabilityEvaluator.CanInteractButton(_profile, eventDefinition))
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog("当前挂机项目已不再满足显示或互动条件，系统已自动停止挂机。");
            return;
        }

        double interval = GetEffectiveIntervalSeconds(levelEntry, tool);
        if (interval <= 0.0)
        {
            return;
        }

        int completedCycles = (int)Math.Floor(idleState.AccumulatedProgressSeconds / interval);
        if (completedCycles <= 0)
        {
            return;
        }

        int affordableCycles = GetAffordableCycleCount(eventDefinition, completedCycles);
        if (affordableCycles <= 0)
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog($"挂机材料不足，已自动停止：{GameManager.Instance?.TranslateText(eventDefinition.NameKey) ?? eventDefinition.NameKey}");
            return;
        }

        idleState.AccumulatedProgressSeconds -= affordableCycles * interval;
        ConsumeCycleCosts(eventDefinition, affordableCycles);

        double outputMultiplier = tool?.ToolBonuses.LogYieldMultiplier ?? 1.0;
        double outputWithFraction = (affordableCycles * levelEntry.Output * outputMultiplier) + idleState.PendingOutputFraction;
        int wholeOutput = (int)Math.Floor(outputWithFraction);
        idleState.PendingOutputFraction = outputWithFraction - wholeOutput;

        if (wholeOutput > 0)
        {
            _profile.Inventory.AddItem(skillDefinition.PrimaryOutputItemId, wholeOutput);
        }

        int expPerCycle = Math.Max(1, (int)Math.Round(levelEntry.Interval, MidpointRounding.AwayFromZero));
        double totalExp = affordableCycles * expPerCycle;
        GameManager.Instance?.SkillSystem?.AddExp(skillDefinition.Id, totalExp);

        string eventName = GameManager.Instance?.TranslateText(eventDefinition.NameKey) ?? eventDefinition.NameKey;
        string outputName = GameManager.Instance?.GetItemDisplayName(skillDefinition.PrimaryOutputItemId) ?? skillDefinition.PrimaryOutputItemId;
        GameManager.Instance?.AddGameLog($"挂机结算：{eventName} 完成 {affordableCycles} 次，获得 {wholeOutput} 个 {outputName}，经验 +{totalExp:0}。");
        EmitIdleEventChanged(idleState.ActiveEventId);

        if (affordableCycles < completedCycles)
        {
            StopIdleEvent();
            GameManager.Instance?.AddGameLog($"挂机材料已耗尽，已自动停止：{eventName}");
        }
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

    private static double GetEffectiveIntervalSeconds(SkillLevelEntry levelEntry, ItemDefinition? tool)
    {
        double speedMultiplier = tool?.ToolBonuses.ChopSpeedMultiplier ?? 1.0;
        if (speedMultiplier <= 0.0)
        {
            speedMultiplier = 1.0;
        }

        return Math.Max(0.2, levelEntry.Interval / speedMultiplier);
    }

    private int GetAffordableCycleCount(EventDefinition definition, int requestedCycles)
    {
        if (_profile == null || requestedCycles <= 0)
        {
            return 0;
        }

        int affordableCycles = requestedCycles;
        foreach (ItemCostEntry cost in definition.Costs)
        {
            if (cost.Amount <= 0)
            {
                continue;
            }

            int ownedAmount = _profile.Inventory.GetItemAmount(cost.ItemId);
            int cyclesByThisCost = ownedAmount / cost.Amount;
            affordableCycles = Math.Min(affordableCycles, cyclesByThisCost);
        }

        return affordableCycles;
    }

    private void ConsumeCycleCosts(EventDefinition definition, int cycleCount)
    {
        if (_profile == null || cycleCount <= 0)
        {
            return;
        }

        foreach (ItemCostEntry cost in definition.Costs)
        {
            int totalAmount = cost.Amount * cycleCount;
            if (totalAmount > 0)
            {
                _profile.Inventory.TryRemoveItem(cost.ItemId, totalAmount);
            }
        }
    }

    private static long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void EmitIdleEventChanged(string eventId)
    {
        _signalBus ??= GetNodeOrNull<SignalBus>("/root/SignalBus");
        _signalBus?.EmitSignal(SignalBus.SignalName.ActiveIdleEventChanged, eventId);
    }
}
