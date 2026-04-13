using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Scenario;

namespace Test00_0410.UI;

public readonly record struct GameplayActionContext(
    string AreaId,
    string AreaTitle,
    string SceneId,
    string SceneTitle);

public sealed class GameplayUiTheme
{
    public Color BackgroundColor { get; set; } = new("#000000");

    public int OuterMargin { get; set; } = 18;

    public int SectionGap { get; set; } = 14;

    public int InnerGap { get; set; } = 10;

    public int LeftRightColumnWidth { get; set; } = 320;

    public int TopBarHeight { get; set; } = 48;

    public int BottomLogHeight { get; set; } = 178;

    public int InfoPanelMinHeight { get; set; } = 228;

    public int TabBarHeight { get; set; } = 50;

    public int SceneCardMinWidth { get; set; } = 280;

    public int SceneButtonMinHeight { get; set; } = 52;

    public int ProgressBarHeight { get; set; } = 10;

    public int RegionHeaderFontSize { get; set; } = 24;

    public int RegionItemFontSize { get; set; } = 18;

    public int RegionCountFontSize { get; set; } = 13;

    public int SceneTitleFontSize { get; set; } = 20;

    public int BodyFontSize { get; set; } = 16;

    public int TabFontSize { get; set; } = 16;

    public int TabActiveFontSize { get; set; } = 20;

    public int StatusHeaderFontSize { get; set; } = 18;

    public int StatusBodyFontSize { get; set; } = 15;

    public int CornerRadius { get; set; } = 12;

    public PanelVisual TopBar { get; } = new();

    public PanelVisual LeftColumn { get; } = new();

    public PanelVisual CenterColumn { get; } = new();

    public PanelVisual BottomLog { get; } = new();

    public PanelVisual RightInfo { get; } = new();

    public PanelVisual RightStatus { get; } = new();

    public PanelVisual SceneCard { get; } = new();

    public TabVisual Tabs { get; } = new();

    public StatusVisual Status { get; } = new();

    public Dictionary<string, AccentVisual> RegionAccents { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentRegionTabText { get; set; } = "当前区域";

    public string InventoryTabText { get; set; } = "背包";

    public string SkillsTabText { get; set; } = "技能";

    public string DictionaryTabText { get; set; } = "图鉴";

    public string SystemTabText { get; set; } = "系统";

    public string TopBarScenarioFormat { get; set; } = "当前剧本：{0}";

    public string TopBarAreaFormat { get; set; } = "当前区域：{0}";

    public string InteractionCountFormat { get; set; } = "({0})";

    public string IdleMarkerText { get; set; } = "★";

    public string EmptySceneText { get; set; } = "当前场景暂无可互动内容。";

    public string EmptyAreaText { get; set; } = "当前区域暂无可显示场景。";

    public string FavoriteHintText { get; set; } = "右键场景标题可切换收藏置顶。";

    public AccentVisual GetRegionAccent(string accentId)
    {
        return RegionAccents.TryGetValue(accentId, out AccentVisual? accent)
            ? accent
            : AccentVisual.CreateFallback();
    }
}

public sealed class PanelVisual
{
    public Color BaseColor { get; set; } = new("#1f2937");

    public float Alpha { get; set; } = 0.25f;

    public Color BorderColor { get; set; } = new("#64748b");

    public float BorderAlpha { get; set; } = 0.6f;
}

public sealed class TabVisual
{
    public Color NormalColor { get; set; } = new("#1f2937");

    public float NormalAlpha { get; set; } = 0.42f;

    public Color ActiveColor { get; set; } = new("#35574a");

    public float ActiveAlpha { get; set; } = 0.82f;

    public Color NormalTextColor { get; set; } = new("#d1d5db");

    public Color ActiveTextColor { get; set; } = new("#f8fafc");

    public Color BorderColor { get; set; } = new("#7aa36f");
}

public sealed class StatusVisual
{
    public Color HeaderTextColor { get; set; } = new("#f4f4f5");

    public Color BodyTextColor { get; set; } = new("#d4d4d8");

    public Color AccentTextColor { get; set; } = new("#86efac");

    public Color ProgressFillColor { get; set; } = new("#f59e0b");

