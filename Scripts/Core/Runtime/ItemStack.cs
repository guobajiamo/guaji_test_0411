using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 某个物品在玩家背包里的实际实例状态。
/// 这里记录的是“当前数量、当前耐久、当前稀有度”等动态数据。
/// </summary>
public class ItemStack
{
    public string ItemId { get; set; } = string.Empty;

    public int Quantity { get; private set; }

    public int CurrentDurability { get; private set; }

    public Rarity CurrentRarity { get; set; } = Rarity.Common;

    public bool CanAdd(int amount, int maxStack)
    {
        return Quantity + amount <= maxStack;
    }

    public void Add(int amount)
    {
        Quantity += amount;
    }

    public bool CanRemove(int amount)
    {
        return Quantity >= amount;
    }

    public bool TryRemove(int amount)
    {
        if (!CanRemove(amount))
        {
            return false;
        }

        Quantity -= amount;
        return true;
    }

    public void SetDurability(int durability)
    {
        CurrentDurability = durability;
    }
}
