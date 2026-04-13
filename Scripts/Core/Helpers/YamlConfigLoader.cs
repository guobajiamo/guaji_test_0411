using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Helpers;

public class YamlConfigLoader
{
    public sealed class FactionConfigSet
    {
        public List<FactionDefinition> Factions { get; } = new();

        public List<NpcDefinition> Npcs { get; } = new();
    }

    public sealed class LocalizationConfig
    {
        public string Locale { get; set; } = "zh";

        public Dictionary<string, string> Translations { get; } = new();
    }

    public IReadOnlyList<string> ResolveYamlFileBundle(string path, string bundleLabel = "yaml")
    {
        List<string> files = new();
        if (string.IsNullOrWhiteSpace(path))
        {
            GD.PushWarning($"[YamlConfigLoader] {bundleLabel} 配置路径为空。");
            return files;
        }

        if (FileAccess.FileExists(path))
        {
            files.Add(path);
            return files;
        }

        CollectYamlFiles(path, files);
        files.Sort(StringComparer.OrdinalIgnoreCase);

        if (files.Count == 0)
        {
            GD.PushWarning($"[YamlConfigLoader] 未在 {path} 下找到任何 .yaml 文件（bundle={bundleLabel}）。");
        }
        else
        {
            GD.Print($"[YamlConfigLoader] bundle={bundleLabel} path={path} 已解析 {files.Count} 个 yaml 文件。");
        }

        return files;
    }

    public Dictionary<string, object?> LoadMap(string path)
    {
        return LoadMergedMap(path, "map");
    }

    public Dictionary<string, object?> LoadMergedMap(string path, string bundleLabel = "map")
    {
        Dictionary<string, object?> merged = new(StringComparer.Ordinal);
        foreach (string filePath in ResolveYamlFileBundle(path, bundleLabel))
        {
            object? document = ParseDocument(LoadRawText(filePath));
            if (document is not Dictionary<string, object?> map)
            {
                GD.PushWarning($"[YamlConfigLoader] {filePath} 不是可合并的 Map 结构，已忽略。");
                continue;
            }

            MergeMapsRecursive(merged, map);
        }

        return merged;
    }

