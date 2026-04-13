using Godot;
using System;
using System.Collections.Generic;
using System.IO;
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
            File.WriteAllText(path, json);

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

            string json = File.ReadAllText(path);
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
            string json = File.ReadAllText(path);
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
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
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
                    AccumulatedProgressSeconds = profile.IdleState.AccumulatedProgressSeconds,
                    PendingOutputFraction = profile.IdleState.PendingOutputFraction,
                    LastProgressUnixSeconds = profile.IdleState.LastProgressUnixSeconds,
                    OfflineSettlementCapSeconds = profile.IdleState.OfflineSettlementCapSeconds
                },
                UiState = new UiStateSnapshot
                {
                    SelectedAreaId = profile.UiState.SelectedAreaId,
                    SelectedTabId = profile.UiState.SelectedTabId,
                    FavoriteSceneIds = profile.UiState.FavoriteSceneIds.ToList()
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
            state.PlayerDisplayOrder = item.PlayerDisplayOrder;
        }

        profile.Economy.AddGold(document.Profile.Economy.Gold);
        foreach ((string currencyId, int amount) in document.Profile.Economy.ExtraCurrencies)
        {
            profile.Economy.ExtraCurrencies[currencyId] = amount;
        }

        profile.IdleState.ActiveEventId = document.Profile.IdleState.ActiveEventId;
        profile.IdleState.IsRunning = document.Profile.IdleState.IsRunning;
        profile.IdleState.AccumulatedProgressSeconds = document.Profile.IdleState.AccumulatedProgressSeconds;
        profile.IdleState.PendingOutputFraction = document.Profile.IdleState.PendingOutputFraction;
        profile.IdleState.LastProgressUnixSeconds = document.Profile.IdleState.LastProgressUnixSeconds;
        profile.IdleState.OfflineSettlementCapSeconds = document.Profile.IdleState.OfflineSettlementCapSeconds;

        profile.UiState.SelectedAreaId = document.Profile.UiState.SelectedAreaId;
        profile.UiState.SelectedTabId = string.IsNullOrWhiteSpace(document.Profile.UiState.SelectedTabId)
            ? "current_region"
            : document.Profile.UiState.SelectedTabId;
        foreach (string sceneId in document.Profile.UiState.FavoriteSceneIds)
        {
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                profile.UiState.FavoriteSceneIds.Add(sceneId);
            }
        }

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

        public List<SkillStateSnapshot> SkillStates { get; set; } = new();

        public List<FactionStateSnapshot> FactionStates { get; set; } = new();

        public List<ZoneStateSnapshot> ZoneStates { get; set; } = new();

        public List<ShopStateSnapshot> ShopStates { get; set; } = new();

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

        public double AccumulatedProgressSeconds { get; set; }

        public double PendingOutputFraction { get; set; }

        public long LastProgressUnixSeconds { get; set; }

        public int OfflineSettlementCapSeconds { get; set; } = 28800;
    }

    private sealed class UiStateSnapshot
    {
        public string SelectedAreaId { get; set; } = string.Empty;

        public string SelectedTabId { get; set; } = "current_region";

        public List<string> FavoriteSceneIds { get; set; } = new();
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
}
