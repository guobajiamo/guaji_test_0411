using Godot;
using System;
using System.Collections.Generic;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

public sealed class BattleStartRequest
{
    public string BattleId { get; set; } = string.Empty;

    public string EncounterDisplayName { get; set; } = string.Empty;

    public double EnemyMaxHp { get; set; } = 20.0;

    public double EnemyCurrentHp { get; set; }

    public double KillSkillExp { get; set; } = 12.0;
}

public partial class BattleSystem : Node
{
    private const double OffHandOpeningDelayMultiplier = 0.5;

    private PlayerProfile? _profile;
    private ItemRegistry? _itemRegistry;
    private EquipmentSystem? _equipmentSystem;
    private ValueSettlementService? _settlementService;
    private SkillRegistry? _skillRegistry;
    private BattleEncounterRegistry? _battleEncounterRegistry;
    private BuffSystem? _buffSystem;
    private readonly BattleRuntimeState _runtime = new();

    public bool IsBattleActive => _runtime.IsActive;

    public string CurrentBattleId => _runtime.BattleId;

    public double CurrentEnemyHp => _runtime.EnemyCurrentHp;

    public double CurrentEnemyMaxHp => _runtime.EnemyMaxHp;

    public double CurrentPlayerHp => _runtime.PlayerCurrentHp;

    public double CurrentPlayerMaxHp => _runtime.PlayerMaxHp;

    public string CurrentEncounterDisplayName => _runtime.EncounterDisplayName;

    public bool LastBattleSucceeded => _runtime.LastBattleSucceeded;

    public string LastEndReason => _runtime.LastEndReason;

    public override void _Ready()
    {
        SetProcess(true);
    }

    public void Configure(
        PlayerProfile profile,
        ItemRegistry itemRegistry,
        EquipmentSystem? equipmentSystem,
        ValueSettlementService? settlementService,
        SkillRegistry? skillRegistry,
        BattleEncounterRegistry? battleEncounterRegistry,
        BuffSystem? buffSystem)
    {
        _profile = profile;
        _itemRegistry = itemRegistry;
        _equipmentSystem = equipmentSystem;
        _settlementService = settlementService;
        _skillRegistry = skillRegistry;
        _battleEncounterRegistry = battleEncounterRegistry;
        _buffSystem = buffSystem;
        RefreshRuntimeStats();
    }

    public override void _Process(double delta)
    {
        TryRefreshStapleAutoConsume();
        AdvanceBattle(delta);
    }

    public void StartBattle(string battleId)
    {
        StartBattle(new BattleStartRequest { BattleId = battleId });
    }

