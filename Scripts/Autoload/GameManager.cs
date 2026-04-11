using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;
using Test00_0410.Core.SaveLoad;
using Test00_0410.Systems;

namespace Test00_0410.Autoload;

/// <summary>
/// 游戏总管理器。
/// 你可以把它理解成项目启动时的总指挥：创建注册表、系统、玩家档案，并协调初始化顺序。
/// </summary>
public partial class GameManager : Node
{
    private const string CategoriesConfigPath = "res://Configs/Items/categories.yaml";
    private const string ItemsConfigPath = "res://Configs/Items/items.yaml";
    private const string SkillsConfigPath = "res://Configs/Skills/skills.yaml";
    private const string OneshotEventsConfigPath = "res://Configs/Events/oneshot_events.yaml";
    private const string ClickEventsConfigPath = "res://Configs/Events/click_events.yaml";
    private const string IdleEventsConfigPath = "res://Configs/Events/idle_events.yaml";
    private const string FactionsConfigPath = "res://Configs/Factions/factions.yaml";
    private const string ZonesConfigPath = "res://Configs/Zones/zones.yaml";
    private const string LocalizationZhPath = "res://Configs/Localization/locale_zh.yaml";

    private readonly YamlConfigLoader _configLoader = new();
    private readonly List<string> _runtimeLogs = new();

    public static GameManager? Instance { get; private set; }

    public ItemRegistry ItemRegistry { get; private set; } = new();

    public SkillRegistry SkillRegistry { get; private set; } = new();

    public EventRegistry EventRegistry { get; private set; } = new();

    public FactionRegistry FactionRegistry { get; private set; } = new();

    public ZoneRegistry ZoneRegistry { get; private set; } = new();

    public LocalizationManager LocalizationManager { get; private set; } = new();

    public SaveManager SaveManager { get; private set; } = new();

    public PlayerProfile PlayerProfile { get; private set; } = new();

    public IdleSystem? IdleSystem { get; private set; }

    public ClickEventSystem? ClickEventSystem { get; private set; }

    public SkillSystem? SkillSystem { get; private set; }

    public ShopSystem? ShopSystem { get; private set; }

    public SellSystem? SellSystem { get; private set; }

    public FactionSystem? FactionSystem { get; private set; }

    public ZoneSystem? ZoneSystem { get; private set; }

    public QuestSystem? QuestSystem { get; private set; }

    public CraftingSystem? CraftingSystem { get; private set; }

    public BattleSystem? BattleSystem { get; private set; }

    public AchievementSystem? AchievementSystem { get; private set; }

    public EquipmentSystem? EquipmentSystem { get; private set; }

    public BuffSystem? BuffSystem { get; private set; }

    /// <summary>
    /// 运行时日志。
    /// UI 会直接读取这里，把最近的操作显示给玩家看。
    /// </summary>
    public IReadOnlyList<string> RuntimeLogs => _runtimeLogs;

    public override void _Ready()
    {
        Instance = this;
        CreateDefaultProfile();
        LoadStaticData();
        InitializeProfileFromDefinitions();
        InitializeSystems();
        AddGameLog("初版游戏已完成启动。");
    }

    private void CreateDefaultProfile()
    {
        PlayerProfile = new PlayerProfile();
    }

    private void InitializeSystems()
    {
        IdleSystem = AddSystemNode<IdleSystem>("IdleSystem");
        ClickEventSystem = AddSystemNode<ClickEventSystem>("ClickEventSystem");
        SkillSystem = AddSystemNode<SkillSystem>("SkillSystem");
        ShopSystem = AddSystemNode<ShopSystem>("ShopSystem");
        SellSystem = AddSystemNode<SellSystem>("SellSystem");
        FactionSystem = AddSystemNode<FactionSystem>("FactionSystem");
        ZoneSystem = AddSystemNode<ZoneSystem>("ZoneSystem");
        QuestSystem = AddSystemNode<QuestSystem>("QuestSystem");
        CraftingSystem = AddSystemNode<CraftingSystem>("CraftingSystem");
        BattleSystem = AddSystemNode<BattleSystem>("BattleSystem");
        AchievementSystem = AddSystemNode<AchievementSystem>("AchievementSystem");
        EquipmentSystem = AddSystemNode<EquipmentSystem>("EquipmentSystem");
        BuffSystem = AddSystemNode<BuffSystem>("BuffSystem");

        ConfigureSystems();
    }

    private void ConfigureSystems()
    {
        IdleSystem!.Configure(PlayerProfile, EventRegistry, SkillRegistry, ItemRegistry);
        ClickEventSystem!.Configure(PlayerProfile, EventRegistry, SkillSystem, FactionSystem, ZoneSystem);
        SkillSystem!.Configure(PlayerProfile, SkillRegistry);
        ShopSystem?.Configure(PlayerProfile, FactionRegistry);
        SellSystem?.Configure(PlayerProfile, ItemRegistry);
        FactionSystem?.Configure(PlayerProfile, FactionRegistry);
        ZoneSystem?.Configure(PlayerProfile, ZoneRegistry);
    }

    private T AddSystemNode<T>(string nodeName) where T : Node, new()
    {
        T node = new() { Name = nodeName };
        AddChild(node);
        return node;
    }

