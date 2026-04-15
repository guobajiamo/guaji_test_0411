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
using Test00_0410.Core.Scenario;
using Test00_0410.Systems;

namespace Test00_0410.Autoload;

/// <summary>
/// 游戏总管理器。
/// 这一版除了负责系统初始化，也开始负责“当前加载的是哪个剧本”。
/// </summary>
public partial class GameManager : Node
{
    private const string ScenarioDirectoryPath = "res://Resources/Scenarios";

    private readonly YamlConfigLoader _configLoader = new();
    private readonly List<string> _runtimeLogs = new();
    private readonly Dictionary<string, GameScenarioDefinition> _scenarioDefinitions = new(StringComparer.Ordinal);
    private readonly List<QuestDefinition> _questDefinitions = new();
    private SignalBus? _signalBus;

    public static GameManager? Instance { get; private set; }

    public ItemRegistry ItemRegistry { get; private set; } = new();

    public SkillRegistry SkillRegistry { get; private set; } = new();

    public EventRegistry EventRegistry { get; private set; } = new();

    public FactionRegistry FactionRegistry { get; private set; } = new();

    public ZoneRegistry ZoneRegistry { get; private set; } = new();

    public LocalizationManager LocalizationManager { get; private set; } = new();

    public SaveManager SaveManager { get; private set; } = new();

    public PlayerProfile PlayerProfile { get; private set; } = new();

    public ValueSettlementService SettlementService { get; } = new();

    public GameScenarioDefinition? ActiveScenario { get; private set; }

