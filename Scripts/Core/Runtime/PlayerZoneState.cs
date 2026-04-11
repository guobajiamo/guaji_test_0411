namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家在某个区域中的探索进度。
/// </summary>
public class PlayerZoneState
{
    public string ZoneId { get; set; } = string.Empty;

    public int ClearCount { get; set; }

    public double ExplorationPercent { get; set; }

    public bool IsUnlocked { get; set; }
}
