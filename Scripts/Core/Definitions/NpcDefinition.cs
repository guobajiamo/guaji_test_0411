using System.Collections.Generic;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// NPC 静态定义。
/// 主要负责描述 NPC 属于哪个势力、何时解锁、有没有商店等。
/// </summary>
public class NpcDefinition
{
    public string Id { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public string FactionId { get; set; } = string.Empty;

    public bool HasShop { get; set; }

    public int RequiredReputation { get; set; }

    public List<ShopItemEntry> ShopItems { get; } = new();
}
