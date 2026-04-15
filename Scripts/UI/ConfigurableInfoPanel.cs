using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Scenario;

namespace Test00_0410.UI;

/// <summary>
/// 右上角可配置信息面板。
/// 负责读取当前剧本对应的 info_panel.yaml，并渲染摘要与提示文本。
/// </summary>
public partial class ConfigurableInfoPanel : Control
{
    private Label? _summaryLabel;
    private RichTextLabel? _contentLabel;
    private MainUiLayoutSettings _layoutSettings = new();
    private bool _useStitchStyle;
    private string _loadedConfigPath = string.Empty;
    private ScenarioInfoPanelConfig _currentConfig = ScenarioInfoPanelConfig.CreateDefault();
    private string _defaultSummaryText = "当前剧本：未载入";
    private string _defaultContentText = string.Empty;
    private bool _hasTransientContent;
    private string _transientSummaryText = string.Empty;
    private string _transientContentText = string.Empty;

    public bool HasTransientContent => _hasTransientContent;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(MainUiLayoutSettings layoutSettings, bool useStitchStyle = false)
    {
        _layoutSettings = layoutSettings;
        _useStitchStyle = useStitchStyle;
        EnsureStructure();
        ApplyVisualTheme();
    }

    public void RefreshPanel(GameManager gameManager)
    {
        EnsureStructure();

        GameScenarioDefinition? scenario = gameManager.ActiveScenario;
        string configPath = scenario?.InfoPanelConfigPath ?? string.Empty;
        if (!string.Equals(configPath, _loadedConfigPath, StringComparison.Ordinal))
        {
            _loadedConfigPath = configPath;
            _currentConfig = LoadConfig(configPath);
        }

        Dictionary<string, string> tokens = BuildTokens(gameManager, scenario);
        _defaultSummaryText = string.Join("\n", _currentConfig.SummaryLines.Select(line => ReplaceTokens(line, tokens)));

        IReadOnlyList<string> contentLines = SelectContentLines(gameManager, scenario);
        _defaultContentText = string.Join("\n", contentLines.Select(line => ReplaceTokens(line, tokens)));
        ApplyDisplayState();
    }

    public void SetTransientContent(string summaryText, string contentText)
    {
        EnsureStructure();

        _hasTransientContent = true;
        _transientSummaryText = string.IsNullOrWhiteSpace(summaryText) ? "悬浮说明" : summaryText;
        _transientContentText = string.IsNullOrWhiteSpace(contentText) ? "暂无说明。" : contentText;
        ApplyDisplayState();
    }

    public void ClearTransientContent()
    {
        if (!_hasTransientContent)
        {
            return;
        }

        _hasTransientContent = false;
        _transientSummaryText = string.Empty;
        _transientContentText = string.Empty;
        ApplyDisplayState();
    }

