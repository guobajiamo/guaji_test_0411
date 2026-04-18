using Godot;

namespace Test00_0410.Autoload;

/// <summary>
/// 全局信号总线。
/// 作用是让 UI、系统、管理器之间可以低耦合地通信。
/// </summary>
public partial class SignalBus : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler(string itemId);

    [Signal]
    public delegate void SkillChangedEventHandler(string skillId);

    [Signal]
    public delegate void FactionChangedEventHandler(string factionId);

    [Signal]
    public delegate void ZoneChangedEventHandler(string zoneId);

    [Signal]
    public delegate void LogMessageRequestedEventHandler(string message);

    [Signal]
    public delegate void ActiveIdleEventChangedEventHandler(string eventId);

    [Signal]
    public delegate void GatheringNodeStateChangedEventHandler(string eventId);
}