    public void StartBattle(BattleStartRequest request)
    {
        if (_profile == null || _itemRegistry == null)
        {
            return;
        }

        if (_runtime.IsActive)
        {
            AbortBattle("当前战斗被新的战斗请求覆盖。", addLog: false);
        }

        EnsureSelectedStaplePreparedForBattle();

        BattleEncounterDefinition? encounterDefinition = ResolveEncounterDefinition(request.BattleId);
        BattleEncounterSnapshot encounterSnapshot = encounterDefinition != null
            ? BattleFormulaService.BuildEncounterSnapshot(encounterDefinition, TranslateText)
            : BuildFallbackEncounterSnapshot(request);
        string battleId = string.IsNullOrWhiteSpace(request.BattleId)
            ? encounterSnapshot.EncounterId
            : request.BattleId.Trim();
        string encounterDisplayName = string.IsNullOrWhiteSpace(request.EncounterDisplayName)
            ? encounterSnapshot.DisplayName
            : request.EncounterDisplayName.Trim();
        BattlePlayerSnapshot playerSnapshot = BattleFormulaService.BuildPlayerSnapshot(_profile, _itemRegistry, _settlementService);
        BattleWeaponSnapshot? mainWeapon = BattleFormulaService.BuildWeaponSnapshot(_profile, _itemRegistry, playerSnapshot, EquipmentSlotId.MainHand);
        BattleWeaponSnapshot? offWeapon = BattleFormulaService.BuildWeaponSnapshot(_profile, _itemRegistry, playerSnapshot, EquipmentSlotId.OffHand);

        if (mainWeapon == null && offWeapon == null)
        {
            RefreshRuntimeStats();
            GameManager.Instance?.AddGameLog(
                $"战斗无法开始：{encounterDisplayName}。当前没有可执行攻击的物理武器。",
                printToConsole: false);
            return;
        }

        if (mainWeapon != null)
        {
            BattleFormulaService.PopulateExpectedDamage(mainWeapon, playerSnapshot, encounterSnapshot);
        }

        if (offWeapon != null)
        {
            BattleFormulaService.PopulateExpectedDamage(offWeapon, playerSnapshot, encounterSnapshot);
        }

        EnemyAttackState enemyAttack = BuildEnemyAttackState(encounterSnapshot, playerSnapshot);
        double enemyCurrentHp = request.EnemyCurrentHp > 0.0
            ? Math.Clamp(request.EnemyCurrentHp, 0.0, encounterSnapshot.MaxHp)
            : encounterSnapshot.MaxHp;

        _runtime.ResetForNewBattle();
        _runtime.IsActive = true;
        _runtime.BattleId = battleId;
        _runtime.EncounterDisplayName = encounterDisplayName;
        _runtime.EncounterDefinition = encounterDefinition;
        _runtime.EncounterSnapshot = encounterSnapshot;
        _runtime.PlayerSnapshot = playerSnapshot;
        _runtime.EnemyCurrentHp = enemyCurrentHp;
        _runtime.EnemyMaxHp = encounterSnapshot.MaxHp;
        _runtime.PlayerCurrentHp = playerSnapshot.MaxHp;
        _runtime.PlayerMaxHp = playerSnapshot.MaxHp;
        _runtime.KillSkillExp = encounterDefinition?.KillSkillExp ?? Math.Max(0.0, request.KillSkillExp);
        _runtime.MainHand = mainWeapon != null ? BuildHandAttackState(mainWeapon) : null;
        _runtime.OffHand = offWeapon != null ? BuildHandAttackState(offWeapon) : null;
        _runtime.Enemy = enemyAttack;

        if (_runtime.MainHand != null && _runtime.OffHand != null)
        {
            _runtime.OffHand.NextAttackAtSeconds = Math.Max(
                BattleFormulaService.IntervalMin,
                _runtime.OffHand.AttackIntervalSeconds * OffHandOpeningDelayMultiplier);
        }

        IncrementBattleStat(BattleRuntimeStatIds.CumulativeBattlesStarted, 1.0);
        RefreshRuntimeStats();

        GameManager.Instance?.AddGameLog(
            $"战斗开始：{encounterDisplayName}，敌方生命 {enemyCurrentHp:0.##}/{encounterSnapshot.MaxHp:0.##}，角色生命 {_runtime.PlayerCurrentHp:0.##}/{_runtime.PlayerMaxHp:0.##}，主手={DescribeAttackState(_runtime.MainHand)}，副手={DescribeAttackState(_runtime.OffHand)}，敌方期望DPS={enemyAttack.AttackDamage / enemyAttack.AttackIntervalSeconds:0.##}。",
            printToConsole: false);
    }

    public void ResolveTurn()
    {
        if (!_runtime.IsActive)
        {
            return;
        }

        AttackStateBase? nextAttack = GetNextScheduledAttack();
        if (nextAttack == null)
        {
            AbortBattle("当前没有可继续执行的攻击。", addLog: true);
            return;
        }

        double deltaToNextAttack = Math.Max(0.0, nextAttack.NextAttackAtSeconds - _runtime.ElapsedSeconds);
        AdvanceBattle(deltaToNextAttack);
    }

    public void AdvanceBattle(double deltaSeconds)
    {
        if (!_runtime.IsActive)
        {
            return;
        }

        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0.0)
        {
            deltaSeconds = 0.0;
        }

        double targetTime = _runtime.ElapsedSeconds + deltaSeconds;
        int safetyCounter = 0;
        while (_runtime.IsActive && safetyCounter++ < 256)
        {
            AttackStateBase? nextAttack = GetNextScheduledAttack();
            if (nextAttack == null)
            {
                AbortBattle("当前没有可继续执行的攻击。", addLog: true);
                break;
            }

            if (targetTime + 0.0001 < nextAttack.NextAttackAtSeconds)
            {
                break;
            }

            double stepDelta = Math.Max(0.0, nextAttack.NextAttackAtSeconds - _runtime.ElapsedSeconds);
            ApplyPassiveDelta(stepDelta);
            _runtime.ElapsedSeconds = nextAttack.NextAttackAtSeconds;
            ExecuteAttack(nextAttack);
        }

        if (_runtime.IsActive)
        {
            double remainingDelta = Math.Max(0.0, targetTime - _runtime.ElapsedSeconds);
            ApplyPassiveDelta(remainingDelta);
            _runtime.ElapsedSeconds = targetTime;
        }

        if (safetyCounter >= 256)
        {
            GD.PushWarning("[BattleSystem] attack loop safety limit reached.");
        }

