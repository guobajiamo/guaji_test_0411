using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;
using Test00_0410.Systems;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// Unified settlement middle-layer for item/currency/exp operations and numeric multiplier resolution.
/// Systems should call this service instead of mutating inventory/economy/exp directly.
/// </summary>
public sealed class ValueSettlementService
{
    public const string GoldCurrencyId = "gold";

    private PlayerProfile? _profile;
    private BuffSystem? _buffSystem;
    private SkillSystem? _skillSystem;

    public void Configure(PlayerProfile profile, BuffSystem? buffSystem, SkillSystem? skillSystem)
    {
        _profile = profile;
        _buffSystem = buffSystem;
        _skillSystem = skillSystem;
        SyncCurrencyInventoryFromEconomy();
    }

    public string GetCurrencyItemId(string currencyId)
    {
        string normalizedCurrencyId = NormalizeCurrencyId(currencyId);
        return normalizedCurrencyId == GoldCurrencyId
            ? "currency_gold"
            : $"currency_{normalizedCurrencyId}";
    }

    public int GetCurrencyAmount(string currencyId)
    {
        if (_profile == null)
        {
            return 0;
        }

        string normalizedCurrencyId = NormalizeCurrencyId(currencyId);
        if (normalizedCurrencyId == GoldCurrencyId)
        {
            return _profile.Economy.Gold;
        }

        return _profile.Economy.ExtraCurrencies.TryGetValue(normalizedCurrencyId, out int amount) ? amount : 0;
    }

    public bool HasCurrency(string currencyId, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return GetCurrencyAmount(currencyId) >= amount;
    }

    public void AddCurrency(string currencyId, int amount)
    {
        if (_profile == null || amount <= 0)
        {
            return;
        }

        string normalizedCurrencyId = NormalizeCurrencyId(currencyId);
        if (normalizedCurrencyId == GoldCurrencyId)
        {
            _profile.Economy.AddGold(amount);
        }
        else
        {
            int currentAmount = GetCurrencyAmount(normalizedCurrencyId);
            _profile.Economy.ExtraCurrencies[normalizedCurrencyId] = currentAmount + amount;
        }

        _profile.Inventory.AddItem(GetCurrencyItemId(normalizedCurrencyId), amount);
    }

    public bool TrySpendCurrency(string currencyId, int amount)
    {
        if (_profile == null || amount < 0)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        string normalizedCurrencyId = NormalizeCurrencyId(currencyId);
        bool spent = normalizedCurrencyId == GoldCurrencyId
            ? _profile.Economy.TrySpendGold(amount)
            : TrySpendExtraCurrency(normalizedCurrencyId, amount);
        if (!spent)
        {
            return false;
        }

        string currencyItemId = GetCurrencyItemId(normalizedCurrencyId);
        if (!_profile.Inventory.TryRemoveItem(currencyItemId, amount))
        {
            SyncCurrencyItemToInventory(normalizedCurrencyId, GetCurrencyAmount(normalizedCurrencyId));
        }

        return true;
    }

