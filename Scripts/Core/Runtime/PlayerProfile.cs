using System.Collections.Generic;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家整份存档在内存中的总入口。
/// 后续保存/读档时，通常会围绕这个对象展开。
/// </summary>
public class PlayerProfile
{
    public PlayerInventory Inventory { get; set; } = new();

    /// <summary>
    /// 玩家经济状态。
    /// 把金币从背包里单独拆出来，避免“金币到底是货币还是物品”混在一起。
    /// </summary>
    public PlayerEconomyState Economy { get; set; } = new();

    /// <summary>
    /// 玩家当前挂机状态。
    /// 挂机系统运行时只应该读写这一份状态，避免出现双份真相源。
    /// </summary>
    public PlayerIdleState IdleState { get; set; } = new();

    public Dictionary<string, PlayerSkillState> SkillStates { get; } = new();

    public Dictionary<string, PlayerFactionState> FactionStates { get; } = new();

    public Dictionary<string, PlayerZoneState> ZoneStates { get; } = new();

    /// <summary>
    /// 已完成事件集合。
    /// 用于一次性事件、解锁链和前置条件判断。
    /// </summary>
    public HashSet<string> CompletedEventIds { get; } = new();

    /// <summary>
    /// 已完成任务集合。
    /// 用于后续任务系统前置判断。
    /// </summary>
    public HashSet<string> CompletedQuestIds { get; } = new();

    /// <summary>
    /// 每个 NPC 商店对应一份运行时状态。
    /// 这里会记录限量商品是否已经卖掉、还剩多少库存等信息。
    /// </summary>
    public Dictionary<string, PlayerShopState> ShopStates { get; } = new();

    public PlayerSkillState GetOrCreateSkillState(string skillId)
    {
        if (!SkillStates.TryGetValue(skillId, out PlayerSkillState? state))
        {
            state = new PlayerSkillState { SkillId = skillId };
            SkillStates[skillId] = state;
        }

        return state;
    }

    public PlayerFactionState GetOrCreateFactionState(string factionId)
    {
        if (!FactionStates.TryGetValue(factionId, out PlayerFactionState? state))
        {
            state = new PlayerFactionState { FactionId = factionId };
            FactionStates[factionId] = state;
        }

        return state;
    }

    public PlayerZoneState GetOrCreateZoneState(string zoneId)
    {
        if (!ZoneStates.TryGetValue(zoneId, out PlayerZoneState? state))
        {
            state = new PlayerZoneState { ZoneId = zoneId };
            ZoneStates[zoneId] = state;
        }

        return state;
    }

    public PlayerShopState GetOrCreateShopState(string npcId)
    {
        if (!ShopStates.TryGetValue(npcId, out PlayerShopState? state))
        {
            state = new PlayerShopState { NpcId = npcId };
            ShopStates[npcId] = state;
        }

        return state;
    }
}
