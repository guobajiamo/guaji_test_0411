namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家某项技能的动态状态。
/// 这里记录等级、已累计经验、是否可升级等内容。
/// </summary>
public class PlayerSkillState
{
    public string SkillId { get; set; } = string.Empty;

    public int Level { get; set; } = 1;

    /// <summary>
    /// 当前存着多少经验。
    /// 这里改成 double，是为了让挂机结算时可以保留小数精度，
    /// 这样像 5.789 秒这种读条时间换算经验时不会丢精度。
    /// </summary>
    public double StoredExp { get; set; }

    /// <summary>
    /// 历史累计获得过多少经验。
    /// 这个值主要方便以后做统计和界面显示。
    /// </summary>
    public double TotalEarnedExp { get; set; }

    public bool CanLevelUp { get; set; }
}
