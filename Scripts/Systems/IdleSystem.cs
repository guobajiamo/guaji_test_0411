using Godot;
using System;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 挂机循环系统。
/// 它以后会负责：计时、读条、结算资源、发放技能经验。
/// </summary>
public partial class IdleSystem : Node
{
    private PlayerProfile? _profile;
    private EventRegistry? _eventRegistry;
    private SkillRegistry? _skillRegistry;
    private ItemRegistry? _itemRegistry;

    public void Configure(PlayerProfile profile, EventRegistry eventRegistry, SkillRegistry skillRegistry, ItemRegistry itemRegistry)
    {
        _profile = profile;
        _eventRegistry = eventRegistry;
        _skillRegistry = skillRegistry;
        _itemRegistry = itemRegistry;
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
    /// 这里只先定义清楚契约，具体收益算法等后续接技能表时再补。
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

    public bool CanStartIdleEvent(string eventId)
    {
        return TryResolveIdleContext(eventId, out _, out _, out _, out _);
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

        idleState.AccumulatedProgressSeconds -= completedCycles * interval;

        double outputMultiplier = tool?.ToolBonuses.LogYieldMultiplier ?? 1.0;
        double outputWithFraction = (completedCycles * levelEntry.Output * outputMultiplier) + idleState.PendingOutputFraction;
        int wholeOutput = (int)Math.Floor(outputWithFraction);
        idleState.PendingOutputFraction = outputWithFraction - wholeOutput;

        if (wholeOutput > 0)
        {
            _profile.Inventory.AddItem(skillDefinition.PrimaryOutputItemId, wholeOutput);
        }

        // 经验门槛表当前都是整数值，所以这里把每轮经验奖励也收敛为整数，
        // 避免升级后 UI 突然出现很多小数经验，影响首版文字游戏的直观体验。
        int expPerCycle = Math.Max(1, (int)Math.Round(levelEntry.Interval, MidpointRounding.AwayFromZero));
        double totalExp = completedCycles * expPerCycle;
        GameManager.Instance?.SkillSystem?.AddExp(skillDefinition.Id, totalExp);

        string eventName = GameManager.Instance?.TranslateText(eventDefinition.NameKey) ?? eventDefinition.NameKey;
        string outputName = GameManager.Instance?.GetItemDisplayName(skillDefinition.PrimaryOutputItemId) ?? skillDefinition.PrimaryOutputItemId;
        GameManager.Instance?.AddGameLog($"挂机结算：{eventName} 完成 {completedCycles} 次，获得 {wholeOutput} 个 {outputName}，经验 +{totalExp:0}。");
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

    private static long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
