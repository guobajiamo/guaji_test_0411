using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Runtime;
using Test00_0410.Systems;

namespace Test00_0410.UI;

/// <summary>
/// 右侧人物状态栏。
/// 使用折叠分组展示目标、技能、状态、货币和声望。
/// </summary>
public partial class CharacterStatusPanel : Control
{
    private static readonly Color ProgressFillColor = new("#39ff88");
    private static readonly Color ProgressBackgroundColor = new("#102018");

    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _rootContainer;
    private GameManager? _gameManager;
    private GameplayUiTheme _theme = new();
    private Func<string, GameplayActionContext?>? _actionContextResolver;
    private string _lastSignature = string.Empty;
    private ProgressBar? _targetProgressBar;
    private readonly Dictionary<string, Label> _dynamicItemLabels = new(StringComparer.Ordinal);
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

    public bool NeedsPeriodicRefresh()
    {
        if (_gameManager == null)
        {
            return false;
        }

        bool idleRunning = _gameManager.PlayerProfile.IdleState.IsRunning;
        bool hasTimedBuff = _gameManager.BuffSystem?.GetActiveBuffs().Any(buff => buff.IsTimed) == true;
        return idleRunning || hasTimedBuff;
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

        string signature = BuildStructureSignature(sections);
        if (signature == _lastSignature)
        {
            UpdateDynamicItems(sections);
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
                TrailingText = $"Lv.{state.Level}",
                BadgeText = state.CanLevelUp && state.Level < skill.MaxLevel ? "(可升级)" : string.Empty,
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
        List<StatusItemData> items = new();
        if (_gameManager?.BuffSystem != null)
        {
            foreach (BuffSystem.ActiveBuffView buff in _gameManager.BuffSystem.GetActiveBuffs())
            {
                string text = buff.IsTimed
                    ? $"{buff.DisplayName}({FormatDuration(buff.RemainingSeconds)})"
                    : buff.DisplayName;
                items.Add(new StatusItemData
                {
                    DynamicKey = $"buff:{buff.BuffId}",
                    Text = text,
                    TooltipText = BuildBuffTooltip(buff),
                    IsAccent = true
                });
            }
        }

        if (items.Count == 0)
        {
            items.Add(new StatusItemData
            {
                Text = _theme.Status.EmptyStatusText,
                TooltipText = "当前没有生效中的 Buff 或 Debuff。"
            });
        }

        items.Add(new StatusItemData
        {
            Text = $"已完成一次性事件：{profile.CompletedEventIds.Count}",
            TooltipText = "当前已完成的一次性事件数量。"
        });

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
        _dynamicItemLabels.Clear();

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
            sectionPanel.MouseFilter = MouseFilterEnum.Ignore;
            sectionPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
            _rootContainer.AddChild(sectionPanel);

            VBoxContainer content = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            content.MouseFilter = MouseFilterEnum.Ignore;
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
                progressBar.AddThemeStyleboxOverride("fill", CreateProgressFillStyle());
                progressBar.AddThemeStyleboxOverride("background", CreateProgressBackgroundStyle());
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
            itemList.MouseFilter = MouseFilterEnum.Ignore;
            itemList.AddThemeConstantOverride("separation", 4);
            content.AddChild(itemList);

            foreach (StatusItemData item in section.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.TrailingText) || !string.IsNullOrWhiteSpace(item.BadgeText))
                {
                    HBoxContainer row = new()
                    {
                        SizeFlagsHorizontal = SizeFlags.ExpandFill
                    };
                    row.MouseFilter = MouseFilterEnum.Ignore;
                    row.AddThemeConstantOverride("separation", 6);

                    Label textLabel = new()
                    {
                        Text = item.Text,
                        AutowrapMode = TextServer.AutowrapMode.WordSmart,
                        SizeFlagsHorizontal = SizeFlags.ExpandFill,
                        TooltipText = item.TooltipText
                    };
                    textLabel.MouseFilter = MouseFilterEnum.Stop;
                    textLabel.AddThemeFontSizeOverride("font_size", _theme.StatusBodyFontSize);
                    textLabel.AddThemeColorOverride("font_color", item.IsAccent ? _theme.Status.AccentTextColor : _theme.Status.BodyTextColor);
                    row.AddChild(textLabel);
                    RegisterDynamicLabel(item, textLabel);

                    HBoxContainer rightGroup = new()
                    {
                        SizeFlagsHorizontal = SizeFlags.ShrinkEnd
                    };
                    rightGroup.MouseFilter = MouseFilterEnum.Ignore;
                    rightGroup.AddThemeConstantOverride("separation", 4);

                    if (!string.IsNullOrWhiteSpace(item.TrailingText))
                    {
                        Label trailingLabel = new()
                        {
                            Text = item.TrailingText,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            TooltipText = item.TooltipText
                        };
                        trailingLabel.MouseFilter = MouseFilterEnum.Stop;
                        trailingLabel.AddThemeFontSizeOverride("font_size", _theme.StatusBodyFontSize);
                        trailingLabel.AddThemeColorOverride("font_color", _theme.Status.HeaderTextColor);
                        rightGroup.AddChild(trailingLabel);
                    }

                    if (!string.IsNullOrWhiteSpace(item.BadgeText))
                    {
                        Label badgeLabel = new()
                        {
                            Text = item.BadgeText,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            TooltipText = item.TooltipText
                        };
                        badgeLabel.MouseFilter = MouseFilterEnum.Stop;
                        badgeLabel.AddThemeFontSizeOverride("font_size", 10);
                        badgeLabel.AddThemeColorOverride("font_color", new Color("#ff0000"));
                        rightGroup.AddChild(badgeLabel);
                    }

                    row.AddChild(rightGroup);
                    itemList.AddChild(row);
                    continue;
                }

                Label label = new()
                {
                    Text = item.Text,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    TooltipText = item.TooltipText
                };
                label.MouseFilter = MouseFilterEnum.Stop;
                label.AddThemeFontSizeOverride("font_size", _theme.StatusBodyFontSize);
                label.AddThemeColorOverride("font_color", item.IsAccent ? _theme.Status.AccentTextColor : _theme.Status.BodyTextColor);
                itemList.AddChild(label);
                RegisterDynamicLabel(item, label);
            }
        }
    }

    private string BuildStructureSignature(IEnumerable<StatusSectionData> sections)
    {
        return string.Join("|", sections.SelectMany(section =>
            new[]
            {
                $"{section.SectionId}:{_expandedStates.GetValueOrDefault(section.SectionId, true)}",
                $"{section.Title}:{section.ProgressRatio:0.###}"
            }.Concat(section.Items.Select(item =>
            {
                if (!string.IsNullOrWhiteSpace(item.DynamicKey))
                {
                    return $"dyn:{item.DynamicKey}:{item.TrailingText}:{item.BadgeText}:{item.TooltipText}";
                }

                return $"txt:{item.Text}:{item.TrailingText}:{item.BadgeText}:{item.TooltipText}";
            }))));
    }

    private void RegisterDynamicLabel(StatusItemData item, Label label)
    {
        if (string.IsNullOrWhiteSpace(item.DynamicKey))
        {
            return;
        }

        _dynamicItemLabels[item.DynamicKey] = label;
    }

    private void UpdateDynamicItems(IEnumerable<StatusSectionData> sections)
    {
        foreach (StatusItemData item in sections.SelectMany(section => section.Items))
        {
            if (string.IsNullOrWhiteSpace(item.DynamicKey))
            {
                continue;
            }

            if (!_dynamicItemLabels.TryGetValue(item.DynamicKey, out Label? label) || !IsInstanceValid(label))
            {
                continue;
            }

            if (!string.Equals(label.Text, item.Text, StringComparison.Ordinal))
            {
                label.Text = item.Text;
            }

            if (!string.Equals(label.TooltipText, item.TooltipText, StringComparison.Ordinal))
            {
                label.TooltipText = item.TooltipText;
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
        Color background = hovered ? _theme.Tabs.ActiveColor : _theme.Tabs.NormalColor;
        background.A = hovered ? Math.Clamp(_theme.Tabs.ActiveAlpha, 0.35f, 0.95f) : Math.Clamp(_theme.Tabs.NormalAlpha, 0.25f, 0.90f);
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = _theme.Tabs.BorderColor,
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

    private StyleBoxFlat CreatePanelStyle()
    {
        Color finalColor = _theme.RightStatus.BaseColor;
        finalColor.A = Math.Clamp(_theme.RightStatus.Alpha, 0.70f, 0.98f);

        Color borderColor = _theme.RightStatus.BorderColor;
        borderColor.A = Math.Clamp(_theme.RightStatus.BorderAlpha, 0.45f, 0.98f);

        return new StyleBoxFlat
        {
            BgColor = finalColor,
            BorderColor = borderColor,
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

    private static StyleBoxFlat CreateProgressFillStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = ProgressFillColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
    }

    private static StyleBoxFlat CreateProgressBackgroundStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = ProgressBackgroundColor,
            BorderColor = new Color("#203128"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
    }

    private static string FormatDuration(double remainingSeconds)
    {
        int seconds = Math.Max(0, (int)Math.Ceiling(remainingSeconds));
        if (seconds >= 3600)
        {
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            int leftSeconds = seconds % 60;
            return $"{hours}小时{minutes:D2}分{leftSeconds:D2}秒";
        }

        int minuteValue = seconds / 60;
        int secondValue = seconds % 60;
        return $"{minuteValue:D2}分{secondValue:D2}秒";
    }

    private string BuildBuffTooltip(BuffSystem.ActiveBuffView buff)
    {
        if (_gameManager == null)
        {
            return string.IsNullOrWhiteSpace(buff.Description) ? "暂无说明。" : buff.Description;
        }

        List<string> lines = new();
        if (!string.IsNullOrWhiteSpace(buff.Description))
        {
            lines.Add(buff.Description);
        }

        List<ItemDefinition> sourceItems = _gameManager.ItemRegistry.Items.Values
            .Where(item => item.ConsumeBuff != null
                && string.Equals(item.ConsumeBuff.BuffId, buff.BuffId, StringComparison.Ordinal))
            .OrderBy(item => item.DefinitionOrder)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        if (sourceItems.Count > 0)
        {
            string sourceText = string.Join("、", sourceItems
                .Select(item => item.GetDisplayName(_gameManager.TranslateText)));
            lines.Add($"获取方式：食用 {sourceText}");
        }

        List<string> effectLines = new();
        HashSet<string> dedupe = new(StringComparer.Ordinal);
        foreach (ItemDefinition sourceItem in sourceItems)
        {
            if (sourceItem.ConsumeBuff == null)
            {
                continue;
            }

            foreach (BuffStatModifierDefinition modifier in sourceItem.ConsumeBuff.StatModifiers)
            {
                string line = DescribeBuffModifier(modifier);
                if (string.IsNullOrWhiteSpace(line) || !dedupe.Add(line))
                {
                    continue;
                }

                effectLines.Add(line);
            }
        }

        if (effectLines.Count > 0)
        {
            lines.Add("具体加成：");
            lines.AddRange(effectLines.Select(line => $"• {line}"));
        }

        return lines.Count == 0 ? "暂无说明。" : string.Join("\n", lines);
    }

    private string DescribeBuffModifier(BuffStatModifierDefinition modifier)
    {
        if (string.IsNullOrWhiteSpace(modifier.StatId))
        {
            return string.Empty;
        }

        string statName = modifier.StatId;
        const string speedPrefix = "idle.speed.";
        const string outputPrefix = "idle.output.";
        const string suffix = ".multiplier";

        if (modifier.StatId.StartsWith(speedPrefix, StringComparison.Ordinal)
            && modifier.StatId.EndsWith(suffix, StringComparison.Ordinal))
        {
            string skillId = modifier.StatId[speedPrefix.Length..^suffix.Length];
            statName = $"{GetSkillDisplayName(skillId)}速度";
        }
        else if (modifier.StatId.StartsWith(outputPrefix, StringComparison.Ordinal)
            && modifier.StatId.EndsWith(suffix, StringComparison.Ordinal))
        {
            string skillId = modifier.StatId[outputPrefix.Length..^suffix.Length];
            statName = $"{GetSkillDisplayName(skillId)}产出";
        }
        else if (string.Equals(modifier.StatId, "idle.speed.multiplier", StringComparison.Ordinal))
        {
            statName = "采集速度";
        }
        else if (string.Equals(modifier.StatId, "idle.output.multiplier", StringComparison.Ordinal))
        {
            statName = "采集产出";
        }

        if (modifier.Multiplier > 0.0
            && (modifier.StatId.StartsWith(speedPrefix, StringComparison.Ordinal)
                || string.Equals(modifier.StatId, "idle.speed.multiplier", StringComparison.Ordinal)))
        {
            double timePercent = 100.0 / modifier.Multiplier;
            return $"{statName}倍率 x{modifier.Multiplier:0.###}（读条耗时约为原来的 {timePercent:0.#}%）";
        }

        return $"{statName}倍率 x{modifier.Multiplier:0.###}";
    }

    private string GetSkillDisplayName(string skillId)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(skillId))
        {
            return string.IsNullOrWhiteSpace(skillId) ? "技能" : skillId;
        }

        SkillDefinition? definition = _gameManager.SkillRegistry.GetSkill(skillId);
        return definition == null ? skillId : _gameManager.TranslateText(definition.NameKey);
    }

    public void UpdateTargetProgress(double progressRatio, bool shouldShow)
    {
        if (_targetProgressBar == null || !IsInstanceValid(_targetProgressBar))
        {
            return;
        }

        if (_targetProgressBar.Visible != shouldShow)
        {
            _targetProgressBar.Visible = shouldShow;
        }

        double clampedValue = Math.Max(0.0, Math.Min(100.0, progressRatio * 100.0));
        if (Math.Abs(_targetProgressBar.Value - clampedValue) > 0.02)
        {
            _targetProgressBar.Value = clampedValue;
        }
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
        public string DynamicKey { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public string TooltipText { get; set; } = string.Empty;

        public bool IsAccent { get; set; }

        public string TrailingText { get; set; } = string.Empty;

        public string BadgeText { get; set; } = string.Empty;
    }
}
