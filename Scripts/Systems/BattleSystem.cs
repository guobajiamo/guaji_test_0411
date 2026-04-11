using Godot;

namespace Test00_0410.Systems;

/// <summary>
/// 战斗系统占位。
/// 本次先只声明接口和职责，数值与流程以后再补。
/// </summary>
public partial class BattleSystem : Node
{
    public void StartBattle(string battleId)
    {
        // 未来会在这里进入战斗流程。
    }

    public void ResolveTurn()
    {
        // 未来会在这里处理一回合战斗结算。
    }
}
