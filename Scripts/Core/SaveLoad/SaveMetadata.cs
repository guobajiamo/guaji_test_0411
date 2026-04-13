using System;

namespace Test00_0410.Core.SaveLoad;

/// <summary>
/// 存档元信息。
/// 与玩家玩法数据分开存放，避免把版本、时间戳等技术字段塞进 PlayerProfile。
/// </summary>
public class SaveMetadata
{
    /// <summary>
    /// 当前存档格式版本。
    /// 项目里只保留这一处作为“存档版本真相源”。
    /// </summary>
    public string SaveVersion { get; set; } = SemanticVersion.Current.ToString();

    /// <summary>
    /// 最近一次保存时的 Unix 时间戳。
    /// </summary>
    public long SavedAtUnixSeconds { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// 保存时的语言。
    /// 以后如果要做语言恢复，可以参考这个值。
    /// </summary>
    public string Locale { get; set; } = "zh";

    /// <summary>
    /// 当前存档所属的剧本 ID。
    /// 这样读档时就知道应该先加载哪一套配置。
    /// </summary>
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>
    /// 当前存档所属的剧本显示名。
    /// 主要给存档列表界面展示用。
    /// </summary>
    public string ScenarioDisplayName { get; set; } = string.Empty;
}
