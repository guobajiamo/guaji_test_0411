using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 右侧人物状态栏。
/// 使用折叠分组展示目标、技能、状态、货币和声望。
/// </summary>
public partial class CharacterStatusPanel : Control
{
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _rootContainer;
    private GameManager? _gameManager;
    private GameplayUiTheme _theme = new();
    private Func<string, GameplayActionContext?>? _actionContextResolver;
    private string _lastSignature = string.Empty;
    private ProgressBar? _targetProgressBar;
    private readonly Dictionary<string, bool> _expandedStates = new()
    {
        ["target"] = true,
        ["skills"] = true,
        ["status"] = true,
        ["currency"] = true,
        ["reputation"] = true
    };

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(
        GameManager gameManager,
        GameplayUiTheme theme,
        Func<string, GameplayActionContext?>? actionContextResolver = null)
    {
        _gameManager = gameManager;
        _theme = theme;
        _actionContextResolver = actionContextResolver;
        EnsureStructure();
    }

    public void RefreshStatus()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            RebuildContent(new[]
            {
                new StatusSectionData
                {
                    SectionId = "empty",
                    Title = "状态",
                    Items = new List<StatusItemData>
                    {
                        new()
                        {
                            Text = "状态栏尚未绑定 GameManager。",
                            TooltipText = "状态栏尚未绑定 GameManager。"
                        }
                    }
                }
            });
            return;
        }

        PlayerProfile profile = _gameManager.PlayerProfile;
        List<StatusSectionData> sections = new()
        {
            BuildTargetSection(profile),
            BuildSkillsSection(profile),
            BuildStatusSection(profile),
            BuildCurrencySection(profile),
            BuildReputationSection(profile)
        };

        string signature = string.Join("|", sections.SelectMany(section =>
            new[]
            {
                $"{section.SectionId}:{_expandedStates.GetValueOrDefault(section.SectionId, true)}",
                $"{section.Title}:{section.ProgressRatio:0.###}"
            }.Concat(section.Items.Select(item => $"{item.Text}:{item.TooltipText}"))));
        if (signature == _lastSignature)
        {
            return;
        }

        _lastSignature = signature;
        RebuildContent(sections);
    }

    private void EnsureStructure()
    {
        if (_scrollContainer != null && _rootContainer != null)
        {
            return;
        }

        _scrollContainer = new ScrollContainer
        {
            Name = "ScrollContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _scrollContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_scrollContainer);

        _rootContainer = new VBoxContainer
        {
            Name = "RootContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _rootContainer.AddThemeConstantOverride("separation", 10);
        _scrollContainer.AddChild(_rootContainer);
    }

    private StatusSectionData BuildTargetSection(PlayerProfile profile)
    {
        string targetText = _theme.Status.RestingText;
        string tooltip = "当前没有进行中的挂机项目。";
        double progressRatio = 0.0;

        if (_gameManager?.IdleSystem != null
            && profile.IdleState.IsRunning
            && !string.IsNullOrWhiteSpace(profile.IdleState.ActiveEventId))
        {
            string eventId = profile.IdleState.ActiveEventId;
            string eventName = _gameManager.GetEventDisplayName(eventId);
            if (eventName.StartsWith("挂机", StringComparison.Ordinal))
            {
                eventName = eventName[2..];
            }

            GameplayActionContext? context = _actionContextResolver?.Invoke(eventId);
            targetText = context.HasValue
                ? $"正在{context.Value.SceneTitle}{eventName}"
                : $"正在{eventName}";
            tooltip = context.HasValue
                ? $"当前正在 {context.Value.AreaTitle} / {context.Value.SceneTitle} 执行 {eventName}。"
                : $"当前正在执行 {eventName}。";
            progressRatio = _gameManager.IdleSystem.GetProgressRatio(eventId);
        }

        return new StatusSectionData
        {
            SectionId = "target",
            Title = _theme.Status.TargetSectionTitle,
            TooltipText = "显示当前正在进行的目标。",
            ProgressRatio = progressRatio,
            Items = new List<StatusItemData>
            {
                new()
                {
                    Text = targetText,
                    TooltipText = tooltip,
                    IsAccent = true
                }
            }
        };
    }

    private StatusSectionData BuildSkillsSection(PlayerProfile profile)
    {
        List<StatusItemData> items = new();
        foreach (SkillDefinition skill in _gameManager!.SkillRegistry.Skills.Values.OrderBy(item => item.Id))
        {
            PlayerSkillState state = profile.GetOrCreateSkillState(skill.Id);
            ItemDefinition? tool = skill.RequiredToolTag == ItemTag.None
                ? null
                : _gameManager.GetBestOwnedTool(skill.RequiredToolTag);

            string tooltip =
                $"{_gameManager.TranslateText(skill.DescriptionKey)}\n" +
                $"等级：Lv.{state.Level}/{skill.MaxLevel}\n" +
                $"当前经验：{FormatExp(state.StoredExp)}\n" +
                $"总经验：{FormatExp(state.TotalEarnedExp)}\n" +
                $"适用工具：{(tool == null ? "未持有" : tool.GetDisplayName(_gameManager.TranslateText))}";

            items.Add(new StatusItemData
            {
                Text = _gameManager.TranslateText(skill.NameKey),
                TooltipText = tooltip
            });
        }

        if (items.Count == 0)
        {
            items.Add(new StatusItemData
            {
                Text = "暂无技能",
                TooltipText = "当前剧本还没有可显示技能。"
            });
        }

        return new StatusSectionData
        {
            SectionId = "skills",
            Title = _theme.Status.SkillsSectionTitle,
            TooltipText = "技能详细信息集中收纳在各条目的悬浮说明中。",
            Items = items
        };
    }

    private StatusSectionData BuildStatusSection(PlayerProfile profile)
    {
        List<StatusItemData> items = new()
        {
            new()
            {
                Text = _theme.Status.EmptyStatusText,
                TooltipText = "后续角色异常状态、Buff 与 Debuff 可继续接入这里。"
            },
            new()
            {
                Text = $"已完成一次性事件：{profile.CompletedEventIds.Count}",
                TooltipText = "当前已完成的一次性事件数量。"
            }
        };

        return new StatusSectionData
        {
            SectionId = "status",
            Title = _theme.Status.StatusSectionTitle,
            TooltipText = "显示角色当前状态概览。",
            Items = items
        };
    }

    private StatusSectionData BuildCurrencySection(PlayerProfile profile)
    {
        List<StatusItemData> items = new()
        {
            new()
            {
                Text = $"金币：{profile.Economy.Gold}",
                TooltipText = "当前持有的金币数量。",
                IsAccent = true
            }
        };

        foreach ((string currencyId, int amount) in profile.Economy.ExtraCurrencies.OrderBy(entry => entry.Key))
        {
            items.Add(new StatusItemData
            {
                Text = $"{currencyId}：{amount}",
                TooltipText = $"额外货币 {currencyId} 的当前数量。"
            });
        }

        return new StatusSectionData
        {
            SectionId = "currency",
            Title = _theme.Status.CurrencySectionTitle,
            TooltipText = "显示金币和额外货币数量。",
            Items = items
        };
    }

    private StatusSectionData BuildReputationSection(PlayerProfile profile)
    {
        List<StatusItemData> items = _gameManager!.FactionRegistry.Factions.Values
            .OrderBy(faction => faction.Id)
            .Select(faction =>
            {
                PlayerFactionState state = profile.GetOrCreateFactionState(faction.Id);
                return new StatusItemData
                {
                    Text = $"{_gameManager.TranslateText(faction.NameKey)}：{state.Reputation}",
                    TooltipText = _gameManager.TranslateText(faction.DescriptionKey)
                };
            })
            .ToList();

        if (items.Count == 0)
        {
            items.Add(new StatusItemData
            {
                Text = _theme.Status.EmptyReputationText,
                TooltipText = "当前剧本还没有已注册的势力声望。"
            });
        }

        return new StatusSectionData
        {
            SectionId = "reputation",
            Title = _theme.Status.ReputationSectionTitle,
            TooltipText = "显示当前已注册势力的声望。",
            Items = items
        };
    }

    private void RebuildContent(IEnumerable<StatusSectionData> sections)
    {
        if (_rootContainer == null)
        {
            return;
        }

        _targetProgressBar = null;

        foreach (Node child in _rootContainer.GetChildren())
        {
            _rootContainer.RemoveChild(child);
            child.QueueFree();
        }

        foreach (StatusSectionData section in sections)
        {
            PanelContainer sectionPanel = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            sectionPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color("#0f1720"), 0.55f));
            _rootContainer.AddChild(sectionPanel);

            VBoxContainer content = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            content.AddThemeConstantOverride("separation", 6);
            sectionPanel.AddChild(content);

            Button headerButton = new()
            {
                Text = $"{(_expandedStates.GetValueOrDefault(section.SectionId, true) ? "[-]" : "[+]")} {section.Title}",
                Alignment = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = section.TooltipText
            };
            headerButton.AddThemeFontSizeOverride("font_size", _theme.StatusHeaderFontSize);
            headerButton.AddThemeColorOverride("font_color", _theme.Status.HeaderTextColor);
            headerButton.AddThemeColorOverride("font_hover_color", _theme.Status.AccentTextColor);
            headerButton.AddThemeStyleboxOverride("normal", CreateButtonStyle(false));
            headerButton.AddThemeStyleboxOverride("hover", CreateButtonStyle(true));
            headerButton.AddThemeStyleboxOverride("pressed", CreateButtonStyle(true));
            headerButton.Pressed += () => ToggleSection(section.SectionId);
            content.AddChild(headerButton);

            if (!_expandedStates.GetValueOrDefault(section.SectionId, true))
            {
                continue;
            }

            if (section.ProgressRatio > 0.0 || string.Equals(section.SectionId, "target", StringComparison.Ordinal))
            {
                ProgressBar progressBar = new()
                {
                    MinValue = 0,
                    MaxValue = 100,
                    Value = section.ProgressRatio * 100.0,
                    ShowPercentage = false,
                    CustomMinimumSize = new Vector2(0, 8),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                progressBar.AddThemeColorOverride("fill", _theme.Status.ProgressFillColor);
                progressBar.AddThemeColorOverride("background", _theme.Status.ProgressBackgroundColor);
                content.AddChild(progressBar);

                if (string.Equals(section.SectionId, "target", StringComparison.Ordinal))
                {
                    _targetProgressBar = progressBar;
                }
            }

            VBoxContainer itemList = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            itemList.AddThemeConstantOverride("separation", 4);
            content.AddChild(itemList);

            foreach (StatusItemData item in section.Items)
            {
                Label label = new()
                {
                    Text = item.Text,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    TooltipText = item.TooltipText
                };
                label.AddThemeFontSizeOverride("font_size", _theme.StatusBodyFontSize);
                label.AddThemeColorOverride("font_color", item.IsAccent ? _theme.Status.AccentTextColor : _theme.Status.BodyTextColor);
                itemList.AddChild(label);
            }
        }
    }

    private void ToggleSection(string sectionId)
    {
        _expandedStates[sectionId] = !_expandedStates.GetValueOrDefault(sectionId, true);
        _lastSignature = string.Empty;
        RefreshStatus();
    }

    private StyleBoxFlat CreateButtonStyle(bool hovered)
    {
        return new StyleBoxFlat
        {
            BgColor = hovered ? new Color(0.27f, 0.32f, 0.24f, 0.78f) : new Color(0.17f, 0.21f, 0.18f, 0.54f),
            BorderColor = hovered ? new Color("#c5a86a") : new Color("#66735f"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ContentMarginLeft = 10,
            ContentMarginTop = 7,
            ContentMarginRight = 10,
            ContentMarginBottom = 7
        };
    }

    private StyleBoxFlat CreatePanelStyle(Color color, float alpha)
    {
        Color finalColor = color;
        finalColor.A = alpha;

        return new StyleBoxFlat
        {
            BgColor = finalColor,
            BorderColor = new Color("#7f6a4d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ShadowColor = new Color(0, 0, 0, 0.26f),
            ShadowSize = 8,
            ContentMarginLeft = 10,
            ContentMarginTop = 10,
            ContentMarginRight = 10,
            ContentMarginBottom = 10
        };
    }

    private static string FormatExp(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString("0")
            : value.ToString("0.###");
    }

    public void UpdateTargetProgress(double progressRatio, bool shouldShow)
    {
        if (_targetProgressBar == null || !IsInstanceValid(_targetProgressBar))
        {
            return;
        }

        _targetProgressBar.Visible = shouldShow;
        _targetProgressBar.Value = Math.Max(0.0, Math.Min(100.0, progressRatio * 100.0));
    }

    private sealed class StatusSectionData
    {
        public string SectionId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string TooltipText { get; set; } = string.Empty;

        public double ProgressRatio { get; set; }

        public List<StatusItemData> Items { get; set; } = new();
    }

    private sealed class StatusItemData
    {
        public string Text { get; set; } = string.Empty;

        public string TooltipText { get; set; } = string.Empty;

        public bool IsAccent { get; set; }
    }
}
