using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.SaveLoad;

namespace Test00_0410.UI;

public partial class GamePlayUI : Control
{
    private const string LayoutSettingsPath = "res://Resources/UI/main_ui_layout.tres";
    protected const string TabCurrentRegion = "current_region";
    protected const string TabInventory = "inventory";
    protected const string TabSkills = "skills";
    protected const string TabDictionary = "dictionary";
    protected const string TabSystem = "system";

    protected GameManager? _gameManager;
    protected MainUiLayoutSettings _layoutSettings = new();
    protected GameplayUiTheme _theme = new();
    protected ScenarioGameplayLayout _scenarioLayout = new();

    protected Label? _topBarScenarioLabel;
    protected Label? _topBarAreaLabel;
    protected VBoxContainer? _regionTreeContainer;
    protected HBoxContainer? _tabBar;
    protected Control? _pageHost;
    protected Control? _currentRegionPage;
    protected Label? _currentRegionTitleLabel;
    protected Label? _currentRegionHintLabel;
    protected VBoxContainer? _currentRegionSceneList;
    protected InventoryPanel? _inventoryPanel;
    protected SkillPanel? _skillPanel;
    protected DictionaryPanel? _dictionaryPanel;
    protected SystemPanel? _systemPanel;
    protected ConfigurableInfoPanel? _infoPanel;
    protected CharacterStatusPanel? _statusPanel;
    protected LogPanel? _collapsedLogPanel;
    protected LogPanel? _expandedLogPanel;
    protected PanelContainer? _collapsedLogDock;
    protected Control? _expandedLogOverlay;

    protected EventDialogPanel? _eventDialogPanel;
    protected SaveSlotDialog? _storySaveDialog;
    protected SaveSlotDialog? _storyLoadDialog;
    protected ConfirmActionDialog? _confirmDialog;

    protected readonly Dictionary<string, bool> _regionExpandedStates = new(StringComparer.Ordinal);
    protected readonly Dictionary<string, EventButtonItem> _eventButtonWidgets = new(StringComparer.Ordinal);
    protected string _loadedScenarioId = string.Empty;
    private bool _isLogExpanded;
    private SignalBus? _signalBus;
    private bool _pendingDeferredFullRefresh;
    private double _progressVisualAccumulator;

    public override void _Ready()
    {
        LoadLayoutSettings();
        _gameManager = GameManager.Instance;
        EnsureStructure();
        ConnectSignalBus();
        RefreshAllPanels();
    }

    public void RefreshAllPanels()
    {
        _gameManager ??= GameManager.Instance;
        EnsureStructure();

        if (_gameManager == null)
        {
            return;
        }

        SyncScenarioSpecificUi();
        RefreshTopBar();
        RefreshLeftRegionTree();
        RefreshTabBar();
        RefreshCurrentRegionPage();
        RefreshCenterTabs();
        RefreshInfoPanel();
        RefreshStatusAndLogs();
    }

    public override void _ExitTree()
    {
        DisconnectSignalBus();
    }

    public override void _Process(double delta)
    {
        _progressVisualAccumulator += delta;
        if (_progressVisualAccumulator < 0.05)
        {
            return;
        }

        _progressVisualAccumulator = 0.0;
        UpdateLiveProgressVisuals();
    }

    protected void LoadLayoutSettings()
    {
        _layoutSettings = ResourceLoader.Load<MainUiLayoutSettings>(LayoutSettingsPath) ?? new MainUiLayoutSettings();
        _theme = GameplayUiConfigLoader.LoadTheme();
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
        _eventDialogPanel.Configure(_layoutSettings);
        AddChild(_eventDialogPanel);

        _storySaveDialog = new SaveSlotDialog { Name = "StorySaveDialog" };
        _storySaveDialog.Configure(_layoutSettings);
        AddChild(_storySaveDialog);

        _storyLoadDialog = new SaveSlotDialog { Name = "StoryLoadDialog" };
        _storyLoadDialog.Configure(_layoutSettings);
        AddChild(_storyLoadDialog);

        _confirmDialog = new ConfirmActionDialog { Name = "ConfirmDialog" };
        _confirmDialog.Configure(_layoutSettings);
        AddChild(_confirmDialog);
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
        _topBarScenarioLabel.AddThemeColorOverride("font_color", new Color("#f7f3e8"));
        _topBarScenarioLabel.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize + 2);
        content.AddChild(_topBarScenarioLabel);

