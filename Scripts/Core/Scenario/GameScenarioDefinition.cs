using Godot;

namespace Test00_0410.Core.Scenario;

/// <summary>
/// 单个剧本的配置定义。
/// 你可以把它理解成“这个剧本到底要读哪一套事件、物品、技能、区域配置文件”。
/// </summary>
[GlobalClass]
public partial class GameScenarioDefinition : Resource
{
    [Export]
    public string ScenarioId { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Export]
    public bool ShowInNewGameMenu { get; set; } = true;

    [Export]
    public bool IsDefaultStoryScenario { get; set; }

    [Export]
    public bool IsTestScenario { get; set; }

    [Export]
    public bool EnableSystemTab { get; set; } = true;

    // AI reminder:
    // - Item categories/items must point to the shared global library under res://Configs/Items/.
    // - Scenarios may selectively use shared items, but must not redefine the same ids per scenario.
    // - Keep UTF-8 when editing config files so existing Chinese text stays intact.
    [Export]
    public string CategoriesConfigPath { get; set; } = string.Empty;

    [Export]
    public string ItemsConfigPath { get; set; } = string.Empty;

    [Export]
    public string SkillsConfigPath { get; set; } = string.Empty;

    [Export]
    public string OneshotEventsConfigPath { get; set; } = string.Empty;

    [Export]
    public string ClickEventsConfigPath { get; set; } = string.Empty;

    [Export]
    public string IdleEventsConfigPath { get; set; } = string.Empty;

    [Export]
    public string FactionsConfigPath { get; set; } = string.Empty;

    [Export]
    public string ZonesConfigPath { get; set; } = string.Empty;

    [Export]
    public string BattleEncountersConfigPath { get; set; } = string.Empty;

    [Export]
    public string LocalizationPath { get; set; } = string.Empty;

    [Export]
    public string InfoPanelConfigPath { get; set; } = string.Empty;

    [Export]
    public string UiTabsConfigPath { get; set; } = string.Empty;

    [Export]
    public string QuestsConfigPath { get; set; } = string.Empty;
}
