using System.Collections.Generic;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 区域/副本的静态定义。
/// 主要记录区域名称、探索上限，以及通关后会解锁什么。
/// </summary>
public class ZoneDefinition
{
    public string Id { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    public int MaxClearCount { get; set; } = 10;

    public string UnlocksZoneId { get; set; } = string.Empty;

    public string UnlocksFactionId { get; set; } = string.Empty;

    public List<EventConditionEntry> UnlockConditions { get; } = new();
}
