using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.SaveLoad;
using Test00_0410.UI.Placeholder;

namespace Test00_0410.UI;

public partial class GamePlayUI : Control
{
    private const string LayoutSettingsPath = "res://Resources/UI/main_ui_layout.tres";
    private const string TooltipDelaySettingPath = "gui/timers/tooltip_delay_sec";
    private const string NonBlockingTooltipBoundMetaKey = "__nb_tooltip_bound";
    private const string NonBlockingTooltipTextMetaKey = "__nb_tooltip_text";
    private const int FloatingTooltipZIndex = 260;
    private const double NonBlockingTooltipHarvestIntervalSeconds = 0.25;
    private const double ProgressVisualRefreshIntervalSeconds = 1.0 / 60.0;
    protected const string TabCurrentRegion = "current_region";
    protected const string TabInventory = "inventory";
    protected const string TabSkills = "skills";
    protected const string TabBattle = "battle";
    protected const string TabQuest = "quest";
    protected const string TabTutorial = "tutorial";
    protected const string TabAchievement = "achievement";
    protected const string TabDictionary = "dictionary";
    protected const string TabSystem = "system";

    protected GameManager? _gameManager;
    protected MainUiLayoutSettings _layoutSettings = new();
    protected GameplayUiTheme _theme = new();
    protected ScenarioGameplayLayout _scenarioLayout = new();

    protected Label? _topBarScenarioLabel;
    protected Label? _topBarAreaLabel;
    protected Button? _leftSidebarRegionToggleButton;
    protected Button? _leftSidebarSkillToggleButton;
    protected Label? _leftSidebarRegionModeNewLabel;
    protected VBoxContainer? _regionTreeContainer;
    protected HBoxContainer? _tabBar;
    protected Control? _pageHost;
    protected Control? _currentRegionPage;
    protected Label? _currentRegionTitleLabel;
    protected Label? _currentRegionHintLabel;
    protected VBoxContainer? _currentRegionSceneList;
    protected InventoryPanel? _inventoryPanel;
    protected SkillPanel? _skillPanel;
    protected BattlePanel? _battlePanel;
    protected QuestPanel? _questPanel;
    protected TutorialPanel? _tutorialPanel;
    protected AchievementPanel? _achievementPanel;
    protected DictionaryPanel? _dictionaryPanel;
    protected SystemPanel? _systemPanel;
    protected ConfigurableInfoPanel? _infoPanel;
    protected CharacterStatusPanel? _statusPanel;
    protected LogPanel? _collapsedLogPanel;
    protected LogPanel? _expandedLogPanel;
    protected PanelContainer? _collapsedLogDock;
    protected Control? _expandedLogOverlay;
    protected Button? _collapsedLogToggleButton;

    protected EventDialogPanel? _eventDialogPanel;
    protected SaveSlotDialog? _storySaveDialog;
    protected SaveSlotDialog? _storyLoadDialog;
    protected ConfirmActionDialog? _confirmDialog;

    protected readonly Dictionary<string, bool> _regionExpandedStates = new(StringComparer.Ordinal);
    protected readonly Dictionary<string, EventButtonItem> _eventButtonWidgets = new(StringComparer.Ordinal);
    protected readonly Dictionary<string, ScenarioTabDefinition> _scenarioTabs = new(StringComparer.Ordinal);
    protected readonly Dictionary<string, Control> _tabPagesById = new(StringComparer.Ordinal);
    protected readonly Dictionary<string, bool> _skillGroupExpandedStates = new(StringComparer.Ordinal);
    protected readonly List<Label> _newAreaMarkerLabels = new();
    protected string _loadedScenarioId = string.Empty;
    private bool _isLogExpanded;
    private bool _isLogMinimized;
    private SignalBus? _signalBus;
    private bool _pendingDeferredFullRefresh;
    private string _lastProgressVisualEventId = string.Empty;
    private double _progressVisualRefreshAccumulator;
    private double _statusRefreshAccumulator;
    private double _newAreaMarkerBlinkAccumulator;
    private bool _newAreaMarkerVisible = true;
    private string _activeIdleEventIdForUi = string.Empty;
    private bool _activeIdleRunningForUi;
    private string _currentUiThemeMode = GameplayUiConfigLoader.UiThemeModeStitch;
    private PanelContainer? _floatingHoverTooltip;
    private RichTextLabel? _floatingHoverTooltipLabel;
    private string _floatingHoverTooltipSummary = string.Empty;
    private string _floatingHoverTooltipContent = string.Empty;
    private double _nonBlockingTooltipHarvestAccumulator;
    private double _originalTooltipDelaySec;
    private bool _hasCapturedTooltipDelay;
    private ulong _scheduledActionRefreshVersion;

    public override void _Ready()
    {
        _gameManager = GameManager.Instance;
        LoadLayoutSettings();
        ConfigureGlobalTooltipBehavior();
        EnsureStructure();
        ConnectSignalBus();
        RefreshAllPanels();
        HarvestNonBlockingTooltipBindings();
    }

    public void RefreshAllPanels()
    {
        _gameManager ??= GameManager.Instance;
        EnsureStructure();
        HideFloatingHoverTooltip();

        if (_gameManager == null)
        {
            return;
        }

        _gameManager.QuestSystem?.RefreshQuestState();
        SyncScenarioSpecificUi();
        RefreshTopBar();
        UpdateAreaNewMarkers();
        RefreshLeftRegionTree();
        RefreshTabBar();
        RefreshCurrentRegionPage();
        RefreshCenterTabs();
        RefreshInfoPanel();
        RefreshStatusAndLogs();
        SyncIdleUiStateSnapshot();
        HarvestNonBlockingTooltipBindings();
    }

