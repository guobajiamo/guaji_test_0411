using System.Collections.Generic;
using Test00_0410.Core.Definitions;

namespace Test00_0410.Core.Registry;

/// <summary>
/// 势力注册表。
/// 同时保存势力和 NPC 的静态定义。
/// </summary>
public class FactionRegistry
{
    private readonly Dictionary<string, FactionDefinition> _factions = new();
    private readonly Dictionary<string, NpcDefinition> _npcs = new();

    public IReadOnlyDictionary<string, FactionDefinition> Factions => _factions;

    public IReadOnlyDictionary<string, NpcDefinition> Npcs => _npcs;

    public void LoadDefinitions(IEnumerable<FactionDefinition> factions, IEnumerable<NpcDefinition> npcs)
    {
        _factions.Clear();
        _npcs.Clear();

        foreach (FactionDefinition faction in factions)
        {
            _factions[faction.Id] = faction;
        }

        foreach (NpcDefinition npc in npcs)
        {
            _npcs[npc.Id] = npc;
        }
    }

    public FactionDefinition? GetFaction(string id)
    {
        return _factions.GetValueOrDefault(id);
    }

    public NpcDefinition? GetNpc(string id)
    {
        return _npcs.GetValueOrDefault(id);
    }
}
