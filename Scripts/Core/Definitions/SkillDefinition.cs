using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Enums;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 技能静态定义。
/// 例如砍树、钓鱼、生火、烹饪等，都可以用同一套结构表达。
/// </summary>
public class SkillDefinition
{
    public string Id { get; set; } = string.Empty;

    public string SourceFilePath { get; set; } = string.Empty;

    public int SourceFileOrder { get; set; }

    public int SourceEntryOrder { get; set; }

    public string NameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    public int MaxLevel { get; set; } = 1;

    public int InitialLevel { get; set; } = 1;

    public int MaxTotalExp { get; set; }

    /// <summary>
    /// 启动这项技能所需的工具标签。
    /// 比如砍树技能要求玩家持有带 Axe 标签的工具。
    /// </summary>
    public ItemTag RequiredToolTag { get; set; } = ItemTag.None;

    public string PrimaryOutputItemId { get; set; } = string.Empty;

    public List<SkillLevelEntry> LevelTable { get; } = new();

    /// <summary>
    /// 获取某一级的配置。
    /// 如果没有找到，就返回 null。
    /// </summary>
    public SkillLevelEntry? GetLevelEntry(int level)
    {
        return LevelTable.FirstOrDefault(entry => entry.Level == level);
    }
}
