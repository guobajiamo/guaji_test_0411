using System;
using System.Collections.Generic;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 分类节点和具体物品都会共用的基础定义类。
/// 你可以把它理解成“树形结构里的通用节点信息”。
/// </summary>
public abstract class NodeDefinitionBase
{
    /// <summary>
    /// 稳定唯一 ID。
    /// 推荐始终使用全小写 + 下划线，避免后续改名影响存档。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 父节点 ID。
    /// 如果为空，说明它是某一棵树的根节点。
    /// </summary>
    public string ParentId { get; set; } = string.Empty;

    /// <summary>
    /// 名称 key。
    /// 前期可以直接写中文，后期可作为多语言 key 使用。
    /// </summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>
    /// 描述 key。
    /// 和 NameKey 一样，前期可直接写中文。
    /// </summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// 默认定义顺序。
    /// 这个顺序主要给策划和程序使用，一般不会频繁变动。
    /// </summary>
    public int DefinitionOrder { get; set; } = 100;

    /// <summary>
    /// 是否是抽象节点。
    /// true 常用于分类节点，false 常用于可持有的具体物品。
    /// </summary>
    public bool IsAbstract { get; protected set; } = true;

    /// <summary>
    /// 是否已废弃。
    /// 当一个旧物品不再继续使用时，不建议直接删除，而是先标记废弃。
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// 从哪个版本开始废弃。
    /// 这个字段是为了帮助以后做存档迁移。
    /// </summary>
    public string DeprecatedSince { get; set; } = string.Empty;

    /// <summary>
    /// 如果旧 ID 被废弃了，可以指向新的替代物品 ID。
    /// </summary>
    public string ReplacementId { get; set; } = string.Empty;

    /// <summary>
    /// 节点是否默认可见。
    /// 例如某些隐藏分类或测试节点可以先设为 false。
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 预留扩展字段。
    /// 注意：这里只建议放“非关键、非强校验”的附加文本参数。
    /// 会影响主逻辑的数据，尽量改成明确字段，不要放进这个字典。
    /// </summary>
    public Dictionary<string, string> ExtensionData { get; } = new();

    /// <summary>
    /// 获取显示名称。
    /// 如果传入了翻译函数，就先走翻译；否则直接返回原始 key。
    /// </summary>
    public virtual string GetDisplayName(Func<string, string>? translator = null)
    {
        return translator?.Invoke(NameKey) ?? NameKey;
    }

    /// <summary>
    /// 获取显示描述。
    /// </summary>
    public virtual string GetDisplayDescription(Func<string, string>? translator = null)
    {
        return translator?.Invoke(DescriptionKey) ?? DescriptionKey;
    }
}
