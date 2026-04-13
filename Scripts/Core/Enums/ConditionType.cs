namespace Test00_0410.Core.Enums;

/// <summary>
/// 条件类型。
/// 用来统一描述“某个按钮/区域/商店是否可以解锁”。
/// </summary>
public enum ConditionType
{
    None = 0,
    HasItem = 1,
    HasGold = 2,
    SkillLevel = 3,
    FactionReputation = 4,
    ZoneCleared = 5,
    EventCompleted = 6,
    QuestCompleted = 7,
    ItemAcquired = 8,
    BattleStatReached = 9
}
