namespace Test00_0410.Core.Definitions;

/// <summary>
/// 技能某一级对应的配置。
/// 它记录“升下一级需要多少经验”“每次产出多少”“每次读条多久”等。
/// </summary>
public class SkillLevelEntry
{
    public int Level { get; set; }

    public int ExpToNext { get; set; }

    public double Output { get; set; }

    public double Interval { get; set; }
}
