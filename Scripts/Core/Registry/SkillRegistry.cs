using System.Collections.Generic;
using Test00_0410.Core.Definitions;

namespace Test00_0410.Core.Registry;

/// <summary>
/// 技能注册表。
/// 统一管理所有技能定义。
/// </summary>
public class SkillRegistry
{
    private readonly Dictionary<string, SkillDefinition> _skills = new();

    public IReadOnlyDictionary<string, SkillDefinition> Skills => _skills;

    public void LoadDefinitions(IEnumerable<SkillDefinition> definitions)
    {
        _skills.Clear();

        foreach (SkillDefinition definition in definitions)
        {
            _skills[definition.Id] = definition;
        }
    }

    public SkillDefinition? GetSkill(string id)
    {
        return _skills.GetValueOrDefault(id);
    }
}
