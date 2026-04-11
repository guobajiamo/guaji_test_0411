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
/// 负责 NPC 商品购买、价格检查和支付方式判定。
/// </summary>
public partial class ShopSystem : Node
{
    private PlayerProfile? _profile;
    private FactionRegistry? _factionRegistry;

    public void Configure(PlayerProfile profile, FactionRegistry factionRegistry)
    {
        _profile = profile;
        _factionRegistry = factionRegistry;
    }

    public bool TryBuyFromNpc(string npcId, ShopItemEntry shopItem)
    {
        if (_profile == null || _factionRegistry == null)
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

        if (!TryPay(shopItem))
        {
            // 如果支付失败，需要把刚才预扣的库存补回去。
            if (shopItem.Stock >= 0)
            {
                int remainingStock = shopState.GetRemainingStock(shopItem.ItemId, shopItem.Stock);
                shopState.SetRemainingStock(shopItem.ItemId, remainingStock + 1);
            }

            return false;
        }

        _profile.Inventory.AddItem(shopItem.ItemId, 1);
        return true;
    }

    /// <summary>
    /// 读取某个 NPC 商品的剩余库存。
    /// 静态配置写在 ShopItemEntry.Stock 里，运行时变化写在 PlayerShopState 里。
    /// </summary>
    public int GetRemainingStock(string npcId, ShopItemEntry shopItem)
    {
        if (_profile == null)
        {
            return shopItem.Stock;
        }

        return _profile.GetOrCreateShopState(npcId).GetRemainingStock(shopItem.ItemId, shopItem.Stock);
    }

    /// <summary>
    /// 判断玩家金币是否足够。
    /// 金币现在放在独立的 Economy 状态里，而不是背包里。
    /// </summary>
    public bool CanAffordWithGold(int goldCost)
    {
        return _profile != null && _profile.Economy.Gold >= goldCost;
    }

    /// <summary>
    /// 检查 NPC 商店入口和商品本身的声望门槛。
    /// 这样 `required_reputation` 配置字段在运行时才会真正生效。
    /// </summary>
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

    private bool TryPay(ShopItemEntry shopItem)
    {
        if (_profile == null)
        {
            return false;
        }

        if (shopItem.PaymentType == CurrencyType.Gold)
        {
            if (shopItem.GoldCost <= 0)
            {
                return true;
            }

            return _profile.Economy.TrySpendGold(shopItem.GoldCost);
        }

        TransactionHelper transaction = new();
        foreach (ItemCostEntry cost in shopItem.BarterCosts)
        {
            transaction.QueueRemoveItem(cost.ItemId, cost.Amount);
        }

        return transaction.TryCommit(_profile.Inventory);
    }
}