    private void LoadStaticData()
    {
        try
        {
            List<CategoryDefinition> categories = _configLoader.LoadCategories(CategoriesConfigPath);
            List<ItemDefinition> items = _configLoader.LoadItems(ItemsConfigPath);
            List<SkillDefinition> skills = _configLoader.LoadSkills(SkillsConfigPath);

            List<EventDefinition> events = new();
            events.AddRange(_configLoader.LoadEvents(OneshotEventsConfigPath));
            events.AddRange(_configLoader.LoadEvents(ClickEventsConfigPath));
            events.AddRange(_configLoader.LoadEvents(IdleEventsConfigPath));

            YamlConfigLoader.FactionConfigSet factionConfig = _configLoader.LoadFactions(FactionsConfigPath);
            List<ZoneDefinition> zones = _configLoader.LoadZones(ZonesConfigPath);
            YamlConfigLoader.LocalizationConfig localization = _configLoader.LoadLocalization(LocalizationZhPath);

            ItemRegistry.LoadDefinitions(categories, items);
            SkillRegistry.LoadDefinitions(skills);
            EventRegistry.LoadDefinitions(events);
            FactionRegistry.LoadDefinitions(factionConfig.Factions, factionConfig.Npcs);
            ZoneRegistry.LoadDefinitions(zones);

            LocalizationManager.SetLocale(localization.Locale);
            LocalizationManager.LoadTranslations(localization.Translations);

            if (categories.Count == 0 || items.Count == 0 || skills.Count == 0 || events.Count == 0)
            {
                AddGameLog("警告：有核心配置未成功加载，请优先检查 YAML 格式和路径。");
            }

            foreach (string message in ItemRegistry.Validate())
            {
                AddGameLog(message, false);
            }

            ItemRegistry.DumpTreeToDefaultRuntimeFile();
            AddGameLog($"静态配置加载完成：分类 {categories.Count}，物品 {items.Count}，技能 {skills.Count}，事件 {events.Count}。");
        }
        catch (Exception exception)
        {
            GD.PushError($"[GameManager] 配置加载失败：{exception}");
            AddGameLog($"配置加载失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 按静态定义给玩家档案补齐默认运行态。
    /// 这样 UI 一启动就能读到技能等级、势力状态、区域解锁状态等基础数据。
    /// </summary>
    private void InitializeProfileFromDefinitions()
    {
        foreach (SkillDefinition skill in SkillRegistry.Skills.Values)
        {
            PlayerSkillState state = PlayerProfile.GetOrCreateSkillState(skill.Id);
            if (state.Level <= 0)
            {
                state.Level = skill.InitialLevel;
            }
            else
            {
                state.Level = Math.Max(state.Level, skill.InitialLevel);
            }
        }

        foreach (FactionDefinition faction in FactionRegistry.Factions.Values)
        {
            PlayerProfile.GetOrCreateFactionState(faction.Id);
        }

        foreach (ZoneDefinition zone in ZoneRegistry.Zones.Values)
        {
            PlayerZoneState state = PlayerProfile.GetOrCreateZoneState(zone.Id);
            if (zone.UnlockConditions.Count == 0)
            {
                state.IsUnlocked = true;
            }
        }
    }

    public string TranslateText(string key)
    {
        return LocalizationManager.Translate(key);
    }

    public void AddGameLog(string message, bool printToConsole = true)
    {
        string finalMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _runtimeLogs.Add(finalMessage);

        while (_runtimeLogs.Count > 120)
        {
            _runtimeLogs.RemoveAt(0);
        }

        if (printToConsole)
        {
            GD.Print($"[GameLog] {finalMessage}");
        }
    }

    /// <summary>
    /// 找出玩家当前拥有的、满足某个标签要求的最佳工具。
    /// 当前首版先按“原木倍率 + 读条速度倍率 + 稀有度 + 定义顺序”综合排序。
    /// </summary>
    public ItemDefinition? GetBestOwnedTool(ItemTag requiredTag)
    {
        return ItemRegistry.Items.Values
            .Where(item => item.HasTag(requiredTag) && PlayerProfile.Inventory.HasItem(item.Id))
            .OrderByDescending(item => item.ToolBonuses.LogYieldMultiplier)
            .ThenByDescending(item => item.ToolBonuses.ChopSpeedMultiplier)
            .ThenByDescending(item => item.BaseRarity)
            .ThenBy(item => item.DefinitionOrder)
            .FirstOrDefault();
    }

    public string GetItemDisplayName(string itemId)
    {
        ItemDefinition? item = ItemRegistry.GetItem(itemId);
        return item == null ? itemId : item.GetDisplayName(TranslateText);
    }

    public string GetEventDisplayName(string eventId)
    {
        EventDefinition? definition = EventRegistry.GetEvent(eventId);
        return definition == null ? eventId : TranslateText(definition.NameKey);
    }

    public bool SaveGame()
    {
        bool success = SaveManager.Save(SaveManager.CreateFromProfile(PlayerProfile));
        AddGameLog(success
            ? $"已执行手动保存。路径：{SaveManager.SavePath}"
            : "手动保存失败，请检查日志。");
        return success;
    }

    public bool LoadGame()
    {
        try
        {
            SaveData saveData = SaveManager.LoadOrCreateDefault();
            PlayerProfile = saveData.Profile;
            InitializeProfileFromDefinitions();
            ConfigureSystems();
            IdleSystem?.ApplyOfflineProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            AddGameLog($"已执行读档。路径：{SaveManager.SavePath}");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"[GameManager] 读档失败：{exception}");
            AddGameLog("读档失败，请检查日志。");
            return false;
        }
    }
}
