using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.Json;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.SaveLoad;

/// <summary>
/// 存读档管理器。
/// 这一版已经接入了真正的 JSON 存读档，
/// 并且会把存档写到系统默认的本地应用数据目录里，
/// 这样调试版和打包版都可以共用同一份存档。
/// </summary>
public class SaveManager
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly MigrationRunner _migrationRunner = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public string SavePath { get; set; } = RuntimePathHelper.SaveFilePath;

    public bool Save(SaveData saveData)
    {
        return SaveToPath(saveData, SavePath);
    }

    public bool SaveToPath(SaveData saveData, string path)
    {
        try
        {
            saveData.Metadata.SaveVersion = SemanticVersion.Current.ToString();
            saveData.Metadata.SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            RuntimePathHelper.EnsureParentDirectoryExists(path);

            SaveFileDocument document = CreateDocument(saveData);
            string json = JsonSerializer.Serialize(document, _jsonOptions);
            File.WriteAllText(path, json, Utf8NoBom);

            SavePath = path;
            GD.Print($"[SaveManager] 存档保存成功：{path}");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveManager] 存档保存失败：{exception}");
            return false;
        }
    }

    public SaveData LoadOrCreateDefault()
    {
        return LoadOrCreateDefault(SavePath);
    }

    public SaveData LoadOrCreateDefault(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                GD.Print($"[SaveManager] 未找到存档，已载入默认新档：{path}");
                return _migrationRunner.RunMigrations(new SaveData());
            }

            string json = File.ReadAllText(path, Utf8NoBom);
            SaveFileDocument? document = JsonSerializer.Deserialize<SaveFileDocument>(json, _jsonOptions);
            if (document == null)
            {
                GD.PushWarning("[SaveManager] 存档内容为空，已回退到默认新档。");
                return _migrationRunner.RunMigrations(new SaveData());
            }

            SaveData saveData = RestoreSaveData(document);
            SavePath = path;
            GD.Print($"[SaveManager] 存档读取成功：{path}");
            return _migrationRunner.RunMigrations(saveData);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveManager] 存档读取失败：{exception}");
            return _migrationRunner.RunMigrations(new SaveData());
        }
    }

    public SaveData CreateFromProfile(PlayerProfile profile)
    {
        return new SaveData
        {
            Metadata = new SaveMetadata(),
            Profile = profile
        };
    }

    public bool TryLoad(string path, out SaveData? saveData)
    {
        saveData = null;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path, Utf8NoBom);
            SaveFileDocument? document = JsonSerializer.Deserialize<SaveFileDocument>(json, _jsonOptions);
            if (document == null)
            {
                return false;
            }

            saveData = _migrationRunner.RunMigrations(RestoreSaveData(document));
            SavePath = path;
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveManager] 读取指定存档失败：{exception}");
            return false;
        }
    }

    public SaveMetadata? TryReadMetadata(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Utf8NoBom));
            if (!document.RootElement.TryGetProperty("Metadata", out JsonElement metadataElement))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SaveMetadata>(metadataElement.GetRawText(), _jsonOptions);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[SaveManager] 读取存档元信息失败：{exception.Message}");
            return null;
        }
    }

    private SaveFileDocument CreateDocument(SaveData saveData)
    {
        PlayerProfile profile = saveData.Profile;

        return new SaveFileDocument
        {
            Metadata = saveData.Metadata,
            Profile = new PlayerProfileSnapshot
            {
                InventoryItems = CollectInventorySnapshots(profile),
                Economy = new EconomySnapshot
                {
                    Gold = profile.Economy.Gold,
                    ExtraCurrencies = profile.Economy.ExtraCurrencies.ToDictionary(entry => entry.Key, entry => entry.Value)
                },
                IdleState = new IdleStateSnapshot
                {
                    ActiveEventId = profile.IdleState.ActiveEventId,
                    IsRunning = profile.IdleState.IsRunning,
                    IsWaitingForGatheringRecovery = profile.IdleState.IsWaitingForGatheringRecovery,
                    AccumulatedProgressSeconds = profile.IdleState.AccumulatedProgressSeconds,
                    PendingOutputFraction = profile.IdleState.PendingOutputFraction,
                    LastProgressUnixSeconds = profile.IdleState.LastProgressUnixSeconds,
                    OfflineSettlementCapSeconds = profile.IdleState.OfflineSettlementCapSeconds,
                    GatheringNodeStates = profile.IdleState.GatheringNodeStates.Values
                        .Where(state => !string.IsNullOrWhiteSpace(state.EventId))
                        .OrderBy(state => state.EventId, StringComparer.Ordinal)
                        .Select(state => new GatheringNodeSnapshot
                        {
                            EventId = state.EventId,
                            AvailableAmount = state.AvailableAmount,
                            LastRecoverUnixSeconds = state.LastRecoverUnixSeconds
                        })
                        .ToList()
                },
                UiState = new UiStateSnapshot
                {
                    SelectedAreaId = profile.UiState.SelectedAreaId,
                    SelectedTabId = profile.UiState.SelectedTabId,
                    IsSkillSidebarMode = profile.UiState.IsSkillSidebarMode,
                    SelectedSkillId = profile.UiState.SelectedSkillId,
                    UiThemeMode = profile.UiState.UiThemeMode,
                    InventorySortMode = profile.UiState.InventorySortMode.ToString(),
                    InventoryTagFilter = profile.UiState.InventoryTagFilter.ToString(),
                    InventoryFilterTab = profile.UiState.InventoryFilterTab.ToString(),
                    InventoryUseLargeIcons = profile.UiState.InventoryUseLargeIcons,
                    FavoriteSceneIds = profile.UiState.FavoriteSceneIds.ToList(),
                    ProcessedInteractableEventIds = profile.UiState.GetSortedProcessedInteractableEventIds().ToList(),
                    AreaIdsWithNewMarker = profile.UiState.GetSortedAreaIdsWithNewMarker().ToList(),
                    SelectedBattleAreaId = profile.UiState.SelectedBattleAreaId,
                    SelectedBattleSceneId = profile.UiState.SelectedBattleSceneId,
                    SelectedBattleEventId = profile.UiState.SelectedBattleEventId,
                    SelectedBattleEncounterId = profile.UiState.SelectedBattleEncounterId,
                    BattleSelectionFromSceneEntry = profile.UiState.BattleSelectionFromSceneEntry,
                    SelectedStapleItemId = profile.UiState.SelectedStapleItemId,
                    SelectedSnackItemIds = profile.UiState.SelectedSnackItemIds.ToList()
                },
                EquipmentState = profile.EquipmentState.EquippedItemIds
                    .OrderBy(entry => entry.Key.ToPersistedId(), StringComparer.Ordinal)
                    .Select(entry => new EquipmentSlotSnapshot
                    {
                        SlotId = entry.Key.ToPersistedId(),
                        ItemId = entry.Value
                    })
                    .ToList(),
                FarmingState = new FarmingStateSnapshot
                {
                    AutoPlantAndHarvestEnabled = profile.FarmingState.AutoPlantAndHarvestEnabled,
                    AutoFertilizeEnabled = profile.FarmingState.AutoFertilizeEnabled,
                    PlotStates = profile.FarmingState.PlotStates.Values
                        .OrderBy(state => state.PlotIndex)
                        .Select(state => new FarmPlotSnapshot
                        {
                            PlotIndex = state.PlotIndex,
                            IsUnlocked = state.IsUnlocked,
                            SelectedSeedItemId = state.SelectedSeedItemId,
                            GrowingSeedItemId = state.GrowingSeedItemId,
                            PlantStartUnixSeconds = state.PlantStartUnixSeconds,
                            GrowthDurationSeconds = state.GrowthDurationSeconds,
                            IsFertilized = state.IsFertilized
                        })
                        .ToList()
                },
                StapleFoodState = new StapleFoodStateSnapshot
                {
                    ActiveItemId = profile.StapleFoodState.ActiveItemId,
                    ActiveStapleId = profile.StapleFoodState.ActiveStapleId,
                    ExpireAtUnixSeconds = profile.StapleFoodState.ExpireAtUnixSeconds,
                    AutoConsumeSelectedStaple = profile.StapleFoodState.AutoConsumeSelectedStaple
                },
                SkillStates = profile.SkillStates.Values
                    .OrderBy(state => state.SkillId)
                    .Select(state => new SkillStateSnapshot
                    {
                        SkillId = state.SkillId,
                        Level = state.Level,
                        StoredExp = state.StoredExp,
                        TotalEarnedExp = state.TotalEarnedExp,
                        CanLevelUp = state.CanLevelUp
                    })
                    .ToList(),
                FactionStates = profile.FactionStates.Values
                    .OrderBy(state => state.FactionId)
                    .Select(state => new FactionStateSnapshot
                    {
                        FactionId = state.FactionId,
                        Reputation = state.Reputation,
                        HasPeaceAgreement = state.HasPeaceAgreement
                    })
                    .ToList(),
                ZoneStates = profile.ZoneStates.Values
                    .OrderBy(state => state.ZoneId)
                    .Select(state => new ZoneStateSnapshot
                    {
                        ZoneId = state.ZoneId,
                        ClearCount = state.ClearCount,
                        ExplorationPercent = state.ExplorationPercent,
                        IsUnlocked = state.IsUnlocked
                    })
                    .ToList(),
                ShopStates = profile.ShopStates.Values
                    .OrderBy(state => state.NpcId)
                    .Select(state => new ShopStateSnapshot
                    {
                        NpcId = state.NpcId,
                        RemainingStockByItemId = state.RemainingStockByItemId.ToDictionary(entry => entry.Key, entry => entry.Value)
                    })
                    .ToList(),
                QuestStates = profile.QuestStates.Values
                    .OrderBy(state => state.QuestId)
                    .Select(state => new QuestStateSnapshot
                    {
                        QuestId = state.QuestId,
                        IsUnlocked = state.IsUnlocked,
                        IsCompleted = state.IsCompleted,
                        IsRewardClaimed = state.IsRewardClaimed
                    })
                    .ToList(),
                BattleStats = profile.BattleStats.ToDictionary(entry => entry.Key, entry => entry.Value),
                ClearedBattleEncounterIds = profile.ClearedBattleEncounterIds.OrderBy(id => id).ToList(),
                UnlockedAchievementIds = profile.UnlockedAchievementIds.OrderBy(id => id).ToList(),
                CompletedEventIds = profile.CompletedEventIds.OrderBy(id => id).ToList(),
                CompletedQuestIds = profile.CompletedQuestIds.OrderBy(id => id).ToList()
            }
        };
    }

    private static SaveData RestoreSaveData(SaveFileDocument document)
    {
        PlayerProfile profile = new();

        foreach (InventoryItemSnapshot item in document.Profile.InventoryItems)
        {
            if (item.Quantity > 0)
            {
                ItemStack stack = profile.Inventory.GetOrCreateStack(item.ItemId);
                stack.Add(item.Quantity);
                stack.SetDurability(item.CurrentDurability);
                stack.CurrentRarity = item.CurrentRarity;
            }

            PlayerItemState state = profile.Inventory.GetOrCreateItemState(item.ItemId);
            state.IsAcquired = item.IsAcquired;
            state.IsFavorite = item.IsFavorite;
            state.IsJunkMarked = item.IsJunkMarked;
            state.AcquiredSequence = item.AcquiredSequence;
            state.LatestAcquiredUnixSeconds = item.LatestAcquiredUnixSeconds;
            state.PlayerDisplayOrder = item.PlayerDisplayOrder;
        }

        profile.Economy.AddGold(document.Profile.Economy.Gold);
        foreach ((string currencyId, int amount) in document.Profile.Economy.ExtraCurrencies)
        {
            profile.Economy.ExtraCurrencies[currencyId] = amount;
        }

        profile.IdleState.ActiveEventId = document.Profile.IdleState.ActiveEventId;
        profile.IdleState.IsRunning = document.Profile.IdleState.IsRunning;
        profile.IdleState.IsWaitingForGatheringRecovery = document.Profile.IdleState.IsWaitingForGatheringRecovery;
        profile.IdleState.AccumulatedProgressSeconds = document.Profile.IdleState.AccumulatedProgressSeconds;
        profile.IdleState.PendingOutputFraction = document.Profile.IdleState.PendingOutputFraction;
        profile.IdleState.LastProgressUnixSeconds = document.Profile.IdleState.LastProgressUnixSeconds;
        profile.IdleState.OfflineSettlementCapSeconds = document.Profile.IdleState.OfflineSettlementCapSeconds;
        foreach (GatheringNodeSnapshot nodeState in document.Profile.IdleState.GatheringNodeStates)
        {
            if (string.IsNullOrWhiteSpace(nodeState.EventId))
            {
                continue;
            }

            profile.IdleState.GatheringNodeStates[nodeState.EventId] = new GatheringNodeState
            {
                EventId = nodeState.EventId,
                AvailableAmount = Math.Max(0, nodeState.AvailableAmount),
                LastRecoverUnixSeconds = nodeState.LastRecoverUnixSeconds
            };
        }

        profile.UiState.SelectedAreaId = document.Profile.UiState.SelectedAreaId;
        profile.UiState.SelectedTabId = string.IsNullOrWhiteSpace(document.Profile.UiState.SelectedTabId)
            ? "current_region"
            : document.Profile.UiState.SelectedTabId;
        profile.UiState.IsSkillSidebarMode = document.Profile.UiState.IsSkillSidebarMode;
        profile.UiState.SelectedSkillId = document.Profile.UiState.SelectedSkillId ?? string.Empty;
        profile.UiState.UiThemeMode = string.IsNullOrWhiteSpace(document.Profile.UiState.UiThemeMode)
            ? "stitch"
            : document.Profile.UiState.UiThemeMode;
        profile.UiState.InventorySortMode = Enum.TryParse(
            document.Profile.UiState.InventorySortMode,
            true,
            out InventorySortMode parsedSortMode)
            ? parsedSortMode
            : InventorySortMode.ArrivalOrder;
        profile.UiState.InventoryTagFilter = Enum.TryParse(
            document.Profile.UiState.InventoryTagFilter,
            true,
            out ItemTag parsedTag)
            ? parsedTag
            : ItemTag.None;
        profile.UiState.InventoryFilterTab = Enum.TryParse(
            document.Profile.UiState.InventoryFilterTab,
            true,
            out InventoryFilterTab parsedFilterTab)
            ? parsedFilterTab
            : InventoryFilterTab.All;
        profile.UiState.InventoryUseLargeIcons = document.Profile.UiState.InventoryUseLargeIcons;
        profile.UiState.SelectedBattleAreaId = document.Profile.UiState.SelectedBattleAreaId ?? string.Empty;
        profile.UiState.SelectedBattleSceneId = document.Profile.UiState.SelectedBattleSceneId ?? string.Empty;
        profile.UiState.SelectedBattleEventId = document.Profile.UiState.SelectedBattleEventId ?? string.Empty;
        profile.UiState.SelectedBattleEncounterId = document.Profile.UiState.SelectedBattleEncounterId ?? string.Empty;
        profile.UiState.BattleSelectionFromSceneEntry = document.Profile.UiState.BattleSelectionFromSceneEntry;
        profile.UiState.SelectedStapleItemId = document.Profile.UiState.SelectedStapleItemId ?? string.Empty;
        foreach (string sceneId in document.Profile.UiState.FavoriteSceneIds)
        {
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                profile.UiState.FavoriteSceneIds.Add(sceneId);
            }
        }

        foreach (string eventId in document.Profile.UiState.ProcessedInteractableEventIds)
        {
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                profile.UiState.ProcessedInteractableEventIds.Add(eventId);
            }
        }

        foreach (string areaId in document.Profile.UiState.AreaIdsWithNewMarker)
        {
            if (!string.IsNullOrWhiteSpace(areaId))
            {
                profile.UiState.AreaIdsWithNewMarker.Add(areaId);
            }
        }

        profile.UiState.SetSelectedSnackItems(document.Profile.UiState.SelectedSnackItemIds);

        foreach (EquipmentSlotSnapshot equippedSlot in document.Profile.EquipmentState)
        {
            if (string.IsNullOrWhiteSpace(equippedSlot.ItemId)
                || !EquipmentSlotCatalog.TryParse(equippedSlot.SlotId, out EquipmentSlotId slotId))
            {
                continue;
            }

            profile.EquipmentState.SetEquippedItem(slotId, equippedSlot.ItemId);
        }

        profile.FarmingState.AutoPlantAndHarvestEnabled = document.Profile.FarmingState.AutoPlantAndHarvestEnabled;
        profile.FarmingState.AutoFertilizeEnabled = document.Profile.FarmingState.AutoFertilizeEnabled;
        foreach (FarmPlotSnapshot plot in document.Profile.FarmingState.PlotStates)
        {
            PlayerFarmPlotState plotState = profile.FarmingState.GetOrCreatePlotState(plot.PlotIndex);
            plotState.IsUnlocked = plot.IsUnlocked;
            plotState.SelectedSeedItemId = plot.SelectedSeedItemId ?? string.Empty;
            plotState.GrowingSeedItemId = plot.GrowingSeedItemId ?? string.Empty;
            plotState.PlantStartUnixSeconds = plot.PlantStartUnixSeconds;
            plotState.GrowthDurationSeconds = plot.GrowthDurationSeconds;
            plotState.IsFertilized = plot.IsFertilized;
            if (!plotState.IsUnlocked
                && (!string.IsNullOrWhiteSpace(plotState.SelectedSeedItemId)
                    || !string.IsNullOrWhiteSpace(plotState.GrowingSeedItemId)
                    || plotState.IsFertilized))
            {
                plotState.IsUnlocked = true;
            }
        }

        profile.StapleFoodState.ActiveItemId = document.Profile.StapleFoodState.ActiveItemId ?? string.Empty;
        profile.StapleFoodState.ActiveStapleId = document.Profile.StapleFoodState.ActiveStapleId ?? string.Empty;
        profile.StapleFoodState.ExpireAtUnixSeconds = Math.Max(0, document.Profile.StapleFoodState.ExpireAtUnixSeconds);
        profile.StapleFoodState.AutoConsumeSelectedStaple = document.Profile.StapleFoodState.AutoConsumeSelectedStaple;

        foreach (SkillStateSnapshot skill in document.Profile.SkillStates)
        {
            PlayerSkillState state = profile.GetOrCreateSkillState(skill.SkillId);
            state.Level = skill.Level;
            state.StoredExp = skill.StoredExp;
            state.TotalEarnedExp = skill.TotalEarnedExp;
            state.CanLevelUp = skill.CanLevelUp;
        }

        foreach (FactionStateSnapshot faction in document.Profile.FactionStates)
        {
            PlayerFactionState state = profile.GetOrCreateFactionState(faction.FactionId);
            state.Reputation = faction.Reputation;
            state.HasPeaceAgreement = faction.HasPeaceAgreement;
        }

        foreach (ZoneStateSnapshot zone in document.Profile.ZoneStates)
        {
            PlayerZoneState state = profile.GetOrCreateZoneState(zone.ZoneId);
            state.ClearCount = zone.ClearCount;
            state.ExplorationPercent = zone.ExplorationPercent;
            state.IsUnlocked = zone.IsUnlocked;
        }

        foreach (ShopStateSnapshot shop in document.Profile.ShopStates)
        {
            PlayerShopState state = profile.GetOrCreateShopState(shop.NpcId);
            foreach ((string itemId, int remainingStock) in shop.RemainingStockByItemId)
            {
                state.SetRemainingStock(itemId, remainingStock);
            }
        }

        foreach (QuestStateSnapshot quest in document.Profile.QuestStates)
        {
            PlayerQuestState state = profile.GetOrCreateQuestState(quest.QuestId);
            state.IsUnlocked = quest.IsUnlocked;
            state.IsCompleted = quest.IsCompleted;
            state.IsRewardClaimed = quest.IsRewardClaimed;
        }

        foreach ((string statId, double value) in document.Profile.BattleStats)
        {
            profile.BattleStats[statId] = value;
        }

        foreach (string encounterId in document.Profile.ClearedBattleEncounterIds)
        {
            if (!string.IsNullOrWhiteSpace(encounterId))
            {
                profile.ClearedBattleEncounterIds.Add(encounterId);
            }
        }

        foreach (string achievementId in document.Profile.UnlockedAchievementIds)
        {
            profile.UnlockedAchievementIds.Add(achievementId);
        }

        foreach (string eventId in document.Profile.CompletedEventIds)
        {
            profile.CompletedEventIds.Add(eventId);
        }

        foreach (string questId in document.Profile.CompletedQuestIds)
        {
            profile.CompletedQuestIds.Add(questId);
        }

        return new SaveData
        {
            Metadata = document.Metadata ?? new SaveMetadata(),
            Profile = profile
        };
    }

    /// <summary>
    /// 这里把“背包堆叠数据”和“物品显示状态”合并成一个快照列表，
    /// 这样序列化更稳定，也更容易给新手排查问题。
    /// </summary>
    private static List<InventoryItemSnapshot> CollectInventorySnapshots(PlayerProfile profile)
    {
        HashSet<string> itemIds = new(profile.Inventory.ItemStates.Keys);
        itemIds.UnionWith(profile.Inventory.Stacks.Keys);

        return itemIds
            .OrderBy(itemId => itemId)
            .Select(itemId =>
            {
                profile.Inventory.Stacks.TryGetValue(itemId, out ItemStack? stack);
                profile.Inventory.ItemStates.TryGetValue(itemId, out PlayerItemState? state);

                return new InventoryItemSnapshot
                {
                    ItemId = itemId,
                    Quantity = stack?.Quantity ?? 0,
                    CurrentDurability = stack?.CurrentDurability ?? 0,
                    CurrentRarity = stack?.CurrentRarity ?? Rarity.Common,
                    IsAcquired = state?.IsAcquired ?? false,
                    IsFavorite = state?.IsFavorite ?? false,
                    IsJunkMarked = state?.IsJunkMarked ?? false,
                    AcquiredSequence = state?.AcquiredSequence,
                    LatestAcquiredUnixSeconds = state?.LatestAcquiredUnixSeconds,
                    PlayerDisplayOrder = state?.PlayerDisplayOrder
                };
            })
            .ToList();
    }

    private sealed class SaveFileDocument
    {
        public SaveMetadata Metadata { get; set; } = new();

        public PlayerProfileSnapshot Profile { get; set; } = new();
    }

    private sealed class PlayerProfileSnapshot
    {
        public List<InventoryItemSnapshot> InventoryItems { get; set; } = new();

        public EconomySnapshot Economy { get; set; } = new();

        public IdleStateSnapshot IdleState { get; set; } = new();

        public UiStateSnapshot UiState { get; set; } = new();

        public List<EquipmentSlotSnapshot> EquipmentState { get; set; } = new();

        public FarmingStateSnapshot FarmingState { get; set; } = new();

        public StapleFoodStateSnapshot StapleFoodState { get; set; } = new();

        public List<SkillStateSnapshot> SkillStates { get; set; } = new();

        public List<FactionStateSnapshot> FactionStates { get; set; } = new();

        public List<ZoneStateSnapshot> ZoneStates { get; set; } = new();

        public List<ShopStateSnapshot> ShopStates { get; set; } = new();

        public List<QuestStateSnapshot> QuestStates { get; set; } = new();

        public Dictionary<string, double> BattleStats { get; set; } = new();

        public List<string> ClearedBattleEncounterIds { get; set; } = new();

        public List<string> UnlockedAchievementIds { get; set; } = new();

        public List<string> CompletedEventIds { get; set; } = new();

        public List<string> CompletedQuestIds { get; set; } = new();
    }

    private sealed class InventoryItemSnapshot
    {
        public string ItemId { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public int CurrentDurability { get; set; }

        public Rarity CurrentRarity { get; set; } = Rarity.Common;

        public bool IsAcquired { get; set; }

        public bool IsFavorite { get; set; }

        public bool IsJunkMarked { get; set; }

        public int? AcquiredSequence { get; set; }

        public long? LatestAcquiredUnixSeconds { get; set; }

        public int? PlayerDisplayOrder { get; set; }
    }

    private sealed class EconomySnapshot
    {
        public int Gold { get; set; }

        public Dictionary<string, int> ExtraCurrencies { get; set; } = new();
    }

    private sealed class IdleStateSnapshot
    {
        public string ActiveEventId { get; set; } = string.Empty;

        public bool IsRunning { get; set; }

        public bool IsWaitingForGatheringRecovery { get; set; }

        public double AccumulatedProgressSeconds { get; set; }

        public double PendingOutputFraction { get; set; }

        public long LastProgressUnixSeconds { get; set; }

        public int OfflineSettlementCapSeconds { get; set; } = 28800;

        public List<GatheringNodeSnapshot> GatheringNodeStates { get; set; } = new();
    }

    private sealed class GatheringNodeSnapshot
    {
        public string EventId { get; set; } = string.Empty;

        public int AvailableAmount { get; set; }

        public double LastRecoverUnixSeconds { get; set; }
    }

    private sealed class UiStateSnapshot
    {
        public string SelectedAreaId { get; set; } = string.Empty;

        public string SelectedTabId { get; set; } = "current_region";

        public bool IsSkillSidebarMode { get; set; }

        public string SelectedSkillId { get; set; } = string.Empty;

        public string UiThemeMode { get; set; } = "stitch";

        public string InventorySortMode { get; set; } = "ArrivalOrder";

        public string InventoryTagFilter { get; set; } = "None";

        public string InventoryFilterTab { get; set; } = "All";

        public bool InventoryUseLargeIcons { get; set; } = true;

        public List<string> FavoriteSceneIds { get; set; } = new();

        public List<string> ProcessedInteractableEventIds { get; set; } = new();

        public List<string> AreaIdsWithNewMarker { get; set; } = new();

        public string SelectedBattleAreaId { get; set; } = string.Empty;

        public string SelectedBattleSceneId { get; set; } = string.Empty;

        public string SelectedBattleEventId { get; set; } = string.Empty;

        public string SelectedBattleEncounterId { get; set; } = string.Empty;

        public bool BattleSelectionFromSceneEntry { get; set; }

        public string SelectedStapleItemId { get; set; } = string.Empty;

        public List<string> SelectedSnackItemIds { get; set; } = new();
    }

    private sealed class EquipmentSlotSnapshot
    {
        public string SlotId { get; set; } = string.Empty;

        public string ItemId { get; set; } = string.Empty;
    }

    private sealed class FarmingStateSnapshot
    {
        public bool AutoPlantAndHarvestEnabled { get; set; }

        public bool AutoFertilizeEnabled { get; set; }

        public List<FarmPlotSnapshot> PlotStates { get; set; } = new();
    }

    private sealed class FarmPlotSnapshot
    {
        public int PlotIndex { get; set; }

        public bool IsUnlocked { get; set; }

        public string SelectedSeedItemId { get; set; } = string.Empty;

        public string GrowingSeedItemId { get; set; } = string.Empty;

        public long PlantStartUnixSeconds { get; set; }

        public double GrowthDurationSeconds { get; set; }

        public bool IsFertilized { get; set; }
    }

    private sealed class StapleFoodStateSnapshot
    {
        public string ActiveItemId { get; set; } = string.Empty;

        public string ActiveStapleId { get; set; } = string.Empty;

        public long ExpireAtUnixSeconds { get; set; }

        public bool AutoConsumeSelectedStaple { get; set; }
    }

    private sealed class SkillStateSnapshot
    {
        public string SkillId { get; set; } = string.Empty;

        public int Level { get; set; } = 1;

        public double StoredExp { get; set; }

        public double TotalEarnedExp { get; set; }

        public bool CanLevelUp { get; set; }
    }

    private sealed class FactionStateSnapshot
    {
        public string FactionId { get; set; } = string.Empty;

        public int Reputation { get; set; }

        public bool HasPeaceAgreement { get; set; }
    }

    private sealed class ZoneStateSnapshot
    {
        public string ZoneId { get; set; } = string.Empty;

        public int ClearCount { get; set; }

        public double ExplorationPercent { get; set; }

        public bool IsUnlocked { get; set; }
    }

    private sealed class ShopStateSnapshot
    {
        public string NpcId { get; set; } = string.Empty;

        public Dictionary<string, int> RemainingStockByItemId { get; set; } = new();
    }

    private sealed class QuestStateSnapshot
    {
        public string QuestId { get; set; } = string.Empty;

        public bool IsUnlocked { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsRewardClaimed { get; set; }
    }
}
