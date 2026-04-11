using Godot;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 稀有度的附加配置。
/// 如果以后你想让“稀有度”不仅是一个枚举，还带颜色和排序权重，就可以用这个类。
/// </summary>
public class RarityDefinition
{
    /// <summary>
    /// 对应的稀有度枚举值。
    /// </summary>
    public Rarity RarityId { get; set; } = Rarity.Common;

    /// <summary>
    /// 稀有度名称。
    /// </summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>
    /// UI 中可使用的颜色。
    /// </summary>
    public Color DisplayColor { get; set; } = Colors.White;

    /// <summary>
    /// 排序权重。
    /// 数字越大，越靠前或越靠后，具体规则由排序器决定。
    /// </summary>
    public int SortWeight { get; set; }
}
