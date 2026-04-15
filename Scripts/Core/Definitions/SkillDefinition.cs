using System;
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

    /// <summary>
    /// 技能分组 ID，例如 collection / craft。
    /// 用于技能页分组、采集类逻辑识别与后续扩展。
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// 技能分组显示名。
    /// 可直接写中文文本，也可写本地化 key。
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    public int GroupOrder { get; set; }

    public int SkillOrder { get; set; }

    public int MaxLevel { get; set; } = 1;

    public int InitialLevel { get; set; } = 1;

    public int MaxTotalExp { get; set; }

    /// <summary>
    /// 启动这项技能所需的工具标签。
    /// 比如砍树技能要求玩家持有带 Axe 标签的工具。
    /// </summary>
    public ItemTag RequiredToolTag { get; set; } = ItemTag.None;

    public string PrimaryOutputItemId { get; set; } = string.Empty;

    /// <summary>
    /// 允许复用另一技能的等级表，减少重复 YAML。
    /// 当当前技能 level_table 为空时生效。
    /// </summary>
    public string InheritLevelTableFrom { get; set; } = string.Empty;

    public List<SkillLevelEntry> LevelTable { get; } = new();

    public bool IsCollectionSkill => string.Equals(GroupId, "collection", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 获取某一级的配置。
    /// 如果没有找到，就返回 null。
    /// </summary>
    public SkillLevelEntry? GetLevelEntry(int level)
    {
        return LevelTable.FirstOrDefault(entry => entry.Level == level);
    }

    public int GetRequiredTotalExpForLevel(int level)
    {
        if (level <= 1)
        {
            return 0;
        }

        SkillLevelEntry? entry = GetLevelEntry(level);
        if (entry != null)
        {
            return Math.Max(0, entry.ExpToNext);
        }

        return Math.Max(0, MaxTotalExp);
    }

    public int GetRequiredTotalExpForNextLevel(int currentLevel)
    {
        if (currentLevel >= MaxLevel)
        {
            return 0;
        }

        return GetRequiredTotalExpForLevel(currentLevel + 1);
    }
}
