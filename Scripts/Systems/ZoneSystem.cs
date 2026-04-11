using Godot;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 区域系统。
/// 负责探索度、通关次数和新区解锁逻辑。
/// </summary>
public partial class ZoneSystem : Node
{
    private PlayerProfile? _profile;
    private ZoneRegistry? _zoneRegistry;

    public void Configure(PlayerProfile profile, ZoneRegistry zoneRegistry)
    {
        _profile = profile;
        _zoneRegistry = zoneRegistry;
    }

    public void AddClearCount(string zoneId, int amount)
    {
        if (_profile == null)
        {
            return;
        }

        PlayerZoneState state = _profile.GetOrCreateZoneState(zoneId);
        state.ClearCount += amount;
        state.ExplorationPercent = System.Math.Min(100.0, state.ClearCount * 10.0);
    }

    public void UnlockZone(string zoneId)
    {
        if (_profile == null)
        {
            return;
        }

        _profile.GetOrCreateZoneState(zoneId).IsUnlocked = true;
    }
}
