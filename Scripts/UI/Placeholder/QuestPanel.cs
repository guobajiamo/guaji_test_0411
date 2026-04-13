using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Enums;
using Test00_0410.Systems;
using Test00_0410.UI;

namespace Test00_0410.UI.Placeholder;

/// <summary>
/// 任务页。
/// 左侧显示主线任务，右侧显示支线任务，下方详情区负责展示当前任务进度与领奖按钮。
/// </summary>
public partial class QuestPanel : Control
{
    private GameManager? _gameManager;
    private GameplayUiTheme _theme = new();
    private Func<string, GameplayActionContext?>? _actionContextResolver;
    private Action<string, string>? _showHoverInfo;
    private Action? _clearHoverInfo;
    private Action? _requestUiRefresh;

    private VBoxContainer? _introContainer;
    private VBoxContainer? _mainQuestList;
    private VBoxContainer? _sideQuestList;
    private Label? _detailTitleLabel;
    private RichTextLabel? _detailBodyLabel;
    private Button? _claimRewardButton;

    private string _selectedQuestId = string.Empty;
    private string _hoveredQuestId = string.Empty;
    private bool _isShiftPressed;
    private string _questTabTitle = "任务";

    public override void _Ready()
    {
        EnsureStructure();
    }

    public override void _Process(double delta)
    {
        bool isShiftPressed = Input.IsKeyPressed(Key.Shift);
        if (isShiftPressed == _isShiftPressed)
        {
            return;
        }

        _isShiftPressed = isShiftPressed;
        UpdateDetailPanel();
        UpdateHoverInfo();
    }

    public void Configure(
        GameManager gameManager,
        GameplayUiTheme theme,
        Func<string, GameplayActionContext?>? actionContextResolver,
        Action<string, string>? showHoverInfo,
        Action? clearHoverInfo,
        Action? requestUiRefresh)
    {
        _gameManager = gameManager;
        _theme = theme;
        _actionContextResolver = actionContextResolver;
        _showHoverInfo = showHoverInfo;
        _clearHoverInfo = clearHoverInfo;
        _requestUiRefresh = requestUiRefresh;
        EnsureStructure();
    }

    public void ApplyTabDefinition(ScenarioTabDefinition? definition)
    {
        EnsureStructure();

        _questTabTitle = string.IsNullOrWhiteSpace(definition?.Title) ? "任务" : definition.Title;

        if (_introContainer == null)
        {
            return;
        }

        foreach (Node child in _introContainer.GetChildren())
        {
            _introContainer.RemoveChild(child);
            child.QueueFree();
        }

        List<string> lines = definition != null && definition.ContentLines.Count > 0
            ? definition.ContentLines.ToList()
            : new List<string>
            {
                "这里会集中显示当前已解锁的主线与支线任务。",
                "鼠标悬浮任务按钮可同步查看任务说明与进度，按住 Shift 可展开已完成关键事件链。"
            };

        foreach (string line in lines)
        {
            Label label = new()
            {
                Text = line,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize - 1);
            label.AddThemeColorOverride("font_color", new Color("#f2e8da"));
            _introContainer.AddChild(label);
        }
    }

    public void RefreshQuests()
    {
        EnsureStructure();

        QuestSystem? questSystem = _gameManager?.QuestSystem;
        if (questSystem == null || _mainQuestList == null || _sideQuestList == null)
        {
            return;
        }

        IReadOnlyList<QuestProgressInfo> mainQuests = questSystem.GetVisibleQuests(QuestCategory.Main);
        IReadOnlyList<QuestProgressInfo> sideQuests = questSystem.GetVisibleQuests(QuestCategory.Side);
        HashSet<string> visibleQuestIds = mainQuests.Select(info => info.Definition.Id)
            .Concat(sideQuests.Select(info => info.Definition.Id))
            .ToHashSet(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(_selectedQuestId) && !visibleQuestIds.Contains(_selectedQuestId))
        {
            _selectedQuestId = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_hoveredQuestId) && !visibleQuestIds.Contains(_hoveredQuestId))
        {
            _hoveredQuestId = string.Empty;
            _clearHoverInfo?.Invoke();
        }

        RebuildQuestColumn(_mainQuestList, mainQuests, true);
        RebuildQuestColumn(_sideQuestList, sideQuests, false);

        if (string.IsNullOrWhiteSpace(_selectedQuestId))
        {
            _selectedQuestId = mainQuests.FirstOrDefault()?.Definition.Id
                ?? sideQuests.FirstOrDefault()?.Definition.Id
                ?? string.Empty;
        }

