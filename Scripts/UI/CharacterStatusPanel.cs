using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Runtime;
using Test00_0410.Systems;

namespace Test00_0410.UI;

/// <summary>
/// 右侧人物状态栏。
/// 使用折叠分组展示目标、技能、状态、货币和声望。
/// </summary>
public partial class CharacterStatusPanel : Control
{
    private const string IdleTargetProgressKey = "target:idle_progress";
    private const string BattleTargetHpProgressKey = "target:battle_hp";
    private const string BattleTargetAttackProgressKey = "target:battle_attack";
    private static readonly Color ProgressFillColor = new("#39ff88");
    private static readonly Color ProgressBackgroundColor = new("#102018");
    private static readonly Color BattleHpFillColor = new("#4fcf6a");
    private static readonly Color BattleHpBackgroundColor = new("#7a1c1c");
    private static readonly Color BattleAttackFillColor = new("#62b7ff");
    private static readonly Color BattleAttackBackgroundColor = new("#21324d");

    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _rootContainer;
    private GameManager? _gameManager;
    private GameplayUiTheme _theme = new();
    private Func<string, GameplayActionContext?>? _actionContextResolver;
    private string _lastSignature = string.Empty;
    private ProgressBar? _targetProgressBar;
    private readonly Dictionary<string, Label> _dynamicItemLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProgressBar> _dynamicProgressBars = new(StringComparer.Ordinal);
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
        bool hasActiveStaple = _gameManager.StapleFoodSystem?.GetActiveStaple() != null;
        bool battleRunning = _gameManager.BattleSystem?.IsBattleActive == true;
        return idleRunning || hasTimedBuff || hasActiveStaple || battleRunning;
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
        List<StatusProgressData> leadingBars = new();
        List<StatusProgressData> trailingBars = new();

        bool battleRunning = _gameManager?.BattleSystem?.IsBattleActive == true;
        bool hasBattleSelection = !string.IsNullOrWhiteSpace(profile.UiState.SelectedBattleEncounterId)
            || !string.IsNullOrWhiteSpace(profile.UiState.SelectedBattleEventId);
        if ((battleRunning || (!profile.IdleState.IsRunning && hasBattleSelection))
            && TryBuildBattleTargetSection(profile, ref targetText, ref tooltip, leadingBars, trailingBars))
        {
            return new StatusSectionData
            {
                SectionId = "target",
                Title = _theme.Status.TargetSectionTitle,
                TooltipText = "显示当前正在进行的目标。",
                LeadingProgressBars = leadingBars,
                TrailingProgressBars = trailingBars,
                Items = new List<StatusItemData>
                {
                    new()
                    {
                        DynamicKey = "target:text",
                        Text = targetText,
                        TooltipText = tooltip,
                        IsAccent = true
                    }
                }
            };
        }

        if (_gameManager?.IdleSystem != null
            && profile.IdleState.IsRunning
            && !string.IsNullOrWhiteSpace(profile.IdleState.ActiveEventId))
        {
            string eventId = profile.IdleState.ActiveEventId;
            bool isWaitingForRecovery = _gameManager.IdleSystem.IsWaitingForGatheringRecovery(eventId);
            string eventName = _gameManager.GetEventDisplayName(eventId);
            if (eventName.StartsWith("挂机", StringComparison.Ordinal))
            {
                eventName = eventName[2..];
            }

            GameplayActionContext? context = _actionContextResolver?.Invoke(eventId);
            targetText = context.HasValue
                ? isWaitingForRecovery
                    ? $"正在{context.Value.SceneTitle}等待{eventName}资源恢复"
                    : $"正在{context.Value.SceneTitle}采集{eventName}"
                : isWaitingForRecovery
                    ? $"正在等待{eventName}资源恢复"
                    : $"正在采集{eventName}";
            tooltip = context.HasValue
                ? isWaitingForRecovery
                    ? $"当前正在 {context.Value.AreaTitle} / {context.Value.SceneTitle} 等待 {eventName} 资源恢复。"
                    : $"当前正在 {context.Value.AreaTitle} / {context.Value.SceneTitle} 采集 {eventName}。"
                : isWaitingForRecovery
                    ? $"当前正在等待 {eventName} 资源恢复。"
                    : $"当前正在采集 {eventName}。";
            leadingBars.Add(new StatusProgressData
            {
                DynamicKey = IdleTargetProgressKey,
                Ratio = isWaitingForRecovery ? 0.0 : _gameManager.IdleSystem.GetProgressRatio(eventId),
                Visible = !isWaitingForRecovery,
                TooltipText = isWaitingForRecovery ? "当前处于资源恢复等待阶段。" : tooltip
            });
        }

