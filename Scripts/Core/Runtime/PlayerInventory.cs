using System.Collections.Generic;
using System.Linq;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家背包管理器。
/// 它负责统一管理所有 ItemStack 和 PlayerItemState。
/// </summary>
public class PlayerInventory
{
    public Dictionary<string, ItemStack> Stacks { get; } = new();

    public Dictionary<string, PlayerItemState> ItemStates { get; } = new();

    public bool HasItem(string itemId, int requiredAmount = 1)
    {
        return Stacks.TryGetValue(itemId, out ItemStack? stack) && stack.Quantity >= requiredAmount;
    }

    public int GetItemAmount(string itemId)
    {
        return Stacks.TryGetValue(itemId, out ItemStack? stack) ? stack.Quantity : 0;
    }

    public ItemStack GetOrCreateStack(string itemId)
    {
        if (!Stacks.TryGetValue(itemId, out ItemStack? stack))
        {
            stack = new ItemStack { ItemId = itemId };
            Stacks[itemId] = stack;
        }

        return stack;
    }

    public PlayerItemState GetOrCreateItemState(string itemId)
    {
        if (!ItemStates.TryGetValue(itemId, out PlayerItemState? state))
        {
            state = new PlayerItemState { ItemId = itemId };
            ItemStates[itemId] = state;
        }

        return state;
    }

    public void AddItem(string itemId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        ItemStack stack = GetOrCreateStack(itemId);
        PlayerItemState state = GetOrCreateItemState(itemId);

        stack.Add(amount);
        state.IsAcquired = true;
    }

    public bool TryRemoveItem(string itemId, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (!Stacks.TryGetValue(itemId, out ItemStack? stack))
        {
            return false;
        }

        bool removed = stack.TryRemove(amount);
        if (!removed)
        {
            return false;
        }

        if (stack.Quantity <= 0)
        {
            Stacks.Remove(itemId);
        }

        return true;
    }

    public IEnumerable<string> GetFavoriteItemIds()
    {
        return ItemStates.Values
            .Where(state => state.IsFavorite)
            .Select(state => state.ItemId);
    }
}
