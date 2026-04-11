using System.Collections.Generic;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 势力静态定义。
/// 用来描述某个阵营、它的 NPC，以及后续声望阈值等。
/// </summary>
public class FactionDefinition
{
    public string Id { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    public int MaxReputation { get; set; } = 100;

    public int PeaceThreshold { get; set; } = 100;

    public List<string> NpcIds { get; } = new();

    public List<string> FactionEventIds { get; } = new();
}