    public bool CanPayItemCosts(IEnumerable<ItemCostEntry> costs, int cycleCount = 1)
    {
        if (_profile == null || cycleCount <= 0)
        {
            return false;
        }

        Dictionary<string, int> requiredTotals = new(StringComparer.Ordinal);
        foreach (ItemCostEntry cost in costs)
        {
            if (cost.Amount <= 0)
            {
                continue;
            }

            long requiredAmount = (long)cost.Amount * cycleCount;
            if (requiredAmount > int.MaxValue)
            {
                return false;
            }

            requiredTotals.TryGetValue(cost.ItemId, out int accumulatedAmount);
            requiredTotals[cost.ItemId] = accumulatedAmount + (int)requiredAmount;
        }

        foreach ((string itemId, int totalAmount) in requiredTotals)
        {
            if (!_profile.Inventory.HasItem(itemId, totalAmount))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryPayItemCosts(IEnumerable<ItemCostEntry> costs, int cycleCount = 1)
    {
        if (_profile == null || cycleCount <= 0 || !CanPayItemCosts(costs, cycleCount))
        {
            return false;
        }

        TransactionHelper transaction = new();
        foreach (ItemCostEntry cost in costs)
        {
            if (cost.Amount <= 0)
            {
                continue;
            }

            int requiredAmount = checked(cost.Amount * cycleCount);
            transaction.QueueRemoveItem(cost.ItemId, requiredAmount);
        }

        return transaction.TryCommit(_profile.Inventory);
    }

    public bool TryExchangeItems(IEnumerable<ItemCostEntry> costs, IEnumerable<ItemCostEntry> rewards)
    {
        if (_profile == null || !CanPayItemCosts(costs))
        {
            return false;
        }

        TransactionHelper transaction = new();
        foreach (ItemCostEntry cost in costs)
        {
            if (cost.Amount > 0)
            {
                transaction.QueueRemoveItem(cost.ItemId, cost.Amount);
            }
        }

        foreach (ItemCostEntry reward in rewards)
        {
            if (reward.Amount > 0)
            {
                transaction.QueueAddItem(reward.ItemId, reward.Amount);
            }
        }

        return transaction.TryCommit(_profile.Inventory);
    }

    public void AddItem(string itemId, int amount)
    {
        if (_profile == null || amount <= 0)
        {
            return;
        }

        _profile.Inventory.AddItem(itemId, amount);
        GameManager.Instance?.NotifyItemAcquired(itemId);
    }

    public bool TryRemoveItem(string itemId, int amount)
    {
        return _profile != null && _profile.Inventory.TryRemoveItem(itemId, amount);
    }

    public int ResolveBuyGoldCost(string npcId, int baseGoldCost)
    {
        if (baseGoldCost <= 0)
        {
            return 0;
        }

        double multiplier = GetCombinedMultiplier(
            SettlementStatIds.TradeBuyPriceMultiplier,
            SettlementStatIds.NpcTradeBuyMultiplier(npcId));
        double adjusted = Math.Max(0.0, baseGoldCost * multiplier);
        return (int)Math.Round(adjusted, MidpointRounding.AwayFromZero);
    }

    public int ResolveSellGoldIncome(string itemId, int unitSellPrice, int quantity)
    {
        if (unitSellPrice <= 0 || quantity <= 0)
        {
            return 0;
        }

        long baseIncome = (long)unitSellPrice * quantity;
        double multiplier = GetCombinedMultiplier(
            SettlementStatIds.TradeSellPriceMultiplier,
            SettlementStatIds.ItemTradeSellMultiplier(itemId));
        double adjusted = Math.Max(0.0, baseIncome * multiplier);
        return (int)Math.Round(Math.Min(int.MaxValue, adjusted), MidpointRounding.AwayFromZero);
    }

    public double ResolveEffectiveIdleIntervalSeconds(string skillId, double baseIntervalSeconds, double baseToolSpeedMultiplier = 1.0)
    {
        double speedMultiplier = baseToolSpeedMultiplier <= 0.0 ? 1.0 : baseToolSpeedMultiplier;
        speedMultiplier *= GetCombinedMultiplier(
            SettlementStatIds.IdleSpeedMultiplier,
            SettlementStatIds.SkillIdleSpeedMultiplier(skillId));
        if (speedMultiplier <= 0.0)
        {
            speedMultiplier = 1.0;
        }

        return Math.Max(0.2, baseIntervalSeconds / speedMultiplier);
    }

    public double ResolveGlobalIdleIntervalSeconds(double baseIntervalSeconds, double baseToolSpeedMultiplier = 1.0)
    {
        double speedMultiplier = baseToolSpeedMultiplier <= 0.0 ? 1.0 : baseToolSpeedMultiplier;
        speedMultiplier *= GetCombinedMultiplier(SettlementStatIds.IdleSpeedMultiplier);
        if (speedMultiplier <= 0.0)
        {
            speedMultiplier = 1.0;
        }

        return Math.Max(0.2, baseIntervalSeconds / speedMultiplier);
    }

    public double ResolveIdleOutput(string skillId, double baseOutput, double baseToolYieldMultiplier = 1.0)
    {
        double yieldMultiplier = baseToolYieldMultiplier <= 0.0 ? 1.0 : baseToolYieldMultiplier;
        yieldMultiplier *= GetCombinedMultiplier(
            SettlementStatIds.IdleOutputMultiplier,
            SettlementStatIds.SkillIdleOutputMultiplier(skillId));
        return Math.Max(0.0, baseOutput * Math.Max(0.0, yieldMultiplier));
    }

    public double ResolveGlobalIdleOutput(double baseOutput, double baseToolYieldMultiplier = 1.0)
    {
        double yieldMultiplier = baseToolYieldMultiplier <= 0.0 ? 1.0 : baseToolYieldMultiplier;
        yieldMultiplier *= GetCombinedMultiplier(SettlementStatIds.IdleOutputMultiplier);
        return Math.Max(0.0, baseOutput * Math.Max(0.0, yieldMultiplier));
    }

    public double ResolveGrantedSkillExp(string skillId, double baseExp)
    {
        if (baseExp <= 0.0)
        {
            return 0.0;
        }

        double multiplier = GetCombinedMultiplier(
            SettlementStatIds.SkillExpGainMultiplier,
            SettlementStatIds.SkillExpGainMultiplierBySkill(skillId));
        return Math.Max(0.0, baseExp * Math.Max(0.0, multiplier));
    }

    public double GetBattlePlayerMaxHpMultiplier()
    {
        return NormalizeMultiplier(GetCombinedMultiplier(SettlementStatIds.BattlePlayerMaxHpMultiplier));
    }

    public double GetBattlePlayerActionSpeedMultiplier()
    {
        return NormalizeMultiplier(GetCombinedMultiplier(SettlementStatIds.BattlePlayerActionSpeedMultiplier));
    }

    public double ResolveRewardDropChance(string itemId, double baseDropChance, ItemRegistry? itemRegistry = null)
    {
        double normalizedBaseChance = Math.Clamp(baseDropChance, 0.0, 1.0);
        if (normalizedBaseChance <= 0.0)
        {
            return 0.0;
        }

        double multiplier = NormalizeMultiplier(GetCombinedMultiplier(SettlementStatIds.DropChanceMultiplier));
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            multiplier *= NormalizeMultiplier(GetCombinedMultiplier(SettlementStatIds.ItemDropChanceMultiplier(itemId)));
        }

        ItemDefinition? itemDefinition = string.IsNullOrWhiteSpace(itemId)
            ? null
            : itemRegistry?.GetItem(itemId) ?? GameManager.Instance?.ItemRegistry.GetItem(itemId);
        if (itemDefinition != null && itemDefinition.BaseRarity >= Rarity.Rare)
        {
            multiplier *= NormalizeMultiplier(GetCombinedMultiplier(SettlementStatIds.DropRareChanceMultiplier));
        }

        return Math.Clamp(normalizedBaseChance * multiplier, 0.0, 1.0);
    }

    public void GrantSkillExp(string skillId, double baseExp)
    {
        if (_skillSystem == null || baseExp <= 0.0)
        {
            return;
        }

        _skillSystem.AddExp(skillId, ResolveGrantedSkillExp(skillId, baseExp));
    }

    public void ApplyLegacyEventRewards(IEnumerable<EventRewardEntry> rewards)
    {
        if (_profile == null)
        {
            return;
        }

        foreach (EventRewardEntry reward in rewards)
        {
            double dropChance = ResolveRewardDropChance(reward.ItemId, reward.DropChance);
            if (dropChance <= 0.0)
            {
                continue;
            }

            if (dropChance < 1.0 && Random.Shared.NextDouble() > dropChance)
            {
                continue;
            }

            AddItem(reward.ItemId, reward.Amount);
        }
    }

    public void ApplyEconomyAndInventoryEffect(EventEffectEntry effect)
    {
        switch (effect.EffectType)
        {
            case EventEffectType.GrantItem:
                AddItem(effect.TargetId, effect.IntValue);
                break;
            case EventEffectType.RemoveItem:
                TryRemoveItem(effect.TargetId, effect.IntValue);
                break;
            case EventEffectType.GrantGold:
                AddCurrency(GoldCurrencyId, effect.IntValue);
                break;
            case EventEffectType.RemoveGold:
                TrySpendCurrency(GoldCurrencyId, effect.IntValue);
                break;
            case EventEffectType.GrantSkillExp:
                GrantSkillExp(effect.TargetId, effect.IntValue);
                break;
        }
    }

    public void SyncCurrencyInventoryFromEconomy()
    {
        if (_profile == null)
        {
            return;
        }

        Dictionary<string, int> desiredAmounts = new(StringComparer.Ordinal)
        {
            [GetCurrencyItemId(GoldCurrencyId)] = _profile.Economy.Gold
        };

        foreach ((string currencyId, int amount) in _profile.Economy.ExtraCurrencies)
        {
            desiredAmounts[GetCurrencyItemId(currencyId)] = Math.Max(0, amount);
        }

        foreach ((string currencyItemId, int amount) in desiredAmounts)
        {
            SyncInventoryItemAmount(currencyItemId, amount);
        }

        List<string> staleCurrencyItemIds = _profile.Inventory.Stacks.Keys
            .Where(IsCurrencyItemId)
            .Where(itemId => !desiredAmounts.ContainsKey(itemId))
            .ToList();
        foreach (string staleCurrencyItemId in staleCurrencyItemIds)
        {
            SyncInventoryItemAmount(staleCurrencyItemId, 0);
        }
    }

    private bool TrySpendExtraCurrency(string normalizedCurrencyId, int amount)
    {
        if (_profile == null)
        {
            return false;
        }

        int currentAmount = GetCurrencyAmount(normalizedCurrencyId);
        if (currentAmount < amount)
        {
            return false;
        }

        int nextAmount = currentAmount - amount;
        if (nextAmount <= 0)
        {
            _profile.Economy.ExtraCurrencies.Remove(normalizedCurrencyId);
        }
        else
        {
            _profile.Economy.ExtraCurrencies[normalizedCurrencyId] = nextAmount;
        }

        return true;
    }

    private double GetCombinedMultiplier(params string[] statIds)
    {
        double result = 1.0;
        if (_buffSystem == null)
        {
            return result;
        }

        foreach (string statId in statIds)
        {
            if (string.IsNullOrWhiteSpace(statId))
            {
                continue;
            }

            double multiplier = _buffSystem.GetMultiplier(statId);
            if (!double.IsFinite(multiplier))
            {
                continue;
            }

            result *= multiplier;
        }

        return result;
    }

    private static double NormalizeMultiplier(double value)
    {
        return !double.IsFinite(value) || value <= 0.0 ? 1.0 : value;
    }

    private void SyncCurrencyItemToInventory(string currencyId, int amount)
    {
        SyncInventoryItemAmount(GetCurrencyItemId(currencyId), amount);
    }

    private void SyncInventoryItemAmount(string itemId, int targetAmount)
    {
        if (_profile == null)
        {
            return;
        }

        int normalizedTargetAmount = Math.Max(0, targetAmount);
        int currentAmount = _profile.Inventory.GetItemAmount(itemId);
        if (currentAmount == normalizedTargetAmount)
        {
            return;
        }

        if (currentAmount < normalizedTargetAmount)
        {
            _profile.Inventory.AddItem(itemId, normalizedTargetAmount - currentAmount);
            return;
        }

        _profile.Inventory.TryRemoveItem(itemId, currentAmount - normalizedTargetAmount);
    }

    private static bool IsCurrencyItemId(string itemId)
    {
        return itemId.StartsWith("currency_", StringComparison.Ordinal);
    }

    private static string NormalizeCurrencyId(string currencyId)
    {
        if (string.IsNullOrWhiteSpace(currencyId))
        {
            return GoldCurrencyId;
        }

        return currencyId.Trim().ToLower(CultureInfo.InvariantCulture);
    }
}
