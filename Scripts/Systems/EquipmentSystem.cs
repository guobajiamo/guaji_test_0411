using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

public partial class EquipmentSystem : Node
{
    private PlayerProfile? _profile;
    private ItemRegistry? _itemRegistry;
    private SkillSystem? _skillSystem;

    public void Configure(PlayerProfile profile, ItemRegistry itemRegistry, SkillSystem? skillSystem = null)
    {
        _profile = profile;
        _itemRegistry = itemRegistry;
        _skillSystem = skillSystem;
        SynchronizeWithInventory();
    }

    public bool TryEquip(string itemId)
    {
        IReadOnlyList<EquipmentSlotId> supportedSlots = GetSupportedSlotsForItem(itemId);
        return supportedSlots.Count > 0 && TryEquip(itemId, supportedSlots[0]);
    }

    public bool TryUnequip(string slotId)
    {
        return EquipmentSlotCatalog.TryParse(slotId, out EquipmentSlotId parsedSlot)
            && TryUnequip(parsedSlot);
    }

    public bool TryEquip(string itemId, EquipmentSlotId slotId)
    {
        if (!CanEquip(itemId, slotId, out _)
            || _profile == null
            || _itemRegistry?.GetItem(itemId) is not ItemDefinition item)
        {
            return false;
        }

        EquipmentSlotId? reassignSourceSlot = FindReassignSourceSlot(itemId, slotId);
        if (reassignSourceSlot.HasValue && reassignSourceSlot.Value != slotId)
        {
            _profile.EquipmentState.TryClearSlot(reassignSourceSlot.Value);
        }

        ApplyAutoSwapRulesBeforeEquip(item, slotId);
        _profile.EquipmentState.SetEquippedItem(slotId, itemId);
        NormalizeBattleLayout();
        EnsureWeaponSkillLearned(item, addLog: true);
        return true;
    }

    public bool TryUnequip(EquipmentSlotId slotId)
    {
        bool result = _profile?.EquipmentState.TryClearSlot(slotId) == true;
        if (result)
        {
            NormalizeBattleLayout();
        }

        return result;
    }

    public string GetEquippedItemId(EquipmentSlotId slotId)
    {
        return _profile?.EquipmentState.GetEquippedItemId(slotId) ?? string.Empty;
    }

    public int GetEquippedCount(string itemId)
    {
        return _profile?.EquipmentState.CountEquippedCopies(itemId) ?? 0;
    }

    public EquipmentSlotId? FindSlotByItemId(string itemId)
    {
        return _profile?.EquipmentState.FindSlotByItemId(itemId);
    }

    public IReadOnlyList<EquipmentSlotId> GetSupportedSlotsForItem(string itemId)
    {
        if (_itemRegistry?.GetItem(itemId) is not ItemDefinition item)
        {
            return Array.Empty<EquipmentSlotId>();
        }

        return ResolveSupportedSlots(item);
    }

    public bool IsItemEquippable(string itemId)
    {
        return GetSupportedSlotsForItem(itemId).Count > 0;
    }

    public bool CanEquip(string itemId, EquipmentSlotId slotId, out string reason)
    {
        reason = string.Empty;

        if (_profile == null || _itemRegistry == null)
        {
            reason = "装备系统尚未绑定。";
            return false;
        }

        ItemDefinition? item = _itemRegistry.GetItem(itemId);
        if (item == null)
        {
            reason = "物品未注册。";
            return false;
        }

        List<EquipmentSlotId> supportedSlots = ResolveSupportedSlots(item);
        if (!supportedSlots.Contains(slotId))
        {
            reason = $"该道具不能装备到“{slotId.GetDisplayName()}”。";
            return false;
        }

        int ownedAmount = _profile.Inventory.GetItemAmount(itemId);
        if (ownedAmount <= 0)
        {
            reason = "背包中没有该道具。";
            return false;
        }

        int occupiedCopies = _profile.EquipmentState.CountEquippedCopies(itemId);
        if (_profile.EquipmentState.GetEquippedItemId(slotId) == itemId)
        {
            occupiedCopies = Math.Max(0, occupiedCopies - 1);
        }

        bool reusingExistingCopy = FindReassignSourceSlot(itemId, slotId).HasValue;
        if (reusingExistingCopy)
        {
            if (ownedAmount < occupiedCopies)
            {
                reason = "当前背包数量不足以移动这件装备。";
                return false;
            }
        }
        else if (ownedAmount <= occupiedCopies)
        {
            reason = "当前背包数量不足以重复装备。";
            return false;
        }

        if (!PassesAdditionalBattleRules(item, slotId, out reason))
        {
            return false;
        }

        return true;
    }

