namespace Test00_0410.Core.Definitions;

/// <summary>
/// 工具加成定义。
/// 当前先把“原木产量倍率”和“砍树速度倍率”做成强类型字段。
/// </summary>
public class ToolBonusDefinition
{
    /// <summary>
    /// 原木产量倍率。
    /// 例如 2.0 表示产量翻倍。
    /// </summary>
    public double LogYieldMultiplier { get; set; } = 1.0;

    /// <summary>
    /// 砍树速度倍率。
    /// 例如 1.5 表示更快。
    /// 计算读条时间时，通常使用“基础读条时间 / 速度倍率”。
    /// </summary>
    public double ChopSpeedMultiplier { get; set; } = 1.0;
}