    public string LoadRawText(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return string.Empty;
        }

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file.GetAsText();
    }

    public List<T> LoadList<T>(string path, string rootKey)
    {
        Dictionary<string, object?> map = LoadMergedMap(path, rootKey);
        if (!map.TryGetValue(rootKey, out object? listValue) || listValue is not List<object?>)
        {
            return new List<T>();
        }

        return new List<T>();
    }

    public List<CategoryDefinition> LoadCategories(string path)
    {
        List<CategoryDefinition> categories = new();
        Dictionary<string, string> idSources = new(StringComparer.Ordinal);
        IReadOnlyList<string> files = ResolveYamlFileBundle(path, "categories");

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            List<Dictionary<string, object?>> entries = GetRootListEntriesFromFile(files[fileIndex], "categories");
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Dictionary<string, object?> entry = entries[entryIndex];
                CategoryDefinition category = new()
                {
                    Id = GetString(entry, "id"),
                    ParentId = GetString(entry, "parent_id"),
                    NameKey = GetString(entry, "name_key"),
                    DescriptionKey = GetString(entry, "description_key"),
                    DefinitionOrder = GetInt(entry, "definition_order", 100),
                    IsVisible = GetBool(entry, "is_visible", true),
                    HideWhenEmpty = GetBool(entry, "hide_when_empty", false),
                    SourceFilePath = files[fileIndex],
                    SourceFileOrder = fileIndex,
                    SourceEntryOrder = entryIndex
                };

                WarnIfDuplicateId(idSources, "分类", category.Id, files[fileIndex]);
                categories.Add(category);
            }
        }

        return categories;
    }

    public List<ItemDefinition> LoadItems(string path)
    {
        List<ItemDefinition> items = new();
        Dictionary<string, string> idSources = new(StringComparer.Ordinal);
        IReadOnlyList<string> files = ResolveYamlFileBundle(path, "items");

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            List<Dictionary<string, object?>> entries = GetRootListEntriesFromFile(files[fileIndex], "items");
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Dictionary<string, object?> entry = entries[entryIndex];
                ItemDefinition item = new()
                {
                    Id = GetString(entry, "id"),
                    ParentId = GetString(entry, "parent_id"),
                    NameKey = GetString(entry, "name_key"),
                    DescriptionKey = GetString(entry, "description_key"),
                    DefinitionOrder = GetInt(entry, "definition_order", 100),
                    BaseRarity = GetEnum(entry, "base_rarity", Rarity.Common),
                    BaseMaxStack = GetInt(entry, "base_max_stack", 99),
                    BuyPrice = GetInt(entry, "buy_price", 0),
                    SellPrice = GetInt(entry, "sell_price", 0),
                    Tags = ParseItemTags(entry.TryGetValue("tags", out object? tagsValue) ? tagsValue : null),
                    HasDurability = GetBool(entry, "has_durability", false),
                    MaxDurability = GetInt(entry, "max_durability", 100),
                    AcquisitionHintKey = GetString(entry, "acquisition_hint_key"),
                    DetailDescriptionKey = GetString(entry, "detail_description_key"),
                    IsVisible = GetBool(entry, "is_visible", true),
                    Deprecated = GetBool(entry, "deprecated", false),
                    DeprecatedSince = GetString(entry, "deprecated_since"),
                    ReplacementId = GetString(entry, "replacement_id"),
                    SourceFilePath = files[fileIndex],
                    SourceFileOrder = fileIndex,
                    SourceEntryOrder = entryIndex
                };

                if (entry.TryGetValue("tool_bonuses", out object? toolBonusValue) && toolBonusValue is Dictionary<string, object?> toolBonusMap)
                {
                    item.ToolBonuses.LogYieldMultiplier = GetDouble(toolBonusMap, "log_yield_multiplier", 1.0);
                    item.ToolBonuses.ChopSpeedMultiplier = GetDouble(toolBonusMap, "chop_speed_multiplier", 1.0);
                }

                WarnIfDuplicateId(idSources, "物品", item.Id, files[fileIndex]);
                items.Add(item);
            }
        }

        return items;
    }

    public List<SkillDefinition> LoadSkills(string path)
    {
        List<SkillDefinition> skills = new();
        Dictionary<string, string> idSources = new(StringComparer.Ordinal);
        IReadOnlyList<string> files = ResolveYamlFileBundle(path, "skills");

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            List<Dictionary<string, object?>> entries = GetRootListEntriesFromFile(files[fileIndex], "skills");
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Dictionary<string, object?> entry = entries[entryIndex];
                SkillDefinition skill = new()
                {
                    Id = GetString(entry, "id"),
                    NameKey = GetString(entry, "name_key"),
                    DescriptionKey = GetString(entry, "description_key"),
                    MaxLevel = GetInt(entry, "max_level", 1),
                    InitialLevel = GetInt(entry, "initial_level", 1),
                    MaxTotalExp = GetInt(entry, "max_total_exp", 0),
                    RequiredToolTag = GetEnum(entry, "required_tool_tag", ItemTag.None),
                    PrimaryOutputItemId = GetString(entry, "primary_output_item_id"),
                    SourceFilePath = files[fileIndex],
                    SourceFileOrder = fileIndex,
                    SourceEntryOrder = entryIndex
                };

                foreach (Dictionary<string, object?> levelEntry in GetListOfMaps(entry, "level_table"))
                {
                    skill.LevelTable.Add(new SkillLevelEntry
                    {
                        Level = GetInt(levelEntry, "level", 1),
                        ExpToNext = GetInt(levelEntry, "exp_to_next", 0),
                        Output = GetDouble(levelEntry, "output", 0.0),
                        Interval = GetDouble(levelEntry, "interval", 1.0)
                    });
                }

                WarnIfDuplicateId(idSources, "技能", skill.Id, files[fileIndex]);
                skills.Add(skill);
            }
        }

        return skills;
    }

    public List<EventDefinition> LoadEvents(string path)
    {
        List<EventDefinition> events = new();
        Dictionary<string, string> idSources = new(StringComparer.Ordinal);
        IReadOnlyList<string> files = ResolveYamlFileBundle(path, "events");

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            List<Dictionary<string, object?>> entries = GetRootListEntriesFromFile(files[fileIndex], "events");
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Dictionary<string, object?> entry = entries[entryIndex];
                EventDefinition definition = new()
                {
                    Id = GetString(entry, "id"),
                    NameKey = GetString(entry, "name_key"),
                    DescriptionKey = GetString(entry, "description_key"),
                    HoverInfoKey = GetString(entry, "hover_info_key"),
                    Type = GetEnum(entry, "type", EventType.RepeatableClick),
                    LinkedSkillId = GetString(entry, "linked_skill_id"),
                    ButtonListGroup = GetEnum(entry, "button_list_group", ButtonListGroup.MainClick),
                    RemoveAfterTriggered = GetBool(entry, "remove_after_triggered", false),
                    SourceFilePath = files[fileIndex],
                    SourceFileOrder = fileIndex,
                    SourceEntryOrder = entryIndex
                };

                List<EventConditionEntry> displayConditions = ParseEventConditions(entry, "display_conditions");
                bool hasDisplayCount = entry.ContainsKey("display_condition_count");
                bool hasDisplayList = entry.ContainsKey("display_conditions");
                definition.DisplayConditionCount = hasDisplayCount
                    ? GetInt(entry, "display_condition_count", 0)
                    : displayConditions.Count;
                definition.DisplayConditions.AddRange(displayConditions);
                if (hasDisplayCount || hasDisplayList)
                {
                    ValidateConditionCount(files[fileIndex], definition.Id, "display", definition.DisplayConditionCount, definition.DisplayConditions.Count);
                }

                List<EventConditionEntry> interactionConditions = entry.ContainsKey("interaction_conditions")
                    ? ParseEventConditions(entry, "interaction_conditions")
                    : ParseEventConditions(entry, "prerequisites");
                bool hasInteractionCount = entry.ContainsKey("interaction_condition_count");
                bool hasInteractionList = entry.ContainsKey("interaction_conditions") || entry.ContainsKey("prerequisites");
                definition.InteractionConditionCount = hasInteractionCount
                    ? GetInt(entry, "interaction_condition_count", 0)
                    : interactionConditions.Count;
                definition.InteractionConditions.AddRange(interactionConditions);
                if (hasInteractionCount || hasInteractionList)
                {
                    ValidateConditionCount(files[fileIndex], definition.Id, "interaction", definition.InteractionConditionCount, definition.InteractionConditions.Count);
                }

                List<EventConditionEntry> hideConditions = ParseEventConditions(entry, "hide_conditions");
                bool hasHideCount = entry.ContainsKey("hide_condition_count");
                bool hasHideList = entry.ContainsKey("hide_conditions");
                definition.HideConditionCount = hasHideCount
                    ? GetInt(entry, "hide_condition_count", 0)
                    : hideConditions.Count;
                definition.HideConditions.AddRange(hideConditions);
                if (hasHideCount || hasHideList)
                {
                    ValidateConditionCount(files[fileIndex], definition.Id, "hide", definition.HideConditionCount, definition.HideConditions.Count);
                }

                if (entry.TryGetValue("dialog", out object? dialogValue) && dialogValue is Dictionary<string, object?> dialogMap)
                {
                    EventDialogDefinition dialog = new()
                    {
                        BodyTextKey = GetString(dialogMap, "body_text"),
                        ConfirmButtonText = GetString(dialogMap, "confirm_button_text"),
                        ShowCancelButton = GetBool(dialogMap, "show_cancel_button", true),
                        ConsumeSourceEventOnChoice = GetBool(dialogMap, "consume_source_event_on_choice", true)
                    };

                    foreach (Dictionary<string, object?> choiceMap in GetListOfMaps(dialogMap, "choices"))
                    {
                        dialog.Choices.Add(new EventDialogChoiceDefinition
                        {
                            ButtonText = GetString(choiceMap, "button_text"),
                            TargetEventId = GetString(choiceMap, "target_event_id")
                        });
                    }

                    definition.Dialog = dialog;
                }

                foreach (Dictionary<string, object?> cost in GetListOfMaps(entry, "costs"))
                {
                    definition.Costs.Add(new ItemCostEntry
                    {
                        ItemId = GetString(cost, "item_id"),
                        Amount = GetInt(cost, "amount", 0)
                    });
                }

                foreach (Dictionary<string, object?> reward in GetListOfMaps(entry, "rewards"))
                {
                    definition.Rewards.Add(new EventRewardEntry
                    {
                        ItemId = GetString(reward, "item_id"),
                        Amount = GetInt(reward, "amount", 0),
                        DropChance = GetDouble(reward, "drop_chance", 1.0)
                    });
                }

                foreach (Dictionary<string, object?> effect in GetListOfMaps(entry, "effects"))
                {
                    definition.Effects.Add(new EventEffectEntry
                    {
                        EffectType = GetEnum(effect, "effect_type", EventEffectType.None),
                        TargetId = GetString(effect, "target_id"),
                        IntValue = GetInt(effect, "int_value", 0),
                        DoubleValue = GetDouble(effect, "double_value", 0.0),
                        TextValue = GetString(effect, "text_value")
                    });
                }

                WarnIfDuplicateId(idSources, "事件", definition.Id, files[fileIndex]);
                events.Add(definition);
            }
        }

        return events;
    }

    public FactionConfigSet LoadFactions(string path)
    {
        FactionConfigSet result = new();
        Dictionary<string, string> factionSources = new(StringComparer.Ordinal);
        Dictionary<string, string> npcSources = new(StringComparer.Ordinal);
        IReadOnlyList<string> files = ResolveYamlFileBundle(path, "factions");

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            object? document = ParseDocument(LoadRawText(files[fileIndex]));
            if (document is not Dictionary<string, object?> map)
            {
                GD.PushWarning($"[YamlConfigLoader] {files[fileIndex]} 不是 factions/npcs Map 结构，已忽略。");
                continue;
            }

            foreach ((Dictionary<string, object?> entry, int entryIndex) in GetListOfMaps(map, "factions").Select((entry, index) => (entry, index)))
            {
                FactionDefinition faction = new()
                {
                    Id = GetString(entry, "id"),
                    NameKey = GetString(entry, "name_key"),
                    DescriptionKey = GetString(entry, "description_key"),
                    MaxReputation = GetInt(entry, "max_reputation", 100),
                    PeaceThreshold = GetInt(entry, "peace_threshold", 100),
                    SourceFilePath = files[fileIndex],
                    SourceFileOrder = fileIndex,
                    SourceEntryOrder = entryIndex
                };

                faction.NpcIds.AddRange(GetStringList(entry, "npc_ids"));
                faction.FactionEventIds.AddRange(GetStringList(entry, "faction_event_ids"));
                WarnIfDuplicateId(factionSources, "势力", faction.Id, files[fileIndex]);
                result.Factions.Add(faction);
            }

            foreach ((Dictionary<string, object?> entry, int entryIndex) in GetListOfMaps(map, "npcs").Select((entry, index) => (entry, index)))
            {
                NpcDefinition npc = new()
                {
                    Id = GetString(entry, "id"),
                    NameKey = GetString(entry, "name_key"),
                    FactionId = GetString(entry, "faction_id"),
                    HasShop = GetBool(entry, "has_shop", false),
                    RequiredReputation = GetInt(entry, "required_reputation", 0),
                    SourceFilePath = files[fileIndex],
                    SourceFileOrder = fileIndex,
                    SourceEntryOrder = entryIndex
                };

                foreach (Dictionary<string, object?> shopItemEntry in GetListOfMaps(entry, "shop_items"))
                {
                    ShopItemEntry shopItem = new()
                    {
                        ItemId = GetString(shopItemEntry, "item_id"),
                        Stock = GetInt(shopItemEntry, "stock", -1),
                        PaymentType = GetEnum(shopItemEntry, "payment_type", CurrencyType.Gold),
                        GoldCost = GetInt(shopItemEntry, "gold_cost", 0),
                        RequiredReputation = GetInt(shopItemEntry, "required_reputation", 0)
                    };

                    foreach (Dictionary<string, object?> barterCost in GetListOfMaps(shopItemEntry, "barter_costs"))
                    {
                        shopItem.BarterCosts.Add(new ItemCostEntry
                        {
                            ItemId = GetString(barterCost, "item_id"),
                            Amount = GetInt(barterCost, "amount", 0)
                        });
                    }

                    npc.ShopItems.Add(shopItem);
                }

                WarnIfDuplicateId(npcSources, "NPC", npc.Id, files[fileIndex]);
                result.Npcs.Add(npc);
            }
        }

        return result;
    }

    public List<ZoneDefinition> LoadZones(string path)
    {
        List<ZoneDefinition> zones = new();
        Dictionary<string, string> idSources = new(StringComparer.Ordinal);
        IReadOnlyList<string> files = ResolveYamlFileBundle(path, "zones");

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            List<Dictionary<string, object?>> entries = GetRootListEntriesFromFile(files[fileIndex], "zones");
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Dictionary<string, object?> entry = entries[entryIndex];
                ZoneDefinition zone = new()
                {
                    Id = GetString(entry, "id"),
                    NameKey = GetString(entry, "name_key"),
                    DescriptionKey = GetString(entry, "description_key"),
                    MaxClearCount = GetInt(entry, "max_clear_count", 10),
                    UnlocksZoneId = GetString(entry, "unlocks_zone_id"),
                    UnlocksFactionId = GetString(entry, "unlocks_faction_id"),
                    SourceFilePath = files[fileIndex],
                    SourceFileOrder = fileIndex,
                    SourceEntryOrder = entryIndex
                };

                foreach (Dictionary<string, object?> condition in GetListOfMaps(entry, "unlock_conditions"))
                {
                    zone.UnlockConditions.Add(new EventConditionEntry
                    {
                        ConditionType = GetEnum(condition, "condition_type", ConditionType.None),
                        TargetId = GetString(condition, "target_id"),
                        RequiredValue = GetDouble(condition, "required_value", 0.0)
                    });
                }

                WarnIfDuplicateId(idSources, "Zone", zone.Id, files[fileIndex]);
                zones.Add(zone);
            }
        }

        return zones;
    }

    public LocalizationConfig LoadLocalization(string path)
    {
        LocalizationConfig result = new();
        IReadOnlyList<string> files = ResolveYamlFileBundle(path, "localization");

        foreach (string filePath in files)
        {
            object? document = ParseDocument(LoadRawText(filePath));
            if (document is not Dictionary<string, object?> map)
            {
                GD.PushWarning($"[YamlConfigLoader] {filePath} 不是 localization Map 结构，已忽略。");
                continue;
            }

            string locale = GetString(map, "locale", "zh");
            if (!string.IsNullOrWhiteSpace(locale)
                && !string.IsNullOrWhiteSpace(result.Locale)
                && !string.Equals(result.Locale, locale, StringComparison.OrdinalIgnoreCase))
            {
                GD.PushWarning($"[YamlConfigLoader] localization bundle 中发现不一致的 locale：已存在 {result.Locale}，但 {filePath} 声明为 {locale}。已继续按后者覆盖。");
            }

            result.Locale = locale;

            if (map.TryGetValue("translations", out object? translationsValue) && translationsValue is Dictionary<string, object?> translationsMap)
            {
                foreach ((string key, object? value) in translationsMap)
                {
                    if (result.Translations.ContainsKey(key))
                    {
                        GD.PushWarning($"[YamlConfigLoader] localization key 重复：{key}。后加载文件 {filePath} 已覆盖旧值。");
                    }

                    result.Translations[key] = ConvertToString(value);
                }
            }
        }

        return result;
    }

    private static List<EventConditionEntry> ParseEventConditions(Dictionary<string, object?> map, string key)
    {
        List<EventConditionEntry> conditions = new();
        foreach (Dictionary<string, object?> condition in GetListOfMaps(map, key))
        {
            conditions.Add(new EventConditionEntry
            {
                ConditionType = GetEnum(condition, "condition_type", ConditionType.None),
                TargetId = GetString(condition, "target_id"),
                RequiredValue = GetDouble(condition, "required_value", 0.0)
            });
        }

        return conditions;
    }

    private static void ValidateConditionCount(string path, string eventId, string groupName, int expectedCount, int actualCount)
    {
        if (expectedCount == actualCount)
        {
            return;
        }

        GD.PushError($"[YamlConfigLoader] {path} 中事件 {eventId} 的 {groupName}_condition_count={expectedCount}，但实际条件条目数为 {actualCount}。");
    }

    private static void WarnIfDuplicateId(Dictionary<string, string> sourceMap, string typeLabel, string id, string currentPath)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            GD.PushWarning($"[YamlConfigLoader] 在 {currentPath} 中发现空的 {typeLabel} ID。");
            return;
        }

        if (sourceMap.TryGetValue(id, out string? previousPath))
        {
            GD.PushWarning($"[YamlConfigLoader] 检测到重复 {typeLabel} ID：{id}。首次来源：{previousPath}，重复来源：{currentPath}。");
            return;
        }

        sourceMap[id] = currentPath;
    }

    private static void MergeMapsRecursive(Dictionary<string, object?> target, Dictionary<string, object?> incoming)
    {
        foreach ((string key, object? value) in incoming)
        {
            if (!target.TryGetValue(key, out object? existing))
            {
                target[key] = CloneValue(value);
                continue;
            }

            if (existing is Dictionary<string, object?> existingMap && value is Dictionary<string, object?> incomingMap)
            {
                MergeMapsRecursive(existingMap, incomingMap);
                continue;
            }

            if (existing is List<object?> existingList && value is List<object?> incomingList)
            {
                foreach (object? item in incomingList)
                {
                    existingList.Add(CloneValue(item));
                }
                continue;
            }

            target[key] = CloneValue(value);
        }
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            Dictionary<string, object?> map => map.ToDictionary(pair => pair.Key, pair => CloneValue(pair.Value), StringComparer.Ordinal),
            List<object?> list => list.Select(CloneValue).ToList(),
            _ => value
        };
    }

    private List<Dictionary<string, object?>> GetRootListEntriesFromFile(string path, string rootKey)
    {
        object? document = ParseDocument(LoadRawText(path));
        if (document is not Dictionary<string, object?> map)
        {
            return new List<Dictionary<string, object?>>();
        }

        return GetListOfMaps(map, rootKey);
    }

    private static List<Dictionary<string, object?>> GetListOfMaps(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out object? value) || value is not List<object?> list)
        {
            return new List<Dictionary<string, object?>>();
        }

        return list
            .OfType<Dictionary<string, object?>>()
            .ToList();
    }

    private static List<string> GetStringList(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out object? value) || value is not List<object?> list)
        {
            return new List<string>();
        }

        return list.Select(item => ConvertToString(item)).ToList();
    }

    private static string GetString(Dictionary<string, object?> map, string key, string defaultValue = "")
    {
        return map.TryGetValue(key, out object? value) ? ConvertToString(value, defaultValue) : defaultValue;
    }

    private static int GetInt(Dictionary<string, object?> map, string key, int defaultValue = 0)
    {
        return map.TryGetValue(key, out object? value) ? ConvertToInt(value, defaultValue) : defaultValue;
    }

    private static double GetDouble(Dictionary<string, object?> map, string key, double defaultValue = 0.0)
    {
        return map.TryGetValue(key, out object? value) ? ConvertToDouble(value, defaultValue) : defaultValue;
    }

    private static bool GetBool(Dictionary<string, object?> map, string key, bool defaultValue = false)
    {
        return map.TryGetValue(key, out object? value) ? ConvertToBool(value, defaultValue) : defaultValue;
    }

    private static TEnum GetEnum<TEnum>(Dictionary<string, object?> map, string key, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        string text = GetString(map, key);
        return Enum.TryParse(text, true, out TEnum parsed) ? parsed : defaultValue;
    }

    private static ItemTag ParseItemTags(object? value)
    {
        if (value is not List<object?> list)
        {
            return ItemTag.None;
        }

        ItemTag combined = ItemTag.None;
        foreach (object? item in list)
        {
            string tagText = ConvertToString(item);
            if (Enum.TryParse(tagText, true, out ItemTag parsedTag))
            {
                combined |= parsedTag;
            }
        }

        return combined;
    }

    private static void CollectYamlFiles(string directoryPath, List<string> files)
    {
        DirAccess? directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            return;
        }

        directory.ListDirBegin();
        while (true)
        {
            string name = directory.GetNext();
            if (string.IsNullOrWhiteSpace(name))
            {
                break;
            }

            if (name == "." || name == "..")
            {
                continue;
            }

            string childPath = $"{directoryPath.TrimEnd('/')}/{name}";
            if (directory.CurrentIsDir())
            {
                CollectYamlFiles(childPath, files);
                continue;
            }

            if (name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(childPath);
            }
        }
        directory.ListDirEnd();
    }

    private object? ParseDocument(string text)
    {
        List<YamlLine> lines = BuildLines(text);
        if (lines.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        int index = 0;
        return ParseBlock(lines, ref index, lines[0].Indent);
    }

    private static List<YamlLine> BuildLines(string text)
    {
        List<YamlLine> lines = new();

        foreach (string rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            string trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            int indent = rawLine.TakeWhile(character => character == ' ').Count();
            lines.Add(new YamlLine(indent, trimmed));
        }

        return lines;
    }

    private object? ParseBlock(List<YamlLine> lines, ref int index, int indent)
    {
        if (index >= lines.Count)
        {
            return null;
        }

        return lines[index].Content.StartsWith("- ")
            ? ParseSequence(lines, ref index, indent)
            : ParseMapping(lines, ref index, indent);
    }

    private Dictionary<string, object?> ParseMapping(List<YamlLine> lines, ref int index, int indent)
    {
        Dictionary<string, object?> map = new(StringComparer.Ordinal);
        ParseMappingEntries(lines, ref index, indent, map);
        return map;
    }

    private void ParseMappingEntries(List<YamlLine> lines, ref int index, int indent, Dictionary<string, object?> target)
    {
        while (index < lines.Count)
        {
            YamlLine line = lines[index];
            if (line.Indent < indent || line.Content.StartsWith("- "))
            {
                break;
            }

            if (line.Indent > indent)
            {
                break;
            }

            ParseSingleMappingEntry(lines, ref index, indent, line.Content, target);
        }
    }

    private void ParseSingleMappingEntry(List<YamlLine> lines, ref int index, int indent, string content, Dictionary<string, object?> target)
    {
        (string key, string valueText) = SplitKeyValue(content);
        index++;

        if (string.IsNullOrEmpty(valueText))
        {
            if (index < lines.Count && lines[index].Indent > indent)
            {
                target[key] = ParseBlock(lines, ref index, lines[index].Indent);
                return;
            }

            target[key] = string.Empty;
            return;
        }

        target[key] = ParseValue(valueText);
    }

    private List<object?> ParseSequence(List<YamlLine> lines, ref int index, int indent)
    {
        List<object?> list = new();

        while (index < lines.Count && lines[index].Indent == indent && lines[index].Content.StartsWith("- "))
        {
            string itemText = lines[index].Content.Substring(2).Trim();
            index++;

            if (string.IsNullOrEmpty(itemText))
            {
                if (index < lines.Count && lines[index].Indent > indent)
                {
                    list.Add(ParseBlock(lines, ref index, lines[index].Indent));
                }
                else
                {
                    list.Add(string.Empty);
                }

                continue;
            }

            if (itemText.StartsWith("{") || itemText.StartsWith("["))
            {
                list.Add(ParseValue(itemText));
                continue;
            }

            if (LooksLikeKeyValue(itemText))
            {
                Dictionary<string, object?> map = new(StringComparer.Ordinal);
                (string key, string valueText) = SplitKeyValue(itemText);
                if (string.IsNullOrEmpty(valueText))
                {
                    if (index < lines.Count && lines[index].Indent > indent)
                    {
                        map[key] = ParseBlock(lines, ref index, lines[index].Indent);
                    }
                    else
                    {
                        map[key] = string.Empty;
                    }
                }
                else
                {
                    map[key] = ParseValue(valueText);
                }

                if (index < lines.Count && lines[index].Indent > indent)
                {
                    ParseMappingEntries(lines, ref index, lines[index].Indent, map);
                }

                list.Add(map);
                continue;
            }

            list.Add(ParseValue(itemText));
        }

        return list;
    }

    private object? ParseValue(string text)
    {
        string valueText = text.Trim();

        if (valueText.StartsWith("{") && valueText.EndsWith("}"))
        {
            return ParseInlineMap(valueText);
        }

        if (valueText.StartsWith("[") && valueText.EndsWith("]"))
        {
            return ParseInlineList(valueText);
        }

        if (valueText.StartsWith('"') && valueText.EndsWith('"'))
        {
            return valueText[1..^1];
        }

        if (bool.TryParse(valueText, out bool boolValue))
        {
            return boolValue;
        }

        if (int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
        {
            return intValue;
        }

        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            return doubleValue;
        }

        return valueText;
    }

    private Dictionary<string, object?> ParseInlineMap(string text)
    {
        Dictionary<string, object?> map = new(StringComparer.Ordinal);
        string body = text[1..^1].Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return map;
        }

        foreach (string part in SplitTopLevel(body, ','))
        {
            (string key, string valueText) = SplitKeyValue(part);
            map[key] = ParseValue(valueText);
        }

        return map;
    }

    private List<object?> ParseInlineList(string text)
    {
        List<object?> list = new();
        string body = text[1..^1].Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return list;
        }

        foreach (string part in SplitTopLevel(body, ','))
        {
            list.Add(ParseValue(part));
        }

        return list;
    }

    private static (string Key, string Value) SplitKeyValue(string text)
    {
        bool inQuotes = false;
        int bracketDepth = 0;
        int braceDepth = 0;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                if (character == '[')
                {
                    bracketDepth++;
                }
                else if (character == ']')
                {
                    bracketDepth--;
                }
                else if (character == '{')
                {
                    braceDepth++;
                }
                else if (character == '}')
                {
                    braceDepth--;
                }
                else if (character == ':' && bracketDepth == 0 && braceDepth == 0)
                {
                    string key = text[..index].Trim();
                    if (key.StartsWith('"') && key.EndsWith('"'))
                    {
                        key = key[1..^1];
                    }

                    string value = index + 1 < text.Length ? text[(index + 1)..].Trim() : string.Empty;
                    return (key, value);
                }
            }
        }

        return (text.Trim(), string.Empty);
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        List<string> parts = new();
        bool inQuotes = false;
        int bracketDepth = 0;
        int braceDepth = 0;
        int startIndex = 0;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                if (character == '[')
                {
                    bracketDepth++;
                }
                else if (character == ']')
                {
                    bracketDepth--;
                }
                else if (character == '{')
                {
                    braceDepth++;
                }
                else if (character == '}')
                {
                    braceDepth--;
                }
                else if (character == separator && bracketDepth == 0 && braceDepth == 0)
                {
                    parts.Add(text[startIndex..index].Trim());
                    startIndex = index + 1;
                }
            }
        }

        parts.Add(text[startIndex..].Trim());
        return parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
    }

    private static bool LooksLikeKeyValue(string text)
    {
        (string key, string _) = SplitKeyValue(text);
        return !string.IsNullOrWhiteSpace(key) && key != text.Trim();
    }

    private static string ConvertToString(object? value, string defaultValue = "")
    {
        return value switch
        {
            null => defaultValue,
            string text => text,
            bool boolValue => boolValue ? "true" : "false",
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? defaultValue
        };
    }

    private static int ConvertToInt(object? value, int defaultValue = 0)
    {
        return value switch
        {
            int intValue => intValue,
            double doubleValue => (int)Math.Round(doubleValue),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble) => (int)Math.Round(parsedDouble),
            _ => defaultValue
        };
    }

    private static double ConvertToDouble(object? value, double defaultValue = 0.0)
    {
        return value switch
        {
            double doubleValue => doubleValue,
            int intValue => intValue,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => defaultValue
        };
    }

    private static bool ConvertToBool(object? value, bool defaultValue = false)
    {
        return value switch
        {
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out bool parsed) => parsed,
            _ => defaultValue
        };
    }

    private sealed record YamlLine(int Indent, string Content);
}