    public void SynchronizeWithInventory()
    {
        if (_profile == null)
        {
            return;
        }

        List<EquipmentSlotId> invalidSlots = new();
        Dictionary<string, int> equippedCountByItemId = new(StringComparer.Ordinal);

        foreach ((EquipmentSlotId slotId, string itemId) in _profile.EquipmentState.EquippedItemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                invalidSlots.Add(slotId);
                continue;
            }

            if (!IsItemEquippable(itemId))
            {
                invalidSlots.Add(slotId);
                continue;
            }

            if (!GetSupportedSlotsForItem(itemId).Contains(slotId))
            {
                invalidSlots.Add(slotId);
                continue;
            }

            equippedCountByItemId[itemId] = equippedCountByItemId.GetValueOrDefault(itemId) + 1;
            if (_profile.Inventory.GetItemAmount(itemId) < equippedCountByItemId[itemId])
            {
                invalidSlots.Add(slotId);
            }
        }

        foreach (EquipmentSlotId slotId in invalidSlots.Distinct())
        {
            _profile.EquipmentState.TryClearSlot(slotId);
        }

        NormalizeBattleLayout();
        EnsureWeaponSkillsLearnedFromEquippedItems();
    }

    private static List<EquipmentSlotId> ResolveSupportedSlots(ItemDefinition item)
    {
        List<EquipmentSlotId> resolved = new();

        foreach (string configuredSlotId in item.EquipSlotIds)
        {
            if (EquipmentSlotCatalog.TryParse(configuredSlotId, out EquipmentSlotId slotId)
                && !resolved.Contains(slotId))
            {
                resolved.Add(slotId);
            }
        }

        if (resolved.Count > 0)
        {
            return resolved;
        }

        AddTagDefinedSlots(item, resolved);
        AddBattleEquipmentDefinedSlots(item, resolved);

        if (resolved.Count > 0)
        {
            return resolved;
        }

        string keywordSource = $"{item.Id}|{item.ParentId}|{item.NameKey}|{item.DescriptionKey}".ToLowerInvariant();
        AddKeywordSlots(keywordSource, resolved);

        if (item.HasTag(ItemTag.Weapon) && !resolved.Contains(EquipmentSlotId.MainHand))
        {
            resolved.Add(EquipmentSlotId.MainHand);
        }

        if (resolved.Count == 0 && (item.HasTag(ItemTag.Battle) || item.HasTag(ItemTag.Tool)))
        {
            resolved.Add(EquipmentSlotId.MainHand);
        }

        return resolved;
    }

    private static void AddKeywordSlots(string keywordSource, List<EquipmentSlotId> resolved)
    {
        if (ContainsAny(keywordSource, "necklace", "amulet", "pendant"))
        {
            AddUnique(resolved, EquipmentSlotId.Necklace);
        }

        if (ContainsAny(keywordSource, "cloak", "cape"))
        {
            AddUnique(resolved, EquipmentSlotId.Cloak);
        }

        if (ContainsAny(keywordSource, "pants", "trousers"))
        {
            AddUnique(resolved, EquipmentSlotId.Pants);
        }

        if (ContainsAny(keywordSource, "boots", "shoes"))
        {
            AddUnique(resolved, EquipmentSlotId.Boots);
        }

        if (ContainsAny(keywordSource, "gloves", "gauntlet"))
        {
            AddUnique(resolved, EquipmentSlotId.Gloves);
        }

        if (ContainsAny(keywordSource, "ring"))
        {
            AddUnique(resolved, EquipmentSlotId.LeftRing);
            AddUnique(resolved, EquipmentSlotId.RightRing);
        }

        if (ContainsAny(keywordSource, "shield", "offhand", "tome", "book", "orb"))
        {
            AddUnique(resolved, EquipmentSlotId.OffHand);
        }

        if (ContainsAny(keywordSource, "ammo", "ammunition", "arrow", "bolt", "bullet", "quiver", "cartridge"))
        {
            AddUnique(resolved, EquipmentSlotId.Ammo);
        }

        if (ContainsAny(keywordSource, "weapon", "sword", "axe", "bow", "staff", "rod", "spear", "hammer", "blade"))
        {
            AddUnique(resolved, EquipmentSlotId.MainHand);
        }
    }

    private static bool ContainsAny(string keywordSource, params string[] keywords)
    {
        return keywords.Any(keyword => keywordSource.Contains(keyword, StringComparison.Ordinal));
    }

    private static void AddUnique(List<EquipmentSlotId> slots, EquipmentSlotId slotId)
    {
        if (!slots.Contains(slotId))
        {
            slots.Add(slotId);
        }
    }
}
