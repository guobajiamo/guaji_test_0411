using System;

namespace Test00_0410.Core.Enums;

[Flags]
public enum ItemTag : long
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
    Axe = 1 << 11,
    Battle = 1 << 12,
    Special = 1 << 13,
    Valuable = 1 << 14,
    Junk = 1 << 15,
    Other = 1 << 16,
    Pickaxe = 1 << 17,
    Seed = 1 << 18,
    Gloves = 1 << 19,
    Pants = 1 << 20,
    Boots = 1 << 21,
    Necklace = 1 << 22,
    Ring = 1 << 23,
    Cloak = 1 << 24,
    Accessory = 1 << 25,
    Ammo = 1 << 26,
    Shield = 1 << 27,
    Melee = 1L << 28,
    Ranged = 1L << 29,
    Slash = 1L << 30,
    Pierce = 1L << 31,
    Strike = 1L << 32
}
