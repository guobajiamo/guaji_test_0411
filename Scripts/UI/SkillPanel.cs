using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

public partial class SkillPanel : Control
{
    private const int MaxSkillGridColumns = 4;
    private const float StitchSkillCardMinWidth = 200f;
    private const float LegacySkillCardMinWidth = 192f;
    private const float SkillGridSpacing = 10f;

    private Label? _summaryLabel;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _skillListContainer;
    private GridContainer? _skillGrid;
    private GameManager? _gameManager;
    private Action<string>? _onUpgradeRequested;
    private string _lastSkillSignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
        CallDeferred(nameof(UpdateSkillGridColumns));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized || what == NotificationVisibilityChanged)
        {
            if (IsVisibleInTree())
            {
                CallDeferred(nameof(UpdateSkillGridColumns));
            }
        }
    }

    public void Configure(GameManager gameManager, Action<string> onUpgradeRequested)
    {
        _gameManager = gameManager;
        _onUpgradeRequested = onUpgradeRequested;
        EnsureStructure();
        CallDeferred(nameof(UpdateSkillGridColumns));
    }

    public void RefreshSkills()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            if (_summaryLabel != null)
            {
                _summaryLabel.Text = "技能面板尚未绑定 GameManager。";
            }

            return;
        }

        bool useStitchTheme = IsUsingStitchTheme();
        List<SkillDefinition> orderedSkills = GetOrderedSkills(_gameManager.SkillRegistry.Skills.Values);
        List<string> signatureParts = new();
        int learnedSkillCount = 0;
        foreach (SkillDefinition definition in orderedSkills)
        {
            PlayerSkillState state = _gameManager.PlayerProfile.GetOrCreateSkillState(definition.Id);
            if (state.Level > 0)
            {
                learnedSkillCount++;
            }

            int requiredTotalExp = definition.GetRequiredTotalExpForNextLevel(state.Level);
            signatureParts.Add($"{definition.Id}:{state.Level}:{state.StoredExp:0.###}:{state.TotalEarnedExp:0.###}:{state.CanLevelUp}:{requiredTotalExp}:{useStitchTheme}");
        }

        string nextSignature = string.Join("|", signatureParts);
        if (nextSignature == _lastSkillSignature)
        {
            UpdateSummaryLabel(orderedSkills.Count, learnedSkillCount);
            CallDeferred(nameof(UpdateSkillGridColumns));
            return;
        }

        _lastSkillSignature = nextSignature;
        ClearSkillList();

        foreach (SkillDefinition definition in orderedSkills)
        {
            _skillGrid?.AddChild(BuildSkillCard(
                definition,
                useStitchTheme,
                ResolveSkillGroupPrimaryColor(definition.GroupId),
                ResolveSkillNodeSecondaryColor(definition.GroupId)));
        }

        UpdateSummaryLabel(orderedSkills.Count, learnedSkillCount);
        CallDeferred(nameof(UpdateSkillGridColumns));
    }

    private static List<SkillDefinition> GetOrderedSkills(IEnumerable<SkillDefinition> definitions)
    {
        return definitions
            .OrderBy(skill => skill.GroupOrder)
            .ThenBy(skill => skill.GroupName, StringComparer.Ordinal)
            .ThenBy(skill => skill.SkillOrder)
            .ThenBy(skill => skill.Id, StringComparer.Ordinal)
            .ToList();
    }

    private Control BuildSkillCard(SkillDefinition definition, bool useStitchTheme, Color primaryColor, Color secondaryColor)
    {
        PlayerSkillState state = _gameManager!.PlayerProfile.GetOrCreateSkillState(definition.Id);
        int requiredTotalExp = definition.GetRequiredTotalExpForNextLevel(state.Level);
        Color titleColor = useStitchTheme ? secondaryColor.Darkened(0.08f) : primaryColor.Lightened(0.18f);
        Color bodyColor = useStitchTheme ? new Color("#474a45") : new Color("#d7e6ff");
        Color mutedColor = useStitchTheme ? new Color("#656a62") : new Color("#adc4ea");

        PanelContainer card = new()
        {
            Name = $"SkillCard_{NormalizeNodeId(definition.Id)}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(GetSkillCardMinWidth(useStitchTheme), useStitchTheme ? 238 : 226)
        };
        card.AddThemeStyleboxOverride("panel", CreateSkillCardStyle(useStitchTheme, primaryColor, secondaryColor));

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 6);
        card.AddChild(content);

        string translatedName = _gameManager.TranslateText(definition.NameKey);
        string levelText = state.Level <= 0
            ? $"{translatedName} · 未习得"
            : $"{translatedName} · Lv.{state.Level}/{definition.MaxLevel}";

        Label nameLabel = new()
        {
            Text = levelText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        nameLabel.AddThemeColorOverride("font_color", titleColor);
        nameLabel.AddThemeFontSizeOverride("font_size", useStitchTheme ? 17 : 16);
        content.AddChild(nameLabel);

        string progressText = state.Level <= 0
            ? "尚未习得该技能。"
            : requiredTotalExp <= 0
                ? "已达到当前技能表上限。"
                : $"经验：{FormatExpValue(state.StoredExp)} / {requiredTotalExp:0}";

        Label expLabel = new()
        {
            Text = progressText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        expLabel.AddThemeColorOverride("font_color", mutedColor);
        expLabel.AddThemeFontSizeOverride("font_size", useStitchTheme ? 14 : 13);
        content.AddChild(expLabel);

        Label descriptionLabel = new()
        {
            Text = _gameManager.TranslateText(definition.DescriptionKey),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, useStitchTheme ? 74 : 68)
        };
        descriptionLabel.AddThemeColorOverride("font_color", bodyColor);
        descriptionLabel.AddThemeFontSizeOverride("font_size", useStitchTheme ? 14 : 13);
        content.AddChild(descriptionLabel);

        Control spacer = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(spacer);

        Button levelUpButton = new()
        {
            Text = state.Level <= 0
                ? "未习得"
                : state.Level >= definition.MaxLevel
                    ? "已满级"
                    : "手动升级",
            Disabled = state.Level <= 0 || state.Level >= definition.MaxLevel || !state.CanLevelUp,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, useStitchTheme ? 40 : 36)
        };
        UiImageThemeManager.ApplyButtonStyle(levelUpButton, "special_action");
        levelUpButton.Pressed += () => _onUpgradeRequested?.Invoke(definition.Id);
        content.AddChild(levelUpButton);

        return card;
    }

    private void EnsureStructure()
    {
        if (_summaryLabel != null && _scrollContainer != null && _skillListContainer != null && _skillGrid != null)
        {
            return;
        }

        bool useStitchTheme = IsUsingStitchTheme();

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        _summaryLabel = new Label
        {
            Name = "SummaryLabel",
            Text = "技能列表",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _summaryLabel.AddThemeColorOverride("font_color", useStitchTheme ? new Color("#30332e") : new Color("#fff0f6"));
        _summaryLabel.AddThemeFontSizeOverride("font_size", useStitchTheme ? 18 : 17);
        root.AddChild(_summaryLabel);

        _scrollContainer = new ScrollContainer
        {
            Name = "ScrollContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        root.AddChild(_scrollContainer);

        _skillListContainer = new VBoxContainer
        {
            Name = "SkillListContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _skillListContainer.AddThemeConstantOverride("separation", 12);
        _scrollContainer.AddChild(_skillListContainer);

        _skillGrid = new GridContainer
        {
            Name = "SkillGrid",
            Columns = MaxSkillGridColumns,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _skillGrid.AddThemeConstantOverride("h_separation", (int)SkillGridSpacing);
        _skillGrid.AddThemeConstantOverride("v_separation", (int)SkillGridSpacing);
        _skillListContainer.AddChild(_skillGrid);
    }

    private void ClearSkillList()
    {
        if (_skillGrid == null)
        {
            return;
        }

        foreach (Node child in _skillGrid.GetChildren())
        {
            _skillGrid.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void UpdateSummaryLabel(int totalSkillCount, int learnedSkillCount)
    {
        if (_summaryLabel == null)
        {
            return;
        }

        _summaryLabel.Text = $"当前共有 {totalSkillCount} 项技能，已习得 {learnedSkillCount} 项。技能块默认每排展示 4 个，窄屏下会自动换列。";
    }

    private void UpdateSkillGridColumns()
    {
        if (_skillGrid == null || !IsInstanceValid(_skillGrid))
        {
            return;
        }

        bool useStitchTheme = IsUsingStitchTheme();
        float availableWidth = Math.Max(
            Math.Max(Size.X, GetParentAreaSize().X),
            Math.Max(_scrollContainer?.Size.X ?? 0f, _skillGrid.Size.X));
        if (availableWidth <= 1f)
        {
            return;
        }

        float scrollBarWidth = 0f;
        if (_scrollContainer != null && IsInstanceValid(_scrollContainer))
        {
            VScrollBar vScrollBar = _scrollContainer.GetVScrollBar();
            if (IsInstanceValid(vScrollBar) && vScrollBar.Visible)
            {
                scrollBarWidth = Math.Max(vScrollBar.Size.X, vScrollBar.GetCombinedMinimumSize().X);
            }
        }

        float innerWidth = Math.Max(1f, availableWidth - scrollBarWidth - (useStitchTheme ? 28f : 24f));
        float minCardWidth = GetSkillCardMinWidth(useStitchTheme);
        int columns = Math.Clamp((int)Math.Floor((innerWidth + SkillGridSpacing) / (minCardWidth + SkillGridSpacing)), 1, MaxSkillGridColumns);
        _skillGrid.Columns = columns;
    }

    private bool IsUsingStitchTheme()
    {
        return string.Equals(
            UiImageThemeManager.GetThemeMode(),
            GameplayUiConfigLoader.UiThemeModeStitch,
            StringComparison.Ordinal);
    }

    private static float GetSkillCardMinWidth(bool useStitchTheme)
    {
        return useStitchTheme ? StitchSkillCardMinWidth : LegacySkillCardMinWidth;
    }

    private static string FormatExpValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString("0")
            : value.ToString("0.###");
    }

    private static string NormalizeNodeId(string? rawText)
    {
        return string.IsNullOrWhiteSpace(rawText)
            ? "ungrouped"
            : rawText.Trim().Replace(' ', '_').ToLowerInvariant();
    }

    private static Color ResolveSkillGroupPrimaryColor(string? groupId)
    {
        string normalized = string.IsNullOrWhiteSpace(groupId) ? string.Empty : groupId.Trim().ToLowerInvariant();
        return normalized switch
        {
            "farming" => new Color("#9b7a30"),
            "collection" => new Color("#985252"),
            "craft" => new Color("#4f6f95"),
            _ => new Color("#b26a2d")
        };
    }

    private static Color ResolveSkillNodeSecondaryColor(string? groupId)
    {
        string normalized = string.IsNullOrWhiteSpace(groupId) ? string.Empty : groupId.Trim().ToLowerInvariant();
        return normalized switch
        {
            "farming" => new Color("#6f5317"),
            "collection" => new Color("#6f2a2a"),
            "craft" => new Color("#2c4d74"),
            _ => new Color("#5e4530")
        };
    }

    private static StyleBoxFlat CreateSkillCardStyle(bool useStitchTheme, Color primaryColor, Color secondaryColor)
    {
        if (useStitchTheme)
        {
            return new StyleBoxFlat
            {
                BgColor = primaryColor.Lerp(Colors.White, 0.87f),
                BorderColor = primaryColor.Lerp(secondaryColor, 0.32f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 14,
                CornerRadiusTopRight = 14,
                CornerRadiusBottomLeft = 14,
                CornerRadiusBottomRight = 14,
                ShadowColor = new Color(0, 0, 0, 0.10f),
                ShadowSize = 5,
                ContentMarginLeft = 12,
                ContentMarginTop = 12,
                ContentMarginRight = 12,
                ContentMarginBottom = 12
            };
        }

        return new StyleBoxFlat
        {
            BgColor = new Color("#121c22"),
            BorderColor = primaryColor.Lightened(0.08f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0, 0, 0, 0.22f),
            ShadowSize = 6,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10
        };
    }
}
