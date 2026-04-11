using Godot;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 势力系统。
/// 负责势力声望变化、NPC 解锁和和平建交判定。
/// </summary>
public partial class FactionSystem : Node
{
    private PlayerProfile? _profile;
    private FactionRegistry? _factionRegistry;

    public void Configure(PlayerProfile profile, FactionRegistry factionRegistry)
    {
        _profile = profile;
        _factionRegistry = factionRegistry;
    }

    public void AddReputation(string factionId, int amount)
    {
        if (_profile == null)
        {
            return;
        }

        PlayerFactionState state = _profile.GetOrCreateFactionState(factionId);
        int nextReputation = state.Reputation + amount;

        // 优先参考静态配置做上下限裁切，并同步和平状态。
        if (_factionRegistry != null && _factionRegistry.GetFaction(factionId) is { } factionDefinition)
        {
            nextReputation = System.Math.Clamp(nextReputation, 0, factionDefinition.MaxReputation);
            state.HasPeaceAgreement = nextReputation >= factionDefinition.PeaceThreshold;
        }
        else
        {
            nextReputation = System.Math.Max(0, nextReputation);
        }

        state.Reputation = nextReputation;
    }

    public bool CanAccessNpc(string npcId)
    {
        if (_profile == null || _factionRegistry == null)
        {
            return false;
        }

        var npcDefinition = _factionRegistry.GetNpc(npcId);
        if (npcDefinition == null)
        {
            return false;
        }

        PlayerFactionState factionState = _profile.GetOrCreateFactionState(npcDefinition.FactionId);
        return factionState.Reputation >= npcDefinition.RequiredReputation;
    }
}