        return new StatusSectionData
        {
            SectionId = "target",
            Title = _theme.Status.TargetSectionTitle,
            TooltipText = "显示当前正在进行的目标。",
            LeadingProgressBars = leadingBars,
            Items = new List<StatusItemData>
            {
                new()
                {
                    DynamicKey = "target:text",
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
        if (_gameManager?.StapleFoodSystem != null)
        {
            StapleFoodSystem.ActiveStapleView? staple = _gameManager.StapleFoodSystem.GetActiveStaple();
            if (staple != null)
            {
                items.Add(new StatusItemData
                {
                    DynamicKey = $"staple:{staple.StapleId}",
                    Text = $"主食：{staple.DisplayName}({FormatDuration(staple.RemainingSeconds)})",
                    TooltipText = BuildStapleTooltip(staple),
                    IsAccent = true
                });
            }
        }

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
        _dynamicProgressBars.Clear();

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

            AddProgressBars(content, section.LeadingProgressBars);

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

            AddProgressBars(content, section.TrailingProgressBars);
        }
    }

    private string BuildStructureSignature(IEnumerable<StatusSectionData> sections)
    {
        return string.Join("|", sections.SelectMany(section =>
            new[]
            {
                $"{section.SectionId}:{_expandedStates.GetValueOrDefault(section.SectionId, true)}",
                $"{section.Title}",
                $"lead:{string.Join(",", section.LeadingProgressBars.Select(progress => $"{progress.DynamicKey}:{progress.Visible}:{progress.Height:0.##}"))}",
                $"trail:{string.Join(",", section.TrailingProgressBars.Select(progress => $"{progress.DynamicKey}:{progress.Visible}:{progress.Height:0.##}"))}"
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

        foreach (StatusProgressData progress in sections.SelectMany(section => section.LeadingProgressBars.Concat(section.TrailingProgressBars)))
        {
            if (string.IsNullOrWhiteSpace(progress.DynamicKey))
            {
                continue;
            }

            if (!_dynamicProgressBars.TryGetValue(progress.DynamicKey, out ProgressBar? progressBar) || !IsInstanceValid(progressBar))
            {
                continue;
            }

            bool shouldShow = progress.Visible;
            if (progressBar.Visible != shouldShow)
            {
                progressBar.Visible = shouldShow;
            }

            double clampedValue = Math.Max(0.0, Math.Min(100.0, progress.Ratio * 100.0));
            if (Math.Abs(progressBar.Value - clampedValue) > 0.02)
            {
                progressBar.Value = clampedValue;
            }

            if (!string.Equals(progressBar.TooltipText, progress.TooltipText, StringComparison.Ordinal))
            {
                progressBar.TooltipText = progress.TooltipText;
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

    private void AddProgressBars(Control parent, IReadOnlyList<StatusProgressData> progressBars)
    {
        foreach (StatusProgressData progress in progressBars)
        {
            ProgressBar progressBar = new()
            {
                MinValue = 0,
                MaxValue = 100,
                Value = Math.Max(0.0, Math.Min(100.0, progress.Ratio * 100.0)),
                ShowPercentage = false,
                Visible = progress.Visible,
                CustomMinimumSize = new Vector2(0, progress.Height <= 0.0f ? 8.0f : progress.Height),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = progress.TooltipText
            };
            progressBar.AddThemeStyleboxOverride("fill", CreateProgressFillStyle(progress.FillColor));
            progressBar.AddThemeStyleboxOverride("background", CreateProgressBackgroundStyle(progress.BackgroundColor));
            parent.AddChild(progressBar);
            RegisterDynamicProgressBar(progress, progressBar);
        }
    }

    private void RegisterDynamicProgressBar(StatusProgressData progress, ProgressBar progressBar)
    {
        if (string.IsNullOrWhiteSpace(progress.DynamicKey))
        {
            return;
        }

        _dynamicProgressBars[progress.DynamicKey] = progressBar;
        if (string.Equals(progress.DynamicKey, IdleTargetProgressKey, StringComparison.Ordinal))
        {
            _targetProgressBar = progressBar;
        }
    }

    private static StyleBoxFlat CreateProgressFillStyle(Color? fillColor = null)
    {
        return new StyleBoxFlat
        {
            BgColor = fillColor ?? ProgressFillColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
    }

    private static StyleBoxFlat CreateProgressBackgroundStyle(Color? backgroundColor = null)
    {
        return new StyleBoxFlat
        {
            BgColor = backgroundColor ?? ProgressBackgroundColor,
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

    private static string BuildStapleTooltip(StapleFoodSystem.ActiveStapleView staple)
    {
        List<string> lines = new();
        if (!string.IsNullOrWhiteSpace(staple.Description))
        {
            lines.Add(staple.Description);
        }

        lines.Add($"剩余时间：{FormatDuration(staple.RemainingSeconds)}");
        lines.Add($"生命上限：+{staple.BattleStats.MaxHpFlat:0.##}");

        if (staple.BattleStats.RegenHps > 0.0)
        {
            lines.Add($"生命回复：+{staple.BattleStats.RegenHps:0.##}/秒");
        }

        if (staple.BattleStats.DamageAddPercent != 0.0)
        {
            lines.Add($"伤害加成：{staple.BattleStats.DamageAddPercent:P0}");
        }

        if (staple.BattleStats.CritChance != 0.0)
        {
            lines.Add($"暴击率：{staple.BattleStats.CritChance:P0}");
        }

        if (staple.BattleStats.AttackIntervalPercent != 0.0)
        {
            double speedPercent = -staple.BattleStats.AttackIntervalPercent;
            lines.Add(speedPercent >= 0.0
                ? $"攻速加成：{speedPercent:P0}"
                : $"攻击间隔增加：{Math.Abs(speedPercent):P0}");
        }

        return string.Join("\n", lines);
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

        if (string.Equals(modifier.StatId, SettlementStatIds.IdleSpeedMultiplier, StringComparison.Ordinal))
        {
            if (modifier.Multiplier > 0.0)
            {
                double timePercent = 100.0 / modifier.Multiplier;
                return $"采集速度倍率 x{modifier.Multiplier:0.###}（读条耗时约为原来的 {timePercent:0.#}%）";
            }

            return $"采集速度倍率 x{modifier.Multiplier:0.###}";
        }

        if (string.Equals(modifier.StatId, SettlementStatIds.IdleOutputMultiplier, StringComparison.Ordinal))
        {
            return $"采集产出倍率 x{modifier.Multiplier:0.###}";
        }

        if (modifier.StatId.StartsWith(speedPrefix, StringComparison.Ordinal)
            && modifier.StatId.EndsWith(suffix, StringComparison.Ordinal)
            && modifier.StatId.Length > speedPrefix.Length + suffix.Length)
        {
            string skillId = modifier.StatId[speedPrefix.Length..^suffix.Length];
            statName = $"{GetSkillDisplayName(skillId)}速度";
        }
        else if (modifier.StatId.StartsWith(outputPrefix, StringComparison.Ordinal)
            && modifier.StatId.EndsWith(suffix, StringComparison.Ordinal)
            && modifier.StatId.Length > outputPrefix.Length + suffix.Length)
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
        else if (string.Equals(modifier.StatId, SettlementStatIds.BattlePlayerMaxHpMultiplier, StringComparison.Ordinal))
        {
            statName = "战斗生命上限";
        }
        else if (string.Equals(modifier.StatId, SettlementStatIds.BattlePlayerActionSpeedMultiplier, StringComparison.Ordinal))
        {
            statName = "战斗行动速度";
        }
        else if (string.Equals(modifier.StatId, SettlementStatIds.DropRareChanceMultiplier, StringComparison.Ordinal))
        {
            statName = "稀有物品掉率";
        }
        else if (string.Equals(modifier.StatId, SettlementStatIds.DropChanceMultiplier, StringComparison.Ordinal))
        {
            statName = "物品掉率";
        }

        if (modifier.Multiplier > 0.0
            && (modifier.StatId.StartsWith(speedPrefix, StringComparison.Ordinal)
                || string.Equals(modifier.StatId, "idle.speed.multiplier", StringComparison.Ordinal)))
        {
            double timePercent = 100.0 / modifier.Multiplier;
            return $"{statName}倍率 x{modifier.Multiplier:0.###}（读条耗时约为原来的 {timePercent:0.#}%）";
        }

        if (modifier.Multiplier > 0.0
            && string.Equals(modifier.StatId, SettlementStatIds.BattlePlayerActionSpeedMultiplier, StringComparison.Ordinal))
        {
            double timePercent = 100.0 / modifier.Multiplier;
            return $"{statName}倍率 x{modifier.Multiplier:0.###}（攻击间隔约为原来的 {timePercent:0.#}%）";
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

    private bool TryBuildBattleTargetSection(
        PlayerProfile profile,
        ref string targetText,
        ref string tooltip,
        List<StatusProgressData> leadingBars,
        List<StatusProgressData> trailingBars)
    {
        if (_gameManager?.BattleSystem == null)
        {
            return false;
        }

        BattleSystem battle = _gameManager.BattleSystem;
        GameplayActionContext? context = ResolveBattleActionContext(profile);
        string encounterName = battle.IsBattleActive
            ? battle.CurrentEncounterDisplayName
            : ResolveSelectedBattleEncounterName(profile);
        bool hasBattleContext = battle.IsBattleActive
            || !string.IsNullOrWhiteSpace(encounterName)
            || !string.IsNullOrWhiteSpace(profile.UiState.SelectedBattleEncounterId)
            || !string.IsNullOrWhiteSpace(profile.UiState.SelectedBattleEventId);
        if (!hasBattleContext)
        {
            return false;
        }

        string areaTitle = context?.AreaTitle ?? string.Empty;
        string sceneTitle = context?.SceneTitle ?? string.Empty;
        string locationText = !string.IsNullOrWhiteSpace(areaTitle) ? areaTitle : (!string.IsNullOrWhiteSpace(sceneTitle) ? sceneTitle : "未知区域");
        string safeEncounterName = string.IsNullOrWhiteSpace(encounterName) ? "未选择敌人" : encounterName;

        targetText = battle.IsBattleActive
            ? $"正在{locationText}挑战{safeEncounterName}"
            : $"正准备向{locationText}的{safeEncounterName}发起挑战";

        List<string> tooltipLines = new()
        {
            battle.IsBattleActive ? "当前处于战斗中。" : "当前处于战斗待机状态。"
        };
        if (!string.IsNullOrWhiteSpace(areaTitle) || !string.IsNullOrWhiteSpace(sceneTitle))
        {
            tooltipLines.Add($"区域：{(string.IsNullOrWhiteSpace(areaTitle) ? "未记录" : areaTitle)} / {(string.IsNullOrWhiteSpace(sceneTitle) ? "未记录" : sceneTitle)}");
        }

        tooltipLines.Add($"目标：{safeEncounterName}");

        if (battle.IsBattleActive)
        {
            double playerMaxHp = Math.Max(1.0, battle.CurrentPlayerMaxHp);
            double playerHpRatio = Math.Clamp(battle.CurrentPlayerHp / playerMaxHp, 0.0, 1.0);
            double attackRatio = Math.Clamp(battle.GetPlayerAttackProgressRatio(), 0.0, 1.0);
            string hpTooltip = $"当前生命：{battle.CurrentPlayerHp:0.##}/{playerMaxHp:0.##}";
            string attackTooltip = $"当前攻击进度：{attackRatio:P0}";

            leadingBars.Add(new StatusProgressData
            {
                DynamicKey = BattleTargetHpProgressKey,
                Ratio = playerHpRatio,
                Visible = true,
                FillColor = BattleHpFillColor,
                BackgroundColor = BattleHpBackgroundColor,
                Height = 10.0f,
                TooltipText = hpTooltip
            });

            trailingBars.Add(new StatusProgressData
            {
                DynamicKey = BattleTargetAttackProgressKey,
                Ratio = attackRatio,
                Visible = true,
                FillColor = BattleAttackFillColor,
                BackgroundColor = BattleAttackBackgroundColor,
                Height = 8.0f,
                TooltipText = attackTooltip
            });

            tooltipLines.Add(hpTooltip);
            tooltipLines.Add(attackTooltip);
        }

        tooltip = string.Join("\n", tooltipLines);
        return true;
    }

    private GameplayActionContext? ResolveBattleActionContext(PlayerProfile profile)
    {
        if (_actionContextResolver == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(profile.UiState.SelectedBattleEventId))
        {
            GameplayActionContext? context = _actionContextResolver.Invoke(profile.UiState.SelectedBattleEventId);
            if (context.HasValue)
            {
                return context;
            }
        }

        return null;
    }

    private string ResolveSelectedBattleEncounterName(PlayerProfile profile)
    {
        if (_gameManager == null)
        {
            return string.Empty;
        }

        string encounterId = profile.UiState.SelectedBattleEncounterId;
        if (string.IsNullOrWhiteSpace(encounterId))
        {
            return string.Empty;
        }

        BattleEncounterDefinition? definition = _gameManager.BattleEncounterRegistry.GetEncounter(encounterId);
        return definition?.GetDisplayName(_gameManager.TranslateText) ?? encounterId;
    }

    private sealed class StatusSectionData
    {
        public string SectionId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string TooltipText { get; set; } = string.Empty;

        public List<StatusProgressData> LeadingProgressBars { get; set; } = new();

        public List<StatusProgressData> TrailingProgressBars { get; set; } = new();

        public List<StatusItemData> Items { get; set; } = new();
    }

    private sealed class StatusProgressData
    {
        public string DynamicKey { get; set; } = string.Empty;

        public double Ratio { get; set; }

        public bool Visible { get; set; } = true;

        public string TooltipText { get; set; } = string.Empty;

        public float Height { get; set; } = 8.0f;

        public Color FillColor { get; set; } = ProgressFillColor;

        public Color BackgroundColor { get; set; } = ProgressBackgroundColor;
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
