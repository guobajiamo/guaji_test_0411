using System.Collections.Generic;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Definitions;

public partial class ItemDefinition : NodeDefinitionBase
{
    public Rarity BaseRarity { get; set; } = Rarity.Common;

    public int BaseMaxStack { get; set; } = 99;

    public int BuyPrice { get; set; }

    public bool IsStackable { get; set; } = true;

    public int SellPrice { get; set; }

    public ItemTag Tags { get; set; } = ItemTag.None;

    public bool HasDurability { get; set; }

    public int MaxDurability { get; set; } = 100;

    public string AcquisitionHintKey { get; set; } = string.Empty;

    public string DetailDescriptionKey { get; set; } = string.Empty;

    public string IconTexturePath { get; set; } = string.Empty;

    public string OwnedUnlockSkillId { get; set; } = string.Empty;

    public ToolBonusDefinition ToolBonuses { get; set; } = new();

    public List<string> HoverInfoFields { get; } = new();

    public List<string> EquipSlotIds { get; } = new();

    public int EquipmentLevel { get; set; }

    public int EquipmentQuality { get; set; }

    public List<string> DetailInfoFields { get; } = new();

    public List<string> HoverExtraLines { get; } = new();

    public List<string> DetailExtraLines { get; } = new();

    public ConsumableBuffDefinition? ConsumeBuff { get; set; }

    public StapleFoodDefinition? StapleFood { get; set; }

    public InventoryUseActionDefinition? InventoryUseAction { get; set; }

    public ItemDefinition()
    {
        IsAbstract = false;
    }

    public bool HasTag(ItemTag tag)
    {
        return (Tags & tag) == tag;
    }

    public int GetEffectiveMaxStack(int extraCapacity)
    {
        if (!IsStackable)
        {
            return 1;
        }

        return int.MaxValue;
    }

    public bool CanConsumeFromInventory => ConsumeBuff != null
        && HasTag(ItemTag.Consumable)
        && HasTag(ItemTag.Food);

    public bool IsStapleFood => StapleFood != null
        && HasTag(ItemTag.Consumable)
        && HasTag(ItemTag.Food);

    public bool CanUseCustomActionFromInventory => InventoryUseAction != null
        && !string.IsNullOrWhiteSpace(InventoryUseAction.ButtonText);
}

public class ConsumableBuffDefinition
{
    public string BuffId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double DurationSeconds { get; set; }

    public bool ExtendDurationOnReapply { get; set; } = true;

    public List<BuffStatModifierDefinition> StatModifiers { get; } = new();
}

public class BuffStatModifierDefinition
{
    public string StatId { get; set; } = string.Empty;

    public double Multiplier { get; set; } = 1.0;
}

public class StapleFoodDefinition
{
    public string StapleId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double DurationSeconds { get; set; } = 1800.0;

    public bool ExtendDurationOnReapply { get; set; } = true;

    public double MaxStackedDurationSeconds { get; set; } = 86400.0;

    public int Tier { get; set; }

    public string Branch { get; set; } = string.Empty;

    public BattleStatBlockDefinition BattleStats { get; set; } = new();
}

public class InventoryUseActionDefinition
{
    public string ButtonText { get; set; } = string.Empty;

    public string TooltipText { get; set; } = string.Empty;

    public int ConsumeAmount { get; set; } = 1;

    public string SuccessLogText { get; set; } = string.Empty;

    public List<ItemCostEntry> Costs { get; } = new();

    public List<EventRewardEntry> Rewards { get; } = new();
}
