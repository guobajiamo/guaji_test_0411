using Godot;
using System;
using System.Collections.Generic;
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
    private ClickEventSystem? _clickEventSystem;

    public void Configure(PlayerProfile profile, SkillRegistry skillRegistry, ClickEventSystem? clickEventSystem = null)
    {
        _profile = profile;
        _skillRegistry = skillRegistry;
        _clickEventSystem = clickEventSystem;

        foreach (KeyValuePair<string, PlayerSkillState> pair in profile.SkillStates)
        {
            RefreshCanLevelUp(pair.Key, pair.Value);
        }
    }

    public void AddExp(string skillId, double exp)
    {
        if (_profile == null || exp <= 0)
        {
            return;
        }

        PlayerSkillState state = _profile.GetOrCreateSkillState(skillId);
        SkillDefinition? definition = _skillRegistry?.GetSkill(skillId);
        double nextTotalExp = state.TotalEarnedExp + exp;
        if (definition != null && definition.MaxTotalExp > 0)
        {
            nextTotalExp = Math.Min(nextTotalExp, definition.MaxTotalExp);
        }

        state.TotalEarnedExp = nextTotalExp;
        state.StoredExp = nextTotalExp;
        RefreshCanLevelUp(skillId, state);
    }

    public bool TryLearnSkill(string skillId, int targetLevel = 1)
    {
        if (_profile == null || _skillRegistry == null || string.IsNullOrWhiteSpace(skillId))
        {
            return false;
        }

        SkillDefinition? definition = _skillRegistry.GetSkill(skillId);
        if (definition == null)
        {
            return false;
        }

        PlayerSkillState state = _profile.GetOrCreateSkillState(skillId);
        int normalizedLevel = Math.Max(1, targetLevel);
        normalizedLevel = Math.Min(definition.MaxLevel, normalizedLevel);
        if (state.Level >= normalizedLevel)
        {
            RefreshCanLevelUp(skillId, state);
            return false;
        }

        state.Level = normalizedLevel;
        state.TotalEarnedExp = Math.Max(state.TotalEarnedExp, definition.GetRequiredTotalExpForLevel(state.Level));
        state.StoredExp = state.TotalEarnedExp;
        RefreshCanLevelUp(skillId, state);
        return true;
    }

    public bool TryLevelUp(string skillId)
    {
        if (_profile == null || _skillRegistry == null)
        {
            return false;
        }

        SkillDefinition? definition = _skillRegistry.GetSkill(skillId);
        PlayerSkillState state = _profile.GetOrCreateSkillState(skillId);
        if (definition == null || state.Level <= 0 || state.Level >= definition.MaxLevel)
        {
            return false;
        }

        if (state.TotalEarnedExp < definition.GetRequiredTotalExpForNextLevel(state.Level))
        {
            return false;
        }

        state.Level += 1;
        state.StoredExp = state.TotalEarnedExp;
        TriggerLevelUpHooks(definition, state.Level);
        RefreshCanLevelUp(skillId, state);
        return true;
    }

    /// <summary>
    /// 刷新“这个技能现在能不能升级”。
    /// 这样 UI 不用自己重复写一次同样的判断。
    /// </summary>
    private void RefreshCanLevelUp(string skillId, PlayerSkillState state)
    {
        state.StoredExp = state.TotalEarnedExp;

        if (_skillRegistry == null)
        {
            state.CanLevelUp = false;
            return;
        }

        SkillDefinition? definition = _skillRegistry.GetSkill(skillId);
        if (definition == null || state.Level <= 0 || state.Level >= definition.MaxLevel)
        {
            state.CanLevelUp = false;
            return;
        }

        int requiredTotalExp = definition.GetRequiredTotalExpForNextLevel(state.Level);
        state.CanLevelUp = requiredTotalExp > 0 && state.TotalEarnedExp >= requiredTotalExp;
    }

    private void TriggerLevelUpHooks(SkillDefinition definition, int reachedLevel)
    {
        SkillLevelEntry? levelEntry = definition.GetLevelEntry(reachedLevel);
        if (levelEntry == null || levelEntry.OnLevelUpEventIds.Count == 0)
        {
            return;
        }

        foreach (string eventId in levelEntry.OnLevelUpEventIds)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                continue;
            }

            bool triggered = _clickEventSystem?.TryTriggerEventFromSystem(eventId) == true;
            if (!triggered)
            {
                GD.PushWarning($"[SkillSystem] level-up hook failed: skill={definition.Id}, level={reachedLevel}, event={eventId}");
            }
        }
    }
}
