namespace Test00_0410.Core.Enums;

/// <summary>
/// 支付类型。
/// 商店系统以后可以根据这个字段判断是金币交易还是以物易物。
/// </summary>
public enum CurrencyType
{
    Gold,
    Item,
    Mixed
}