        RefreshRuntimeStats();
    }

    public void AbortBattle(string reason, bool addLog = true)
    {
        if (!_runtime.IsActive)
        {
            return;
        }

        _runtime.IsActive = false;
        _runtime.LastBattleSucceeded = false;
        _runtime.LastEndReason = reason;
        RefreshRuntimeStats();

        if (addLog && !string.IsNullOrWhiteSpace(reason))
        {
            GameManager.Instance?.AddGameLog(
                $"战斗中断：{_runtime.EncounterDisplayName}。{reason}",
                printToConsole: false);
        }
    }

    public BattlePlayerSnapshot? GetPlayerPanelSnapshot()
    {
        if (_profile == null || _itemRegistry == null)
        {
            return null;
        }

        return BattleFormulaService.BuildPlayerSnapshot(_profile, _itemRegistry, _settlementService);
    }

    public BattleWeaponSnapshot? GetWeaponPanelSnapshot(EquipmentSlotId slotId)
    {
        if (_profile == null || _itemRegistry == null)
        {
            return null;
        }

        BattlePlayerSnapshot player = BattleFormulaService.BuildPlayerSnapshot(_profile, _itemRegistry, _settlementService);
        BattleWeaponSnapshot? weapon = BattleFormulaService.BuildWeaponSnapshot(_profile, _itemRegistry, player, slotId);
        if (weapon == null)
        {
            return null;
        }

        if (_runtime.EncounterSnapshot != null)
        {
            BattleFormulaService.PopulateExpectedDamage(weapon, player, _runtime.EncounterSnapshot);
        }

        return weapon;
    }

    public double GetPlayerAttackProgressRatio()
    {
        if (!_runtime.IsActive)
        {
            return 0.0;
        }

        HandAttackState? nextAttack = GetNextScheduledPlayerAttack();
        if (nextAttack == null)
        {
            return 0.0;
        }

        return ComputeAttackProgressRatio(nextAttack.NextAttackAtSeconds, nextAttack.AttackIntervalSeconds);
    }

    public double GetEnemyAttackProgressRatio()
    {
        if (!_runtime.IsActive || _runtime.Enemy == null)
        {
            return 0.0;
        }

        return ComputeAttackProgressRatio(_runtime.Enemy.NextAttackAtSeconds, _runtime.Enemy.AttackIntervalSeconds);
    }

    public void SetBattleStat(string statId, double value)
    {
        if (_profile == null || string.IsNullOrWhiteSpace(statId))
        {
            return;
        }

        _profile.BattleStats[statId] = value;
    }

    private void ApplyPassiveDelta(double deltaSeconds)
    {
        if (!_runtime.IsActive || deltaSeconds <= 0.0)
        {
            return;
        }

        if (_runtime.PlayerSnapshot != null)
        {
            _runtime.PlayerCurrentHp = Math.Clamp(
                _runtime.PlayerCurrentHp + _runtime.PlayerSnapshot.RegenHps * deltaSeconds,
                0.0,
                _runtime.PlayerMaxHp);
        }

        if (_runtime.EncounterSnapshot != null)
        {
            _runtime.EnemyCurrentHp = Math.Clamp(
                _runtime.EnemyCurrentHp + _runtime.EncounterSnapshot.RegenHps * deltaSeconds,
                0.0,
                _runtime.EnemyMaxHp);
        }
    }

    private void TryRefreshStapleAutoConsume()
    {
        if (_profile == null)
        {
            return;
        }

        GameManager.Instance?.StapleFoodSystem?.RefreshState();
        PlayerStapleFoodState stapleState = _profile.StapleFoodState;
        if (stapleState.HasActiveStaple)
        {
            return;
        }

        GameManager.Instance?.StapleFoodSystem?.TryAutoConsumeSelectedStaple();
    }

    private void EnsureSelectedStaplePreparedForBattle()
    {
        if (_profile == null)
        {
            return;
        }

        GameManager.Instance?.StapleFoodSystem?.RefreshState();
        if (_profile.StapleFoodState.HasActiveStaple)
        {
            return;
        }

        string selectedItemId = _profile.UiState.SelectedStapleItemId;
        if (string.IsNullOrWhiteSpace(selectedItemId) || _profile.Inventory.GetItemAmount(selectedItemId) <= 0)
        {
            return;
        }

        GameManager.Instance?.StapleFoodSystem?.TryConsumeStaple(selectedItemId, addLog: true);
    }

    private HandAttackState BuildHandAttackState(BattleWeaponSnapshot weapon)
    {
        return new HandAttackState
        {
            SlotId = weapon.SlotId,
            ItemId = weapon.ItemId,
            ItemDisplayName = weapon.DisplayName,
            WeaponArchetype = weapon.Archetype,
            AttackStyle = weapon.AttackStyle,
            PassiveSkillId = weapon.PassiveSkillId,
            RequiredAmmoKind = weapon.RequiredAmmoKind,
            AmmoPerAttack = weapon.AmmoPerAttack,
            AttackIntervalSeconds = weapon.AttackIntervalSeconds,
            AttackDamage = Math.Max(0.0, weapon.ExpectedDamagePerHit),
            NextAttackAtSeconds = Math.Max(BattleFormulaService.IntervalMin, weapon.AttackIntervalSeconds),
            OffHandPenaltySummary = weapon.OffHandPenaltySummary
        };
    }

    private EnemyAttackState BuildEnemyAttackState(BattleEncounterSnapshot encounter, BattlePlayerSnapshot player)
    {
        return new EnemyAttackState
        {
            DisplayName = encounter.DisplayName,
            AttackIntervalSeconds = encounter.AttackIntervalSeconds,
            AttackDamage = Math.Max(0.0, BattleFormulaService.ComputeExpectedEnemyDamagePerHit(encounter, player)),
            NextAttackAtSeconds = Math.Max(BattleFormulaService.IntervalMin, encounter.AttackIntervalSeconds)
        };
    }

    private void ExecuteAttack(AttackStateBase attackState)
    {
        switch (attackState)
        {
            case HandAttackState handAttack:
                ExecutePlayerAttack(handAttack);
                break;
            case EnemyAttackState enemyAttack:
                ExecuteEnemyAttack(enemyAttack);
                break;
        }
    }

    private void ExecutePlayerAttack(HandAttackState attackState)
    {
        if (!_runtime.IsActive)
        {
            return;
        }

        if (attackState.RequiredAmmoKind != AmmoKind.None && !TryConsumeAmmo(attackState))
        {
            attackState.IsDisabled = true;
            attackState.DisabledReason = "missing_ammo";
            attackState.NextAttackAtSeconds = double.PositiveInfinity;

            if (GetNextScheduledPlayerAttack() == null)
            {
                AbortBattle($"{attackState.ItemDisplayName} 需要可用弹药，当前战斗无法继续。", addLog: true);
            }
            else
            {
                GameManager.Instance?.AddGameLog(
                    $"攻击停用：{attackState.ItemDisplayName} 需要 {attackState.RequiredAmmoKind.GetDisplayName()}，但弹药槽没有可用弹药。",
                    printToConsole: false);
            }

            return;
        }

        _runtime.TotalAttackEvents += 1;
        attackState.AttackCount += 1;
        attackState.NextAttackAtSeconds += attackState.AttackIntervalSeconds;

        double damage = Math.Max(0.0, attackState.AttackDamage);
        _runtime.EnemyCurrentHp = Math.Max(0.0, _runtime.EnemyCurrentHp - damage);
        _runtime.LastAttackDamage = damage;
        _runtime.LastAttackWasKill = _runtime.EnemyCurrentHp <= 0.0;
        _runtime.LastAttackWasFromEnemy = false;
        _runtime.LastAttackSlot = attackState.SlotId;
        _runtime.LastAttackStyle = attackState.AttackStyle;
        _runtime.LastKillSkillExpGranted = 0.0;

        IncrementBattleStat(BattleRuntimeStatIds.CumulativeAttackEvents, 1.0);

        if (_runtime.EnemyCurrentHp <= 0.0)
        {
            CompleteBattle(attackState);
        }
    }

    private void ExecuteEnemyAttack(EnemyAttackState attackState)
    {
        if (!_runtime.IsActive)
        {
            return;
        }

        _runtime.TotalEnemyAttackEvents += 1;
        attackState.AttackCount += 1;
        attackState.NextAttackAtSeconds += attackState.AttackIntervalSeconds;

        double damage = Math.Max(0.0, attackState.AttackDamage);
        _runtime.PlayerCurrentHp = Math.Max(0.0, _runtime.PlayerCurrentHp - damage);
        _runtime.LastAttackDamage = damage;
        _runtime.LastAttackWasKill = _runtime.PlayerCurrentHp <= 0.0;
        _runtime.LastAttackWasFromEnemy = true;
        _runtime.LastAttackSlot = null;
        _runtime.LastAttackStyle = CombatAttackStyle.None;
        _runtime.LastKillSkillExpGranted = 0.0;

        if (_runtime.PlayerCurrentHp <= 0.0)
        {
            AbortBattle("角色被击倒，未能撑到击杀敌人。", addLog: true);
        }
    }

    private bool TryConsumeAmmo(HandAttackState attackState)
    {
        if (_profile == null || _itemRegistry == null || _settlementService == null)
        {
            return false;
        }

        if (attackState.RequiredAmmoKind == AmmoKind.None || attackState.AmmoPerAttack <= 0)
        {
            return true;
        }

        string ammoItemId = _profile.EquipmentState.GetEquippedItemId(EquipmentSlotId.Ammo);
        if (string.IsNullOrWhiteSpace(ammoItemId))
        {
            return false;
        }

        ItemDefinition? ammoItem = _itemRegistry.GetItem(ammoItemId);
        if (ammoItem == null || ResolveAmmoKind(ammoItem) != attackState.RequiredAmmoKind)
        {
            return false;
        }

        if (_profile.Inventory.GetItemAmount(ammoItemId) < attackState.AmmoPerAttack)
        {
            _equipmentSystem?.SynchronizeWithInventory();
            return false;
        }

        if (!_settlementService.TryRemoveItem(ammoItemId, attackState.AmmoPerAttack))
        {
            return false;
        }

        _runtime.TotalAmmoConsumed += attackState.AmmoPerAttack;
        IncrementBattleStat(BattleRuntimeStatIds.CumulativeAmmoConsumed, attackState.AmmoPerAttack);

        if (_profile.Inventory.GetItemAmount(ammoItemId) <= 0)
        {
            _equipmentSystem?.SynchronizeWithInventory();
        }

        return true;
    }

    private void CompleteBattle(HandAttackState killingAttack)
    {
        _runtime.IsActive = false;
        _runtime.LastBattleSucceeded = true;
        _runtime.LastEndReason = "victory";
        _runtime.TotalVictories += 1;
        _runtime.LastKillSkillExpGranted = GrantKillSkillExp(killingAttack);

        IncrementBattleStat(BattleRuntimeStatIds.CumulativeBattlesWon, 1.0);
        IncrementBattleStat(BattleRuntimeStatIds.CumulativeBattleKills, 1.0);
        IncrementBattleStat(BattleRuntimeStatIds.CumulativeStyleKills(killingAttack.AttackStyle), 1.0);
        if (_profile != null && !string.IsNullOrWhiteSpace(_runtime.BattleId))
        {
            _profile.ClearedBattleEncounterIds.Add(_runtime.BattleId);
        }

        string rewardSummary = GrantEncounterRewards();
        RefreshRuntimeStats();

        string expText = _runtime.LastKillSkillExpGranted > 0.0
            ? $"，{killingAttack.AttackStyle.GetDisplayName()}经验 +{_runtime.LastKillSkillExpGranted:0.##}"
            : string.Empty;
        string rewardText = string.IsNullOrWhiteSpace(rewardSummary)
            ? string.Empty
            : $"，奖励：{rewardSummary}";
        GameManager.Instance?.AddGameLog(
            $"战斗胜利：{_runtime.EncounterDisplayName}。终结攻击={DescribeAttackState(killingAttack)}，总攻击次数={_runtime.TotalAttackEvents}{expText}{rewardText}。",
            printToConsole: false);
    }

    private string GrantEncounterRewards()
    {
        if (_runtime.EncounterDefinition == null || _settlementService == null)
        {
            return string.Empty;
        }

        List<string> parts = new();
        if (_runtime.EncounterDefinition.GoldReward > 0)
        {
            _settlementService.AddCurrency(ValueSettlementService.GoldCurrencyId, _runtime.EncounterDefinition.GoldReward);
            parts.Add($"金币 +{_runtime.EncounterDefinition.GoldReward}");
        }

        foreach (EventRewardEntry drop in _runtime.EncounterDefinition.DropEntries)
        {
            if (string.IsNullOrWhiteSpace(drop.ItemId) || drop.Amount <= 0)
            {
                continue;
            }

            double dropChance = _settlementService.ResolveRewardDropChance(drop.ItemId, drop.DropChance, _itemRegistry);
            if (dropChance <= 0.0)
            {
                continue;
            }

            if (dropChance < 1.0 && Random.Shared.NextDouble() > dropChance)
            {
                continue;
            }

            _settlementService.AddItem(drop.ItemId, drop.Amount);
            string displayName = GameManager.Instance?.GetItemDisplayName(drop.ItemId) ?? drop.ItemId;
            parts.Add($"{displayName} x{drop.Amount}");
        }

        return string.Join("，", parts);
    }

    private double GrantKillSkillExp(HandAttackState killingAttack)
    {
        if (_profile == null || _settlementService == null || _skillRegistry == null)
        {
            return 0.0;
        }

        string skillId = killingAttack.PassiveSkillId;
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return 0.0;
        }

        SkillDefinition? skillDefinition = _skillRegistry.GetSkill(skillId);
        if (skillDefinition == null)
        {
            return 0.0;
        }

        PlayerSkillState skillState = _profile.GetOrCreateSkillState(skillId);
        if (skillState.Level <= 0)
        {
            return 0.0;
        }

        double grantedExp = _settlementService.ResolveGrantedSkillExp(skillId, _runtime.KillSkillExp);
        _settlementService.GrantSkillExp(skillId, _runtime.KillSkillExp);
        IncrementBattleStat(BattleRuntimeStatIds.CumulativeWeaponStyleExpGranted, grantedExp);
        return grantedExp;
    }

    private void RefreshRuntimeStats()
    {
        if (_profile == null)
        {
            return;
        }

        double playerExpectedDps = (_runtime.MainHand?.AttackDamage ?? 0.0) / Math.Max(BattleFormulaService.IntervalMin, _runtime.MainHand?.AttackIntervalSeconds ?? 1.0)
            + (_runtime.OffHand?.AttackDamage ?? 0.0) / Math.Max(BattleFormulaService.IntervalMin, _runtime.OffHand?.AttackIntervalSeconds ?? 1.0);
        double enemyExpectedDps = _runtime.Enemy == null
            ? 0.0
            : _runtime.Enemy.AttackDamage / Math.Max(BattleFormulaService.IntervalMin, _runtime.Enemy.AttackIntervalSeconds);

        SetBattleStat(BattleRuntimeStatIds.RuntimeIsActive, _runtime.IsActive ? 1.0 : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeElapsedSeconds, _runtime.ElapsedSeconds);
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyCurrentHp, _runtime.EnemyCurrentHp);
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyMaxHp, _runtime.EnemyMaxHp);
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyRemainingRatio, _runtime.EnemyMaxHp > 0.0 ? _runtime.EnemyCurrentHp / _runtime.EnemyMaxHp : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimePlayerCurrentHp, _runtime.PlayerCurrentHp);
        SetBattleStat(BattleRuntimeStatIds.RuntimePlayerMaxHp, _runtime.PlayerMaxHp);
        SetBattleStat(BattleRuntimeStatIds.RuntimePlayerRemainingRatio, _runtime.PlayerMaxHp > 0.0 ? _runtime.PlayerCurrentHp / _runtime.PlayerMaxHp : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeAttackEvents, _runtime.TotalAttackEvents);
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyAttackEvents, _runtime.TotalEnemyAttackEvents);
        SetBattleStat(BattleRuntimeStatIds.RuntimeAmmoConsumed, _runtime.TotalAmmoConsumed);
        SetBattleStat(BattleRuntimeStatIds.RuntimeLastAttackDamage, _runtime.LastAttackDamage);
        SetBattleStat(BattleRuntimeStatIds.RuntimeLastAttackWasKill, _runtime.LastAttackWasKill ? 1.0 : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeLastAttackWasFromEnemy, _runtime.LastAttackWasFromEnemy ? 1.0 : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeLastBattleSucceeded, _runtime.LastBattleSucceeded ? 1.0 : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeLastKillSkillExpGranted, _runtime.LastKillSkillExpGranted);
        SetBattleStat(BattleRuntimeStatIds.RuntimeLastAttackSlotCode, ToHandCode(_runtime.LastAttackSlot));
        SetBattleStat(BattleRuntimeStatIds.RuntimeLastAttackStyleCode, (double)_runtime.LastAttackStyle);
        SetBattleStat(BattleRuntimeStatIds.RuntimeMainHandAttackCount, _runtime.MainHand?.AttackCount ?? 0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeOffHandAttackCount, _runtime.OffHand?.AttackCount ?? 0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyAttackCount, _runtime.Enemy?.AttackCount ?? 0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeMainHandEnabled, _runtime.MainHand?.CanContinueAttacking == true ? 1.0 : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeOffHandEnabled, _runtime.OffHand?.CanContinueAttacking == true ? 1.0 : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyEnabled, _runtime.Enemy?.CanContinueAttacking == true ? 1.0 : 0.0);
        SetBattleStat(BattleRuntimeStatIds.RuntimeMainHandNextAttackAt, ToFiniteStatValue(_runtime.MainHand?.NextAttackAtSeconds ?? 0.0));
        SetBattleStat(BattleRuntimeStatIds.RuntimeOffHandNextAttackAt, ToFiniteStatValue(_runtime.OffHand?.NextAttackAtSeconds ?? 0.0));
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyNextAttackAt, ToFiniteStatValue(_runtime.Enemy?.NextAttackAtSeconds ?? 0.0));
        SetBattleStat(BattleRuntimeStatIds.RuntimePlayerExpectedDps, playerExpectedDps);
        SetBattleStat(BattleRuntimeStatIds.RuntimeEnemyExpectedDps, enemyExpectedDps);
    }

    private AttackStateBase? GetNextScheduledAttack()
    {
        AttackStateBase? nextAttack = null;
        if (_runtime.MainHand?.CanContinueAttacking == true)
        {
            nextAttack = _runtime.MainHand;
        }

        if (_runtime.OffHand?.CanContinueAttacking == true
            && (nextAttack == null || _runtime.OffHand.NextAttackAtSeconds < nextAttack.NextAttackAtSeconds))
        {
            nextAttack = _runtime.OffHand;
        }

        if (_runtime.Enemy?.CanContinueAttacking == true
            && (nextAttack == null || _runtime.Enemy.NextAttackAtSeconds < nextAttack.NextAttackAtSeconds))
        {
            nextAttack = _runtime.Enemy;
        }

        return nextAttack;
    }

    private HandAttackState? GetNextScheduledPlayerAttack()
    {
        HandAttackState? nextAttack = null;
        if (_runtime.MainHand?.CanContinueAttacking == true)
        {
            nextAttack = _runtime.MainHand;
        }

        if (_runtime.OffHand?.CanContinueAttacking == true
            && (nextAttack == null || _runtime.OffHand.NextAttackAtSeconds < nextAttack.NextAttackAtSeconds))
        {
            nextAttack = _runtime.OffHand;
        }

        return nextAttack;
    }

    private BattleEncounterDefinition? ResolveEncounterDefinition(string battleId)
    {
        if (_battleEncounterRegistry == null || string.IsNullOrWhiteSpace(battleId))
        {
            return null;
        }

        return _battleEncounterRegistry.GetEncounter(battleId.Trim());
    }

    private BattleEncounterSnapshot BuildFallbackEncounterSnapshot(BattleStartRequest request)
    {
        int inferredLevel = _profile != null && _itemRegistry != null
            ? BattleFormulaService.ResolvePlayerBattleLevel(_profile, _itemRegistry)
            : 1;
        double maxHp = Math.Max(1.0, request.EnemyMaxHp);
        string displayName = string.IsNullOrWhiteSpace(request.EncounterDisplayName)
            ? (string.IsNullOrWhiteSpace(request.BattleId) ? "战斗测试目标" : request.BattleId)
            : request.EncounterDisplayName;

        return new BattleEncounterSnapshot
        {
            EncounterId = string.IsNullOrWhiteSpace(request.BattleId) ? "battle" : request.BattleId.Trim(),
            DisplayName = displayName,
            EncounterType = "Normal",
            BattleLevel = Math.Max(1, inferredLevel),
            MaxHp = maxHp,
            Accuracy = 45.0 + inferredLevel * 4.0,
            Evasion = 18.0 + inferredLevel * 2.0,
            Defense = 10.0 + inferredLevel * 3.0,
            PhysicalResistance = 0.02,
            DamageReductionFlat = 0.0,
            DamageAddPercent = 0.0,
            CritChance = 0.02,
            CritMultiplier = 1.5,
            WeaponPower = 12.0 + inferredLevel * 1.8,
            AttackIntervalSeconds = 2.0,
            RegenHps = 0.0
        };
    }

    private static AmmoKind ResolveAmmoKind(ItemDefinition ammoItem)
    {
        if (ammoItem.BattleEquipment.Ammo.IsConfigured)
        {
            return ammoItem.BattleEquipment.Ammo.AmmoKind;
        }

        if (!ammoItem.HasTag(ItemTag.Ammo))
        {
            return AmmoKind.None;
        }

        string keywordSource = $"{ammoItem.Id}|{ammoItem.ParentId}|{ammoItem.NameKey}|{ammoItem.DescriptionKey}".ToLowerInvariant();
        if (keywordSource.Contains("arrow", StringComparison.Ordinal)
            || keywordSource.Contains("箭", StringComparison.Ordinal))
        {
            return AmmoKind.Arrow;
        }

        if (keywordSource.Contains("bolt", StringComparison.Ordinal)
            || keywordSource.Contains("弩", StringComparison.Ordinal))
        {
            return AmmoKind.Bolt;
        }

        if (keywordSource.Contains("bullet", StringComparison.Ordinal)
            || keywordSource.Contains("弹", StringComparison.Ordinal))
        {
            return AmmoKind.Bullet;
        }

        return AmmoKind.None;
    }

    private string TranslateText(string text)
    {
        return GameManager.Instance?.TranslateText(text) ?? text;
    }

    private static string DescribeAttackState(HandAttackState? attackState)
    {
        if (attackState == null)
        {
            return "空";
        }

        string handText = attackState.SlotId == EquipmentSlotId.MainHand ? "主手" : "副手";
        string disabledSuffix = attackState.IsDisabled ? "（停用）" : string.Empty;
        string penaltySuffix = string.IsNullOrWhiteSpace(attackState.OffHandPenaltySummary) || attackState.OffHandPenaltySummary == "无"
            ? string.Empty
            : $"，副手修正={attackState.OffHandPenaltySummary}";
        return $"{handText}:{attackState.ItemDisplayName}{disabledSuffix}，伤害≈{attackState.AttackDamage:0.##}，间隔={attackState.AttackIntervalSeconds:0.##}s{penaltySuffix}";
    }

    private void IncrementBattleStat(string statId, double delta)
    {
        if (_profile == null || string.IsNullOrWhiteSpace(statId) || delta == 0.0)
        {
            return;
        }

        _profile.BattleStats.TryGetValue(statId, out double currentValue);
        _profile.BattleStats[statId] = currentValue + delta;
    }

    private static double ToHandCode(EquipmentSlotId? slotId)
    {
        return slotId switch
        {
            EquipmentSlotId.MainHand => 1.0,
            EquipmentSlotId.OffHand => 2.0,
            _ => 0.0
        };
    }

    private static double ToFiniteStatValue(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }

    private double ComputeAttackProgressRatio(double nextAttackAtSeconds, double attackIntervalSeconds)
    {
        if (!_runtime.IsActive || attackIntervalSeconds <= BattleFormulaService.IntervalMin)
        {
            return 0.0;
        }

        double elapsedSincePreviousAttack = attackIntervalSeconds - Math.Max(0.0, nextAttackAtSeconds - _runtime.ElapsedSeconds);
        return Math.Clamp(elapsedSincePreviousAttack / attackIntervalSeconds, 0.0, 1.0);
    }

    private abstract class AttackStateBase
    {
        public double AttackIntervalSeconds { get; set; } = 1.0;

        public double AttackDamage { get; set; } = 1.0;

        public double NextAttackAtSeconds { get; set; } = 1.0;

        public int AttackCount { get; set; }

        public bool IsDisabled { get; set; }

        public string DisabledReason { get; set; } = string.Empty;

        public bool CanContinueAttacking => !IsDisabled;
    }

    private sealed class BattleRuntimeState
    {
        public bool IsActive { get; set; }

        public string BattleId { get; set; } = string.Empty;

        public string EncounterDisplayName { get; set; } = string.Empty;

        public BattleEncounterDefinition? EncounterDefinition { get; set; }

        public BattleEncounterSnapshot? EncounterSnapshot { get; set; }

        public BattlePlayerSnapshot? PlayerSnapshot { get; set; }

        public double EnemyCurrentHp { get; set; }

        public double EnemyMaxHp { get; set; }

        public double PlayerCurrentHp { get; set; }

        public double PlayerMaxHp { get; set; }

        public double KillSkillExp { get; set; }

        public double ElapsedSeconds { get; set; }

        public int TotalAttackEvents { get; set; }

        public int TotalEnemyAttackEvents { get; set; }

        public int TotalVictories { get; set; }

        public int TotalAmmoConsumed { get; set; }

        public double LastAttackDamage { get; set; }

        public double LastKillSkillExpGranted { get; set; }

        public bool LastAttackWasKill { get; set; }

        public bool LastAttackWasFromEnemy { get; set; }

        public bool LastBattleSucceeded { get; set; }

        public string LastEndReason { get; set; } = string.Empty;

        public EquipmentSlotId? LastAttackSlot { get; set; }

        public CombatAttackStyle LastAttackStyle { get; set; } = CombatAttackStyle.None;

        public HandAttackState? MainHand { get; set; }

        public HandAttackState? OffHand { get; set; }

        public EnemyAttackState? Enemy { get; set; }

        public void ResetForNewBattle()
        {
            IsActive = false;
            BattleId = string.Empty;
            EncounterDisplayName = string.Empty;
            EncounterDefinition = null;
            EncounterSnapshot = null;
            PlayerSnapshot = null;
            EnemyCurrentHp = 0.0;
            EnemyMaxHp = 0.0;
            PlayerCurrentHp = 0.0;
            PlayerMaxHp = 0.0;
            KillSkillExp = 0.0;
            ElapsedSeconds = 0.0;
            TotalAttackEvents = 0;
            TotalEnemyAttackEvents = 0;
            TotalVictories = 0;
            TotalAmmoConsumed = 0;
            LastAttackDamage = 0.0;
            LastKillSkillExpGranted = 0.0;
            LastAttackWasKill = false;
            LastAttackWasFromEnemy = false;
            LastBattleSucceeded = false;
            LastEndReason = string.Empty;
            LastAttackSlot = null;
            LastAttackStyle = CombatAttackStyle.None;
            MainHand = null;
            OffHand = null;
            Enemy = null;
        }
    }

    private sealed class HandAttackState : AttackStateBase
    {
        public EquipmentSlotId SlotId { get; set; }

        public string ItemId { get; set; } = string.Empty;

        public string ItemDisplayName { get; set; } = string.Empty;

        public WeaponArchetype WeaponArchetype { get; set; } = WeaponArchetype.None;

        public CombatAttackStyle AttackStyle { get; set; } = CombatAttackStyle.None;

        public string PassiveSkillId { get; set; } = string.Empty;

        public AmmoKind RequiredAmmoKind { get; set; } = AmmoKind.None;

        public int AmmoPerAttack { get; set; }

        public string OffHandPenaltySummary { get; set; } = string.Empty;
    }

    private sealed class EnemyAttackState : AttackStateBase
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}
