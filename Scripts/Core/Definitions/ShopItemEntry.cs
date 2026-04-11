using System.Collections.Generic;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 商店商品条目。
/// 这个类只描述“卖什么、卖几次、卖多少钱、需要什么条件”。
/// </summary>
public class ShopItemEntry
{
    public string ItemId { get; set; } = string.Empty;

    public int Stock { get; set; } = -1;

    public CurrencyType PaymentType { get; set; } = CurrencyType.Gold;

    public int GoldCost { get; set; }

    public List<ItemCostEntry> BarterCosts { get; } = new();

    public int RequiredReputation { get; set; }
}
