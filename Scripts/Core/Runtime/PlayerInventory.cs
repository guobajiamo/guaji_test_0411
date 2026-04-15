using System;
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

    public bool HasEverAcquired(string itemId)
    {
        return ItemStates.TryGetValue(itemId, out PlayerItemState? state) && state.IsAcquired;
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

        EnsureRuntimeSlotOrder();

        ItemStack stack = GetOrCreateStack(itemId);
        PlayerItemState state = GetOrCreateItemState(itemId);
        bool wasEmptyBeforeAdd = stack.Quantity <= 0;

        stack.Add(amount);
        state.IsAcquired = true;

        if (wasEmptyBeforeAdd)
        {
            state.AcquiredSequence = GetNextOrderValue(useArrivalSequence: true);
            state.PlayerDisplayOrder = GetNextOrderValue(useArrivalSequence: false);
            return;
        }

        state.AcquiredSequence ??= GetNextOrderValue(useArrivalSequence: true);
        state.PlayerDisplayOrder ??= GetNextOrderValue(useArrivalSequence: false);
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

            if (ItemStates.TryGetValue(itemId, out PlayerItemState? state))
            {
                state.AcquiredSequence = null;
                state.PlayerDisplayOrder = null;
            }

            EnsureRuntimeSlotOrder();
        }

        return true;
    }

    /// <summary>
    /// 获取当前在背包内（数量大于 0）的物品 ID，按“当前显示顺序”排序。
    /// </summary>
    public List<string> GetActiveItemIdsByDisplayOrder()
    {
        EnsureRuntimeSlotOrder();

        return GetActiveItemIds()
            .OrderBy(itemId => GetOrCreateItemState(itemId).PlayerDisplayOrder ?? int.MaxValue)
            .ThenBy(itemId => itemId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 获取当前在背包内（数量大于 0）的物品 ID，按“入袋顺序”排序。
    /// </summary>
    public List<string> GetActiveItemIdsByArrivalOrder()
    {
        EnsureRuntimeSlotOrder();

        return GetActiveItemIds()
            .OrderBy(itemId => GetOrCreateItemState(itemId).AcquiredSequence ?? int.MaxValue)
            .ThenBy(itemId => itemId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 按给定顺序重排当前背包显示序列。
    /// 仅会对“当前数量大于 0”的物品生效。
    /// </summary>
    public void ApplyDisplayOrder(IEnumerable<string> orderedItemIds)
    {
        EnsureRuntimeSlotOrder();

        HashSet<string> activeIds = GetActiveItemIds().ToHashSet(StringComparer.Ordinal);
        List<string> normalized = orderedItemIds
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId) && activeIds.Contains(itemId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (string itemId in GetActiveItemIdsByDisplayOrder())
        {
            if (!normalized.Contains(itemId, StringComparer.Ordinal))
            {
                normalized.Add(itemId);
            }
        }

        for (int index = 0; index < normalized.Count; index++)
        {
            GetOrCreateItemState(normalized[index]).PlayerDisplayOrder = index + 1;
        }
    }

    /// <summary>
    /// 把当前显示顺序恢复为“先入袋先显示”。
    /// </summary>
    public void RestoreArrivalDisplayOrder()
    {
        ApplyDisplayOrder(GetActiveItemIdsByArrivalOrder());
    }

    /// <summary>
    /// 修复运行时槽位顺序。
    /// 用于兼容旧存档和缺失顺序字段的情况。
    /// </summary>
    public void EnsureRuntimeSlotOrder()
    {
        List<string> activeIds = GetActiveItemIds();
        if (activeIds.Count == 0)
        {
            return;
        }

        foreach (string itemId in activeIds)
        {
            PlayerItemState state = GetOrCreateItemState(itemId);
            state.IsAcquired = true;
        }

        NormalizeOrderValues(
            activeIds,
            state => state.AcquiredSequence,
            (state, nextValue) => state.AcquiredSequence = nextValue,
            itemId => itemId);

        NormalizeOrderValues(
            activeIds,
            state => state.PlayerDisplayOrder,
            (state, nextValue) => state.PlayerDisplayOrder = nextValue,
            itemId =>
            {
                PlayerItemState state = GetOrCreateItemState(itemId);
                return $"{state.AcquiredSequence ?? int.MaxValue:0000000000}:{itemId}";
            });
    }

    public IEnumerable<string> GetFavoriteItemIds()
    {
        return ItemStates.Values
            .Where(state => state.IsFavorite)
            .Select(state => state.ItemId);
    }

    private List<string> GetActiveItemIds()
    {
        return Stacks.Values
            .Where(stack => stack.Quantity > 0 && !string.IsNullOrWhiteSpace(stack.ItemId))
            .Select(stack => stack.ItemId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private void NormalizeOrderValues(
        IEnumerable<string> activeItemIds,
        Func<PlayerItemState, int?> orderSelector,
        Action<PlayerItemState, int> orderSetter,
        Func<string, string> fallbackSelector)
    {
        List<string> sortedIds = activeItemIds
            .OrderBy(itemId => orderSelector(GetOrCreateItemState(itemId)) ?? int.MaxValue)
            .ThenBy(fallbackSelector, StringComparer.Ordinal)
            .ToList();

        for (int index = 0; index < sortedIds.Count; index++)
        {
            orderSetter(GetOrCreateItemState(sortedIds[index]), index + 1);
        }
    }

    private int GetNextOrderValue(bool useArrivalSequence)
    {
        int max = GetActiveItemIds()
            .Select(itemId =>
            {
                PlayerItemState state = GetOrCreateItemState(itemId);
                return useArrivalSequence
                    ? state.AcquiredSequence ?? 0
                    : state.PlayerDisplayOrder ?? 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return max + 1;
    }
}
