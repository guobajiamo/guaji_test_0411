using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 具体物品的静态定义。
/// 这里保存“模板数据”，不会直接记录玩家当前拥有多少个。
/// </summary>
public class ItemDefinition : NodeDefinitionBase
{
    /// <summary>
    /// 默认稀有度。
    /// 玩家状态里如果没有改动，就以这里为准。
    /// </summary>
    public Rarity BaseRarity { get; set; } = Rarity.Common;

    /// <summary>
    /// 基础堆叠上限。
    /// 后续可与背包扩容、Buff 等额外值相加。
    /// </summary>
    public int BaseMaxStack { get; set; } = 99;

    /// <summary>
    /// 商店买入价。
    /// 如果为 0，通常表示不允许从普通商店购买。
    /// </summary>
    public int BuyPrice { get; set; }

    /// <summary>
    /// 玩家卖出价。
    /// </summary>
    public int SellPrice { get; set; }

    /// <summary>
    /// 标签集合。
    /// 可以用它做筛选、条件判断或商店分类。
    /// </summary>
    public ItemTag Tags { get; set; } = ItemTag.None;

    /// <summary>
    /// 这个物品是否启用耐久度。
    /// </summary>
    public bool HasDurability { get; set; }

    /// <summary>
    /// 最大耐久。
    /// 仅在 HasDurability 为 true 时有意义。
    /// </summary>
    public int MaxDurability { get; set; } = 100;

    /// <summary>
    /// 获得提示。
    /// 比如“通过砍树获得”“在红魔馆商店购买”等。
    /// </summary>
    public string AcquisitionHintKey { get; set; } = string.Empty;

    /// <summary>
    /// 字典/图鉴中显示的长描述。
    /// </summary>
    public string DetailDescriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// 用于工具类物品的额外加成。
    /// 这里改成了明确字段，避免关键配置变成字符串字典。
    /// </summary>
    public ToolBonusDefinition ToolBonuses { get; set; } = new();

    public ItemDefinition()
    {
        IsAbstract = false;
    }

    /// <summary>
    /// 判断这个物品是否带有某个标签。
    /// </summary>
    public bool HasTag(ItemTag tag)
    {
        return (Tags & tag) == tag;
    }

    /// <summary>
    /// 计算带扩容后的实际堆叠上限。
    /// </summary>
    public int GetEffectiveMaxStack(int extraCapacity)
    {
        return BaseMaxStack + extraCapacity;
    }
}
