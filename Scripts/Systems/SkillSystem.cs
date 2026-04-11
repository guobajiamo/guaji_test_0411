using Godot;
using System;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Registry;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 技能系统。
/// 负责经验累积、升级判定和读取等级表配置。
/// </summary>
public partial class SkillSystem : Node
{
    private PlayerProfile? _profile;
    private SkillRegistry? _skillRegistry;

    public void Configure(PlayerProfile profile, SkillRegistry skillRegistry)
    {
        _profile = profile;
        _skillRegistry = skillRegistry;
    }

    public void AddExp(string skillId, double exp)
    {
        if (_profile == null || exp <= 0)
        {
            return;
        }

        PlayerSkillState state = _profile.GetOrCreateSkillState(skillId);
        SkillDefinition? definition = _skillRegistry?.GetSkill(skillId);
        double nextStoredExp = state.StoredExp + exp;
        double nextTotalExp = state.TotalEarnedExp + exp;

        if (definition != null && definition.MaxTotalExp > 0)
        {
            nextStoredExp = Math.Min(nextStoredExp, definition.MaxTotalExp);
            nextTotalExp = Math.Min(nextTotalExp, definition.MaxTotalExp);
        }

        state.StoredExp = nextStoredExp;
        state.TotalEarnedExp = nextTotalExp;
        RefreshCanLevelUp(skillId, state);
    }

    public bool TryLevelUp(string skillId)
    {
        if (_profile == null || _skillRegistry == null)
        {
            return false;
        }

        SkillDefinition? definition = _skillRegistry.GetSkill(skillId);
        PlayerSkillState state = _profile.GetOrCreateSkillState(skillId);
        if (definition == null || state.Level >= definition.MaxLevel)
        {
            return false;
        }

        SkillLevelEntry? levelEntry = definition.GetLevelEntry(state.Level);
        if (levelEntry == null || state.StoredExp < levelEntry.ExpToNext)
        {
            return false;
        }

        state.StoredExp -= levelEntry.ExpToNext;
        state.Level += 1;
        RefreshCanLevelUp(skillId, state);
        return true;
    }

    /// <summary>
    /// 刷新“这个技能现在能不能升级”。
    /// 这样 UI 不用自己重复写一次同样的判断。
    /// </summary>
    private void RefreshCanLevelUp(string skillId, PlayerSkillState state)
    {
        if (_skillRegistry == null)
        {
            state.CanLevelUp = false;
            return;
        }

        SkillDefinition? definition = _skillRegistry.GetSkill(skillId);
        if (definition == null || state.Level >= definition.MaxLevel)
        {
            state.CanLevelUp = false;
            return;
        }

        SkillLevelEntry? levelEntry = definition.GetLevelEntry(state.Level);
        state.CanLevelUp = levelEntry != null && state.StoredExp >= levelEntry.ExpToNext;
    }
}
