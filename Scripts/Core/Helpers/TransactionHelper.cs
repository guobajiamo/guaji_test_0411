using System;
using System.Collections.Generic;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// Batch transaction helper for inventory item operations.
/// Guarantees "all checks pass first, then apply changes".
/// </summary>
public class TransactionHelper
{
    private readonly List<TransactionStep> _steps = new();

    public void QueueAddItem(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        _steps.Add(new TransactionStep(TransactionStepType.AddItem, itemId, amount));
    }

    public void QueueRemoveItem(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        _steps.Add(new TransactionStep(TransactionStepType.RemoveItem, itemId, amount));
    }

    public bool TryCommit(PlayerInventory inventory)
    {
        Dictionary<string, int> removeTotals = new(StringComparer.Ordinal);
        foreach (TransactionStep step in _steps)
        {
            if (step.StepType != TransactionStepType.RemoveItem)
            {
                continue;
            }

            removeTotals.TryGetValue(step.ItemId, out int currentAmount);
            removeTotals[step.ItemId] = currentAmount + step.Amount;
        }

        foreach ((string itemId, int totalAmount) in removeTotals)
        {
            if (!inventory.HasItem(itemId, totalAmount))
            {
                return false;
            }
        }

        foreach ((string itemId, int totalAmount) in removeTotals)
        {
            if (!inventory.TryRemoveItem(itemId, totalAmount))
            {
                return false;
            }
        }

        foreach (TransactionStep step in _steps)
        {
            if (step.StepType == TransactionStepType.AddItem)
            {
                inventory.AddItem(step.ItemId, step.Amount);
            }
        }

        _steps.Clear();
        return true;
    }

    public void Clear()
    {
        _steps.Clear();
    }
}

public readonly struct TransactionStep
{
    public TransactionStep(TransactionStepType stepType, string itemId, int amount)
    {
        StepType = stepType;
        ItemId = itemId;
        Amount = amount;
    }

    public TransactionStepType StepType { get; }

    public string ItemId { get; }

    public int Amount { get; }
}

public enum TransactionStepType
{
    AddItem,
    RemoveItem
}