    public Color ProgressBackgroundColor { get; set; } = new("#201c14");

    public string TargetSectionTitle { get; set; } = "目标";

    public string SkillsSectionTitle { get; set; } = "技能";

    public string StatusSectionTitle { get; set; } = "状态";

    public string CurrencySectionTitle { get; set; } = "货币";

    public string ReputationSectionTitle { get; set; } = "声望";

    public string RestingText { get; set; } = "正在休息";

    public string EmptyStatusText { get; set; } = "暂无异常状态";

    public string EmptyReputationText { get; set; } = "暂无已解锁声望";
}

public sealed class AccentVisual
{
    public Color TextColor { get; set; } = new("#e5e7eb");

    public int FontSize { get; set; } = 18;

    public static AccentVisual CreateFallback()
    {
        return new AccentVisual();
    }
}

public sealed class ScenarioGameplayLayout
{
    public string DefaultAreaId { get; set; } = string.Empty;

    public List<PrimaryRegionLayout> Regions { get; } = new();

    public IEnumerable<SecondaryAreaLayout> EnumerateAreas()
    {
        return Regions.SelectMany(region => region.Areas);
    }

    public SecondaryAreaLayout? FindArea(string areaId)
    {
        return EnumerateAreas().FirstOrDefault(area => string.Equals(area.Id, areaId, StringComparison.Ordinal));
    }

    public SceneLayout? FindScene(string sceneId)
    {
        return EnumerateAreas()
            .SelectMany(area => area.Scenes)
            .FirstOrDefault(scene => string.Equals(scene.Id, sceneId, StringComparison.Ordinal));
    }

    public GameplayActionContext? FindActionContext(string eventId)
    {
        foreach (SecondaryAreaLayout area in EnumerateAreas())
        {
            foreach (SceneLayout scene in area.Scenes)
            {
                if (scene.EventIds.Contains(eventId))
                {
                    return new GameplayActionContext(area.Id, area.Title, scene.Id, scene.Title);
                }
            }
        }

        return null;
    }

    public string ResolveDefaultAreaId()
    {
        if (!string.IsNullOrWhiteSpace(DefaultAreaId) && FindArea(DefaultAreaId) != null)
        {
            return DefaultAreaId;
        }

        return EnumerateAreas().FirstOrDefault()?.Id ?? string.Empty;
    }
}

public sealed class PrimaryRegionLayout
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string AccentId { get; set; } = "primary_story";

    public bool ExpandedByDefault { get; set; } = true;

    public int VisibilityConditionCount { get; set; }

    public List<EventConditionEntry> VisibilityConditions { get; } = new();

    public List<SecondaryAreaLayout> Areas { get; } = new();
}

public sealed class SecondaryAreaLayout
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string AccentId { get; set; } = "secondary_peace";

    public bool TwoColumnCandidate { get; set; }

    public int VisibilityConditionCount { get; set; }

    public List<EventConditionEntry> VisibilityConditions { get; } = new();

    public List<SceneLayout> Scenes { get; } = new();
}

public sealed class SceneLayout
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int VisibilityConditionCount { get; set; }

    public List<EventConditionEntry> VisibilityConditions { get; } = new();

    public List<string> EventIds { get; } = new();
}

public static class GameplayUiConfigLoader
{
    private const string ThemeConfigPath = "res://Configs/UI/gameplay_ui_theme.yaml";

