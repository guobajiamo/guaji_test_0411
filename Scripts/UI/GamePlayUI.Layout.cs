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
    private static readonly Color FarmingGroupPrimaryColor = new("#9b7a30");
    private static readonly Color FarmingSkillSecondaryColor = new("#6f5317");
    private static readonly Color CollectionGroupPrimaryColor = new("#985252");
    private static readonly Color CollectionSkillSecondaryColor = new("#6f2a2a");
    private static readonly Color CraftGroupPrimaryColor = new("#4f6f95");
    private static readonly Color CraftSkillSecondaryColor = new("#2c4d74");
    private static readonly Color DefaultGroupPrimaryColor = new("#4f5f8a");
    private static readonly Color DefaultSkillSecondaryColor = new("#626f8f");

    private sealed record SkillGroupView(
        string GroupId,
        string GroupName,
        int GroupOrder,
        List<SkillDefinition> Skills);

    private sealed record SkillRelatedSceneEntry(
        SecondaryAreaLayout Area,
        SceneLayout Scene,
        List<EventDefinition> Events);

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
            _skillGroupExpandedStates.Clear();
            foreach (PrimaryRegionLayout region in _scenarioLayout.Regions)
            {
                _regionExpandedStates[region.Id] = region.ExpandedByDefault;
            }
        }

        ApplyScenarioTabDefinitions();
        EnsureAreaSelection();
        EnsureSkillSelection();
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

    protected void EnsureSkillSelection()
    {
        if (_gameManager == null)
        {
            return;
        }

        PlayerUiState uiState = _gameManager.PlayerProfile.UiState;
        List<SkillDefinition> learnedSkills = GetOrderedLearnedSkills();
        if (learnedSkills.Count == 0)
        {
            uiState.SelectedSkillId = string.Empty;
            return;
        }

        if (learnedSkills.Any(skill => string.Equals(skill.Id, uiState.SelectedSkillId, StringComparison.Ordinal)))
        {
            return;
        }

        uiState.SelectedSkillId = learnedSkills[0].Id;
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
                    RequestReturnToMainMenu,
                    true,
                    IsUsingStitchUiTheme(),
                    UseStitchUiTheme,
                    UseLegacyUiTheme);
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
        RefreshLeftSidebarModeHeader();

        if (_gameManager.PlayerProfile.UiState.IsSkillSidebarMode)
        {
            BuildSkillTree();
            return;
        }

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

    private void BuildSkillTree()
    {
        if (_regionTreeContainer == null || _gameManager == null)
        {
            return;
        }

        List<SkillGroupView> groups = GetVisibleSkillGroups();
        if (groups.Count == 0)
        {
            _regionTreeContainer.AddChild(CreateHintLabel("尚未习得可显示技能。"));
            return;
        }

        foreach (SkillGroupView group in groups)
        {
            if (!_skillGroupExpandedStates.ContainsKey(group.GroupId))
            {
                _skillGroupExpandedStates[group.GroupId] = true;
            }

            _regionTreeContainer.AddChild(BuildSkillGroupNode(group));
            if (!_skillGroupExpandedStates.GetValueOrDefault(group.GroupId, true))
            {
                continue;
            }

            foreach (SkillDefinition skill in group.Skills)
            {
                _regionTreeContainer.AddChild(BuildSkillNode(skill));
            }
        }
    }

    private Control BuildSkillGroupNode(SkillGroupView group)
    {
        bool expanded = _skillGroupExpandedStates.GetValueOrDefault(group.GroupId, true);
        bool isStitchTheme = IsUsingStitchUiTheme();
        int fontSize = isStitchTheme
            ? ResolveSkillGroupFontSize()
            : Math.Max(16, _theme.RegionItemFontSize + 1);

        Button button = new()
        {
            Text = $"{(expanded ? "[-]" : "[+]")} {group.GroupName}",
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = $"该分组下已习得技能：{group.Skills.Count}",
            CustomMinimumSize = new Vector2(0, isStitchTheme ? Math.Max(50, fontSize + 18) : 42)
        };

        button.AddThemeFontSizeOverride("font_size", fontSize);
        if (isStitchTheme)
        {
            (Color primaryColor, Color secondaryColor) = GetSkillGroupColors(group.GroupId);
            button.AddThemeColorOverride("font_color", primaryColor);
            button.AddThemeColorOverride("font_hover_color", primaryColor.Darkened(0.1f));
            button.AddThemeColorOverride("font_pressed_color", primaryColor.Darkened(0.16f));
            button.AddThemeStyleboxOverride("normal", CreateSidebarButtonStyle(new Color("#e7e8e0"), secondaryColor, 2));
            button.AddThemeStyleboxOverride("hover", CreateSidebarButtonStyle(new Color("#dde2da"), primaryColor, 2));
            button.AddThemeStyleboxOverride("pressed", CreateSidebarButtonStyle(new Color("#d2dad2"), primaryColor.Darkened(0.12f), 3));
        }
        else
        {
            button.AddThemeColorOverride("font_color", new Color("#ffe38a"));
            button.AddThemeColorOverride("font_hover_color", new Color("#fff3b5"));
            button.AddThemeStyleboxOverride("normal", CreateFlatStyle(new Color(1, 1, 1, 0.03f)));
            button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1, 1, 1, 0.07f)));
            button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color(1, 1, 1, 0.10f)));
        }

        BindHoverInfo(button, group.GroupName, button.TooltipText);
        button.Pressed += () =>
        {
            _skillGroupExpandedStates[group.GroupId] = !expanded;
            RefreshLeftRegionTree();
        };

        return button;
    }

    private Control BuildSkillNode(SkillDefinition skill)
    {
        if (_gameManager == null)
        {
            return new Control();
        }

        bool isSelected = string.Equals(_gameManager.PlayerProfile.UiState.SelectedSkillId, skill.Id, StringComparison.Ordinal);
        bool isStitchTheme = IsUsingStitchUiTheme();
        int fontSize = isStitchTheme ? ResolveSkillNodeFontSize() : _theme.RegionItemFontSize;

        PlayerSkillState state = _gameManager.PlayerProfile.GetOrCreateSkillState(skill.Id);
        string skillName = Translate(skill.NameKey);
        string tooltipText = $"{skillName}\n等级：{state.Level}/{Math.Max(1, skill.MaxLevel)}\n{Translate(skill.DescriptionKey)}";

        Button button = new()
        {
            Text = $"- {skillName}",
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltipText,
            CustomMinimumSize = new Vector2(0, isStitchTheme ? Math.Max(50, fontSize + 18) : 40)
        };

        button.AddThemeFontSizeOverride("font_size", fontSize);
        if (isStitchTheme)
        {
            (Color primaryColor, Color secondaryColor) = GetSkillGroupColors(skill.GroupId);
            Color selectedBackground = primaryColor;
            selectedBackground.A = 0.18f;
            Color selectedHoverBackground = primaryColor;
            selectedHoverBackground.A = 0.26f;
            button.AddThemeColorOverride("font_color", isSelected ? primaryColor : secondaryColor);
            button.AddThemeColorOverride("font_hover_color", primaryColor.Darkened(0.08f));
            button.AddThemeColorOverride("font_pressed_color", primaryColor.Darkened(0.15f));
            button.AddThemeStyleboxOverride("normal", isSelected
                ? CreateSidebarButtonStyle(selectedBackground, primaryColor, 2)
                : CreateSidebarButtonStyle(new Color("#f1f1eb"), new Color("#c2c8be"), 2));
            button.AddThemeStyleboxOverride("hover", isSelected
                ? CreateSidebarButtonStyle(selectedHoverBackground, primaryColor.Darkened(0.10f), 3)
                : CreateSidebarButtonStyle(new Color("#e4e7de"), primaryColor, 2));
            button.AddThemeStyleboxOverride("pressed", isSelected
                ? CreateSidebarButtonStyle(selectedHoverBackground, primaryColor.Darkened(0.18f), 3)
                : CreateSidebarButtonStyle(new Color("#d8ddd4"), primaryColor.Darkened(0.14f), 3));
        }
        else
        {
            button.AddThemeColorOverride("font_color", isSelected ? new Color("#f3f9ff") : new Color("#d8e8ff"));
            button.AddThemeColorOverride("font_hover_color", new Color("#ffffff"));
            button.AddThemeStyleboxOverride("normal", CreateFlatStyle(isSelected
                ? new Color("#22498d") { A = 0.34f }
                : new Color(1, 1, 1, 0.04f)));
            button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1, 1, 1, 0.08f)));
            button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color("#22498d") { A = 0.40f }));
        }

        BindHoverInfo(button, skillName, tooltipText);
        button.Pressed += () => SelectSkill(skill.Id);
        if (!isStitchTheme)
        {
            return button;
        }

        MarginContainer indentContainer = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        indentContainer.AddThemeConstantOverride("margin_left", ResolveSkillNodeIndent());
        indentContainer.AddChild(button);
        return indentContainer;
    }

    private int ResolveSkillGroupFontSize()
    {
        int primaryRegionFont = _theme.GetRegionAccent("primary_story").FontSize;
        return Math.Max(Math.Max(18, _theme.RegionItemFontSize + 1), primaryRegionFont);
    }

    private int ResolveSkillNodeFontSize()
    {
        int secondaryRegionFont = _theme.GetRegionAccent("secondary_peace").FontSize;
        return Math.Max(Math.Max(16, _theme.RegionItemFontSize), secondaryRegionFont);
    }

    private int ResolveSkillNodeIndent()
    {
        return Math.Max(20, _theme.RegionItemFontSize + 4);
    }

    private (Color Primary, Color Secondary) GetSkillGroupColors(string? groupId)
    {
        string normalizedGroupId = string.IsNullOrWhiteSpace(groupId) ? "__ungrouped" : groupId.Trim().ToLowerInvariant();
        return normalizedGroupId switch
        {
            "farming" => (FarmingGroupPrimaryColor, FarmingSkillSecondaryColor),
            "collection" => (CollectionGroupPrimaryColor, CollectionSkillSecondaryColor),
            "craft" => (CraftGroupPrimaryColor, CraftSkillSecondaryColor),
            _ => (DefaultGroupPrimaryColor, DefaultSkillSecondaryColor)
        };
    }

    private StyleBoxFlat CreateSidebarButtonStyle(Color backgroundColor, Color borderColor, int borderWidth)
    {
        StyleBoxFlat style = CreateFlatStyle(backgroundColor);
        style.BorderColor = borderColor;
        style.BorderWidthLeft = borderWidth;
        style.BorderWidthTop = borderWidth;
        style.BorderWidthRight = borderWidth;
        style.BorderWidthBottom = borderWidth;
        style.ShadowColor = new Color(0, 0, 0, 0.12f);
        style.ShadowSize = borderWidth >= 3 ? 5 : 4;
        return style;
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

        Control root = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, Math.Max(42, accent.FontSize + 18)),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = tooltipText,
            ZIndex = isSelected ? 2 : 0
        };
        BindHoverInfo(root, area.Title, tooltipText);

        PanelContainer panel = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.AddThemeStyleboxOverride("panel", CreateFlatStyle(isSelected
            ? new Color("#22498d") { A = 0.34f }
            : new Color(1, 1, 1, 0.04f)));
        root.AddChild(panel);

        if (isSelected)
        {
            float outlineThickness = Math.Max(1.5f, _theme.CornerRadius / 10.0f);
            root.AddChild(CreateCornerBracketOverlay(new Color("#f6a53b"), outlineThickness));
        }

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

        root.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton mouseButton
                && mouseButton.ButtonIndex == MouseButton.Left
                && mouseButton.Pressed)
            {
                SelectArea(area.Id);
            }
        };

        return root;
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

    private void SelectSkill(string skillId)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(skillId))
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.SelectedSkillId = skillId;
        if (IsBattleSkillId(skillId))
        {
            _gameManager.PlayerProfile.UiState.ClearBattleSelection();
            _gameManager.PlayerProfile.UiState.SelectedTabId = TabBattle;
            PrepareBattleTabLayout();
        }
        else
        {
            _gameManager.PlayerProfile.UiState.SelectedTabId = TabCurrentRegion;
        }
        RefreshAllPanels();
    }

    private void SetLeftSidebarMode(bool isSkillMode)
    {
        if (_gameManager == null)
        {
            return;
        }

        PlayerUiState uiState = _gameManager.PlayerProfile.UiState;
        if (uiState.IsSkillSidebarMode == isSkillMode)
        {
            RefreshLeftSidebarModeHeader();
            return;
        }

        uiState.IsSkillSidebarMode = isSkillMode;
        if (isSkillMode)
        {
            EnsureSkillSelection();
        }
        else
        {
            EnsureAreaSelection();
        }

        uiState.SelectedTabId = TabCurrentRegion;
        RefreshAllPanels();
    }

    private void RefreshLeftSidebarModeHeader()
    {
        if (_leftSidebarRegionToggleButton == null || _leftSidebarSkillToggleButton == null)
        {
            return;
        }

        bool isSkillMode = _gameManager?.PlayerProfile.UiState.IsSkillSidebarMode == true;
        ApplyLeftSidebarToggleVisual(_leftSidebarRegionToggleButton, !isSkillMode);
        ApplyLeftSidebarToggleVisual(_leftSidebarSkillToggleButton, isSkillMode);

        if (_leftSidebarRegionModeNewLabel == null)
        {
            return;
        }

        bool shouldShowRegionMarker = isSkillMode
            && _gameManager?.PlayerProfile.UiState.AreaIdsWithNewMarker.Count > 0;
        _leftSidebarRegionModeNewLabel.Visible = shouldShowRegionMarker;
        _leftSidebarRegionModeNewLabel.SelfModulate = new Color(1, 1, 1, _newAreaMarkerVisible ? 1.0f : 0.0f);
        if (shouldShowRegionMarker && !_newAreaMarkerLabels.Contains(_leftSidebarRegionModeNewLabel))
        {
            _newAreaMarkerLabels.Add(_leftSidebarRegionModeNewLabel);
        }
    }

    private void ApplyLeftSidebarToggleVisual(Button button, bool isActive)
    {
        if (IsUsingStitchUiTheme())
        {
            button.AddThemeColorOverride("font_color", isActive ? new Color("#224545") : new Color("#4f5750"));
            button.AddThemeColorOverride("font_hover_color", isActive ? new Color("#183737") : new Color("#30332e"));
            button.AddThemeColorOverride("font_pressed_color", isActive ? new Color("#183737") : new Color("#30332e"));
            button.AddThemeStyleboxOverride("normal", isActive
                ? CreateSidebarButtonStyle(new Color("#c5eae9"), new Color("#8db8b6"), 3)
                : CreateSidebarButtonStyle(new Color("#e7e8e0"), new Color("#b5b8af"), 2));
            button.AddThemeStyleboxOverride("hover", isActive
                ? CreateSidebarButtonStyle(new Color("#b8dfde"), new Color("#7ea6a4"), 3)
                : CreateSidebarButtonStyle(new Color("#dde2da"), new Color("#9fa49a"), 2));
            button.AddThemeStyleboxOverride("pressed", isActive
                ? CreateSidebarButtonStyle(new Color("#acd5d3"), new Color("#678e8b"), 3)
                : CreateSidebarButtonStyle(new Color("#d1d6cd"), new Color("#8f9489"), 3));
            return;
        }

        button.AddThemeColorOverride("font_color", isActive ? new Color("#f3f9ff") : new Color("#93bbff"));
        button.AddThemeColorOverride("font_hover_color", new Color("#f3f9ff"));
        button.AddThemeStyleboxOverride("normal", CreateFlatStyle(isActive
            ? new Color("#22498d") { A = 0.34f }
            : new Color(1, 1, 1, 0.04f)));
        button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1, 1, 1, 0.08f)));
        button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color("#22498d") { A = 0.40f }));
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
        if (string.Equals(tabId, TabBattle, StringComparison.Ordinal))
        {
            PrepareBattleTabLayout();
        }

        RefreshAllPanels();
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

        ConfigureCurrentRegionOuterScrollForFarming(false);

        if (_gameManager?.PlayerProfile.UiState.IsSkillSidebarMode == true)
        {
            RefreshSkillModeCurrentRegionPage();
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

    private void RefreshSkillModeCurrentRegionPage()
    {
        if (_currentRegionTitleLabel == null || _currentRegionHintLabel == null || _currentRegionSceneList == null)
        {
            return;
        }

        _eventButtonWidgets.Clear();
        ClearContainer(_currentRegionSceneList);

        SkillDefinition? selectedSkill = GetSelectedSkillDefinition();
        if (selectedSkill == null)
        {
            _currentRegionTitleLabel.Text = "技能相关";
            _currentRegionHintLabel.Text = "尚未习得可显示技能。";
            _currentRegionHintLabel.Visible = true;
            _currentRegionSceneList.AddChild(CreateHintLabel("当前没有可显示的技能相关场景。"));
            return;
        }

        string skillName = Translate(selectedSkill.NameKey);
        _currentRegionTitleLabel.Text = $"{skillName}相关";
        if (IsFarmingSkill(selectedSkill))
        {
            RefreshFarmingSkillModeCurrentRegionPage(selectedSkill);
            return;
        }

        _currentRegionHintLabel.Text = "已聚合所有可见场景中的相关项目。";
        _currentRegionHintLabel.Visible = true;

        List<SkillRelatedSceneEntry> relatedScenes = GetSkillRelatedSceneEntries(selectedSkill.Id);
        if (relatedScenes.Count == 0)
        {
            _currentRegionSceneList.AddChild(CreateHintLabel($"当前没有可见的“{skillName}”相关项目。"));
            return;
        }

        foreach (SkillRelatedSceneEntry entry in relatedScenes)
        {
            _currentRegionSceneList.AddChild(BuildSceneCard(entry.Area, entry.Scene, entry.Events));
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

    private List<SkillDefinition> GetOrderedLearnedSkills()
    {
        if (_gameManager == null)
        {
            return new List<SkillDefinition>();
        }

        return _gameManager.SkillRegistry.Skills.Values
            .Where(skill => _gameManager.PlayerProfile.GetOrCreateSkillState(skill.Id).Level > 0)
            .OrderBy(skill => skill.GroupOrder)
            .ThenBy(skill => skill.GroupName, StringComparer.Ordinal)
            .ThenBy(skill => skill.SkillOrder)
            .ThenBy(skill => skill.SourceFileOrder)
            .ThenBy(skill => skill.SourceEntryOrder)
            .ThenBy(skill => skill.Id, StringComparer.Ordinal)
            .ToList();
    }

    private List<SkillGroupView> GetVisibleSkillGroups()
    {
        if (_gameManager == null)
        {
            return new List<SkillGroupView>();
        }

        return GetOrderedLearnedSkills()
            .GroupBy(skill => string.IsNullOrWhiteSpace(skill.GroupId) ? "__ungrouped" : skill.GroupId, StringComparer.Ordinal)
            .Select(group =>
            {
                SkillDefinition first = group.First();
                string groupNameText = string.IsNullOrWhiteSpace(first.GroupName)
                    ? "未分组技能"
                    : Translate(first.GroupName);
                return new SkillGroupView(
                    group.Key,
                    groupNameText,
                    first.GroupOrder,
                    group.ToList());
            })
            .OrderBy(group => group.GroupOrder)
            .ThenBy(group => group.GroupName, StringComparer.Ordinal)
            .ToList();
    }

    private SkillDefinition? GetSelectedSkillDefinition()
    {
        if (_gameManager == null)
        {
            return null;
        }

        EnsureSkillSelection();
        string selectedSkillId = _gameManager.PlayerProfile.UiState.SelectedSkillId;
        if (string.IsNullOrWhiteSpace(selectedSkillId))
        {
            return null;
        }

        SkillDefinition? definition = _gameManager.SkillRegistry.GetSkill(selectedSkillId);
        if (definition == null)
        {
            return null;
        }

        PlayerSkillState state = _gameManager.PlayerProfile.GetOrCreateSkillState(selectedSkillId);
        return state.Level > 0 ? definition : null;
    }

    private List<SkillRelatedSceneEntry> GetSkillRelatedSceneEntries(string skillId)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(skillId))
        {
            return new List<SkillRelatedSceneEntry>();
        }

        List<SkillRelatedSceneEntry> entries = new();
        foreach (PrimaryRegionLayout region in GetVisibleRegions())
        {
            foreach (SecondaryAreaLayout area in GetVisibleAreas(region))
            {
                foreach (SceneLayout scene in GetVisibleScenes(area))
                {
                    List<EventDefinition> relatedEvents = GetSceneEvents(scene)
                        .Where(definition => string.Equals(definition.LinkedSkillId, skillId, StringComparison.Ordinal))
                        .ToList();
                    if (relatedEvents.Count == 0)
                    {
                        continue;
                    }

                    entries.Add(new SkillRelatedSceneEntry(area, scene, relatedEvents));
                }
            }
        }

        return entries;
    }

    private Control BuildSceneCard(SecondaryAreaLayout area, SceneLayout scene, IReadOnlyList<EventDefinition>? sceneEventsOverride = null)
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
                ? (IsUsingStitchUiTheme() ? new Color("#eedfb3") : new Color("#fff0b3"))
                : (IsUsingStitchUiTheme() ? new Color("#bfcdc6") : new Color(1, 1, 1, 0.62f)),
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

        string headerText = isFavorite ? $"★ {scene.Title}" : scene.Title;
        int headerFontSize = _theme.SceneTitleFontSize - 1;
        Color topLightColor = IsUsingStitchUiTheme()
            ? (isFavorite ? new Color("#f8efcf") : new Color("#d6e1dc"))
            : (isFavorite ? new Color("#fff4cf") : new Color(1, 1, 1, 0.68f));
        Color baseTextColor = IsUsingStitchUiTheme()
            ? (isFavorite ? new Color("#6c532a") : new Color("#2f3b39"))
            : (isFavorite ? new Color("#ffe3a3") : new Color("#f7f8fc"));
        Color bottomShadowColor = IsUsingStitchUiTheme()
            ? (isFavorite ? new Color("#b19462") : new Color("#8ca097"))
            : (isFavorite ? new Color("#8b6d35") : new Color(0, 0, 0, 0.42f));

        titleLayer.AddChild(CreateSceneHeaderTextLayer(headerText, bottomShadowColor, 1, 1, headerFontSize));
        titleLayer.AddChild(CreateSceneHeaderTextLayer(headerText, topLightColor, -1, -1, headerFontSize));
        titleLayer.AddChild(CreateSceneHeaderTextLayer(headerText, baseTextColor, 0, 0, headerFontSize));

        ColorRect bottomShadeLine = new()
        {
            CustomMinimumSize = new Vector2(0, 2),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Color = isFavorite
                ? (IsUsingStitchUiTheme() ? new Color("#b79f73") : new Color("#92703c"))
                : (IsUsingStitchUiTheme() ? new Color("#99aba4") : new Color(0, 0, 0, 0.30f)),
            MouseFilter = MouseFilterEnum.Ignore
        };
        headerRoot.AddChild(bottomShadeLine);
        content.AddChild(headerPanel);

        IReadOnlyList<EventDefinition> events = sceneEventsOverride ?? GetSceneEvents(scene);
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
        item.BindEvent(
            data,
            eventId =>
            {
                SyncSelectedAreaFromEventAction(area.Id);
                OnEventButtonPressed(eventId);
            },
            _layoutSettings,
            _theme.ProgressBarHeight);

        if (item.ActionButton != null)
        {
            BindHoverInfo(item.ActionButton, data.DisplayName, data.TooltipText, definition.Id);
        }

        if (item.ProgressSlot != null)
        {
            BindHoverInfo(item.ProgressSlot, data.DisplayName, data.TooltipText, definition.Id);
        }

        _eventButtonWidgets[definition.Id] = item;
        return item;
    }

    private void SyncSelectedAreaFromEventAction(string areaId)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(areaId))
        {
            return;
        }

        if (string.Equals(_gameManager.PlayerProfile.UiState.SelectedAreaId, areaId, StringComparison.Ordinal))
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.SelectedAreaId = areaId;
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
        _battlePanel?.RefreshPanel();
        _equipmentPanel?.RefreshPanel();
        _questPanel?.RefreshQuests();
        _dictionaryPanel?.RefreshDictionary();
    }

    protected void RefreshInfoPanel()
    {
        if (_infoPanel == null || _gameManager == null)
        {
            return;
        }

        _infoPanel.RefreshPanel(_gameManager);
        if (string.Equals(_gameManager.PlayerProfile.UiState.SelectedTabId, TabBattle, StringComparison.Ordinal))
        {
            _infoPanel.SetPinnedContent(
                _battlePanel?.GetPinnedSummary() ?? string.Empty,
                _battlePanel?.GetPinnedContent() ?? string.Empty);
            return;
        }

        _infoPanel.ClearPinnedContent();
    }

    protected void RefreshStatusAndLogs()
    {
        _statusPanel?.RefreshStatus();
        IReadOnlyList<string> logs = _gameManager?.RuntimeLogs ?? Array.Empty<string>();
        _collapsedLogPanel?.SetMessages(logs);
        _expandedLogPanel?.SetMessages(logs);
    }

    private void BindHoverInfo(Control control, string summaryText, string contentText, string eventId = "")
    {
        string finalSummary = string.IsNullOrWhiteSpace(summaryText) ? "悬浮信息" : summaryText;
        string finalContent = string.IsNullOrWhiteSpace(contentText) ? HoverInfoFallbackText : contentText;
        control.SetMeta(NonBlockingTooltipEventIdMetaKey, eventId ?? string.Empty);
        control.SetMeta(NonBlockingTooltipSummaryMetaKey, finalSummary);
        control.SetMeta(NonBlockingTooltipTextMetaKey, finalContent);
        control.TooltipText = string.Empty;

        if (control.HasMeta(NonBlockingTooltipBoundMetaKey))
        {
            if (_activeHoverControl != null
                && ReferenceEquals(_activeHoverControl, control))
            {
                _infoPanel?.SetTransientContent(finalSummary, finalContent);
                ShowFloatingHoverTooltip(finalSummary, finalContent);
            }

            return;
        }

        control.SetMeta(NonBlockingTooltipBoundMetaKey, true);
        control.MouseEntered += () =>
        {
            string liveSummary = control.HasMeta(NonBlockingTooltipSummaryMetaKey)
                ? control.GetMeta(NonBlockingTooltipSummaryMetaKey).AsString()
                : finalSummary;
            string liveContent = control.HasMeta(NonBlockingTooltipTextMetaKey)
                ? control.GetMeta(NonBlockingTooltipTextMetaKey).AsString()
                : finalContent;

            if (string.IsNullOrWhiteSpace(liveSummary))
            {
                liveSummary = finalSummary;
            }

            if (string.IsNullOrWhiteSpace(liveContent))
            {
                liveContent = HoverInfoFallbackText;
            }

            control.SetMeta(NonBlockingTooltipSummaryMetaKey, liveSummary);
            control.SetMeta(NonBlockingTooltipTextMetaKey, liveContent);
            control.TooltipText = string.Empty;

            _activeHoverControl = control;
            _infoPanel?.SetTransientContent(liveSummary, liveContent);
            ShowFloatingHoverTooltip(liveSummary, liveContent);
        };
        control.MouseExited += () =>
        {
            if (_activeHoverControl != null
                && ReferenceEquals(_activeHoverControl, control))
            {
                _activeHoverControl = null;
            }

            _infoPanel?.ClearTransientContent();
            HideFloatingHoverTooltip();
            CallDeferred(nameof(FlushDeferredRefreshIfNeeded));
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
        if (_battlePanel == null || _equipmentPanel == null || _questPanel == null || _tutorialPanel == null || _achievementPanel == null)
        {
            return;
        }

        SyncTabPageTitle(TabCurrentRegion, _currentRegionPage);
        SyncTabPageTitle(TabInventory, _inventoryPanel);
        SyncTabPageTitle(TabSkills, _skillPanel);
        SyncTabPageTitle(TabBattle, _battlePanel);
        SyncTabPageTitle(TabEquipment, _equipmentPanel);
        SyncTabPageTitle(TabQuest, _questPanel);
        SyncTabPageTitle(TabTutorial, _tutorialPanel);
        SyncTabPageTitle(TabAchievement, _achievementPanel);
        SyncTabPageTitle(TabDictionary, _dictionaryPanel);
        SyncTabPageTitle(TabSystem, _systemPanel);

        _tutorialPanel.Configure(
            GetPlaceholderTitle(TabTutorial, "教程"),
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

    private bool IsBattleSkillId(string skillId)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(skillId))
        {
            return false;
        }

        SkillDefinition? definition = _gameManager.SkillRegistry.GetSkill(skillId);
        return string.Equals(definition?.GroupId, "battle", StringComparison.Ordinal);
    }

    private void PrepareBattleTabLayout()
    {
        SetLogExpanded(false);
        SetLogMinimized(true);
    }
}

