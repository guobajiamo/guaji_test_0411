using Godot;

namespace Test00_0410.Systems;

/// <summary>
/// Buff/道具加成系统。
/// 后续工具倍率、速度倍率、临时增益都可以统一从这里取值。
/// </summary>
public partial class BuffSystem : Node
{
    public double GetMultiplier(string statId)
    {
        return 1.0;
    }

    public void RefreshActiveBuffs()
    {
        // 未来会在这里刷新已装备道具或临时效果提供的加成。
    }
}
