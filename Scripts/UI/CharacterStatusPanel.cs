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
/// 右侧角色状态栏。
/// 这一版改成了“可折叠大类 + 子项列表”的结构，
/// 这样后面继续加工具、技能、挂机状态时会更清晰。
/// </summary>
public partial class CharacterStatusPanel : Control
{
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _rootContainer;
    private GameManager? _gameManager;
    private MainUiLayoutSettings _layoutSettings = new();
    private string _lastStatusSignature = string.Empty;
    private readonly Dictionary<string, bool> _expandedStates = new()
    {
        ["economy"] = true,
        ["tool"] = true,
        ["skill"] = true,
        ["idle"] = true
    };

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(GameManager gameManager, MainUiLayoutSettings? layoutSettings = null)
    {
        _gameManager = gameManager;
        _layoutSettings = layoutSettings ?? new MainUiLayoutSettings();
        EnsureStructure();
    }

    public void RefreshStatus()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            RebuildContent(new[]
            {
                BuildInfoSection(
                    "empty",
                    "状态",
                    new[] { CreateItem("状态栏尚未绑定 GameManager。", "状态栏尚未绑定 GameManager。") })
            });
            return;
        }

        PlayerProfile profile = _gameManager.PlayerProfile;
        List<ItemDefinition> ownedTools = _gameManager.ItemRegistry.Items.Values
            .Where(item => item.HasTag(ItemTag.Tool) && profile.Inventory.HasItem(item.Id))
            .OrderBy(item => item.DefinitionOrder)
            .ThenBy(item => item.Id)
            .ToList();

        ItemDefinition? currentTool = _gameManager.GetBestOwnedTool(ItemTag.Axe);
        string currentToolId = currentTool?.Id ?? "none";

        List<StatusItemData> economyItems = new()
        {
            CreateItem($"金币：{profile.Economy.Gold}", "当前持有的金币数量。你后面可以把更详细的经济说明写在这里。")
        };

        List<StatusItemData> toolItems = new();
        if (ownedTools.Count == 0)
        {
            toolItems.Add(CreateItem("当前没有已获得工具", "当前工具附加信息"));
        }
        else
        {
            foreach (ItemDefinition tool in ownedTools)
            {
                string toolName = tool.GetDisplayName(_gameManager.TranslateText);
                string label = tool.Id == currentToolId ? $"{toolName}（当前使用）" : toolName;
                string detailText = string.IsNullOrWhiteSpace(tool.DetailDescriptionKey)
                    ? tool.GetDisplayDescription(_gameManager.TranslateText)
                    : _gameManager.TranslateText(tool.DetailDescriptionKey);
                toolItems.Add(CreateItem(label, detailText));
            }
        }

        List<StatusItemData> skillItems = new();
        foreach (SkillDefinition skillDefinition in _gameManager.SkillRegistry.Skills.Values.OrderBy(skill => skill.Id))
        {
            PlayerSkillState state = profile.GetOrCreateSkillState(skillDefinition.Id);
            skillItems.Add(CreateItem(
                $"{_gameManager.TranslateText(skillDefinition.NameKey)}等级：Lv.{state.Level}",
                _gameManager.TranslateText(skillDefinition.DescriptionKey)));
            skillItems.Add(CreateItem(
                $"{_gameManager.TranslateText(skillDefinition.NameKey)}经验：{FormatExpValue(state.StoredExp)}",
                $"{_gameManager.TranslateText(skillDefinition.NameKey)}当前已存储的经验值。"));
        }

        string idleStatusText = "当前未挂机";
        string progressText = "当前读条进度：0%";
        string idleTooltip = "当前挂机项目附加信息";
        if (_gameManager.IdleSystem != null && profile.IdleState.IsRunning && !string.IsNullOrWhiteSpace(profile.IdleState.ActiveEventId))
        {
            idleStatusText = $"当前挂机：{_gameManager.GetEventDisplayName(profile.IdleState.ActiveEventId)}";
            progressText = $"当前读条进度：{_gameManager.IdleSystem.GetProgressRatio(profile.IdleState.ActiveEventId):P0}";
            idleTooltip = "这里显示当前正在进行的挂机项目。";
        }

        List<StatusSectionData> sections = new()
        {
            BuildInfoSection("economy", "经济", economyItems),
            BuildInfoSection("tool", "工具", toolItems),
            BuildInfoSection("skill", "技能", skillItems),
            BuildInfoSection("idle", "当前挂机项目", new[]
            {
                CreateItem(idleStatusText, idleTooltip),
                CreateItem(progressText, "当前挂机项目的读条进度。")
            })
        };

        string nextSignature = BuildSignature(sections);
        if (nextSignature == _lastStatusSignature)
        {
            return;
        }

        _lastStatusSignature = nextSignature;
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
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _scrollContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_scrollContainer);

        _rootContainer = new VBoxContainer
        {
            Name = "RootContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _rootContainer.AddThemeConstantOverride("separation", 8);
        _scrollContainer.AddChild(_rootContainer);
    }

    private void RebuildContent(IEnumerable<StatusSectionData> sections)
    {
        if (_rootContainer == null)
        {
            return;
        }

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
            _rootContainer.AddChild(sectionPanel);

            VBoxContainer sectionContent = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            sectionContent.AddThemeConstantOverride("separation", 4);
            sectionPanel.AddChild(sectionContent);

            Button headerButton = new()
            {
                Text = $"{(_expandedStates.GetValueOrDefault(section.SectionId, true) ? "[-]" : "[+]")} {section.Title}",
                Alignment = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = section.TooltipText
            };
            headerButton.AddThemeFontSizeOverride("font_size", _layoutSettings.StatusCategoryFontSize);
            headerButton.AddThemeColorOverride("font_color", new Color("d62828"));
            headerButton.AddThemeColorOverride("font_hover_color", new Color("ef233c"));
            headerButton.AddThemeStyleboxOverride("normal", CreateFlatStyle(new Color(0, 0, 0, 0.08f)));
            headerButton.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(0, 0, 0, 0.14f)));
            headerButton.Pressed += () => ToggleSection(section.SectionId);
            sectionContent.AddChild(headerButton);

            if (!_expandedStates.GetValueOrDefault(section.SectionId, true))
            {
                continue;
            }

            VBoxContainer itemList = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            itemList.AddThemeConstantOverride("separation", 2);
            sectionContent.AddChild(itemList);

            foreach (StatusItemData item in section.Items)
            {
                Label label = new()
                {
                    Text = item.Text,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    TooltipText = item.TooltipText
                };
                label.AddThemeFontSizeOverride("font_size", _layoutSettings.StatusItemFontSize);
                label.AddThemeColorOverride("font_color", new Color("2563eb"));
                itemList.AddChild(label);
            }
        }
    }

    private void ToggleSection(string sectionId)
    {
        _expandedStates[sectionId] = !_expandedStates.GetValueOrDefault(sectionId, true);
        _lastStatusSignature = string.Empty;
        RefreshStatus();
    }

    private StatusSectionData BuildInfoSection(string sectionId, string title, IEnumerable<StatusItemData> items)
    {
        return new StatusSectionData
        {
            SectionId = sectionId,
            Title = title,
            TooltipText = $"{title}附加信息",
            Items = items.ToList()
        };
    }

    private static StatusItemData CreateItem(string text, string tooltipText)
    {
        return new StatusItemData
        {
            Text = text,
            TooltipText = tooltipText
        };
    }

    private string BuildSignature(IEnumerable<StatusSectionData> sections)
    {
        List<string> parts = new();
        foreach (StatusSectionData section in sections)
        {
            parts.Add($"{section.SectionId}:{_expandedStates.GetValueOrDefault(section.SectionId, true)}");
            parts.AddRange(section.Items.Select(item => $"{item.Text}:{item.TooltipText}"));
        }

        return string.Join("|", parts);
    }

    private static StyleBoxFlat CreateFlatStyle(Color backgroundColor)
    {
        return new StyleBoxFlat
        {
            BgColor = backgroundColor,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
    }

    private static string FormatExpValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString("0")
            : value.ToString("0.###");
    }

    private sealed class StatusSectionData
    {
        public string SectionId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string TooltipText { get; set; } = string.Empty;

        public List<StatusItemData> Items { get; set; } = new();
    }

    private sealed class StatusItemData
    {
        public string Text { get; set; } = string.Empty;

        public string TooltipText { get; set; } = string.Empty;
    }
}