        UpdateDetailPanel();
        UpdateHoverInfo();
    }

    private void EnsureStructure()
    {
        if (_introContainer != null
            && _mainQuestList != null
            && _sideQuestList != null
            && _detailTitleLabel != null
            && _detailBodyLabel != null
            && _claimRewardButton != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);

        MarginContainer margin = new()
        {
            Name = "Margin"
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(margin);

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        _introContainer = new VBoxContainer
        {
            Name = "IntroContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _introContainer.AddThemeConstantOverride("separation", 4);
        root.AddChild(_introContainer);

        HBoxContainer columns = new()
        {
            Name = "QuestColumns",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        columns.AddThemeConstantOverride("separation", 12);
        root.AddChild(columns);

        columns.AddChild(BuildQuestColumn("主线任务", new Color("#cc4a54"), out _mainQuestList));
        columns.AddChild(BuildQuestColumn("支线任务", new Color("#4ca45a"), out _sideQuestList));

        PanelContainer detailPanel = new()
        {
            Name = "QuestDetailPanel",
            CustomMinimumSize = new Vector2(0, 220),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        detailPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color("#231a2e"), new Color("#f0b75f")));
        root.AddChild(detailPanel);

        VBoxContainer detailRoot = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detailRoot.AddThemeConstantOverride("separation", 10);
        detailPanel.AddChild(detailRoot);

        _detailTitleLabel = new Label
        {
            Name = "DetailTitleLabel",
            Text = _questTabTitle
        };
        _detailTitleLabel.AddThemeFontSizeOverride("font_size", _theme.SceneTitleFontSize);
        _detailTitleLabel.AddThemeColorOverride("font_color", new Color("#fff1d2"));
        detailRoot.AddChild(_detailTitleLabel);

        _detailBodyLabel = new RichTextLabel
        {
            Name = "DetailBodyLabel",
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _detailBodyLabel.AddThemeFontSizeOverride("normal_font_size", _theme.BodyFontSize - 1);
        _detailBodyLabel.AddThemeColorOverride("default_color", new Color("#f4e7db"));
        _detailBodyLabel.AddThemeConstantOverride("line_separation", 6);
        detailRoot.AddChild(_detailBodyLabel);

        _claimRewardButton = new Button
        {
            Name = "ClaimRewardButton",
            Text = "领取奖励",
            CustomMinimumSize = new Vector2(0, 42),
            Visible = false
        };
        _claimRewardButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color("#77521b"), new Color("#e0b253")));
        _claimRewardButton.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color("#91662b"), new Color("#ffd47f")));
        _claimRewardButton.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color("#a8742a"), new Color("#ffe19d")));
        _claimRewardButton.Pressed += OnClaimRewardPressed;
        detailRoot.AddChild(_claimRewardButton);
    }

    private Control BuildQuestColumn(string title, Color accentColor, out VBoxContainer listContainer)
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(accentColor.Darkened(0.72f), accentColor));

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        Label header = new()
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", _theme.RegionHeaderFontSize - 2);
        header.AddThemeColorOverride("font_color", accentColor.Lightened(0.35f));
        content.AddChild(header);

        ScrollContainer scroll = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        content.AddChild(scroll);

        listContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        listContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(listContainer);
        return panel;
    }

    private void RebuildQuestColumn(VBoxContainer listContainer, IReadOnlyList<QuestProgressInfo> quests, bool isMainQuestColumn)
    {
        foreach (Node child in listContainer.GetChildren())
        {
            listContainer.RemoveChild(child);
            child.QueueFree();
        }

        if (quests.Count == 0)
        {
            Label hint = new()
            {
                Text = "当前没有已解锁任务。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            hint.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize - 1);
            hint.AddThemeColorOverride("font_color", new Color("#d9d7e2"));
            listContainer.AddChild(hint);
            return;
        }

        foreach (QuestProgressInfo info in quests)
        {
            Button button = new()
            {
                Text = BuildQuestButtonText(info),
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 46),
                TooltipText = BuildQuestContent(info),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };

            Color baseColor = isMainQuestColumn ? new Color("#6d1f27") : new Color("#1f5c2c");
            Color borderColor = isMainQuestColumn ? new Color("#ff8e99") : new Color("#8df0a2");
            if (string.Equals(_selectedQuestId, info.Definition.Id, StringComparison.Ordinal))
            {
                baseColor = baseColor.Lightened(0.24f);
            }

            button.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize);
            button.AddThemeColorOverride("font_color", new Color("#fff8f2"));
            button.AddThemeColorOverride("font_hover_color", new Color("#fff8f2"));
            button.AddThemeStyleboxOverride("normal", CreateButtonStyle(baseColor, borderColor));
            button.AddThemeStyleboxOverride("hover", CreateButtonStyle(baseColor.Lightened(0.12f), borderColor.Lightened(0.16f)));
            button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(baseColor.Lightened(0.18f), borderColor.Lightened(0.24f)));

            string questId = info.Definition.Id;
            button.Pressed += () =>
            {
                _selectedQuestId = questId;
                UpdateDetailPanel();
            };
            button.MouseEntered += () =>
            {
                _hoveredQuestId = questId;
                UpdateDetailPanel();
                UpdateHoverInfo();
            };
            button.MouseExited += () =>
            {
                if (!string.Equals(_hoveredQuestId, questId, StringComparison.Ordinal))
                {
                    return;
                }

                _hoveredQuestId = string.Empty;
                _clearHoverInfo?.Invoke();
                UpdateDetailPanel();
            };
            listContainer.AddChild(button);
        }
    }

    private string BuildQuestButtonText(QuestProgressInfo info)
    {
        string suffix = info.CanClaimReward
            ? " [可领奖]"
            : info.State.IsCompleted
                ? " [已完成]"
                : string.Empty;
        return $"{info.Definition.Title}{suffix}";
    }

    private void UpdateDetailPanel()
    {
        if (_detailTitleLabel == null || _detailBodyLabel == null || _claimRewardButton == null)
        {
            return;
        }

        QuestProgressInfo? info = ResolveDisplayedQuest();
        if (info == null)
        {
            _detailTitleLabel.Text = _questTabTitle;
            _detailBodyLabel.Text = "当前没有可显示的任务。";
            _claimRewardButton.Visible = false;
            return;
        }

        _detailTitleLabel.Text = info.Definition.Title;
        _detailBodyLabel.Text = BuildQuestContent(info);
        _claimRewardButton.Visible = info.HasRewards;
        _claimRewardButton.Disabled = !info.CanClaimReward;
        _claimRewardButton.Text = info.CanClaimReward
            ? "领取奖励"
            : info.State.IsRewardClaimed
                ? "奖励已领取"
                : "奖励未完成";
    }

    private void UpdateHoverInfo()
    {
        if (_showHoverInfo == null)
        {
            return;
        }

        QuestProgressInfo? info = string.IsNullOrWhiteSpace(_hoveredQuestId)
            ? null
            : _gameManager?.QuestSystem?.GetProgressInfo(_hoveredQuestId);
        if (info == null)
        {
            return;
        }

        _showHoverInfo.Invoke(info.Definition.Title, BuildQuestContent(info));
    }

    private QuestProgressInfo? ResolveDisplayedQuest()
    {
        string questId = !string.IsNullOrWhiteSpace(_hoveredQuestId) ? _hoveredQuestId : _selectedQuestId;
        if (string.IsNullOrWhiteSpace(questId))
        {
            return null;
        }

        return _gameManager?.QuestSystem?.GetProgressInfo(questId);
    }

    private string BuildQuestContent(QuestProgressInfo info)
    {
        return _gameManager?.QuestSystem?.BuildQuestHoverContent(
                   info,
                   ResolveStepLocationText,
                   _isShiftPressed)
               ?? info.Definition.Description;
    }

    private string ResolveStepLocationText(string eventId)
    {
        GameplayActionContext? context = _actionContextResolver?.Invoke(eventId);
        if (!context.HasValue)
        {
            return string.Empty;
        }

        return $"{context.Value.AreaTitle} / {context.Value.SceneTitle}";
    }

    private void OnClaimRewardPressed()
    {
        QuestProgressInfo? info = ResolveDisplayedQuest();
        if (info == null || !info.CanClaimReward)
        {
            return;
        }

        bool success = _gameManager?.QuestSystem?.TryClaimReward(info.Definition.Id) == true;
        if (!success)
        {
            return;
        }

        _requestUiRefresh?.Invoke();
        RefreshQuests();
    }

    private StyleBoxFlat CreatePanelStyle(Color backgroundColor, Color borderColor)
    {
        Color finalBackground = backgroundColor;
        finalBackground.A = 0.92f;

        return new StyleBoxFlat
        {
            BgColor = finalBackground,
            BorderColor = borderColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ContentMarginLeft = 10,
            ContentMarginTop = 10,
            ContentMarginRight = 10,
            ContentMarginBottom = 10
        };
    }

    private StyleBoxFlat CreateButtonStyle(Color backgroundColor, Color borderColor)
    {
        Color finalBackground = backgroundColor;
        finalBackground.A = 0.94f;

        return new StyleBoxFlat
        {
            BgColor = finalBackground,
            BorderColor = borderColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8
        };
    }
}