    public IReadOnlyDictionary<string, GameScenarioDefinition> ScenarioDefinitions => _scenarioDefinitions;

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
        LoadScenarioDefinitions();
        CreateDefaultProfile();
        InitializeSystems();
        AddGameLog("游戏主菜单入口已准备完成。");
    }

    public IReadOnlyList<GameScenarioDefinition> GetVisibleNewGameScenarios()
    {
        return _scenarioDefinitions.Values
            .Where(scenario => scenario.ShowInNewGameMenu)
            .OrderByDescending(scenario => scenario.IsDefaultStoryScenario)
            .ThenBy(scenario => scenario.DisplayName)
            .ToList();
    }

    public GameScenarioDefinition? GetDefaultStoryScenario()
    {
        return _scenarioDefinitions.Values.FirstOrDefault(scenario => scenario.IsDefaultStoryScenario)
            ?? GetVisibleNewGameScenarios().FirstOrDefault();
    }

    public GameScenarioDefinition? GetTestScenario()
    {
        return _scenarioDefinitions.Values.FirstOrDefault(scenario => scenario.IsTestScenario);
    }

    public GameScenarioDefinition? GetScenario(string scenarioId)
    {
        return _scenarioDefinitions.GetValueOrDefault(scenarioId);
    }

    public bool StartDefaultStoryScenario()
    {
        GameScenarioDefinition? scenario = GetDefaultStoryScenario();
        return scenario != null && StartScenario(scenario.ScenarioId, string.Empty, true);
    }

    public bool StartTestScenario()
    {
        GameScenarioDefinition? scenario = GetTestScenario();
        return scenario != null && StartScenario(scenario.ScenarioId, RuntimePathHelper.GetTestSavePath(), true);
    }

    /// <summary>
    /// 开始载入某个剧本。
    /// resetProfile=true 时会丢弃当前进度，重新创建新档。
    /// </summary>
    public bool StartScenario(string scenarioId, string savePath, bool resetProfile)
    {
        if (!_scenarioDefinitions.TryGetValue(scenarioId, out GameScenarioDefinition? scenario))
        {
            AddGameLog($"未找到剧本：{scenarioId}");
            return false;
        }

        ActiveScenario = scenario;
        _runtimeLogs.Clear();

        if (resetProfile)
        {
            CreateDefaultProfile();
        }

        SaveManager.SavePath = string.IsNullOrWhiteSpace(savePath)
            ? (scenario.IsTestScenario ? RuntimePathHelper.GetTestSavePath() : string.Empty)
            : savePath;

        LoadStaticData(scenario);
        InitializeProfileFromDefinitions();
        ConfigureSystems();
        AddGameLog($"已载入剧本：{scenario.DisplayName}");
        return true;
    }

    public IReadOnlyList<SaveSlotSummary> GetStorySaveSlotSummaries()
    {
        List<SaveSlotSummary> summaries = new();
        for (int slotIndex = 1; slotIndex <= 10; slotIndex++)
        {
            string path = RuntimePathHelper.GetStorySaveSlotPath(slotIndex);
            SaveMetadata? metadata = SaveManager.TryReadMetadata(path);
            summaries.Add(new SaveSlotSummary
            {
                SlotIndex = slotIndex,
                FilePath = path,
                FileName = System.IO.Path.GetFileName(path),
                Exists = metadata != null,
                ScenarioId = metadata?.ScenarioId ?? string.Empty,
                ScenarioDisplayName = metadata?.ScenarioDisplayName ?? string.Empty,
                SavedAtUnixSeconds = metadata?.SavedAtUnixSeconds ?? 0
            });
        }

        return summaries;
    }

    /// <summary>
    /// 给 GDScript 和调试工具使用的简化接口。
    /// 直接返回某个故事模式槽位对应的绝对路径，避免跨语言传递复杂泛型对象时受限。
    /// </summary>
    public string GetStorySaveSlotPath(int slotIndex)
    {
        return RuntimePathHelper.GetStorySaveSlotPath(slotIndex);
    }

    public bool SaveGameToPath(string path)
    {
        if (ActiveScenario == null)
        {
            AddGameLog("当前没有已加载的剧本，无法保存。");
            return false;
        }

        QuestSystem?.RefreshQuestState();

        SaveData saveData = SaveManager.CreateFromProfile(PlayerProfile);
        PopulateSaveMetadata(saveData.Metadata);

        bool success = SaveManager.SaveToPath(saveData, path);
        AddGameLog(success
            ? $"已保存到存档位：{path}"
            : $"保存失败：{path}");
        return success;
    }

    public bool LoadGameFromPath(string path)
    {
        SaveMetadata? metadata = SaveManager.TryReadMetadata(path);
        if (metadata == null)
        {
            AddGameLog($"读取存档失败：未找到存档 {path}");
            return false;
        }

        string scenarioId = string.IsNullOrWhiteSpace(metadata.ScenarioId)
            ? (GetDefaultStoryScenario()?.ScenarioId ?? string.Empty)
            : metadata.ScenarioId;

        if (string.IsNullOrWhiteSpace(scenarioId) || !_scenarioDefinitions.ContainsKey(scenarioId))
        {
            AddGameLog($"读取存档失败：存档引用了未知剧本 {metadata.ScenarioId}");
            return false;
        }

        if (!StartScenario(scenarioId, path, true))
        {
            return false;
        }

        if (!SaveManager.TryLoad(path, out SaveData? saveData) || saveData == null)
        {
            AddGameLog($"读取存档失败：{path}");
            return false;
        }

        PlayerProfile = saveData.Profile;
        InitializeProfileFromDefinitions();
        ConfigureSystems();
        IdleSystem?.ApplyOfflineProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        QuestSystem?.RefreshQuestState();
        AddGameLog($"已执行读档。路径：{path}");
        return true;
    }

    private void LoadScenarioDefinitions()
    {
        _scenarioDefinitions.Clear();

        DirAccess? directory = DirAccess.Open(ScenarioDirectoryPath);
        if (directory == null)
        {
            GD.PushWarning($"[GameManager] 未找到剧本目录：{ScenarioDirectoryPath}");
            return;
        }

        directory.ListDirBegin();
        while (true)
        {
            string fileName = directory.GetNext();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                break;
            }

            if (directory.CurrentIsDir())
            {
                continue;
            }

            string? resourcePath = ResolveScenarioResourcePath(fileName);
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                continue;
            }

            GameScenarioDefinition? scenario = ResourceLoader.Load<GameScenarioDefinition>(resourcePath);
            if (scenario == null || string.IsNullOrWhiteSpace(scenario.ScenarioId))
            {
                GD.PushWarning($"[GameManager] 剧本资源加载失败：{resourcePath}");
                continue;
            }

            _scenarioDefinitions[scenario.ScenarioId] = scenario;
        }
        directory.ListDirEnd();

        if (_scenarioDefinitions.Count == 0)
        {
            GD.PushWarning($"[GameManager] 剧本目录扫描完成，但未发现可用剧本资源。目录={ScenarioDirectoryPath}");
        }

        AddGameLog($"已发现剧本数量：{_scenarioDefinitions.Count}");
    }

    private static string? ResolveScenarioResourcePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string normalizedFileName = fileName;
        if (normalizedFileName.EndsWith(".remap", StringComparison.OrdinalIgnoreCase))
        {
            normalizedFileName = normalizedFileName[..^".remap".Length];
        }

        if (!normalizedFileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
            && !normalizedFileName.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{ScenarioDirectoryPath}/{normalizedFileName}";
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
        SkillSystem!.Configure(PlayerProfile, SkillRegistry, ClickEventSystem);
        SettlementService.Configure(PlayerProfile, BuffSystem, SkillSystem);
        IdleSystem!.Configure(PlayerProfile, EventRegistry, SkillRegistry, ItemRegistry, SettlementService);
        ClickEventSystem!.Configure(PlayerProfile, EventRegistry, SettlementService, FactionSystem, ZoneSystem, SkillSystem);
        ShopSystem?.Configure(PlayerProfile, FactionRegistry, SettlementService);
        SellSystem?.Configure(PlayerProfile, ItemRegistry, SettlementService);
        FactionSystem?.Configure(PlayerProfile, FactionRegistry);
        ZoneSystem?.Configure(PlayerProfile, ZoneRegistry);
        BattleSystem?.Configure(PlayerProfile);
        AchievementSystem?.Configure(PlayerProfile);
        QuestSystem?.Configure(this, PlayerProfile, SettlementService, _questDefinitions);
        QuestSystem?.RefreshQuestState();
        ApplyOwnedItemSkillUnlocks(false);
    }

    private T AddSystemNode<T>(string nodeName) where T : Node, new()
    {
        T node = new() { Name = nodeName };
        AddChild(node);
        return node;
    }

    private void LoadStaticData(GameScenarioDefinition scenario)
    {
        try
        {
            ItemRegistry = new ItemRegistry();
            SkillRegistry = new SkillRegistry();
            EventRegistry = new EventRegistry();
            FactionRegistry = new FactionRegistry();
            ZoneRegistry = new ZoneRegistry();
            LocalizationManager = new LocalizationManager();
            _questDefinitions.Clear();

            List<CategoryDefinition> categories = _configLoader.LoadCategories(scenario.CategoriesConfigPath);
            List<ItemDefinition> items = _configLoader.LoadItems(scenario.ItemsConfigPath);
            List<SkillDefinition> skills = _configLoader.LoadSkills(scenario.SkillsConfigPath);
            _questDefinitions.AddRange(_configLoader.LoadQuests(scenario.QuestsConfigPath));

            List<EventDefinition> events = new();
            events.AddRange(_configLoader.LoadEvents(scenario.OneshotEventsConfigPath));
            events.AddRange(_configLoader.LoadEvents(scenario.ClickEventsConfigPath));
            events.AddRange(_configLoader.LoadEvents(scenario.IdleEventsConfigPath));

            YamlConfigLoader.FactionConfigSet factionConfig = _configLoader.LoadFactions(scenario.FactionsConfigPath);
            List<ZoneDefinition> zones = _configLoader.LoadZones(scenario.ZonesConfigPath);
            YamlConfigLoader.LocalizationConfig localization = _configLoader.LoadLocalization(scenario.LocalizationPath);

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
            AddGameLog($"静态配置加载完成：剧本 {scenario.DisplayName}，分类 {categories.Count}，物品 {items.Count}，技能 {skills.Count}，事件 {events.Count}，任务 {_questDefinitions.Count}。");
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

        ApplyOwnedItemSkillUnlocks(false);

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

        ResolveSignalBus()?.EmitSignal(SignalBus.SignalName.LogMessageRequested, finalMessage);
    }

    private SignalBus? ResolveSignalBus()
    {
        if (_signalBus != null && IsInstanceValid(_signalBus))
        {
            return _signalBus;
        }

        _signalBus = GetNodeOrNull<SignalBus>("/root/SignalBus");
        return _signalBus;
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

    public bool EnsureSkillLearned(string skillId, int targetLevel = 1, bool addLog = true)
    {
        SkillDefinition? definition = SkillRegistry.GetSkill(skillId);
        if (definition == null || SkillSystem == null)
        {
            return false;
        }

        bool learned = SkillSystem.TryLearnSkill(skillId, targetLevel);
        if (learned && addLog)
        {
            AddGameLog($"习得技能：{TranslateText(definition.NameKey)}");
        }

        return learned;
    }

    public void NotifyItemAcquired(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        ItemDefinition? item = ItemRegistry.GetItem(itemId);
        if (item == null || string.IsNullOrWhiteSpace(item.OwnedUnlockSkillId))
        {
            return;
        }

        if (PlayerProfile.Inventory.GetItemAmount(itemId) <= 0)
        {
            return;
        }

        EnsureSkillLearned(item.OwnedUnlockSkillId, 1, true);
    }

    private void ApplyOwnedItemSkillUnlocks(bool addLog)
    {
        foreach ((string itemId, ItemStack stack) in PlayerProfile.Inventory.Stacks)
        {
            if (stack.Quantity <= 0)
            {
                continue;
            }

            ItemDefinition? item = ItemRegistry.GetItem(itemId);
            if (item == null || string.IsNullOrWhiteSpace(item.OwnedUnlockSkillId))
            {
                continue;
            }

            EnsureSkillLearned(item.OwnedUnlockSkillId, 1, addLog);
        }
    }

    /// <summary>
    /// 给 GDScript、调试脚本和 UI 快速判断“背包里是否拥有某个物品”。
    /// </summary>
    public bool HasOwnedItem(string itemId)
    {
        return PlayerProfile.Inventory.HasItem(itemId);
    }

    /// <summary>
    /// 给调试脚本快速确认当前剧本到底载入了多少事件。
    /// 这次主要用来确认“主线剧本是否还是误用了测试事件配置”。
    /// </summary>
    public int GetRegisteredEventCount()
    {
        return EventRegistry.Events.Count;
    }

    public string GetEventDisplayName(string eventId)
    {
        EventDefinition? definition = EventRegistry.GetEvent(eventId);
        return definition == null ? eventId : TranslateText(definition.NameKey);
    }

    public bool SaveGame()
    {
        if (string.IsNullOrWhiteSpace(SaveManager.SavePath))
        {
            SaveManager.SavePath = RuntimePathHelper.GetTestSavePath();
        }

        QuestSystem?.RefreshQuestState();

        SaveData saveData = SaveManager.CreateFromProfile(PlayerProfile);
        PopulateSaveMetadata(saveData.Metadata);
        bool success = SaveManager.Save(saveData);
        AddGameLog(success
            ? $"已执行手动保存。路径：{SaveManager.SavePath}"
            : "手动保存失败，请检查日志。");
        return success;
    }

    public bool LoadGame()
    {
        string path = string.IsNullOrWhiteSpace(SaveManager.SavePath)
            ? RuntimePathHelper.GetTestSavePath()
            : SaveManager.SavePath;
        return LoadGameFromPath(path);
    }

    private void PopulateSaveMetadata(SaveMetadata metadata)
    {
        metadata.Locale = LocalizationManager.CurrentLocale;
        metadata.ScenarioId = ActiveScenario?.ScenarioId ?? string.Empty;
        metadata.ScenarioDisplayName = ActiveScenario?.DisplayName ?? string.Empty;
    }
}