    public override void _ExitTree()
    {
        DisconnectSignalBus();
        RestoreGlobalTooltipBehavior();
    }

    public override void _Process(double delta)
    {
        UpdateNewAreaMarkerBlink(delta);
        double timedRefreshInterval = Math.Clamp(_layoutSettings.RefreshIntervalSeconds, 0.08f, 0.25f);

        bool needsTimedStatusRefresh = _statusPanel?.NeedsPeriodicRefresh() == true;
        if (needsTimedStatusRefresh)
        {
            _statusRefreshAccumulator += delta;
            if (_statusRefreshAccumulator >= Math.Max(0.5, timedRefreshInterval * 2.0))
            {
                _statusRefreshAccumulator = 0.0;
                _statusPanel?.RefreshStatus();
            }
        }
        else
        {
            _statusRefreshAccumulator = 0.0;
        }

        bool shouldDriveProgressVisuals = _gameManager?.IdleSystem != null
            && (_gameManager.PlayerProfile.IdleState.IsRunning || !string.IsNullOrWhiteSpace(_lastProgressVisualEventId));
        if (shouldDriveProgressVisuals)
        {
            _progressVisualRefreshAccumulator += delta;
            if (_progressVisualRefreshAccumulator >= ProgressVisualRefreshIntervalSeconds)
            {
                _progressVisualRefreshAccumulator = 0.0;
                UpdateLiveProgressVisuals();
            }
        }
        else
        {
            _progressVisualRefreshAccumulator = 0.0;
        }

        _nonBlockingTooltipHarvestAccumulator += delta;
        if (_nonBlockingTooltipHarvestAccumulator >= NonBlockingTooltipHarvestIntervalSeconds)
        {
            _nonBlockingTooltipHarvestAccumulator = 0.0;
            HarvestNonBlockingTooltipBindings();
        }

        UpdateFloatingHoverTooltipPosition();
    }

    protected void LoadLayoutSettings()
    {
        _layoutSettings = ResourceLoader.Load<MainUiLayoutSettings>(LayoutSettingsPath) ?? new MainUiLayoutSettings();
        _currentUiThemeMode = ResolveUiThemeMode();
        UiImageThemeManager.SetThemeMode(_currentUiThemeMode);
        _theme = GameplayUiConfigLoader.LoadTheme(_currentUiThemeMode);
    }

    protected bool IsUsingStitchUiTheme()
    {
        return string.Equals(_currentUiThemeMode, GameplayUiConfigLoader.UiThemeModeStitch, StringComparison.Ordinal);
    }

    protected void UseStitchUiTheme()
    {
        SwitchUiThemeMode(GameplayUiConfigLoader.UiThemeModeStitch);
    }

    protected void UseLegacyUiTheme()
    {
        SwitchUiThemeMode(GameplayUiConfigLoader.UiThemeModeLegacy);
    }

    protected void SwitchUiThemeMode(string mode)
    {
        if (_gameManager == null)
        {
            return;
        }

        string normalizedMode = GameplayUiConfigLoader.NormalizeUiThemeMode(mode);
        if (string.Equals(_currentUiThemeMode, normalizedMode, StringComparison.Ordinal))
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.UiThemeMode = normalizedMode;
        LoadLayoutSettings();
        RebuildUiStructure();
        _gameManager.AddGameLog($"已切换 UI 主题：{(IsUsingStitchUiTheme() ? "新UI(stitch)" : "旧UI(legacy)")}。");
    }

    private string ResolveUiThemeMode()
    {
        string configuredMode = _gameManager?.PlayerProfile.UiState.UiThemeMode
            ?? GameplayUiConfigLoader.UiThemeModeStitch;
        return GameplayUiConfigLoader.NormalizeUiThemeMode(configuredMode);
    }

    private void RebuildUiStructure()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _topBarScenarioLabel = null;
        _topBarAreaLabel = null;
        _leftSidebarRegionToggleButton = null;
        _leftSidebarSkillToggleButton = null;
        _leftSidebarRegionModeNewLabel = null;
        _regionTreeContainer = null;
        _tabBar = null;
        _pageHost = null;
        _currentRegionPage = null;
        _currentRegionTitleLabel = null;
        _currentRegionHintLabel = null;
        _currentRegionSceneList = null;
        _inventoryPanel = null;
        _skillPanel = null;
        _battlePanel = null;
        _questPanel = null;
        _tutorialPanel = null;
        _achievementPanel = null;
        _dictionaryPanel = null;
        _systemPanel = null;
        _infoPanel = null;
        _statusPanel = null;
        _collapsedLogPanel = null;
        _expandedLogPanel = null;
        _collapsedLogDock = null;
        _expandedLogOverlay = null;
        _collapsedLogToggleButton = null;
        _eventDialogPanel = null;
        _storySaveDialog = null;
        _storyLoadDialog = null;
        _confirmDialog = null;
        _floatingHoverTooltip = null;
        _floatingHoverTooltipLabel = null;
        _floatingHoverTooltipSummary = string.Empty;
        _floatingHoverTooltipContent = string.Empty;

        _eventButtonWidgets.Clear();
        _tabPagesById.Clear();
        _newAreaMarkerLabels.Clear();
        _lastProgressVisualEventId = string.Empty;
        _progressVisualRefreshAccumulator = 0.0;