    public static GameplayUiTheme LoadTheme()
    {
        YamlConfigLoader loader = new();
        Dictionary<string, object?> root = loader.LoadMap(ThemeConfigPath);
        GameplayUiTheme theme = new();

        if (root.TryGetValue("general", out object? generalValue) && generalValue is Dictionary<string, object?> generalMap)
        {
            theme.BackgroundColor = ParseColor(generalMap, "background_color", theme.BackgroundColor);
            theme.OuterMargin = GetInt(generalMap, "outer_margin", theme.OuterMargin);
            theme.SectionGap = GetInt(generalMap, "section_gap", theme.SectionGap);
            theme.InnerGap = GetInt(generalMap, "inner_gap", theme.InnerGap);
            theme.LeftRightColumnWidth = GetInt(generalMap, "left_right_column_width", theme.LeftRightColumnWidth);
            theme.TopBarHeight = GetInt(generalMap, "top_bar_height", theme.TopBarHeight);
            theme.BottomLogHeight = GetInt(generalMap, "bottom_log_height", theme.BottomLogHeight);
            theme.InfoPanelMinHeight = GetInt(generalMap, "info_panel_min_height", theme.InfoPanelMinHeight);
            theme.TabBarHeight = GetInt(generalMap, "tab_bar_height", theme.TabBarHeight);
            theme.SceneCardMinWidth = GetInt(generalMap, "scene_card_min_width", theme.SceneCardMinWidth);
            theme.SceneButtonMinHeight = GetInt(generalMap, "scene_button_min_height", theme.SceneButtonMinHeight);
            theme.ProgressBarHeight = GetInt(generalMap, "progress_bar_height", theme.ProgressBarHeight);
            theme.RegionHeaderFontSize = GetInt(generalMap, "region_header_font_size", theme.RegionHeaderFontSize);
            theme.RegionItemFontSize = GetInt(generalMap, "region_item_font_size", theme.RegionItemFontSize);
            theme.RegionCountFontSize = GetInt(generalMap, "region_count_font_size", theme.RegionCountFontSize);
            theme.SceneTitleFontSize = GetInt(generalMap, "scene_title_font_size", theme.SceneTitleFontSize);
            theme.BodyFontSize = GetInt(generalMap, "body_font_size", theme.BodyFontSize);
            theme.TabFontSize = GetInt(generalMap, "tab_font_size", theme.TabFontSize);
            theme.TabActiveFontSize = GetInt(generalMap, "tab_active_font_size", theme.TabActiveFontSize);
            theme.StatusHeaderFontSize = GetInt(generalMap, "status_header_font_size", theme.StatusHeaderFontSize);
            theme.StatusBodyFontSize = GetInt(generalMap, "status_body_font_size", theme.StatusBodyFontSize);
            theme.CornerRadius = GetInt(generalMap, "corner_radius", theme.CornerRadius);
        }

        if (root.TryGetValue("panels", out object? panelValue) && panelValue is Dictionary<string, object?> panelMap)
        {
            ApplyPanelVisual(panelMap, "top_bar", theme.TopBar);
            ApplyPanelVisual(panelMap, "left_column", theme.LeftColumn);
            ApplyPanelVisual(panelMap, "center_column", theme.CenterColumn);
            ApplyPanelVisual(panelMap, "bottom_log", theme.BottomLog);
            ApplyPanelVisual(panelMap, "right_info", theme.RightInfo);
            ApplyPanelVisual(panelMap, "right_status", theme.RightStatus);
            ApplyPanelVisual(panelMap, "scene_card", theme.SceneCard);
        }

        if (root.TryGetValue("tabs", out object? tabValue) && tabValue is Dictionary<string, object?> tabMap)
        {
            theme.CurrentRegionTabText = GetString(tabMap, "current_region_text", theme.CurrentRegionTabText);
            theme.InventoryTabText = GetString(tabMap, "inventory_text", theme.InventoryTabText);
            theme.SkillsTabText = GetString(tabMap, "skills_text", theme.SkillsTabText);
            theme.DictionaryTabText = GetString(tabMap, "dictionary_text", theme.DictionaryTabText);
            theme.SystemTabText = GetString(tabMap, "system_text", theme.SystemTabText);
            theme.Tabs.NormalColor = ParseColor(tabMap, "normal_color", theme.Tabs.NormalColor);
            theme.Tabs.NormalAlpha = GetFloat(tabMap, "normal_alpha", theme.Tabs.NormalAlpha);
            theme.Tabs.ActiveColor = ParseColor(tabMap, "active_color", theme.Tabs.ActiveColor);
            theme.Tabs.ActiveAlpha = GetFloat(tabMap, "active_alpha", theme.Tabs.ActiveAlpha);
            theme.Tabs.NormalTextColor = ParseColor(tabMap, "normal_text_color", theme.Tabs.NormalTextColor);
            theme.Tabs.ActiveTextColor = ParseColor(tabMap, "active_text_color", theme.Tabs.ActiveTextColor);
            theme.Tabs.BorderColor = ParseColor(tabMap, "border_color", theme.Tabs.BorderColor);
        }

        if (root.TryGetValue("status", out object? statusValue) && statusValue is Dictionary<string, object?> statusMap)
        {
            theme.Status.HeaderTextColor = ParseColor(statusMap, "header_text_color", theme.Status.HeaderTextColor);
            theme.Status.BodyTextColor = ParseColor(statusMap, "body_text_color", theme.Status.BodyTextColor);
            theme.Status.AccentTextColor = ParseColor(statusMap, "accent_text_color", theme.Status.AccentTextColor);
            theme.Status.ProgressFillColor = ParseColor(statusMap, "progress_fill_color", theme.Status.ProgressFillColor);
            theme.Status.ProgressBackgroundColor = ParseColor(statusMap, "progress_background_color", theme.Status.ProgressBackgroundColor);
            theme.Status.TargetSectionTitle = GetString(statusMap, "target_title", theme.Status.TargetSectionTitle);
            theme.Status.SkillsSectionTitle = GetString(statusMap, "skills_title", theme.Status.SkillsSectionTitle);
            theme.Status.StatusSectionTitle = GetString(statusMap, "status_title", theme.Status.StatusSectionTitle);
            theme.Status.CurrencySectionTitle = GetString(statusMap, "currency_title", theme.Status.CurrencySectionTitle);
            theme.Status.ReputationSectionTitle = GetString(statusMap, "reputation_title", theme.Status.ReputationSectionTitle);
            theme.Status.RestingText = GetString(statusMap, "resting_text", theme.Status.RestingText);
            theme.Status.EmptyStatusText = GetString(statusMap, "empty_status_text", theme.Status.EmptyStatusText);
            theme.Status.EmptyReputationText = GetString(statusMap, "empty_reputation_text", theme.Status.EmptyReputationText);
        }

        if (root.TryGetValue("labels", out object? labelsValue) && labelsValue is Dictionary<string, object?> labelsMap)
        {
            theme.TopBarScenarioFormat = GetString(labelsMap, "top_bar_scenario_format", theme.TopBarScenarioFormat);
            theme.TopBarAreaFormat = GetString(labelsMap, "top_bar_area_format", theme.TopBarAreaFormat);
            theme.InteractionCountFormat = GetString(labelsMap, "interaction_count_format", theme.InteractionCountFormat);
            theme.IdleMarkerText = GetString(labelsMap, "idle_marker_text", theme.IdleMarkerText);
            theme.EmptySceneText = GetString(labelsMap, "empty_scene_text", theme.EmptySceneText);
            theme.EmptyAreaText = GetString(labelsMap, "empty_area_text", theme.EmptyAreaText);
            theme.FavoriteHintText = GetString(labelsMap, "favorite_hint_text", theme.FavoriteHintText);
        }

        if (root.TryGetValue("region_accents", out object? accentsValue) && accentsValue is Dictionary<string, object?> accentsMap)
        {
            foreach ((string accentId, object? accentValue) in accentsMap)
            {
                if (accentValue is not Dictionary<string, object?> accentMap)
                {
                    continue;
                }

                theme.RegionAccents[accentId] = new AccentVisual
                {
                    TextColor = ParseColor(accentMap, "text_color", new Color("#e5e7eb")),
                    FontSize = GetInt(accentMap, "font_size", theme.RegionItemFontSize)
                };
            }
        }

        return theme;
    }

