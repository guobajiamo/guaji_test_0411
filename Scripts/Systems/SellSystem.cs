using Godot;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 卖出系统。
/// 以后可支持“一键卖出标记为垃圾的物品”。
/// </summary>
public partial class SellSystem : Node
{
    private PlayerProfile? _profile;
    private ItemRegistry? _itemRegistry;

    public void Configure(PlayerProfile profile, ItemRegistry itemRegistry)
    {
        _profile = profile;
        _itemRegistry = itemRegistry;
    }

    public int SellMarkedItems()
    {
        if (_profile == null || _itemRegistry == null)
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

            if (_profile.Inventory.TryRemoveItem(itemId, quantity))
            {
                int goldEarned = sellPrice * quantity;
                totalGoldEarned += goldEarned;
                _profile.Economy.AddGold(goldEarned);

                // 卖完后清掉“垃圾标记”，避免以后重新获得时被自动当垃圾处理。
                _profile.Inventory.GetOrCreateItemState(itemId).IsJunkMarked = false;
            }
        }

        return totalGoldEarned;
    }
}
