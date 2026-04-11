using System.Collections.Generic;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 事务型批量操作辅助器。
/// 它的目标是让“要么全部成功，要么全部回滚”的流程更容易实现。
/// </summary>
public class TransactionHelper
{
    private readonly List<TransactionStep> _steps = new();

    public void QueueAddItem(string itemId, int amount)
    {
        _steps.Add(new TransactionStep(TransactionStepType.AddItem, itemId, amount));
    }

    public void QueueRemoveItem(string itemId, int amount)
    {
        _steps.Add(new TransactionStep(TransactionStepType.RemoveItem, itemId, amount));
    }

    public bool TryCommit(PlayerInventory inventory)
    {
        foreach (TransactionStep step in _steps)
        {
            if (step.StepType == TransactionStepType.RemoveItem && !inventory.HasItem(step.ItemId, step.Amount))
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
            else
            {
                inventory.TryRemoveItem(step.ItemId, step.Amount);
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

/// <summary>
/// 单个事务步骤。
/// 为了让初学者更容易看懂，这里使用最普通的结构体写法。
/// </summary>
public struct TransactionStep
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
