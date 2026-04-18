namespace Test00_0410.Core.Helpers;

/// <summary>
/// Numeric modifier stat-id keys used by the settlement middle-layer.
/// Future systems can safely register additional timed/persistent multipliers on the same keys.
/// </summary>
public static class SettlementStatIds
{
    public const string IdleOutputMultiplier = "idle.output.multiplier";

    public const string IdleSpeedMultiplier = "idle.speed.multiplier";

    public const string BattlePlayerMaxHpMultiplier = "battle.player.max_hp.multiplier";

    public const string BattlePlayerActionSpeedMultiplier = "battle.player.action_speed.multiplier";

    public const string DropChanceMultiplier = "drop.chance.multiplier";

    public const string DropRareChanceMultiplier = "drop.rare_chance.multiplier";

    public const string TradeBuyPriceMultiplier = "trade.buy_price.multiplier";

    public const string TradeSellPriceMultiplier = "trade.sell_price.multiplier";

    public const string SkillExpGainMultiplier = "skill.exp_gain.multiplier";

    public static string SkillIdleOutputMultiplier(string skillId)
    {
        return $"idle.output.{skillId}.multiplier";
    }

    public static string SkillIdleSpeedMultiplier(string skillId)
    {
        return $"idle.speed.{skillId}.multiplier";
    }

    public static string SkillExpGainMultiplierBySkill(string skillId)
    {
        return $"skill.exp_gain.{skillId}.multiplier";
    }

    public static string NpcTradeBuyMultiplier(string npcId)
    {
        return $"trade.buy_price.{npcId}.multiplier";
    }

    public static string ItemTradeSellMultiplier(string itemId)
    {
        return $"trade.sell_price.{itemId}.multiplier";
    }

    public static string ItemDropChanceMultiplier(string itemId)
    {
        return $"drop.chance.{itemId}.multiplier";
    }
}