    public static ScenarioGameplayLayout LoadScenarioLayout(GameScenarioDefinition? scenario)
    {
        ScenarioGameplayLayout layout = new();
        string path = ResolveScenarioLayoutPath(scenario);
        if (string.IsNullOrWhiteSpace(path))
        {
            return layout;
        }

        YamlConfigLoader loader = new();
        IReadOnlyList<string> bundleFiles = loader.ResolveYamlFileBundle(path, "gameplay_layout");
        if (bundleFiles.Count == 0)
        {
            return layout;
        }

        Dictionary<string, object?> root = loader.LoadMergedMap(path, "gameplay_layout");
        layout.DefaultAreaId = GetString(root, "default_area_id");
        Dictionary<string, string> eventSceneBindings = new(StringComparer.Ordinal);
        HashSet<string> regionIds = new(StringComparer.Ordinal);
        HashSet<string> areaIds = new(StringComparer.Ordinal);
        HashSet<string> sceneIds = new(StringComparer.Ordinal);

        foreach (Dictionary<string, object?> regionMap in GetMapList(root, "regions"))
        {
            PrimaryRegionLayout region = new()
            {
                Id = GetString(regionMap, "id"),
                Title = GetString(regionMap, "title"),
                AccentId = GetString(regionMap, "accent", "primary_story"),
                ExpandedByDefault = GetBool(regionMap, "expanded_by_default", true),
                VisibilityConditionCount = GetInt(regionMap, "visibility_condition_count", 0)
            };
            region.VisibilityConditions.AddRange(ParseConditions(regionMap, "visibility_conditions"));
            if (!regionIds.Add(region.Id))
            {
                GD.PushWarning($"[GameplayUiConfig] 检测到重复一级区域 ID：{region.Id}。后续重复区域仍会继续加载，请尽快整理配置。");
            }
            ValidateConditionCount(path, $"一级区域 {region.Id}", region.VisibilityConditionCount, region.VisibilityConditions.Count);

            foreach (Dictionary<string, object?> areaMap in GetMapList(regionMap, "areas"))
            {
                SecondaryAreaLayout area = new()
                {
                    Id = GetString(areaMap, "id"),
                    Title = GetString(areaMap, "title"),
                    AccentId = GetString(areaMap, "accent", "secondary_peace"),
                    TwoColumnCandidate = GetBool(areaMap, "two_column_candidate", false),
                    VisibilityConditionCount = GetInt(areaMap, "visibility_condition_count", 0)
                };
                area.VisibilityConditions.AddRange(ParseConditions(areaMap, "visibility_conditions"));
                if (!areaIds.Add(area.Id))
                {
                    GD.PushWarning($"[GameplayUiConfig] 检测到重复二级区域 ID：{area.Id}。后续重复区域仍会继续加载，请尽快整理配置。");
                }
                ValidateConditionCount(path, $"二级区域 {area.Id}", area.VisibilityConditionCount, area.VisibilityConditions.Count);

                foreach (Dictionary<string, object?> sceneMap in GetMapList(areaMap, "scenes"))
                {
                    SceneLayout scene = new()
                    {
                        Id = GetString(sceneMap, "id"),
                        Title = GetString(sceneMap, "title"),
                        Description = GetString(sceneMap, "description"),
                        VisibilityConditionCount = GetInt(sceneMap, "visibility_condition_count", 0)
                    };
                    scene.VisibilityConditions.AddRange(ParseConditions(sceneMap, "visibility_conditions"));
                    if (!sceneIds.Add(scene.Id))
                    {
                        GD.PushWarning($"[GameplayUiConfig] 检测到重复子场景 ID：{scene.Id}。后续重复子场景仍会继续加载，请尽快整理配置。");
                    }
                    ValidateConditionCount(path, $"子场景 {scene.Id}", scene.VisibilityConditionCount, scene.VisibilityConditions.Count);

                    foreach (string eventId in GetStringList(sceneMap, "event_ids"))
                    {
                        if (eventSceneBindings.TryGetValue(eventId, out string? existingSceneId))
                        {
                            GD.PushWarning($"[GameplayUiConfig] 事件 {eventId} 在 gameplay_layout.yaml 中被重复绑定到场景 {existingSceneId} 和 {scene.Id}，后者已忽略。");
                            continue;
                        }

                        eventSceneBindings[eventId] = scene.Id;
                        scene.EventIds.Add(eventId);
                    }

                    area.Scenes.Add(scene);
                }

                region.Areas.Add(area);
            }

            layout.Regions.Add(region);
        }

        return layout;
    }

