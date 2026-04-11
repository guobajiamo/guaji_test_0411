using Godot;
using System;
using System.Collections.Generic;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.UI.Placeholder;
using UiTabContainer = Test00_0410.UI.TabContainer;

namespace Test00_0410.UI;

/// <summary>
/// 主界面脚本。
/// 这里统一组织左侧事件区、中间标签页、右侧提示与状态栏，
/// 同时也负责窗口自适应和底部特殊功能按钮。
/// </summary>
public partial class MainUI : Control
{
    [Export]
    public string LayoutSettingsPath { get; set; } = "res://Resources/UI/main_ui_layout.tres";

    private GameManager? _gameManager;
    private MainUiLayoutSettings _layoutSettings = new();
    private float _refreshAccumulator;

    private ButtonListPanel? _oneshotPanel;
    private ButtonListPanel? _clickPanel;
    private ButtonListPanel? _idlePanel;
    private InventoryPanel? _inventoryPanel;
    private SkillPanel? _skillPanel;
    private LogPanel? _logPanel;
    private DictionaryPanel? _dictionaryPanel;
    private CharacterStatusPanel? _statusPanel;
    private FactionPanel? _factionPanel;
    private ZoneSelectPanel? _zoneSelectPanel;

    public override void _Ready()
    {
        _gameManager = GameManager.Instance;
        LoadLayoutSettings();
        ConfigureAdaptiveWindow();

        if (_gameManager == null)
        {
            Label errorLabel = new()
            {
                Text = "未找到 GameManager，主界面无法初始化。",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            errorLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(errorLabel);
            return;
        }

        BuildLayout();
        RefreshAllPanels();
    }

    public override void _Process(double delta)
    {
        if (_gameManager == null)
        {
            return;
        }

        _refreshAccumulator += (float)delta;
        if (_refreshAccumulator >= _layoutSettings.RefreshIntervalSeconds)
        {
            _refreshAccumulator = 0.0f;
            RefreshAllPanels();
        }
    }

    public void RefreshAllPanels()
    {
        if (_gameManager == null)
        {
            return;
        }

        _oneshotPanel?.RebuildButtons(BuildEventButtonData(ButtonListGroup.MainOneshot), OnClickEventPressed);
        _clickPanel?.RebuildButtons(BuildEventButtonData(ButtonListGroup.MainClick), OnClickEventPressed);
        _idlePanel?.RebuildButtons(BuildEventButtonData(ButtonListGroup.MainIdle), OnIdleEventPressed);

        _inventoryPanel?.RefreshInventory();
        _skillPanel?.RefreshSkills();
        _dictionaryPanel?.RefreshDictionary();
        _logPanel?.SetMessages(_gameManager.RuntimeLogs);
        _statusPanel?.RefreshStatus();
        _factionPanel?.RefreshFactions();
        _zoneSelectPanel?.RefreshZones();
    }

    private void LoadLayoutSettings()
    {
        MainUiLayoutSettings? loadedSettings = ResourceLoader.Load<MainUiLayoutSettings>(LayoutSettingsPath);
        _layoutSettings = loadedSettings ?? new MainUiLayoutSettings();
        _layoutSettings.RefreshIntervalSeconds = Mathf.Max(_layoutSettings.RefreshIntervalSeconds, 0.05f);

        if (loadedSettings == null)
        {
            _gameManager?.AddGameLog($"未找到 UI 布局资源：{LayoutSettingsPath}，已回退到脚本默认布局。");
        }
    }

    private void ConfigureAdaptiveWindow()
    {
        Window rootWindow = GetWindow();
        Vector2I designSize = new(_layoutSettings.WindowBaseWidth, _layoutSettings.WindowBaseHeight);

        rootWindow.Unresizable = false;
        rootWindow.Borderless = false;
        rootWindow.MinSize = designSize;
        rootWindow.ContentScaleSize = designSize;
        rootWindow.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        rootWindow.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
    }

    private void BuildLayout()
    {
        if (GetNodeOrNull("OuterMargin") != null)
        {
            return;
        }

        MarginContainer outerMargin = new()
        {
            Name = "OuterMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        outerMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        outerMargin.AddThemeConstantOverride("margin_left", _layoutSettings.OuterMargin);
        outerMargin.AddThemeConstantOverride("margin_top", _layoutSettings.OuterMargin);
        outerMargin.AddThemeConstantOverride("margin_right", _layoutSettings.OuterMargin);
        outerMargin.AddThemeConstantOverride("margin_bottom", _layoutSettings.OuterMargin);
        AddChild(outerMargin);

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", _layoutSettings.PanelSpacing);
        outerMargin.AddChild(root);

        Label titleLabel = new()
        {
            Name = "TitleLabel",
            Text = "文字挂机测试版",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", _layoutSettings.HeaderFontSize);
        root.AddChild(titleLabel);

        HBoxContainer contentRow = new()
        {
            Name = "ContentRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentRow.AddThemeConstantOverride("separation", _layoutSettings.PanelSpacing);
        root.AddChild(contentRow);

        VBoxContainer leftColumn = new()
        {
            Name = "LeftColumn",
            CustomMinimumSize = new Vector2(_layoutSettings.LeftColumnMinWidth, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        leftColumn.AddThemeConstantOverride("separation", _layoutSettings.PanelSpacing);
        contentRow.AddChild(leftColumn);

        Label eventSectionTitle = new()
        {
            Name = "EventSectionTitle",
            Text = "事件操作区"
        };
        eventSectionTitle.AddThemeFontSizeOverride("font_size", _layoutSettings.SectionHeaderFontSize);
        leftColumn.AddChild(eventSectionTitle);

        HBoxContainer eventColumnsRow = new()
        {
            Name = "EventColumnsRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        eventColumnsRow.AddThemeConstantOverride("separation", _layoutSettings.EventColumnSpacing);
        leftColumn.AddChild(eventColumnsRow);

        _oneshotPanel = CreateEventColumnPanel("一次性事件");
        eventColumnsRow.AddChild(_oneshotPanel);

        _clickPanel = CreateEventColumnPanel("单次点击事件");
        eventColumnsRow.AddChild(_clickPanel);

        _idlePanel = CreateEventColumnPanel("挂机事件");
        eventColumnsRow.AddChild(_idlePanel);

        Control leftSpacer = new()
        {
            Name = "LeftSpacer",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        leftColumn.AddChild(leftSpacer);

        VBoxContainer specialButtonSection = new()
        {
            Name = "SpecialButtonSection",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        specialButtonSection.AddThemeConstantOverride("separation", 6);
        leftColumn.AddChild(specialButtonSection);

        Label specialButtonTitle = new()
        {
            Name = "SpecialButtonTitle",
            Text = "特殊功能"
        };
        specialButtonTitle.AddThemeFontSizeOverride("font_size", _layoutSettings.SectionHeaderFontSize);
        specialButtonSection.AddChild(specialButtonTitle);

        HBoxContainer specialButtonRow = new()
        {
            Name = "SpecialButtonRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        specialButtonRow.AddThemeConstantOverride("separation", _layoutSettings.FooterButtonSpacing);
        specialButtonSection.AddChild(specialButtonRow);

        specialButtonRow.AddChild(CreateSpecialActionButton(
            "保存存档",
            "把当前进度写入系统默认存档目录，调试版和打包版都可共用。",
            OnSaveGamePressed));
        specialButtonRow.AddChild(CreateSpecialActionButton(
            "读取存档",
            "从系统默认存档目录读取进度。如果没有存档，会载入默认新档。",
            OnLoadGamePressed));

        VBoxContainer centerColumn = new()
        {
            Name = "CenterColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentRow.AddChild(centerColumn);

        UiTabContainer tabContainer = new()
        {
            Name = "MainTabs",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        centerColumn.AddChild(tabContainer);

        _inventoryPanel = new InventoryPanel { Name = "背包" };
        _inventoryPanel.Configure(_gameManager!);
        tabContainer.AddChild(_inventoryPanel);

        _skillPanel = new SkillPanel { Name = "技能" };
        _skillPanel.Configure(_gameManager!, OnSkillUpgradeRequested);
        tabContainer.AddChild(_skillPanel);

        _logPanel = new LogPanel { Name = "日志" };
        tabContainer.AddChild(_logPanel);

        _dictionaryPanel = new DictionaryPanel { Name = "图鉴" };
        _dictionaryPanel.Configure(_gameManager!);
        tabContainer.AddChild(_dictionaryPanel);

        _factionPanel = new FactionPanel { Name = "势力" };
        _factionPanel.Configure(_gameManager!);
        tabContainer.AddChild(_factionPanel);

        _zoneSelectPanel = new ZoneSelectPanel { Name = "区域" };
        _zoneSelectPanel.Configure(_gameManager!);
        tabContainer.AddChild(_zoneSelectPanel);

        tabContainer.AddChild(new QuestPanel { Name = "任务" });
        tabContainer.AddChild(new BattlePanel { Name = "战斗" });
        tabContainer.AddChild(new AchievementPanel { Name = "成就" });

        VBoxContainer rightColumn = new()
        {
            Name = "RightColumn",
            CustomMinimumSize = new Vector2(_layoutSettings.RightColumnMinWidth, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rightColumn.AddThemeConstantOverride("separation", _layoutSettings.PanelSpacing);
        contentRow.AddChild(rightColumn);

        Label tipLabel = new()
        {
            Name = "TipLabel",
            Text = "上手提示：\n1. 先点“捡起石斧”\n2. 再点“点击砍树”或“挂机砍树”\n3. 技能有经验后去“技能”页手动升级",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        tipLabel.AddThemeFontSizeOverride("font_size", _layoutSettings.BodyFontSize);
        rightColumn.AddChild(tipLabel);

        _statusPanel = new CharacterStatusPanel
        {
            Name = "角色状态",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _statusPanel.Configure(_gameManager!);
        rightColumn.AddChild(_statusPanel);
    }

    private ButtonListPanel CreateEventColumnPanel(string title)
    {
        ButtonListPanel panel = new()
        {
            CustomMinimumSize = new Vector2(_layoutSettings.EventColumnMinWidth, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.Configure(title, _layoutSettings);
        return panel;
    }

    private IEnumerable<EventButtonViewData> BuildEventButtonData(ButtonListGroup group)
    {
        if (_gameManager == null)
        {
            yield break;
        }

        foreach (EventDefinition definition in _gameManager.EventRegistry.GetEventsByGroup(group))
        {
            if (group == ButtonListGroup.MainIdle)
            {
                bool isRunning = _gameManager.IdleSystem?.IsRunningEvent(definition.Id) ?? false;
                bool canStart = _gameManager.IdleSystem?.CanStartIdleEvent(definition.Id) ?? false;
                if (!isRunning && !canStart)
                {
                    continue;
                }

                yield return new EventButtonViewData
                {
                    EventId = definition.Id,
                    DisplayName = isRunning ? $"停止：{_gameManager.TranslateText(definition.NameKey)}" : _gameManager.TranslateText(definition.NameKey),
                    Description = _gameManager.TranslateText(definition.DescriptionKey),
                    IsDisabled = false,
                    ProgressRatio = isRunning ? (_gameManager.IdleSystem?.GetProgressRatio(definition.Id) ?? 0.0) : 0.0
                };
                continue;
            }

            bool shouldShow = _gameManager.ClickEventSystem?.ShouldShowEvent(definition.Id) ?? false;
            if (!shouldShow)
            {
                continue;
            }

            yield return new EventButtonViewData
            {
                EventId = definition.Id,
                DisplayName = _gameManager.TranslateText(definition.NameKey),
                Description = _gameManager.TranslateText(definition.DescriptionKey),
                IsDisabled = !(_gameManager.ClickEventSystem?.CanTriggerEvent(definition.Id) ?? false),
                ProgressRatio = 0.0
            };
        }
    }

    private void OnClickEventPressed(string eventId)
    {
        if (_gameManager?.ClickEventSystem == null)
        {
            return;
        }

        string eventName = _gameManager.GetEventDisplayName(eventId);
        bool success = _gameManager.ClickEventSystem.TryTriggerEvent(eventId);
        _gameManager.AddGameLog(success
            ? $"执行事件成功：{eventName}"
            : $"执行事件失败：{eventName}");
        RefreshAllPanels();
    }

    private void OnIdleEventPressed(string eventId)
    {
        if (_gameManager?.IdleSystem == null)
        {
            return;
        }

        string eventName = _gameManager.GetEventDisplayName(eventId);

        if (_gameManager.IdleSystem.IsRunningEvent(eventId))
        {
            _gameManager.IdleSystem.StopIdleEvent();
            _gameManager.AddGameLog($"已停止挂机：{eventName}");
        }
        else if (_gameManager.IdleSystem.StartIdleEvent(eventId))
        {
            _gameManager.AddGameLog($"已开始挂机：{eventName}");
        }
        else
        {
            _gameManager.AddGameLog($"挂机启动失败：{eventName}");
        }

        RefreshAllPanels();
    }

    private void OnSkillUpgradeRequested(string skillId)
    {
        if (_gameManager?.SkillSystem == null)
        {
            return;
        }

        string skillName = _gameManager.TranslateText(_gameManager.SkillRegistry.GetSkill(skillId)?.NameKey ?? skillId);
        bool success = _gameManager.SkillSystem.TryLevelUp(skillId);
        _gameManager.AddGameLog(success
            ? $"技能升级成功：{skillName}"
            : $"技能升级失败：{skillName}");
        RefreshAllPanels();
    }

    private Button CreateSpecialActionButton(string text, string tooltipText, Action onPressed)
    {
        Button button = new()
        {
            Text = text,
            TooltipText = $"{tooltipText}\n当前存档路径：{RuntimePathHelper.SaveFilePath}",
            CustomMinimumSize = new Vector2(_layoutSettings.SpecialButtonMinWidth, _layoutSettings.SpecialButtonMinHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.AddThemeFontSizeOverride("font_size", _layoutSettings.BodyFontSize);
        button.AddThemeColorOverride("font_color", new Color("fff8eb"));
        button.AddThemeColorOverride("font_hover_color", new Color("fff8eb"));
        button.AddThemeColorOverride("font_pressed_color", new Color("fff8eb"));
        button.AddThemeStyleboxOverride("normal", CreateSpecialButtonStyle(new Color("2d6a4f")));
        button.AddThemeStyleboxOverride("hover", CreateSpecialButtonStyle(new Color("40916c")));
        button.AddThemeStyleboxOverride("pressed", CreateSpecialButtonStyle(new Color("1b4332")));
        button.AddThemeStyleboxOverride("disabled", CreateSpecialButtonStyle(new Color("6c757d")));
        button.Pressed += onPressed;
        return button;
    }

    private static StyleBoxFlat CreateSpecialButtonStyle(Color backgroundColor)
    {
        StyleBoxFlat styleBox = new()
        {
            BgColor = backgroundColor,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            BorderColor = new Color("143d2f"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2
        };
        styleBox.ContentMarginLeft = 12;
        styleBox.ContentMarginTop = 8;
        styleBox.ContentMarginRight = 12;
        styleBox.ContentMarginBottom = 8;
        return styleBox;
    }

    private void OnSaveGamePressed()
    {
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.SaveGame();
        RefreshAllPanels();
    }

    private void OnLoadGamePressed()
    {
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.LoadGame();
        RefreshAllPanels();
    }
}