        EnsureStructure();
        RefreshAllPanels();
    }

    protected void EnsureStructure()
    {
        if (_topBarScenarioLabel != null
            && _topBarAreaLabel != null
            && _regionTreeContainer != null
            && _tabBar != null
            && _pageHost != null
            && _currentRegionPage != null
            && _currentRegionTitleLabel != null
            && _currentRegionHintLabel != null
            && _currentRegionSceneList != null
            && _inventoryPanel != null
            && _skillPanel != null
            && _battlePanel != null
            && _questPanel != null
            && _tutorialPanel != null
            && _achievementPanel != null
            && _dictionaryPanel != null
            && _infoPanel != null
            && _statusPanel != null
            && _collapsedLogPanel != null
            && _expandedLogPanel != null
            && _collapsedLogDock != null
            && _expandedLogOverlay != null
            && _eventDialogPanel != null
            && _storySaveDialog != null
            && _storyLoadDialog != null
            && _confirmDialog != null)
        {
            return;
        }

        if (GetChildCount() > 0)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        ApplyRuntimeTooltipTheme();

        ColorRect background = new()
        {
            Name = "Background",
            Color = _theme.BackgroundColor,
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        MarginContainer margin = new()
        {
            Name = "OuterMargin"
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", _theme.OuterMargin);
        margin.AddThemeConstantOverride("margin_top", _theme.OuterMargin);
        margin.AddThemeConstantOverride("margin_right", _theme.OuterMargin);
        margin.AddThemeConstantOverride("margin_bottom", _theme.OuterMargin);
        AddChild(margin);

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", _theme.SectionGap);
        margin.AddChild(root);

        root.AddChild(BuildTopBar());

        HBoxContainer bodyRow = new()
        {
            Name = "BodyRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        bodyRow.AddThemeConstantOverride("separation", _theme.SectionGap);
        root.AddChild(bodyRow);

        bodyRow.AddChild(BuildLeftRegionColumn());
        bodyRow.AddChild(BuildCenterColumn());
        bodyRow.AddChild(BuildRightColumn());

        _expandedLogOverlay = BuildExpandedLogOverlay();
        AddChild(_expandedLogOverlay);
        SyncLogVisibility();

        _eventDialogPanel = new EventDialogPanel { Name = "EventDialogPanel" };
        _eventDialogPanel.Configure(_layoutSettings, IsUsingStitchUiTheme());
        AddChild(_eventDialogPanel);

        _storySaveDialog = new SaveSlotDialog { Name = "StorySaveDialog" };
        _storySaveDialog.Configure(_layoutSettings, IsUsingStitchUiTheme());
        AddChild(_storySaveDialog);

        _storyLoadDialog = new SaveSlotDialog { Name = "StoryLoadDialog" };
        _storyLoadDialog.Configure(_layoutSettings, IsUsingStitchUiTheme());
        AddChild(_storyLoadDialog);

        _confirmDialog = new ConfirmActionDialog { Name = "ConfirmDialog" };
        _confirmDialog.Configure(_layoutSettings, IsUsingStitchUiTheme());
        AddChild(_confirmDialog);

        EnsureFloatingHoverTooltip();
    }

    private Control BuildTopBar()
    {
        PanelContainer panel = new()
        {
            Name = "TopBar",
            CustomMinimumSize = new Vector2(0, _theme.TopBarHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.TopBar));

        HBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", _theme.InnerGap);
        panel.AddChild(content);

        _topBarScenarioLabel = new Label
        {
            Name = "TopBarScenarioLabel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _topBarScenarioLabel.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#30332e") : new Color("#fff6ea"));
        _topBarScenarioLabel.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize + 2);
        content.AddChild(_topBarScenarioLabel);

        _topBarAreaLabel = new Label
        {
            Name = "TopBarAreaLabel",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _topBarAreaLabel.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#426464") : new Color("#ffd474"));
        _topBarAreaLabel.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize + 1);
        content.AddChild(_topBarAreaLabel);

        return panel;
    }

    private Control BuildLeftRegionColumn()
    {
        PanelContainer panel = new()
        {
            Name = "LeftRegionColumn",
            CustomMinimumSize = new Vector2(_theme.LeftRightColumnWidth, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.LeftColumn));

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", _theme.InnerGap);
        panel.AddChild(content);

        HBoxContainer titleRow = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleRow.AddThemeConstantOverride("separation", 6);
        content.AddChild(titleRow);

        Control regionToggleLayer = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 42)
        };
        titleRow.AddChild(regionToggleLayer);

        _leftSidebarRegionToggleButton = new Button
        {
            Text = "区域",
            MouseFilter = MouseFilterEnum.Stop
        };
        _leftSidebarRegionToggleButton.SetAnchorsPreset(LayoutPreset.FullRect);
        _leftSidebarRegionToggleButton.Pressed += () => SetLeftSidebarMode(false);
        regionToggleLayer.AddChild(_leftSidebarRegionToggleButton);

        _leftSidebarRegionModeNewLabel = new Label
        {
            Text = "New",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _leftSidebarRegionModeNewLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _leftSidebarRegionModeNewLabel.OffsetTop = 2;
        _leftSidebarRegionModeNewLabel.OffsetRight = -6;
        _leftSidebarRegionModeNewLabel.AddThemeFontSizeOverride("font_size", Math.Max(11, _theme.RegionCountFontSize));
        _leftSidebarRegionModeNewLabel.AddThemeColorOverride("font_color", new Color("#ff2d2d"));
        _leftSidebarRegionModeNewLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        _leftSidebarRegionModeNewLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _leftSidebarRegionModeNewLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _leftSidebarRegionModeNewLabel.SelfModulate = new Color(1, 1, 1, _newAreaMarkerVisible ? 1.0f : 0.0f);
        _leftSidebarRegionModeNewLabel.ZIndex = 2;
        regionToggleLayer.AddChild(_leftSidebarRegionModeNewLabel);

        _leftSidebarSkillToggleButton = new Button
        {
            Text = "技能",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 42)
        };
        _leftSidebarSkillToggleButton.Pressed += () => SetLeftSidebarMode(true);
        titleRow.AddChild(_leftSidebarSkillToggleButton);

        StyleBoxFlat toggleInactiveStyle = CreateFlatStyle(new Color(1, 1, 1, IsUsingStitchUiTheme() ? 0.25f : 0.04f));
        Color toggleActiveColor = IsUsingStitchUiTheme() ? new Color("#c5eae9") : new Color("#22498d");
        toggleActiveColor.A = IsUsingStitchUiTheme() ? 0.92f : 0.34f;
        StyleBoxFlat toggleActiveStyle = CreateFlatStyle(toggleActiveColor);
        foreach (Button toggle in new[] { _leftSidebarRegionToggleButton, _leftSidebarSkillToggleButton })
        {
            toggle.AddThemeFontSizeOverride("font_size", _theme.RegionHeaderFontSize - 2);
            toggle.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#5d605a") : new Color("#93bbff"));
            toggle.AddThemeColorOverride("font_hover_color", IsUsingStitchUiTheme() ? new Color("#30332e") : new Color("#f3f9ff"));
            toggle.AddThemeStyleboxOverride("normal", toggleInactiveStyle);
            toggle.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1, 1, 1, IsUsingStitchUiTheme() ? 0.35f : 0.08f)));
            toggle.AddThemeStyleboxOverride("pressed", toggleActiveStyle);
        }

        ScrollContainer scroll = new()
        {
            Name = "RegionTreeScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        content.AddChild(scroll);

        _regionTreeContainer = new VBoxContainer
        {
            Name = "RegionTreeContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _regionTreeContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_regionTreeContainer);
        RefreshLeftSidebarModeHeader();

        return panel;
    }

    private Control BuildCenterColumn()
    {
        PanelContainer panel = new()
        {
            Name = "CenterColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.CenterColumn));

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", _theme.InnerGap);
        panel.AddChild(content);

        _tabBar = new HBoxContainer
        {
            Name = "MainTabBar",
            CustomMinimumSize = new Vector2(0, _theme.TabBarHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _tabBar.AddThemeConstantOverride("separation", 8);
        content.AddChild(_tabBar);

        Control pageDock = new()
        {
            Name = "PageDock",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(pageDock);

        _pageHost = new Control
        {
            Name = "MainTabs",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _pageHost.SetAnchorsPreset(LayoutPreset.FullRect);
        pageDock.AddChild(_pageHost);

        _currentRegionPage = BuildCurrentRegionPage();
        _currentRegionPage.Name = _theme.CurrentRegionTabText;
        _pageHost.AddChild(_currentRegionPage);
        _tabPagesById[TabCurrentRegion] = _currentRegionPage;

        _inventoryPanel = new InventoryPanel { Name = _theme.InventoryTabText };
        _inventoryPanel.Configure(_gameManager!);
        _inventoryPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_inventoryPanel);
        _tabPagesById[TabInventory] = _inventoryPanel;

        _skillPanel = new SkillPanel { Name = _theme.SkillsTabText };
        _skillPanel.Configure(_gameManager!, TryLevelUpSkill);
        _skillPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_skillPanel);
        _tabPagesById[TabSkills] = _skillPanel;

        _battlePanel = new BattlePanel { Name = "战斗" };
        _battlePanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_battlePanel);
        _tabPagesById[TabBattle] = _battlePanel;

        _questPanel = new QuestPanel { Name = "任务" };
        _questPanel.Configure(
            _gameManager!,
            _theme,
            FindActionContextByEventId,
            (summary, content) => _infoPanel?.SetTransientContent(summary, content),
            () =>
            {
                _infoPanel?.ClearTransientContent();
                FlushDeferredRefreshIfNeeded();
            },
            RefreshAllPanels);
        _questPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_questPanel);
        _tabPagesById[TabQuest] = _questPanel;

        _tutorialPanel = new TutorialPanel { Name = "教学" };
        _tutorialPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_tutorialPanel);
        _tabPagesById[TabTutorial] = _tutorialPanel;

        _achievementPanel = new AchievementPanel { Name = "成就" };
        _achievementPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_achievementPanel);
        _tabPagesById[TabAchievement] = _achievementPanel;

        _dictionaryPanel = new DictionaryPanel { Name = _theme.DictionaryTabText };
        _dictionaryPanel.Configure(_gameManager!);
        _dictionaryPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_dictionaryPanel);
        _tabPagesById[TabDictionary] = _dictionaryPanel;

        _collapsedLogDock = BuildCollapsedLogDock();
        content.AddChild(_collapsedLogDock);

        return panel;
    }

    private Control BuildCurrentRegionPage()
    {
        VBoxContainer root = new()
        {
            Name = "CurrentRegionPage",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", _theme.InnerGap);

        _currentRegionTitleLabel = new Label { Name = "CurrentRegionTitleLabel" };
        _currentRegionTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _currentRegionTitleLabel.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#30332e") : new Color("#fff0f6"));
        _currentRegionTitleLabel.AddThemeFontSizeOverride("font_size", _theme.SceneTitleFontSize + 2);
        root.AddChild(_currentRegionTitleLabel);

        _currentRegionHintLabel = new Label
        {
            Name = "CurrentRegionHintLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _currentRegionHintLabel.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#5d605a") : new Color("#e5a7bf"));
        _currentRegionHintLabel.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize - 1);
        root.AddChild(_currentRegionHintLabel);

        ScrollContainer scroll = new()
        {
            Name = "CurrentRegionScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        root.AddChild(scroll);

        _currentRegionSceneList = new VBoxContainer
        {
            Name = "CurrentRegionSceneList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _currentRegionSceneList.AddThemeConstantOverride("separation", _theme.InnerGap + 4);
        scroll.AddChild(_currentRegionSceneList);

        return root;
    }

    private PanelContainer BuildCollapsedLogDock()
    {
        PanelContainer dock = new()
        {
            Name = "CollapsedLogDock",
            CustomMinimumSize = new Vector2(0, _theme.BottomLogHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        dock.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.BottomLog));

        VBoxContainer logContent = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        logContent.AddThemeConstantOverride("separation", 6);
        dock.AddChild(logContent);

        HBoxContainer logHeader = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        logHeader.AddThemeConstantOverride("separation", 4);
        logContent.AddChild(logHeader);

        Label logTitle = new()
        {
            Text = "运行日志",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        logTitle.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#30332e") : new Color("#f8fafc"));
        logTitle.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize + 1);
        logHeader.AddChild(logTitle);

        _collapsedLogToggleButton = new Button()
        {
            Name = "ToggleLogHeightButton",
            Text = "最小化",
            CustomMinimumSize = new Vector2(90, 34)
        };
        ApplyLogActionButtonStyle(_collapsedLogToggleButton);
        _collapsedLogToggleButton.Pressed += () => SetLogMinimized(!_isLogMinimized);
        logHeader.AddChild(_collapsedLogToggleButton);

        Button maximizeButton = new()
        {
            Name = "MaximizeLogButton",
            Text = "最大化",
            CustomMinimumSize = new Vector2(90, 34)
        };
        ApplyLogActionButtonStyle(maximizeButton);
        maximizeButton.Pressed += () => SetLogExpanded(true);
        logHeader.AddChild(maximizeButton);

        _collapsedLogPanel = new LogPanel
        {
            Name = "CollapsedLogPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _collapsedLogPanel.SetDisplayMode(false, 5);
        logContent.AddChild(_collapsedLogPanel);
        RefreshCollapsedLogDockState();

        return dock;
    }

    private Control BuildRightColumn()
    {
        VBoxContainer root = new()
        {
            Name = "RightColumn",
            CustomMinimumSize = new Vector2(_theme.LeftRightColumnWidth, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", _theme.SectionGap);

        PanelContainer infoDock = new()
        {
            Name = "InfoDock",
            CustomMinimumSize = new Vector2(0, _theme.InfoPanelMinHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        infoDock.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.RightInfo));
        root.AddChild(infoDock);

        _infoPanel = new ConfigurableInfoPanel
        {
            Name = "InfoPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _infoPanel.Configure(_layoutSettings, IsUsingStitchUiTheme());
        infoDock.AddChild(_infoPanel);

        PanelContainer statusDock = new()
        {
            Name = "StatusDock",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        statusDock.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.RightStatus));
        root.AddChild(statusDock);

        _statusPanel = new CharacterStatusPanel
        {
            Name = "CharacterStatusPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _statusPanel.Configure(_gameManager!, _theme, FindActionContextByEventId);
        statusDock.AddChild(_statusPanel);

        return root;
    }

    private Control BuildExpandedLogOverlay()
    {
        Control overlay = new()
        {
            Name = "ExpandedLogOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 120
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);

        ColorRect shade = new()
        {
            Name = "ExpandedLogShade",
            Color = new Color(0, 0, 0, 0.72f),
            MouseFilter = MouseFilterEnum.Stop
        };
        shade.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(shade);

        MarginContainer margin = new()
        {
            Name = "ExpandedLogMargin"
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 48);
        margin.AddThemeConstantOverride("margin_top", 48);
        margin.AddThemeConstantOverride("margin_right", 48);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        overlay.AddChild(margin);

        PanelContainer dock = new()
        {
            Name = "ExpandedLogDock",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        dock.AddThemeStyleboxOverride(
            "panel",
            IsUsingStitchUiTheme()
                ? StitchElementStyleLibrary.CreateDeepOpaqueFrame(_theme.CornerRadius)
                : CreatePanelStyle(_theme.BottomLog));
        margin.AddChild(dock);

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);
        dock.AddChild(content);

        HBoxContainer header = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(header);

        Label title = new()
        {
            Text = "运行日志（展开）",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#d9ffff") : new Color("#f8fafc"));
        title.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize + 2);
        header.AddChild(title);

        Button collapseButton = new()
        {
            Name = "CollapseLogButton",
            Text = "收起",
            CustomMinimumSize = new Vector2(90, 34)
        };
        ApplyLogActionButtonStyle(collapseButton);
        collapseButton.Pressed += () => SetLogExpanded(false);
        header.AddChild(collapseButton);

        _expandedLogPanel = new LogPanel
        {
            Name = "ExpandedLogPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _expandedLogPanel.SetDisplayMode(true);
        content.AddChild(_expandedLogPanel);

        return overlay;
    }

    private void SetLogExpanded(bool isExpanded)
    {
        _isLogExpanded = isExpanded;
        if (!isExpanded)
        {
            _isLogMinimized = false;
        }

        SyncLogVisibility();
        RefreshCollapsedLogDockState();
        RefreshStatusAndLogs();
    }

    private void SetLogMinimized(bool isMinimized)
    {
        if (_isLogExpanded)
        {
            return;
        }

        _isLogMinimized = isMinimized;
        RefreshCollapsedLogDockState();
        RefreshStatusAndLogs();
    }

    private void SyncLogVisibility()
    {
        if (_collapsedLogDock != null)
        {
            _collapsedLogDock.Visible = !_isLogExpanded;
        }

        if (_expandedLogOverlay != null)
        {
            _expandedLogOverlay.Visible = _isLogExpanded;
        }
    }

    private void RefreshCollapsedLogDockState()
    {
        if (_collapsedLogDock != null)
        {
            _collapsedLogDock.CustomMinimumSize = new Vector2(0, _isLogMinimized ? GetMinimizedLogHeight() : _theme.BottomLogHeight);
        }

        if (_collapsedLogToggleButton != null)
        {
            _collapsedLogToggleButton.Text = _isLogMinimized ? "展开" : "最小化";
        }

        _collapsedLogPanel?.SetDisplayMode(false, _isLogMinimized ? 1 : 5);
    }

    private bool IsModalDialogVisible()
    {
        return _eventDialogPanel?.Visible == true
            || _storySaveDialog?.Visible == true
            || _storyLoadDialog?.Visible == true
            || _confirmDialog?.Visible == true;
    }

    private int GetMinimizedLogHeight()
    {
        return Math.Max(108, (_theme.BodyFontSize * 2) + 72);
    }

    private void EnsureFloatingHoverTooltip()
    {
        if (_floatingHoverTooltip != null && _floatingHoverTooltipLabel != null)
        {
            return;
        }

        _floatingHoverTooltip = new PanelContainer
        {
            Name = "FloatingHoverTooltip",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = FloatingTooltipZIndex,
            CustomMinimumSize = new Vector2(320, 0)
        };
        _floatingHoverTooltip.SetAnchorsPreset(LayoutPreset.TopLeft);
        AddChild(_floatingHoverTooltip);

        MarginContainer margin = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _floatingHoverTooltip.AddChild(margin);

        _floatingHoverTooltipLabel = new RichTextLabel
        {
            Name = "FloatingHoverTooltipLabel",
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(300, 0)
        };
        margin.AddChild(_floatingHoverTooltipLabel);
        ApplyFloatingHoverTooltipStyle();
    }

    private void ApplyFloatingHoverTooltipStyle()
    {
        if (_floatingHoverTooltip == null || _floatingHoverTooltipLabel == null)
        {
            return;
        }

        if (IsUsingStitchUiTheme())
        {
            _floatingHoverTooltip.AddThemeStyleboxOverride(
                "panel",
                StitchElementStyleLibrary.CreateDeepTooltipFrame(Math.Max(10, _theme.CornerRadius - 4)));
            _floatingHoverTooltipLabel.AddThemeColorOverride("default_color", new Color("#f4f9ff"));
            _floatingHoverTooltipLabel.AddThemeFontSizeOverride("normal_font_size", Math.Max(14, _theme.BodyFontSize - 5));
            return;
        }

        StyleBoxFlat background = new()
        {
            BgColor = new Color("#101826"),
            BorderColor = new Color("#2f456b"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        _floatingHoverTooltip.AddThemeStyleboxOverride("panel", background);
        _floatingHoverTooltipLabel.AddThemeColorOverride("default_color", new Color("#eaf2ff"));
        _floatingHoverTooltipLabel.AddThemeFontSizeOverride("normal_font_size", Math.Max(13, _theme.BodyFontSize - 4));
    }

    protected void ShowFloatingHoverTooltip(string summaryText, string contentText)
    {
        EnsureFloatingHoverTooltip();
        if (_floatingHoverTooltip == null || _floatingHoverTooltipLabel == null)
        {
            return;
        }

        _floatingHoverTooltipSummary = summaryText;
        _floatingHoverTooltipContent = contentText;
        _floatingHoverTooltipLabel.Text = BuildFloatingHoverTooltipText(summaryText, contentText);
        _floatingHoverTooltip.Visible = true;
        UpdateFloatingHoverTooltipPosition();
    }

    protected void HideFloatingHoverTooltip()
    {
        _floatingHoverTooltipSummary = string.Empty;
        _floatingHoverTooltipContent = string.Empty;
        if (_floatingHoverTooltip != null)
        {
            _floatingHoverTooltip.Visible = false;
        }
    }

    private string BuildFloatingHoverTooltipText(string summaryText, string contentText)
    {
        if (string.IsNullOrWhiteSpace(summaryText))
        {
            return contentText;
        }

        if (string.IsNullOrWhiteSpace(contentText))
        {
            return summaryText;
        }

        return $"{summaryText}\n{contentText}";
    }

    private void UpdateFloatingHoverTooltipPosition()
    {
        if (_floatingHoverTooltip == null
            || !_floatingHoverTooltip.Visible
            || !IsInstanceValid(_floatingHoverTooltip))
        {
            return;
        }

        Vector2 mousePosition = GetViewport().GetMousePosition();
        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 tooltipSize = _floatingHoverTooltip.Size;
        if (tooltipSize.X <= 1 || tooltipSize.Y <= 1)
        {
            tooltipSize = _floatingHoverTooltip.GetCombinedMinimumSize();
        }

        Vector2 target = mousePosition + new Vector2(18, 20);
        float padding = 8f;
        if (target.X + tooltipSize.X > viewportSize.X - padding)
        {
            target.X = Math.Max(padding, mousePosition.X - tooltipSize.X - 16f);
        }

        if (target.Y + tooltipSize.Y > viewportSize.Y - padding)
        {
            target.Y = Math.Max(padding, mousePosition.Y - tooltipSize.Y - 16f);
        }

        target.X = Math.Clamp(target.X, padding, Math.Max(padding, viewportSize.X - tooltipSize.X - padding));
        target.Y = Math.Clamp(target.Y, padding, Math.Max(padding, viewportSize.Y - tooltipSize.Y - padding));
        _floatingHoverTooltip.Position = target;
    }

    private void ConfigureGlobalTooltipBehavior()
    {
        if (_hasCapturedTooltipDelay)
        {
            return;
        }

        _originalTooltipDelaySec = (double)ProjectSettings.GetSetting(TooltipDelaySettingPath, 0.7);
        _hasCapturedTooltipDelay = true;
        ProjectSettings.SetSetting(TooltipDelaySettingPath, 86400.0);
    }

    private void RestoreGlobalTooltipBehavior()
    {
        if (!_hasCapturedTooltipDelay)
        {
            return;
        }

        ProjectSettings.SetSetting(TooltipDelaySettingPath, _originalTooltipDelaySec);
        _hasCapturedTooltipDelay = false;
    }

    protected void HarvestNonBlockingTooltipBindings()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Queue<Node> pending = new();
        pending.Enqueue(this);

        while (pending.Count > 0)
        {
            Node node = pending.Dequeue();
            foreach (Node child in node.GetChildren())
            {
                pending.Enqueue(child);
            }

            if (node is not Control control)
            {
                continue;
            }

            if (_floatingHoverTooltip != null
                && (ReferenceEquals(control, _floatingHoverTooltip) || _floatingHoverTooltip.IsAncestorOf(control)))
            {
                continue;
            }

            if (control.HasMeta(NonBlockingTooltipBoundMetaKey))
            {
                SyncNonBlockingTooltipContent(control);
                continue;
            }

            if (string.IsNullOrWhiteSpace(control.TooltipText)
                && !control.HasMeta(NonBlockingTooltipTextMetaKey))
            {
                continue;
            }

            BindNonBlockingTooltip(control);
        }
    }

    private void BindNonBlockingTooltip(Control control)
    {
        SyncNonBlockingTooltipContent(control);
        control.SetMeta(NonBlockingTooltipBoundMetaKey, true);
        control.MouseEntered += () =>
        {
            if (!IsInstanceValid(control))
            {
                return;
            }

            string tooltipText = ResolveNonBlockingTooltipContent(control);
            if (string.IsNullOrWhiteSpace(tooltipText))
            {
                return;
            }

            ShowFloatingHoverTooltip(BuildTooltipSummary(control), tooltipText);
        };

        control.MouseExited += HideFloatingHoverTooltip;
    }

    private static void SyncNonBlockingTooltipContent(Control control, bool suppressNativeTooltip = false)
    {
        if (!IsInstanceValid(control))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(control.TooltipText))
        {
            return;
        }

        control.SetMeta(NonBlockingTooltipTextMetaKey, control.TooltipText);
        if (suppressNativeTooltip)
        {
            control.TooltipText = string.Empty;
        }
    }

    private static string ResolveNonBlockingTooltipContent(Control control)
    {
        if (!IsInstanceValid(control))
        {
            return string.Empty;
        }

        // If runtime logic rewrites TooltipText later, keep syncing it into our non-blocking channel.
        SyncNonBlockingTooltipContent(control, suppressNativeTooltip: true);
        if (!control.HasMeta(NonBlockingTooltipTextMetaKey))
        {
            return string.Empty;
        }

        Variant metaValue = control.GetMeta(NonBlockingTooltipTextMetaKey);
        return metaValue.VariantType == Variant.Type.Nil
            ? string.Empty
            : metaValue.AsString();
    }

    private static string BuildTooltipSummary(Control control)
    {
        if (control is Button button && !string.IsNullOrWhiteSpace(button.Text))
        {
            return button.Text;
        }

        if (control is Label label && !string.IsNullOrWhiteSpace(label.Text))
        {
            return label.Text;
        }

        return "悬浮说明";
    }

    private void ConnectSignalBus()
    {
        _signalBus = GetNodeOrNull<SignalBus>("/root/SignalBus");
        if (_signalBus == null)
        {
            return;
        }

        _signalBus.ActiveIdleEventChanged += OnActiveIdleEventChanged;
        _signalBus.LogMessageRequested += OnLogMessageRequested;
    }

    private void DisconnectSignalBus()
    {
        if (_signalBus == null)
        {
            return;
        }

        _signalBus.ActiveIdleEventChanged -= OnActiveIdleEventChanged;
        _signalBus.LogMessageRequested -= OnLogMessageRequested;
        _signalBus = null;
    }

    private void OnActiveIdleEventChanged(string eventId)
    {
        bool wasIdleRunning = _activeIdleRunningForUi;
        string previousEventId = _activeIdleEventIdForUi;
        bool isIdleRunning = _gameManager?.PlayerProfile.IdleState.IsRunning == true;
        string activeEventId = isIdleRunning
            ? _gameManager?.PlayerProfile.IdleState.ActiveEventId ?? string.Empty
            : string.Empty;
        bool stateChanged = _activeIdleRunningForUi != isIdleRunning
            || !string.Equals(_activeIdleEventIdForUi, activeEventId, StringComparison.Ordinal);

        _activeIdleRunningForUi = isIdleRunning;
        _activeIdleEventIdForUi = activeEventId;

        if (stateChanged)
        {
            RefreshIdleUiWithoutFullRebuild(previousEventId, activeEventId, wasIdleRunning, isIdleRunning);
            return;
        }

        RefreshStatusAndLogs();
    }

    private void OnLogMessageRequested(string message)
    {
        RefreshLogPanels();
        if (!IsModalDialogVisible() && !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            RefreshAllVisibleEventWidgets();
        }
    }

    protected void RequestEventDrivenRefresh()
    {
        if (_infoPanel?.HasTransientContent == true)
        {
            _pendingDeferredFullRefresh = true;
            RefreshStatusAndLogs();
            return;
        }

        _pendingDeferredFullRefresh = false;
        RefreshAllPanels();
    }

    protected void RequestActionDrivenRefresh()
    {
        _pendingDeferredFullRefresh = false;
        _infoPanel?.ClearTransientContent();
        HideFloatingHoverTooltip();
        RefreshAllPanels();
    }

    protected void ScheduleActionDrivenRefresh(double delaySeconds = 0.08)
    {
        if (!IsInsideTree())
        {
            RequestActionDrivenRefresh();
            return;
        }

        if (delaySeconds <= 0.0001)
        {
            RequestActionDrivenRefresh();
            return;
        }

        ulong currentVersion = ++_scheduledActionRefreshVersion;
        SceneTreeTimer timer = GetTree().CreateTimer(delaySeconds);
        timer.Timeout += () =>
        {
            if (!IsInsideTree() || currentVersion != _scheduledActionRefreshVersion)
            {
                return;
            }

            RequestActionDrivenRefresh();
        };
    }

    protected void FlushDeferredRefreshIfNeeded()
    {
        if (!_pendingDeferredFullRefresh)
        {
            return;
        }

        _pendingDeferredFullRefresh = false;
        RefreshAllPanels();
    }

    private void SyncIdleUiStateSnapshot()
    {
        bool isIdleRunning = _gameManager?.PlayerProfile.IdleState.IsRunning == true;
        _activeIdleRunningForUi = isIdleRunning;
        _activeIdleEventIdForUi = isIdleRunning
            ? _gameManager?.PlayerProfile.IdleState.ActiveEventId ?? string.Empty
            : string.Empty;
        _lastProgressVisualEventId = _activeIdleEventIdForUi;
        _progressVisualRefreshAccumulator = 0.0;
    }

    private void RefreshIdleUiWithoutFullRebuild(string previousEventId, string activeEventId, bool wasIdleRunning, bool isIdleRunning)
    {
        RefreshAllVisibleEventWidgets();

        bool shouldRefreshLeftTree = wasIdleRunning != isIdleRunning;
        if (!shouldRefreshLeftTree)
        {
            string previousAreaId = FindActionContextByEventId(previousEventId)?.AreaId ?? string.Empty;
            string currentAreaId = FindActionContextByEventId(activeEventId)?.AreaId ?? string.Empty;
            shouldRefreshLeftTree = !string.Equals(previousAreaId, currentAreaId, StringComparison.Ordinal);
        }

        if (shouldRefreshLeftTree)
        {
            RefreshLeftRegionTree();
        }

        _statusPanel?.RefreshStatus();
        UpdateLiveProgressVisuals();
    }

    private void RefreshAllVisibleEventWidgets()
    {
        if (_gameManager == null)
        {
            return;
        }

        foreach ((string eventId, EventButtonItem widget) in _eventButtonWidgets.ToArray())
        {
            if (!IsInstanceValid(widget))
            {
                _eventButtonWidgets.Remove(eventId);
                continue;
            }

            EventDefinition? definition = _gameManager.EventRegistry.GetEvent(eventId);
            if (definition == null)
            {
                continue;
            }

            EventButtonViewData data = BuildEventButtonData(definition);
            widget.UpdateView(data, _layoutSettings, _theme.ProgressBarHeight);
        }
    }

    private void RefreshLogPanels()
    {
        IReadOnlyList<string> logs = _gameManager?.RuntimeLogs ?? Array.Empty<string>();
        _collapsedLogPanel?.SetMessages(logs);
        _expandedLogPanel?.SetMessages(logs);
    }

    protected void UpdateLiveProgressVisuals()
    {
        if (_gameManager?.IdleSystem == null)
        {
            return;
        }

        string activeEventId = _gameManager.PlayerProfile.IdleState.ActiveEventId;
        bool isIdleRunning = _gameManager.PlayerProfile.IdleState.IsRunning && !string.IsNullOrWhiteSpace(activeEventId);
        if (!isIdleRunning)
        {
            if (TryGetValidEventButtonWidget(_lastProgressVisualEventId, out EventButtonItem lastWidget))
            {
                lastWidget.SetLiveProgress(0.0, false);
            }

            _lastProgressVisualEventId = string.Empty;
            _statusPanel?.UpdateTargetProgress(0.0, false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_lastProgressVisualEventId)
            && !string.Equals(_lastProgressVisualEventId, activeEventId, StringComparison.Ordinal)
            && TryGetValidEventButtonWidget(_lastProgressVisualEventId, out EventButtonItem staleWidget))
        {
            staleWidget.SetLiveProgress(0.0, false);
        }

        double targetRatio = _gameManager.IdleSystem.GetProgressRatio(activeEventId);
        if (TryGetValidEventButtonWidget(activeEventId, out EventButtonItem activeWidget))
        {
            activeWidget.SetLiveProgress(targetRatio, true);
        }

        _statusPanel?.UpdateTargetProgress(targetRatio, true);
        _lastProgressVisualEventId = activeEventId;
    }

    private bool TryGetValidEventButtonWidget(string eventId, out EventButtonItem widget)
    {
        widget = null!;
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        if (!_eventButtonWidgets.TryGetValue(eventId, out EventButtonItem? found))
        {
            return false;
        }

        if (!IsInstanceValid(found))
        {
            _eventButtonWidgets.Remove(eventId);
            return false;
        }

        widget = found;
        return true;
    }

    private void UpdateNewAreaMarkerBlink(double delta)
    {
        _newAreaMarkerBlinkAccumulator += delta;
        if (_newAreaMarkerBlinkAccumulator < 0.45)
        {
            return;
        }

        _newAreaMarkerBlinkAccumulator = 0.0;
        _newAreaMarkerVisible = !_newAreaMarkerVisible;

        foreach (Label label in _newAreaMarkerLabels.ToArray())
        {
            if (!IsInstanceValid(label))
            {
                _newAreaMarkerLabels.Remove(label);
                continue;
            }

            Color modulate = label.SelfModulate;
            modulate.A = _newAreaMarkerVisible ? 1.0f : 0.0f;
            label.SelfModulate = modulate;
        }
    }
}

