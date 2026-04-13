using System.Collections.Generic;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 事件静态定义。
/// 游戏里的各种按钮行为，最终都可以归纳为某种事件定义。
/// </summary>
public class EventDefinition
{
    public string Id { get; set; } = string.Empty;

    public string SourceFilePath { get; set; } = string.Empty;

    public int SourceFileOrder { get; set; }

    public int SourceEntryOrder { get; set; }

    public string NameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// 鼠标悬浮说明。
    /// 如果为空，就默认回退到 DescriptionKey。
    /// </summary>
    public string HoverInfoKey { get; set; } = string.Empty;

    public EventType Type { get; set; } = EventType.RepeatableClick;

    public int DisplayConditionCount { get; set; }

    public List<EventConditionEntry> DisplayConditions { get; } = new();

    public int InteractionConditionCount { get; set; }

    public List<EventConditionEntry> InteractionConditions { get; } = new();

    public int HideConditionCount { get; set; }

    public List<EventConditionEntry> HideConditions { get; } = new();

    /// <summary>
    /// 兼容旧 YAML 的别名。
    /// 当前建议统一改用 InteractionConditions / interaction_conditions。
    /// </summary>
    public List<EventConditionEntry> Prerequisites => InteractionConditions;

    public List<ItemCostEntry> Costs { get; } = new();

    public List<EventRewardEntry> Rewards { get; } = new();

    /// <summary>
    /// 通用效果列表。
    /// 用来表达“加金币、加声望、解锁区域”等更宽的行为。
    /// </summary>
    public List<EventEffectEntry> Effects { get; } = new();

    public string LinkedSkillId { get; set; } = string.Empty;

    public ButtonListGroup ButtonListGroup { get; set; } = ButtonListGroup.MainClick;

    public bool RemoveAfterTriggered { get; set; }

    /// <summary>
    /// 预留的弹窗配置。
    /// 只有部分一次性事件会用到它。
    /// </summary>
    public EventDialogDefinition? Dialog { get; set; }
}

/// <summary>
/// 通用条件条目。
/// 以后很多系统都能复用，例如事件解锁、区域解锁、商店开放等。
/// </summary>
public class EventConditionEntry
{
    public ConditionType ConditionType { get; set; } = ConditionType.None;

    public string TargetId { get; set; } = string.Empty;

    public double RequiredValue { get; set; }
}

/// <summary>
/// 事件通用效果条目。
/// 一个事件如果需要同时改多个系统状态，就可以由多个效果条目来组合完成。
/// </summary>
public class EventEffectEntry
{
    public EventEffectType EffectType { get; set; } = EventEffectType.None;

    /// <summary>
    /// 目标 ID。
    /// 例如物品 ID、技能 ID、势力 ID、区域 ID。
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// 整数值。
    /// 例如 +1 物品、+10 金币、+5 声望。
    /// </summary>
    public int IntValue { get; set; }

    /// <summary>
    /// 小数值。
    /// 以后若某些效果需要倍率或概率，可放这里。
    /// </summary>
    public double DoubleValue { get; set; }

    /// <summary>
    /// 预留文本参数。
    /// 给未来战斗、任务、剧情之类的系统使用。
    /// </summary>
    public string TextValue { get; set; } = string.Empty;
}

/// <summary>
/// 物品消耗条目。
/// </summary>
public class ItemCostEntry
{
    public string ItemId { get; set; } = string.Empty;

    public int Amount { get; set; }
}

/// <summary>
/// 事件奖励条目。
/// 目前先支持物品与概率，后面也可以继续扩展金币、声望等。
/// </summary>
public class EventRewardEntry
{
    public string ItemId { get; set; } = string.Empty;

    public int Amount { get; set; }

    public double DropChance { get; set; } = 1.0;
}
