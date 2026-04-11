using System.Collections.Generic;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家经济状态。
/// 当前先把金币单独拿出来，后续如果有别的货币，也可以继续往这里扩展。
/// </summary>
public class PlayerEconomyState
{
    public int Gold { get; private set; }

    /// <summary>
    /// 预留的其他货币。
    /// 比如以后如果有“红魔馆代币”“寺院供奉点数”等，可以加在这里。
    /// </summary>
    public Dictionary<string, int> ExtraCurrencies { get; } = new();

    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Gold += amount;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        if (Gold < amount)
        {
            return false;
        }

        Gold -= amount;
        return true;
    }
}
