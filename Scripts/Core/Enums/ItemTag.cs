using System;

namespace Test00_0410.Core.Enums;

/// <summary>
/// 物品标签。
/// 这里使用位标记，表示一个物品可以同时拥有多个标签。
/// 例如“柴火”可以同时是 Material 和 Fuel。
/// </summary>
[Flags]
public enum ItemTag
{
    None = 0,
    Material = 1 << 0,
    Consumable = 1 << 1,
    Equipment = 1 << 2,
    Tool = 1 << 3,
    Helmet = 1 << 4,
    Armor = 1 << 5,
    Weapon = 1 << 6,
    Fuel = 1 << 7,
    Food = 1 << 8,
    QuestItem = 1 << 9,
    Currency = 1 << 10,
    Axe = 1 << 11
}
