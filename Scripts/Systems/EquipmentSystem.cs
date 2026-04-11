using Godot;

namespace Test00_0410.Systems;

/// <summary>
/// 装备系统占位。
/// 以后武器、头盔、护甲等都可以接到这里。
/// </summary>
public partial class EquipmentSystem : Node
{
    public bool TryEquip(string itemId)
    {
        return false;
    }

    public bool TryUnequip(string slotId)
    {
        return false;
    }
}
