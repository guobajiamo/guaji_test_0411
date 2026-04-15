using Godot;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 商店系统。
/// 负责 NPC 商店购买、库存检查与支付结算。
/// </summary>
public partial class ShopSystem : Node
{
    private PlayerProfile? _profile;
    private FactionRegistry? _factionRegistry;
    private ValueSettlementService? _settlementService;

    public void Configure(PlayerProfile profile, FactionRegistry factionRegistry, ValueSettlementService settlementService)
    {
        _profile = profile;
        _factionRegistry = factionRegistry;
        _settlementService = settlementService;
    }

    public bool TryBuyFromNpc(string npcId, ShopItemEntry shopItem)
    {
        if (_profile == null || _factionRegistry == null || _settlementService == null)
        {
            return false;
        }

        NpcDefinition? npcDefinition = _factionRegistry.GetNpc(npcId);
        if (npcDefinition == null || !CanAccessNpcShop(npcDefinition, shopItem))
        {
            return false;
        }

        PlayerShopState shopState = _profile.GetOrCreateShopState(npcId);
        if (!shopState.TryConsumeStock(shopItem.ItemId, shopItem.Stock))
        {
            return false;
        }

        if (!TryPay(npcId, shopItem))
        {
            // 支付失败时回滚预扣库存。
            if (shopItem.Stock >= 0)
            {
                int remainingStock = shopState.GetRemainingStock(shopItem.ItemId, shopItem.Stock);
                shopState.SetRemainingStock(shopItem.ItemId, remainingStock + 1);
            }

            return false;
        }

        _settlementService.AddItem(shopItem.ItemId, 1);
        return true;
    }

    /// <summary>
    /// 读取某个 NPC 商店条目的运行时剩余库存。
    /// </summary>
    public int GetRemainingStock(string npcId, ShopItemEntry shopItem)
    {
        if (_profile == null)
        {
            return shopItem.Stock;
        }

        return _profile.GetOrCreateShopState(npcId).GetRemainingStock(shopItem.ItemId, shopItem.Stock);
    }

    public bool CanAffordWithGold(int goldCost)
    {
        if (_settlementService == null)
        {
            return false;
        }

        int settledCost = _settlementService.ResolveBuyGoldCost(string.Empty, goldCost);
        return _settlementService.HasCurrency(ValueSettlementService.GoldCurrencyId, settledCost);
    }

    private bool CanAccessNpcShop(NpcDefinition npcDefinition, ShopItemEntry shopItem)
    {
        if (_profile == null)
        {
            return false;
        }

        if (!npcDefinition.HasShop)
        {
            return false;
        }

        bool containsItem = npcDefinition.ShopItems.Any(entry => entry.ItemId == shopItem.ItemId);
        if (!containsItem)
        {
            return false;
        }

        PlayerFactionState factionState = _profile.GetOrCreateFactionState(npcDefinition.FactionId);
        return factionState.Reputation >= npcDefinition.RequiredReputation
            && factionState.Reputation >= shopItem.RequiredReputation;
    }

    private bool TryPay(string npcId, ShopItemEntry shopItem)
    {
        if (_settlementService == null)
        {
            return false;
        }

        int settledGoldCost = _settlementService.ResolveBuyGoldCost(npcId, shopItem.GoldCost);

        return shopItem.PaymentType switch
        {
            CurrencyType.Gold => settledGoldCost <= 0 || _settlementService.TrySpendCurrency(ValueSettlementService.GoldCurrencyId, settledGoldCost),
            CurrencyType.Item => _settlementService.TryPayItemCosts(shopItem.BarterCosts),
            CurrencyType.Mixed => TryPayMixed(settledGoldCost, shopItem.BarterCosts),
            _ => false
        };
    }

    private bool TryPayMixed(int settledGoldCost, System.Collections.Generic.IEnumerable<ItemCostEntry> barterCosts)
    {
        if (_settlementService == null)
        {
            return false;
        }

        if (!_settlementService.HasCurrency(ValueSettlementService.GoldCurrencyId, settledGoldCost)
            || !_settlementService.CanPayItemCosts(barterCosts))
        {
            return false;
        }

        if (!_settlementService.TryPayItemCosts(barterCosts))
        {
            return false;
        }

        if (_settlementService.TrySpendCurrency(ValueSettlementService.GoldCurrencyId, settledGoldCost))
        {
            return true;
        }

        // 极端情况下如果金币扣除失败，则把刚扣除的物品补回。
        foreach (ItemCostEntry cost in barterCosts)
        {
            if (cost.Amount > 0)
            {
                _settlementService.AddItem(cost.ItemId, cost.Amount);
            }
        }

        return false;
    }
}