    private static string ResolveScenarioLayoutPath(GameScenarioDefinition? scenario)
    {
        string infoPath = scenario?.InfoPanelConfigPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(infoPath))
        {
            return string.Empty;
        }

        if (DirAccess.Open(infoPath) != null)
        {
            int directorySlashIndex = infoPath.LastIndexOf('/');
            if (directorySlashIndex < 0)
            {
                return string.Empty;
            }

            return $"{infoPath[..directorySlashIndex]}/GameplayLayout";
        }

        int slashIndex = infoPath.LastIndexOf('/');
        if (slashIndex < 0)
        {
            return string.Empty;
        }

        return $"{infoPath[..slashIndex]}/gameplay_layout.yaml";
    }

    private static List<EventConditionEntry> ParseConditions(Dictionary<string, object?> map, string key)
    {
        List<EventConditionEntry> conditions = new();
        foreach (Dictionary<string, object?> conditionMap in GetMapList(map, key))
        {
            conditions.Add(new EventConditionEntry
            {
                ConditionType = ParseEnum(conditionMap, "condition_type", Core.Enums.ConditionType.None),
                TargetId = GetString(conditionMap, "target_id"),
                RequiredValue = GetDouble(conditionMap, "required_value", 0.0)
            });
        }

        return conditions;
    }

    private static void ValidateConditionCount(string path, string ownerLabel, int expectedCount, int actualCount)
    {
        if (expectedCount == actualCount)
        {
            return;
        }

        GD.PushError($"[GameplayUiConfig] {path} 中 {ownerLabel} 的 visibility_condition_count={expectedCount}，但 visibility_conditions 实际条目数为 {actualCount}。");
    }

    private static void ApplyPanelVisual(Dictionary<string, object?> panelMap, string key, PanelVisual target)
    {
        if (!panelMap.TryGetValue(key, out object? value) || value is not Dictionary<string, object?> map)
        {
            return;
        }

        target.BaseColor = ParseColor(map, "color", target.BaseColor);
        target.Alpha = GetFloat(map, "alpha", target.Alpha);
        target.BorderColor = ParseColor(map, "border_color", target.BorderColor);
        target.BorderAlpha = GetFloat(map, "border_alpha", target.BorderAlpha);
    }

    private static List<Dictionary<string, object?>> GetMapList(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out object? value) || value is not List<object?> rawList)
        {
            return new List<Dictionary<string, object?>>();
        }

        return rawList.OfType<Dictionary<string, object?>>().ToList();
    }

    private static List<string> GetStringList(Dictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out object? value) || value is not List<object?> rawList)
        {
            return new List<string>();
        }

        return rawList
            .Select(item => item?.ToString() ?? string.Empty)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static string GetString(Dictionary<string, object?> map, string key, string fallback = "")
    {
        return map.TryGetValue(key, out object? value)
            ? value?.ToString() ?? fallback
            : fallback;
    }

    private static int GetInt(Dictionary<string, object?> map, string key, int fallback)
    {
        return map.TryGetValue(key, out object? value)
            ? value switch
            {
                int intValue => intValue,
                double doubleValue => (int)Math.Round(doubleValue),
                string text when int.TryParse(text, out int parsed) => parsed,
                _ => fallback
            }
            : fallback;
    }

    private static double GetDouble(Dictionary<string, object?> map, string key, double fallback)
    {
        return map.TryGetValue(key, out object? value)
            ? value switch
            {
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                int intValue => intValue,
                string text when double.TryParse(text, out double parsed) => parsed,
                _ => fallback
            }
            : fallback;
    }

    private static float GetFloat(Dictionary<string, object?> map, string key, float fallback)
    {
        return map.TryGetValue(key, out object? value)
            ? value switch
            {
                float floatValue => floatValue,
                double doubleValue => (float)doubleValue,
                int intValue => intValue,
                string text when float.TryParse(text, out float parsed) => parsed,
                _ => fallback
            }
            : fallback;
    }

    private static bool GetBool(Dictionary<string, object?> map, string key, bool fallback)
    {
        return map.TryGetValue(key, out object? value)
            ? value switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out bool parsed) => parsed,
                _ => fallback
            }
            : fallback;
    }

    private static TEnum ParseEnum<TEnum>(Dictionary<string, object?> map, string key, TEnum fallback)
        where TEnum : struct, Enum
    {
        string text = GetString(map, key);
        return Enum.TryParse(text, true, out TEnum parsed) ? parsed : fallback;
    }

    private static Color ParseColor(Dictionary<string, object?> map, string key, Color fallback)
    {
        string text = GetString(map, key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        try
        {
            return new Color(text);
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
