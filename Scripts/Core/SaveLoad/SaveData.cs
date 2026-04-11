using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.SaveLoad;

/// <summary>
/// 存档根结构。
/// 你可以把它理解成“写入磁盘前的总打包对象”。
/// </summary>
public class SaveData
{
    /// <summary>
    /// 存档元信息。
    /// 版本号、保存时间等技术字段统一放在这里。
    /// </summary>
    public SaveMetadata Metadata { get; set; } = new();

    public PlayerProfile Profile { get; set; } = new();
}
