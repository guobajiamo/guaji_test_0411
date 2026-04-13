using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

public partial class GamePlayUI
{
    private const string HoverInfoFallbackText = "暂无说明。";

    protected void SyncScenarioSpecificUi()
    {
        if (_gameManager == null)
        {
            return;
        }

        string scenarioId = _gameManager.ActiveScenario?.ScenarioId ?? string.Empty;
        if (!string.Equals(_loadedScenarioId, scenarioId, StringComparison.Ordinal))
        {
            _loadedScenarioId = scenarioId;
            _scenarioLayout = GameplayUiConfigLoader.LoadScenarioLayout(_gameManager.ActiveScenario);
            _scenarioTabs.Clear();
            foreach ((string tabId, ScenarioTabDefinition definition) in GameplayUiConfigLoader.LoadScenarioTabs(_gameManager.ActiveScenario))
            {
                _scenarioTabs[tabId] = definition;
            }
            _regionExpandedStates.Clear();
            foreach (PrimaryRegionLayout region in _scenarioLayout.Regions)
            {
                _regionExpandedStates[region.Id] = region.ExpandedByDefault;
            }
        }

        ApplyScenarioTabDefinitions();
        EnsureAreaSelection();
        EnsureSystemPage();
        EnsureTabSelection();
        UpdatePageVisibility();
    }

    protected void EnsureAreaSelection()
    {
        if (_gameManager == null)
        {
            return;
        }

        if (_scenarioLayout.FindArea(_gameManager.PlayerProfile.UiState.SelectedAreaId) is SecondaryAreaLayout selectedArea
            && IsAreaVisible(selectedArea))
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.SelectedAreaId = ResolveVisibleDefaultAreaId();
    }

    protected void EnsureSystemPage()
    {
        if (_pageHost == null || _gameManager == null)
        {
            return;
        }

        bool shouldShowSystemTab = _gameManager.ActiveScenario?.EnableSystemTab == true;
        Control? existingPage = _tabPagesById.GetValueOrDefault(TabSystem);

        if (shouldShowSystemTab)
        {
            if (existingPage == null)
            {
                _systemPanel = new SystemPanel
                {
                    Name = _theme.SystemTabText
                };
                _systemPanel.SetAnchorsPreset(LayoutPreset.FullRect);
                _pageHost.AddChild(_systemPanel);
                _tabPagesById[TabSystem] = _systemPanel;
            }
            else
            {
                _systemPanel = existingPage as SystemPanel;
            }

            if (_systemPanel != null)
            {
                bool isTestScenario = _gameManager.ActiveScenario?.IsTestScenario == true;
                _systemPanel.Configure(
                    "系统",
                    isTestScenario
                        ? "测试剧本的一键存档与一键读档已统一收口到这里。"
                        : "正式剧本使用槽位存读档，也可以从这里返回主菜单。",
                    isTestScenario ? "一键存档" : "保存存档",
                    isTestScenario ? "一键读档" : "读取存档",
                    "返回主菜单",
                    isTestScenario ? SaveQuickTestGame : ShowStorySaveDialog,
                    isTestScenario ? ConfirmQuickLoadTestGame : ShowStoryLoadDialog,
                    RequestReturnToMainMenu);
                _systemPanel.Name = GetTabText(TabSystem);
            }
        }
        else if (existingPage != null)
        {
            _pageHost.RemoveChild(existingPage);
            existingPage.QueueFree();
            _systemPanel = null;
            _tabPagesById.Remove(TabSystem);
        }
    }

    protected void EnsureTabSelection()
    {
        if (_gameManager == null)
        {
            return;
        }

        List<string> availableTabs = GetAvailableTabIds();
        if (!availableTabs.Contains(_gameManager.PlayerProfile.UiState.SelectedTabId))
        {
            _gameManager.PlayerProfile.UiState.SelectedTabId = TabCurrentRegion;
        }
    }

