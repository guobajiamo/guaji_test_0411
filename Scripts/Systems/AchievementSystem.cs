using Godot;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Systems;

/// <summary>
/// 成就系统占位。
/// </summary>
public partial class AchievementSystem : Node
{
    private PlayerProfile? _profile;

    public void Configure(PlayerProfile profile)
    {
        _profile = profile;
    }

    public void RefreshAchievements()
    {
        // 未来会在这里检查成就是否达成。
    }

    public bool UnlockAchievement(string achievementId)
    {
        return _profile != null
            && !string.IsNullOrWhiteSpace(achievementId)
            && _profile.UnlockedAchievementIds.Add(achievementId);
    }
}
