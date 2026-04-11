namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家与某个势力之间的动态状态。
/// 例如当前好感度、是否已经达成和平建交等。
/// </summary>
public class PlayerFactionState
{
    public string FactionId { get; set; } = string.Empty;

    public int Reputation { get; set; }

    public bool HasPeaceAgreement { get; set; }
}
