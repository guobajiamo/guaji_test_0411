using Godot;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 出售系统。
/// 当前用于一键出售“垃圾标记”物品。
/// </summary>
public partial class SellSystem : Node
{
    private PlayerProfile? _profile;
    private ItemRegistry? _itemRegistry;
    private ValueSettlementService? _settlementService;

    public void Configure(PlayerProfile profile, ItemRegistry itemRegistry, ValueSettlementService settlementService)
    {
        _profile = profile;
        _itemRegistry = itemRegistry;
        _settlementService = settlementService;
    }

    public int SellMarkedItems()
    {
        if (_profile == null || _itemRegistry == null || _settlementService == null)
        {
            return 0;
        }

        List<string> markedItemIds = _profile.Inventory.ItemStates.Values
            .Where(state => state.IsJunkMarked)
            .Select(state => state.ItemId)
            .ToList();

        int totalGoldEarned = 0;
        foreach (string itemId in markedItemIds)
        {
            int quantity = _profile.Inventory.GetItemAmount(itemId);
            if (quantity <= 0)
            {
                continue;
            }

            int sellPrice = _itemRegistry.GetItem(itemId)?.SellPrice ?? 0;
            if (sellPrice <= 0)
            {
                continue;
            }

            if (!_settlementService.TryRemoveItem(itemId, quantity))
            {
                continue;
            }

            int goldEarned = _settlementService.ResolveSellGoldIncome(itemId, sellPrice, quantity);
            if (goldEarned > 0)
            {
                totalGoldEarned += goldEarned;
                _settlementService.AddCurrency(ValueSettlementService.GoldCurrencyId, goldEarned);
            }

            // 卖完后清掉“垃圾标记”，避免以后重新获得时自动被当垃圾处理。
            _profile.Inventory.GetOrCreateItemState(itemId).IsJunkMarked = false;
        }

        return totalGoldEarned;
    }
}