        _topBarAreaLabel = new Label
        {
            Name = "TopBarAreaLabel",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _topBarAreaLabel.AddThemeColorOverride("font_color", new Color("#d9e8bf"));
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

        Label title = new()
        {
            Text = "区域",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", new Color("#f4e4bf"));
        title.AddThemeFontSizeOverride("font_size", _theme.RegionHeaderFontSize);
        content.AddChild(title);

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

        _inventoryPanel = new InventoryPanel { Name = _theme.InventoryTabText };
        _inventoryPanel.Configure(_gameManager!);
        _inventoryPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_inventoryPanel);

        _skillPanel = new SkillPanel { Name = _theme.SkillsTabText };
        _skillPanel.Configure(_gameManager!, TryLevelUpSkill);
        _skillPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_skillPanel);

        _dictionaryPanel = new DictionaryPanel { Name = _theme.DictionaryTabText };
        _dictionaryPanel.Configure(_gameManager!);
        _dictionaryPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _pageHost.AddChild(_dictionaryPanel);

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
        _currentRegionTitleLabel.AddThemeColorOverride("font_color", new Color("#f7f0db"));
        _currentRegionTitleLabel.AddThemeFontSizeOverride("font_size", _theme.SceneTitleFontSize + 2);
        root.AddChild(_currentRegionTitleLabel);

        _currentRegionHintLabel = new Label
        {
            Name = "CurrentRegionHintLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _currentRegionHintLabel.AddThemeColorOverride("font_color", new Color("#c8d0cf"));
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
        _currentRegionSceneList.AddThemeConstantOverride("separation", _theme.InnerGap + 8);
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
        logContent.AddChild(logHeader);

        Label logTitle = new()
        {
            Text = "运行日志",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        logTitle.AddThemeColorOverride("font_color", new Color("#f8fafc"));
        logTitle.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize + 1);
        logHeader.AddChild(logTitle);

        Button expandButton = new()
        {
            Name = "ExpandLogButton",
            Text = "展开",
            CustomMinimumSize = new Vector2(90, 34)
        };
        expandButton.AddThemeStyleboxOverride("normal", CreateTabStyle(false));
        expandButton.AddThemeStyleboxOverride("hover", CreateTabStyle(true));
        expandButton.Pressed += () => SetLogExpanded(true);
        logHeader.AddChild(expandButton);

        _collapsedLogPanel = new LogPanel
        {
            Name = "CollapsedLogPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _collapsedLogPanel.SetDisplayMode(false);
        logContent.AddChild(_collapsedLogPanel);

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
        _infoPanel.Configure(_layoutSettings);
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
        dock.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.BottomLog));
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
        title.AddThemeColorOverride("font_color", new Color("#f8fafc"));
        title.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize + 2);
        header.AddChild(title);

        Button collapseButton = new()
        {
            Name = "CollapseLogButton",
            Text = "收起",
            CustomMinimumSize = new Vector2(90, 34)
        };
        collapseButton.AddThemeStyleboxOverride("normal", CreateTabStyle(false));
        collapseButton.AddThemeStyleboxOverride("hover", CreateTabStyle(true));
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
        SyncLogVisibility();
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

    private void ConnectSignalBus()
    {
        _signalBus = GetNodeOrNull<SignalBus>("/root/SignalBus");
        if (_signalBus == null)
        {
            return;
        }

        _signalBus.ActiveIdleEventChanged += OnActiveIdleEventChanged;
    }

    private void DisconnectSignalBus()
    {
        if (_signalBus == null)
        {
            return;
        }

        _signalBus.ActiveIdleEventChanged -= OnActiveIdleEventChanged;
        _signalBus = null;
    }

    private void OnActiveIdleEventChanged(string eventId)
    {
        RequestEventDrivenRefresh();
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

    protected void FlushDeferredRefreshIfNeeded()
    {
        if (!_pendingDeferredFullRefresh)
        {
            return;
        }

        _pendingDeferredFullRefresh = false;
        RefreshAllPanels();
    }

    protected void UpdateLiveProgressVisuals()
    {
        if (_gameManager?.IdleSystem == null)
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

            bool isRunning = _gameManager.IdleSystem.IsRunningEvent(eventId);
            double progressRatio = isRunning ? _gameManager.IdleSystem.GetProgressRatio(eventId) : 0.0;
            widget.SetLiveProgress(progressRatio, isRunning);
        }

        string activeEventId = _gameManager.PlayerProfile.IdleState.ActiveEventId;
        bool isIdleRunning = _gameManager.PlayerProfile.IdleState.IsRunning && !string.IsNullOrWhiteSpace(activeEventId);
        double targetRatio = isIdleRunning ? _gameManager.IdleSystem.GetProgressRatio(activeEventId) : 0.0;
        _statusPanel?.UpdateTargetProgress(targetRatio, isIdleRunning);
    }
}