    protected void RefreshTopBar()
    {
        if (_gameManager == null || _topBarScenarioLabel == null || _topBarAreaLabel == null)
        {
            return;
        }

        string mainQuestText = _gameManager.QuestSystem?.GetCurrentMainQuestLabel() ?? "暂无主线任务";
        _topBarScenarioLabel.Text = string.Format(CultureInfo.InvariantCulture, _theme.TopBarMainQuestFormat, mainQuestText);

        SecondaryAreaLayout? area = GetSelectedArea();
        _topBarAreaLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            _theme.TopBarAreaFormat,
            area?.Title ?? "未选择区域");
    }

    protected void RefreshLeftRegionTree()
    {
        if (_regionTreeContainer == null || _gameManager == null)
        {
            return;
        }

        _newAreaMarkerLabels.Clear();
        ClearContainer(_regionTreeContainer);

        foreach (PrimaryRegionLayout region in GetVisibleRegions())
        {
            _regionTreeContainer.AddChild(BuildPrimaryRegionNode(region));
            if (!_regionExpandedStates.GetValueOrDefault(region.Id, true))
            {
                continue;
            }

            foreach (SecondaryAreaLayout area in GetVisibleAreas(region))
            {
                _regionTreeContainer.AddChild(BuildSecondaryAreaNode(area));
            }
        }
    }

    private Control BuildPrimaryRegionNode(PrimaryRegionLayout region)
    {
        AccentVisual accent = _theme.GetRegionAccent(region.AccentId);
        string tooltipText = BuildPrimaryRegionTooltip(region);
        Button button = new()
        {
            Text = $"{(_regionExpandedStates.GetValueOrDefault(region.Id, true) ? "[-]" : "[+]")} {region.Title}",
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltipText,
            CustomMinimumSize = new Vector2(0, 42)
        };
        button.AddThemeFontSizeOverride("font_size", accent.FontSize);
        button.AddThemeColorOverride("font_color", accent.TextColor);
        button.AddThemeColorOverride("font_hover_color", accent.TextColor.Lightened(0.12f));
        button.AddThemeStyleboxOverride("normal", CreateFlatStyle(Colors.Transparent));
        button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1, 1, 1, 0.05f)));
        button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color(1, 1, 1, 0.08f)));
        BindHoverInfo(button, region.Title, tooltipText);
        button.Pressed += () =>
        {
            _regionExpandedStates[region.Id] = !_regionExpandedStates.GetValueOrDefault(region.Id, true);
            RefreshLeftRegionTree();
        };

        return button;
    }

    private Control BuildSecondaryAreaNode(SecondaryAreaLayout area)
    {
        bool isSelected = string.Equals(_gameManager?.PlayerProfile.UiState.SelectedAreaId, area.Id, StringComparison.Ordinal);
        bool isRunning = IsAreaRunningIdle(area);
        int interactionCount = GetInteractableCount(area);
        AccentVisual accent = _theme.GetRegionAccent(area.AccentId);
        string tooltipText = BuildSecondaryAreaTooltip(area, interactionCount, isRunning);

        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CreateFlatStyle(isSelected
            ? new Color("#22498d") { A = 0.34f }
            : new Color(1, 1, 1, 0.04f)));

        HBoxContainer row = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);

        Label marker = new()
        {
            Text = isRunning ? _theme.IdleMarkerText : " ",
            CustomMinimumSize = new Vector2(18, 0),
            MouseFilter = MouseFilterEnum.Ignore
        };
        marker.AddThemeFontSizeOverride("font_size", _theme.RegionItemFontSize);
        marker.AddThemeColorOverride("font_color", new Color("#ff6b98"));
        row.AddChild(marker);

        HBoxContainer contentRow = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        contentRow.AddThemeConstantOverride("separation", 5);
        row.AddChild(contentRow);

        panel.TooltipText = tooltipText;
        panel.MouseFilter = MouseFilterEnum.Stop;
        BindHoverInfo(panel, area.Title, tooltipText);

        Label titleLabel = new()
        {
            Text = area.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TooltipText = tooltipText,
            MouseFilter = MouseFilterEnum.Ignore
        };
        titleLabel.AddThemeFontSizeOverride("font_size", accent.FontSize);
        titleLabel.AddThemeColorOverride("font_color", accent.TextColor);
        contentRow.AddChild(titleLabel);

        if (_gameManager?.PlayerProfile.UiState.HasNewMarker(area.Id) == true)
        {
            Label newLabel = new()
            {
                Text = "New",
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            newLabel.AddThemeFontSizeOverride("font_size", Math.Max(11, _theme.RegionCountFontSize));
            newLabel.AddThemeColorOverride("font_color", new Color("#ff2d2d"));
            newLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
            newLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            newLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            newLabel.SelfModulate = new Color(1, 1, 1, _newAreaMarkerVisible ? 1.0f : 0.0f);
            contentRow.AddChild(newLabel);
            _newAreaMarkerLabels.Add(newLabel);
        }

        if (interactionCount > 0)
        {
            Label countLabel = new()
            {
                Text = string.Format(CultureInfo.InvariantCulture, _theme.InteractionCountFormat, interactionCount),
                MouseFilter = MouseFilterEnum.Ignore
            };
            countLabel.AddThemeFontSizeOverride("font_size", _theme.RegionCountFontSize);
            countLabel.AddThemeColorOverride("font_color", new Color("#63d8ff"));
            contentRow.AddChild(countLabel);
        }

        Control spacer = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(spacer);

        panel.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton mouseButton
                && mouseButton.ButtonIndex == MouseButton.Left
                && mouseButton.Pressed)
            {
                SelectArea(area.Id);
            }
        };

        return panel;
    }

    private void SelectArea(string areaId)
    {
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.SelectedAreaId = areaId;
        _gameManager.PlayerProfile.UiState.ClearNewMarker(areaId);
        _gameManager.PlayerProfile.UiState.SelectedTabId = TabCurrentRegion;
        RefreshAllPanels();
    }

    protected void RefreshTabBar()
    {
        if (_tabBar == null || _gameManager == null)
        {
            return;
        }

        ClearContainer(_tabBar);

        foreach (string tabId in GetAvailableTabIds())
        {
            bool isActive = string.Equals(_gameManager.PlayerProfile.UiState.SelectedTabId, tabId, StringComparison.Ordinal);
            Button button = new()
            {
                Text = GetTabText(tabId),
                CustomMinimumSize = new Vector2(0, isActive ? _theme.TabBarHeight + 6 : _theme.TabBarHeight)
            };
            button.AddThemeFontSizeOverride("font_size", isActive ? _theme.TabActiveFontSize : _theme.TabFontSize);
            button.AddThemeColorOverride("font_color", isActive ? _theme.Tabs.ActiveTextColor : _theme.Tabs.NormalTextColor);
            button.AddThemeColorOverride("font_hover_color", _theme.Tabs.ActiveTextColor);
            button.AddThemeStyleboxOverride("normal", CreateTabStyle(isActive));
            button.AddThemeStyleboxOverride("hover", CreateTabStyle(true));
            button.AddThemeStyleboxOverride("pressed", CreateTabStyle(true));
            button.Pressed += () => SetActiveTab(tabId);
            _tabBar.AddChild(button);
        }
    }

    private void SetActiveTab(string tabId)
    {
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.SelectedTabId = tabId;
        UpdatePageVisibility();
        RefreshTabBar();
    }

    protected void UpdatePageVisibility()
    {
        if (_gameManager == null)
        {
            return;
        }

        foreach ((string tabId, Control page) in _tabPagesById)
        {
            if (IsInstanceValid(page))
            {
                page.Visible = string.Equals(tabId, _gameManager.PlayerProfile.UiState.SelectedTabId, StringComparison.Ordinal);
            }
        }
    }

    protected void RefreshCurrentRegionPage()
    {
        if (_currentRegionTitleLabel == null || _currentRegionHintLabel == null || _currentRegionSceneList == null)
        {
            return;
        }

        SecondaryAreaLayout? area = GetSelectedArea();
        _currentRegionTitleLabel.Text = area?.Title ?? "当前区域";
        _currentRegionHintLabel.Text = string.Empty;
        _currentRegionHintLabel.Visible = false;

        _eventButtonWidgets.Clear();
        ClearContainer(_currentRegionSceneList);

        if (area == null || area.Scenes.Count == 0)
        {
            _currentRegionSceneList.AddChild(CreateHintLabel(_theme.EmptyAreaText));
            return;
        }

        List<SceneLayout> visibleScenes = GetOrderedScenes(area).ToList();
        if (visibleScenes.Count == 0)
        {
            _currentRegionSceneList.AddChild(CreateHintLabel(_theme.EmptySceneText));
            return;
        }

        foreach (SceneLayout scene in visibleScenes)
        {
            _currentRegionSceneList.AddChild(BuildSceneCard(area, scene));
        }
    }

    private IEnumerable<SceneLayout> GetOrderedScenes(SecondaryAreaLayout area)
    {
        if (_gameManager == null)
        {
            return GetVisibleScenes(area);
        }

        return GetVisibleScenes(area)
            .Select((scene, index) => new
            {
                Scene = scene,
                Index = index,
                FavoriteWeight = _gameManager.PlayerProfile.UiState.GetFavoriteSortWeight(scene.Id)
            })
            .OrderByDescending(item => item.FavoriteWeight >= 0)
            .ThenByDescending(item => item.FavoriteWeight)
            .ThenBy(item => item.Index)
            .Select(item => item.Scene)
            .ToList();
    }

    private Control BuildSceneCard(SecondaryAreaLayout area, SceneLayout scene)
    {
        PanelContainer card = new()
        {
            CustomMinimumSize = new Vector2(_theme.SceneCardMinWidth, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(_theme.SceneCard));

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);
        card.AddChild(content);

        bool isFavorite = _gameManager?.PlayerProfile.UiState.IsSceneFavorited(scene.Id) == true;
        string hoverDescription = GetSceneDescriptionText(scene);

        PanelContainer headerPanel = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = hoverDescription,
            MouseFilter = MouseFilterEnum.Stop
        };
        headerPanel.AddThemeStyleboxOverride("panel", CreateSceneHeaderStyle());
        BindHoverInfo(headerPanel, scene.Title, hoverDescription);
        headerPanel.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton mouseButton
                && mouseButton.ButtonIndex == MouseButton.Right
                && mouseButton.Pressed)
            {
                ToggleSceneFavorite(scene.Id);
            }
        };

        VBoxContainer headerRoot = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        headerRoot.AddThemeConstantOverride("separation", 0);
        headerPanel.AddChild(headerRoot);

        ColorRect topHighlightLine = new()
        {
            CustomMinimumSize = new Vector2(0, 2),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Color = isFavorite
                ? new Color("#fff0b3")
                : new Color(1, 1, 1, 0.62f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        headerRoot.AddChild(topHighlightLine);

        MarginContainer headerMargin = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        headerMargin.AddThemeConstantOverride("margin_left", 8);
        headerMargin.AddThemeConstantOverride("margin_top", 1);
        headerMargin.AddThemeConstantOverride("margin_right", 8);
        headerMargin.AddThemeConstantOverride("margin_bottom", 1);
        headerRoot.AddChild(headerMargin);

        Control titleLayer = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _theme.SceneTitleFontSize + 3),
            MouseFilter = MouseFilterEnum.Ignore
        };
        headerMargin.AddChild(titleLayer);

        string headerText = isFavorite ? $"● {scene.Title}" : scene.Title;
        int headerFontSize = _theme.SceneTitleFontSize - 1;
        Color topLightColor = isFavorite
            ? new Color("#fff4cf")
            : new Color(1, 1, 1, 0.68f);
        Color baseTextColor = isFavorite
            ? new Color("#ffe3a3")
            : new Color("#f7f8fc");
        Color bottomShadowColor = isFavorite
            ? new Color("#8b6d35")
            : new Color(0, 0, 0, 0.42f);

        titleLayer.AddChild(CreateSceneHeaderTextLayer(headerText, bottomShadowColor, 1, 1, headerFontSize));
        titleLayer.AddChild(CreateSceneHeaderTextLayer(headerText, topLightColor, -1, -1, headerFontSize));
        titleLayer.AddChild(CreateSceneHeaderTextLayer(headerText, baseTextColor, 0, 0, headerFontSize));

        ColorRect bottomShadeLine = new()
        {
            CustomMinimumSize = new Vector2(0, 2),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Color = isFavorite
                ? new Color("#92703c")
                : new Color(0, 0, 0, 0.30f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        headerRoot.AddChild(bottomShadeLine);
        content.AddChild(headerPanel);

        List<EventDefinition> events = GetSceneEvents(scene);
        if (events.Count == 0)
        {
            content.AddChild(CreateHintLabel(_theme.EmptySceneText));
            return card;
        }

        GridContainer grid = new()
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 10);
        content.AddChild(grid);

        foreach (EventDefinition definition in events)
        {
            grid.AddChild(BuildEventActionCell(area, scene, definition));
        }

        return card;
    }

    private void ToggleSceneFavorite(string sceneId)
    {
        if (_gameManager == null)
        {
            return;
        }

        bool nextValue = !_gameManager.PlayerProfile.UiState.IsSceneFavorited(sceneId);
        _gameManager.PlayerProfile.UiState.SetSceneFavorited(sceneId, nextValue);
        RefreshCurrentRegionPage();
    }

    private List<EventDefinition> GetSceneEvents(SceneLayout scene)
    {
        if (_gameManager == null)
        {
            return new List<EventDefinition>();
        }

        return scene.EventIds
            .Select((eventId, index) => new
            {
                Definition = _gameManager.EventRegistry.GetEvent(eventId),
                Index = index
            })
            .Where(item => item.Definition != null && ShouldShowEvent(item.Definition))
            .OrderBy(item => GetEventSortPriority(item.Definition!.Type))
            .ThenBy(item => item.Index)
            .Select(item => item.Definition!)
            .ToList();
    }

    private Control BuildEventActionCell(SecondaryAreaLayout area, SceneLayout scene, EventDefinition definition)
    {
        EventButtonViewData data = BuildEventButtonData(definition);
        EventButtonItem item = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill
        };
        item.BindEvent(data, OnEventButtonPressed, _layoutSettings, _theme.ProgressBarHeight);

        if (item.ActionButton != null)
        {
            BindHoverInfo(item.ActionButton, data.DisplayName, data.TooltipText);
        }

        if (item.ProgressSlot != null)
        {
            BindHoverInfo(item.ProgressSlot, data.DisplayName, data.TooltipText);
        }

        _eventButtonWidgets[definition.Id] = item;
        return item;
    }

    private static int GetEventSortPriority(EventType type)
    {
        return type switch
        {
            EventType.OneshotClick => 0,
            EventType.RepeatableClick => 1,
            EventType.IdleLoop => 2,
            _ => 99
        };
    }

    protected void RefreshCenterTabs()
    {
        _inventoryPanel?.RefreshInventory();
        _skillPanel?.RefreshSkills();
        _questPanel?.RefreshQuests();
        _dictionaryPanel?.RefreshDictionary();
    }

    protected void RefreshInfoPanel()
    {
        _infoPanel?.RefreshPanel(_gameManager!);
    }

    protected void RefreshStatusAndLogs()
    {
        _statusPanel?.RefreshStatus();
        IReadOnlyList<string> logs = _gameManager?.RuntimeLogs ?? Array.Empty<string>();
        _collapsedLogPanel?.SetMessages(logs);
        _expandedLogPanel?.SetMessages(logs);
    }

    private void BindHoverInfo(Control control, string summaryText, string contentText)
    {
        string finalContent = string.IsNullOrWhiteSpace(contentText) ? HoverInfoFallbackText : contentText;
        control.MouseEntered += () => _infoPanel?.SetTransientContent(summaryText, finalContent);
        control.MouseExited += () =>
        {
            _infoPanel?.ClearTransientContent();
            FlushDeferredRefreshIfNeeded();
        };
    }

    private string BuildPrimaryRegionTooltip(PrimaryRegionLayout region)
    {
        List<SecondaryAreaLayout> visibleAreas = GetVisibleAreas(region).ToList();
        if (visibleAreas.Count == 0)
        {
            return "这个一级区域下当前没有已显示的二级区域。";
        }

        string areaNames = string.Join("、", visibleAreas.Select(area => area.Title));
        return $"当前显示 {visibleAreas.Count} 个二级区域：{areaNames}";
    }

    private string BuildSecondaryAreaTooltip(SecondaryAreaLayout area, int interactionCount, bool isRunning)
    {
        List<SceneLayout> visibleScenes = GetVisibleScenes(area).ToList();
        List<string> lines = new()
        {
            $"子场景数：{visibleScenes.Count}",
            $"当前可互动内容：{interactionCount}"
        };

        if (visibleScenes.Count > 0)
        {
            lines.Add($"子场景：{string.Join("、", visibleScenes.Select(scene => scene.Title))}");
        }

        if (isRunning)
        {
            lines.Add("该区域当前有挂机项目正在运行。");
        }

        return string.Join("\n", lines);
    }

    private string GetSceneDescriptionText(SceneLayout scene)
    {
        return string.IsNullOrWhiteSpace(scene.Description)
            ? HoverInfoFallbackText
            : scene.Description;
    }

    private void UpdateAreaNewMarkers()
    {
        if (_gameManager == null)
        {
            return;
        }

        PlayerUiState uiState = _gameManager.PlayerProfile.UiState;
        string currentAreaId = _gameManager.PlayerProfile.UiState.SelectedAreaId;

        if (!string.IsNullOrWhiteSpace(currentAreaId))
        {
            uiState.ClearNewMarker(currentAreaId);
        }

        foreach (SecondaryAreaLayout area in GetVisibleRegions().SelectMany(GetVisibleAreas))
        {
            foreach (SceneLayout scene in GetVisibleScenes(area))
            {
                foreach (string eventId in scene.EventIds)
                {
                    EventDefinition? definition = _gameManager.EventRegistry.GetEvent(eventId);
                    if (definition == null || !ShouldShowEvent(definition) || IsEventDisabled(definition))
                    {
                        continue;
                    }

                    if (uiState.HasProcessedInteractableEvent(eventId))
                    {
                        continue;
                    }

                    uiState.MarkInteractableEventProcessed(eventId);
                    if (!string.Equals(area.Id, currentAreaId, StringComparison.Ordinal))
                    {
                        uiState.AddNewMarker(area.Id);
                        PrimaryRegionLayout? parentRegion = _scenarioLayout.Regions.FirstOrDefault(region => region.Areas.Contains(area));
                        if (parentRegion != null && !_regionExpandedStates.GetValueOrDefault(parentRegion.Id, true))
                        {
                            _regionExpandedStates[parentRegion.Id] = true;
                        }
                    }
                }
            }
        }
    }

    private void ApplyScenarioTabDefinitions()
    {
        if (_battlePanel == null || _questPanel == null || _tutorialPanel == null || _achievementPanel == null)
        {
            return;
        }

        SyncTabPageTitle(TabCurrentRegion, _currentRegionPage);
        SyncTabPageTitle(TabInventory, _inventoryPanel);
        SyncTabPageTitle(TabSkills, _skillPanel);
        SyncTabPageTitle(TabBattle, _battlePanel);
        SyncTabPageTitle(TabQuest, _questPanel);
        SyncTabPageTitle(TabTutorial, _tutorialPanel);
        SyncTabPageTitle(TabAchievement, _achievementPanel);
        SyncTabPageTitle(TabDictionary, _dictionaryPanel);
        SyncTabPageTitle(TabSystem, _systemPanel);

        _battlePanel.Configure(
            GetPlaceholderTitle(TabBattle, "战斗"),
            GetPlaceholderLines(TabBattle,
                "战斗系统尚未正式实装。",
                "这里已经预留了任务解锁所需的战斗属性接口，后续可以继续补战斗流程和数值规则。"),
            new Color("#ff8f9e"));

        _tutorialPanel.Configure(
            GetPlaceholderTitle(TabTutorial, "教学"),
            GetPlaceholderLines(TabTutorial,
                "教学页暂时保留为配置入口。",
                "后续可以把开局引导、快捷键提示和系统说明整理到这里。"),
            new Color("#7ce0ff"));

        _achievementPanel.Configure(
            GetPlaceholderTitle(TabAchievement, "成就"),
            GetPlaceholderLines(TabAchievement,
                "成就页当前只预留接口。",
                "任务奖励已经支持“解锁成就”效果，正式成就定义 YAML 可在后续继续补齐。"),
            new Color("#ffe58a"));

        _questPanel.ApplyTabDefinition(GetTabDefinition(TabQuest));
    }

    private void SyncTabPageTitle(string tabId, Control? page)
    {
        if (page != null)
        {
            page.Name = GetTabText(tabId);
        }
    }

    private ScenarioTabDefinition? GetTabDefinition(string tabId)
    {
        return _scenarioTabs.GetValueOrDefault(tabId);
    }

    private string GetPlaceholderTitle(string tabId, string fallback)
    {
        ScenarioTabDefinition? definition = GetTabDefinition(tabId);
        return string.IsNullOrWhiteSpace(definition?.PlaceholderTitle)
            ? fallback
            : definition.PlaceholderTitle;
    }

    private IReadOnlyList<string> GetPlaceholderLines(string tabId, params string[] fallback)
    {
        ScenarioTabDefinition? definition = GetTabDefinition(tabId);
        return definition?.ContentLines?.Count > 0
            ? definition.ContentLines
            : fallback;
    }
}
