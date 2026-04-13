using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;

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
            _regionExpandedStates.Clear();
            foreach (PrimaryRegionLayout region in _scenarioLayout.Regions)
            {
                _regionExpandedStates[region.Id] = region.ExpandedByDefault;
            }
        }

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
        Control? existingPage = _pageHost.GetNodeOrNull<Control>(_theme.SystemTabText);

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
            }
        }
        else if (existingPage != null)
        {
            _pageHost.RemoveChild(existingPage);
            existingPage.QueueFree();
            _systemPanel = null;
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

        string scenarioName = _gameManager.ActiveScenario?.DisplayName ?? "未载入剧本";
        _topBarScenarioLabel.Text = string.Format(CultureInfo.InvariantCulture, _theme.TopBarScenarioFormat, scenarioName);

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
            ? new Color("#ebe2c1") { A = 0.16f }
            : new Color(1, 1, 1, 0.04f)));

        HBoxContainer row = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);

        Label marker = new()
        {
            Text = isRunning ? _theme.IdleMarkerText : " ",
            CustomMinimumSize = new Vector2(18, 0)
        };
        marker.AddThemeFontSizeOverride("font_size", _theme.RegionItemFontSize);
        marker.AddThemeColorOverride("font_color", new Color("#ef4444"));
        row.AddChild(marker);

        Button button = new()
        {
            Text = area.Title,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltipText,
            CustomMinimumSize = new Vector2(0, 38)
        };
        button.AddThemeFontSizeOverride("font_size", accent.FontSize);
        button.AddThemeColorOverride("font_color", accent.TextColor);
        button.AddThemeColorOverride("font_hover_color", accent.TextColor.Lightened(0.1f));
        button.AddThemeStyleboxOverride("normal", CreateFlatStyle(Colors.Transparent));
        button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1, 1, 1, 0.04f)));
        button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color(1, 1, 1, 0.07f)));
        BindHoverInfo(button, area.Title, tooltipText);
        button.Pressed += () => SelectArea(area.Id);
        row.AddChild(button);

        if (interactionCount > 0)
        {
            Label countLabel = new()
            {
                Text = string.Format(CultureInfo.InvariantCulture, _theme.InteractionCountFormat, interactionCount)
            };
            countLabel.AddThemeFontSizeOverride("font_size", _theme.RegionCountFontSize);
            countLabel.AddThemeColorOverride("font_color", new Color("#9ad9a4"));
            row.AddChild(countLabel);
        }

        return panel;
    }

    private void SelectArea(string areaId)
    {
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.SelectedAreaId = areaId;
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
        if (_pageHost == null || _gameManager == null)
        {
            return;
        }

        string activePageName = GetTabText(_gameManager.PlayerProfile.UiState.SelectedTabId);
        foreach (Node child in _pageHost.GetChildren())
        {
            if (child is Control page)
            {
                page.Visible = string.Equals(page.Name, activePageName, StringComparison.Ordinal);
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
        _currentRegionHintLabel.Text = _theme.FavoriteHintText;

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
        content.AddThemeConstantOverride("separation", 10);
        card.AddChild(content);

        bool isFavorite = _gameManager?.PlayerProfile.UiState.IsSceneFavorited(scene.Id) == true;
        string hoverDescription = GetSceneDescriptionText(scene);

        PanelContainer headerPanel = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = hoverDescription,
            MouseFilter = MouseFilterEnum.Stop
        };
        headerPanel.AddThemeStyleboxOverride("panel", CreateFlatStyle(new Color(1, 1, 1, 0.30f)));
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

        MarginContainer headerMargin = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        headerMargin.AddThemeConstantOverride("margin_left", 8);
        headerMargin.AddThemeConstantOverride("margin_top", 6);
        headerMargin.AddThemeConstantOverride("margin_right", 8);
        headerMargin.AddThemeConstantOverride("margin_bottom", 6);
        headerPanel.AddChild(headerMargin);

        Label header = new()
        {
            Text = isFavorite ? $"● {scene.Title}" : scene.Title,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        header.AddThemeFontSizeOverride("font_size", _theme.SceneTitleFontSize);
        header.AddThemeColorOverride("font_color", isFavorite ? new Color("#f6d77e") : new Color("#f7f2e6"));
        headerMargin.AddChild(header);
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
}
