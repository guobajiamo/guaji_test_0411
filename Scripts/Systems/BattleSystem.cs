using Godot;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 战斗系统占位。
/// 本次先只声明接口和职责，数值与流程以后再补。
/// </summary>
public partial class BattleSystem : Node
{
    private PlayerProfile? _profile;

    public void Configure(PlayerProfile profile)
    {
        _profile = profile;
    }

    public void StartBattle(string battleId)
    {
        // 未来会在这里进入战斗流程。
    }

    public void ResolveTurn()
    {
        // 未来会在这里处理一回合战斗结算。
    }

    public void SetBattleStat(string statId, double value)
    {
        if (_profile == null || string.IsNullOrWhiteSpace(statId))
        {
            return;
        }

        _profile.BattleStats[statId] = value;
    }
}