    private void EnsureStructure()
    {
        if (_summaryLabel != null && _contentLabel != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);

        MarginContainer margin = new()
        {
            Name = "Margin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        _summaryLabel = new Label
        {
            Name = "SummaryLabel",
            Text = "当前剧本：未载入",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _summaryLabel.AddThemeFontSizeOverride("font_size", _layoutSettings.SectionHeaderFontSize);
        root.AddChild(_summaryLabel);

        _contentLabel = new RichTextLabel
        {
            Name = "ContentLabel",
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _contentLabel.AddThemeFontSizeOverride("normal_font_size", _layoutSettings.BodyFontSize);
        _contentLabel.AddThemeConstantOverride("line_separation", 6);
        root.AddChild(_contentLabel);

        ApplyVisualTheme();
        ApplyDisplayState();
    }

    private void ApplyVisualTheme()
    {
        if (_summaryLabel == null || _contentLabel == null)
        {
            return;
        }

        if (_useStitchStyle)
        {
            _summaryLabel.AddThemeColorOverride("font_color", new Color("#224545"));
            _contentLabel.AddThemeColorOverride("default_color", new Color("#30332e"));
            return;
        }

        _summaryLabel.AddThemeColorOverride("font_color", new Color("#c9ffe6"));
        _contentLabel.AddThemeColorOverride("default_color", new Color("#d8f8ea"));
    }

    private void ApplyDisplayState()
    {
        if (_summaryLabel == null || _contentLabel == null)
        {
            return;
        }

        if (_hasTransientContent)
        {
            _summaryLabel.Text = _transientSummaryText;
            _contentLabel.Text = _transientContentText;
            return;
        }

        _summaryLabel.Text = _defaultSummaryText;
        _contentLabel.Text = _defaultContentText;
    }

    private IReadOnlyList<string> SelectContentLines(GameManager gameManager, GameScenarioDefinition? scenario)
    {
        if (scenario == null)
        {
            return _currentConfig.NoScenarioLines;
        }

        if (scenario.IsTestScenario)
        {
            return _currentConfig.TestScenarioLines.Count > 0
                ? _currentConfig.TestScenarioLines
                : _currentConfig.StoryDefaultLines;
        }

        if (gameManager.GetRegisteredEventCount() == 0)
        {
            return _currentConfig.StoryEmptyLines.Count > 0
                ? _currentConfig.StoryEmptyLines
                : _currentConfig.StoryDefaultLines;
        }

        return _currentConfig.StoryDefaultLines;
    }

    private static ScenarioInfoPanelConfig LoadConfig(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ScenarioInfoPanelConfig.CreateDefault();
        }

        YamlConfigLoader loader = new();
        IReadOnlyList<string> bundleFiles = loader.ResolveYamlFileBundle(path, "info_panel");
        if (bundleFiles.Count == 0)
        {
            return ScenarioInfoPanelConfig.CreateDefault();
        }

        Dictionary<string, object?> root = loader.LoadMergedMap(path, "info_panel");
        ScenarioInfoPanelConfig config = ScenarioInfoPanelConfig.CreateDefault();

        config.SummaryLines = ReadStringList(root, "summary_lines", config.SummaryLines);

        if (root.TryGetValue("states", out object? statesValue)
            && statesValue is Dictionary<string, object?> statesMap)
        {
            config.NoScenarioLines = ReadStateLines(statesMap, "no_scenario", config.NoScenarioLines);
            config.TestScenarioLines = ReadStateLines(statesMap, "test_scenario", config.TestScenarioLines);
            config.StoryEmptyLines = ReadStateLines(statesMap, "story_empty", config.StoryEmptyLines);
            config.StoryDefaultLines = ReadStateLines(statesMap, "story_default", config.StoryDefaultLines);
        }

        return config;
    }

    private static List<string> ReadStateLines(Dictionary<string, object?> statesMap, string stateKey, List<string> fallback)
    {
        if (!statesMap.TryGetValue(stateKey, out object? stateValue)
            || stateValue is not Dictionary<string, object?> stateMap)
        {
            return fallback;
        }

        return ReadStringList(stateMap, "content_lines", fallback);
    }

    private static List<string> ReadStringList(Dictionary<string, object?> map, string key, List<string> fallback)
    {
        if (!map.TryGetValue(key, out object? value) || value is not List<object?> rawList)
        {
            return fallback;
        }

        List<string> lines = rawList
            .Select(item => item?.ToString() ?? string.Empty)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        return lines.Count > 0 ? lines : fallback;
    }

    private static Dictionary<string, string> BuildTokens(GameManager gameManager, GameScenarioDefinition? scenario)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["scenario_display_name"] = scenario?.DisplayName ?? "未载入",
            ["scenario_id"] = scenario?.ScenarioId ?? string.Empty,
            ["scenario_description"] = scenario?.Description ?? string.Empty,
            ["event_count"] = gameManager.GetRegisteredEventCount().ToString(),
            ["is_test_scenario"] = (scenario?.IsTestScenario ?? false) ? "true" : "false",
            ["system_tab_enabled"] = (scenario?.EnableSystemTab ?? false) ? "true" : "false",
            ["current_main_quest"] = gameManager.QuestSystem?.GetCurrentMainQuestLabel() ?? "暂无主线任务"
        };
    }

    private static string ReplaceTokens(string text, IReadOnlyDictionary<string, string> tokens)
    {
        string result = text;
        foreach ((string key, string value) in tokens)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private sealed class ScenarioInfoPanelConfig
    {
        public List<string> SummaryLines { get; set; } = new();

        public List<string> NoScenarioLines { get; set; } = new();

        public List<string> TestScenarioLines { get; set; } = new();

        public List<string> StoryEmptyLines { get; set; } = new();

        public List<string> StoryDefaultLines { get; set; } = new();

        public static ScenarioInfoPanelConfig CreateDefault()
        {
            return new ScenarioInfoPanelConfig
            {
                SummaryLines = new List<string>
                {
                    "当前剧本：{{scenario_display_name}}",
                    "当前主线：{{current_main_quest}}"
                },
                NoScenarioLines = new List<string>
                {
                    "请先从主菜单进入一个剧本。"
                },
                TestScenarioLines = new List<string>
                {
                    "上手提示：",
                    "1. 先点“捡起石斧”",
                    "2. 再点“点击砍树”或“挂机砍树”",
                    "3. 技能有经验后再去“技能页手动升级”"
                },
                StoryEmptyLines = new List<string>
                {
                    "当前主线剧本还没有配置事件。",
                    "这正是主线与测试剧本分离后的正常状态。",
                    "后续你可以直接去 Configs/Scenarios/MainStory/ 下逐步填写 YAML。"
                },
                StoryDefaultLines = new List<string>
                {
                    "当前为正式剧本模式。",
                    "建议在“系统”标签里手动保存存档。",
                    "如果你要继续扩写剧情，优先修改当前剧本对应的 YAML 配置。"
                }
            };
        }
    }
}
