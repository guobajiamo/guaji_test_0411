using System.Collections.Generic;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家视角下某个 NPC 商店的运行时状态。
/// 主要是为了持久化“限量商品还剩多少”。
/// </summary>
public class PlayerShopState
{
    public string NpcId { get; set; } = string.Empty;

    /// <summary>
    /// key 是商品 itemId，value 是剩余库存。
    /// 如果字典里没有该商品，通常表示它还没有被初始化或仍沿用静态库存。
    /// </summary>
    public Dictionary<string, int> RemainingStockByItemId { get; } = new();

    public int GetRemainingStock(string itemId, int defaultStock)
    {
        return RemainingStockByItemId.TryGetValue(itemId, out int remainingStock) ? remainingStock : defaultStock;
    }

    /// <summary>
    /// 扣减库存。
    /// 如果默认库存是 -1，表示这是无限库存商品，不需要真的扣减。
    /// </summary>
    public bool TryConsumeStock(string itemId, int defaultStock, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (defaultStock < 0)
        {
            return true;
        }

        int remainingStock = GetRemainingStock(itemId, defaultStock);
        if (remainingStock < amount)
        {
            return false;
        }

        RemainingStockByItemId[itemId] = remainingStock - amount;
        return true;
    }

    public void SetRemainingStock(string itemId, int remainingStock)
    {
        RemainingStockByItemId[itemId] = remainingStock < 0 ? 0 : remainingStock;
    }
}
