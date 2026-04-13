using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 技能面板。
/// 用于显示技能等级、经验条和升级按钮。
/// </summary>
public partial class SkillPanel : Control
{
    private Label? _summaryLabel;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _skillListContainer;
    private GameManager? _gameManager;
    private Action<string>? _onUpgradeRequested;
    private string _lastSkillSignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(GameManager gameManager, Action<string> onUpgradeRequested)
    {
        _gameManager = gameManager;
        _onUpgradeRequested = onUpgradeRequested;
        EnsureStructure();
    }

    public void RefreshSkills()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            _summaryLabel!.Text = "技能面板尚未绑定 GameManager。";
            return;
        }

        List<string> signatureParts = new();
        int skillCount = 0;
        foreach (SkillDefinition definition in _gameManager.SkillRegistry.Skills.Values.OrderBy(skill => skill.Id))
        {
            skillCount++;

            PlayerSkillState state = _gameManager.PlayerProfile.GetOrCreateSkillState(definition.Id);
            SkillLevelEntry? levelEntry = definition.GetLevelEntry(state.Level);
            signatureParts.Add($"{definition.Id}:{state.Level}:{state.StoredExp:0.###}:{state.CanLevelUp}:{levelEntry?.ExpToNext ?? -1}");
        }

        string nextSignature = string.Join("|", signatureParts);
        if (nextSignature == _lastSkillSignature)
        {
            return;
        }

        _lastSkillSignature = nextSignature;
        ClearSkillList();

        foreach (SkillDefinition definition in _gameManager.SkillRegistry.Skills.Values.OrderBy(skill => skill.Id))
        {
            PlayerSkillState state = _gameManager.PlayerProfile.GetOrCreateSkillState(definition.Id);
            SkillLevelEntry? levelEntry = definition.GetLevelEntry(state.Level);

            PanelContainer skillCard = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            skillCard.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.13f, 0.12f, 0.88f),
                BorderColor = new Color("#7f6a4d"),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12,
                CornerRadiusBottomRight = 12,
                ShadowColor = new Color(0, 0, 0, 0.18f),
                ShadowSize = 6,
                ContentMarginLeft = 12,
                ContentMarginTop = 10,
                ContentMarginRight = 12,
                ContentMarginBottom = 10
            });
            _skillListContainer!.AddChild(skillCard);

            VBoxContainer content = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            content.AddThemeConstantOverride("separation", 4);
            skillCard.AddChild(content);

            Label nameLabel = new()
            {
                Text = $"{_gameManager.TranslateText(definition.NameKey)}  Lv.{state.Level}/{definition.MaxLevel}"
            };
            nameLabel.AddThemeColorOverride("font_color", new Color("#fff0f6"));
            nameLabel.AddThemeFontSizeOverride("font_size", 17);
            content.AddChild(nameLabel);

            string progressText = levelEntry == null
                ? "已达到当前技能表上限。"
                : $"经验：{FormatExpValue(state.StoredExp)} / {levelEntry.ExpToNext:0}";
            Label expLabel = new()
            {
                Text = progressText
            };
            expLabel.AddThemeColorOverride("font_color", new Color("#ff9dc1"));
            content.AddChild(expLabel);

            Label descriptionLabel = new()
            {
                Text = _gameManager.TranslateText(definition.DescriptionKey),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            descriptionLabel.AddThemeColorOverride("font_color", new Color("#f1dbe4"));
            content.AddChild(descriptionLabel);

            Button levelUpButton = new()
            {
                Text = state.Level >= definition.MaxLevel ? "已满级" : "手动升级",
                Disabled = state.Level >= definition.MaxLevel || !state.CanLevelUp
            };
            UiImageThemeManager.ApplyButtonStyle(levelUpButton, "special_action");
            levelUpButton.Pressed += () => _onUpgradeRequested?.Invoke(definition.Id);
            content.AddChild(levelUpButton);
        }

        _summaryLabel!.Text = $"当前共有 {skillCount} 项技能。";
    }

    private void EnsureStructure()
    {
        if (_summaryLabel != null && _scrollContainer != null && _skillListContainer != null)
        {
            return;
        }

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        _summaryLabel = new Label
        {
            Name = "SummaryLabel",
            Text = "技能列表"
        };
        _summaryLabel.AddThemeColorOverride("font_color", new Color("#fff0f6"));
        _summaryLabel.AddThemeFontSizeOverride("font_size", 18);
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
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _skillListContainer.AddThemeConstantOverride("separation", 8);
        _scrollContainer.AddChild(_skillListContainer);
    }

    private void ClearSkillList()
    {
        if (_skillListContainer == null)
        {
            return;
        }

        foreach (Node child in _skillListContainer.GetChildren())
        {
            _skillListContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string FormatExpValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString("0")
            : value.ToString("0.###");
    }
}
