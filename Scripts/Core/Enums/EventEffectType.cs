namespace Test00_0410.Core.Enums;

/// <summary>
/// 事件效果类型。
/// 目的是把事件的影响做成统一模型，而不是以后按事件 ID 写很多特判。
/// </summary>
public enum EventEffectType
{
    None = 0,
    GrantItem = 1,
    RemoveItem = 2,
    GrantGold = 3,
    RemoveGold = 4,
    GrantSkillExp = 5,
    AddFactionReputation = 6,
    UnlockZone = 7,
    CompleteEvent = 8,
    CompleteQuest = 9,
    StartBattle = 10,
    UnlockAchievement = 11,
    LearnSkill = 12
}
