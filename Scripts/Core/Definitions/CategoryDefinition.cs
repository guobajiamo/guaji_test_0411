using System.Collections.Generic;

namespace Test00_0410.Core.Definitions;

/// <summary>
/// 分类节点定义。
/// 它本身通常不进入玩家背包，而是负责承载更下一级的分类或物品。
/// </summary>
public class CategoryDefinition : NodeDefinitionBase
{
    /// <summary>
    /// 如果这个分类下面既没有子分类也没有物品，UI 是否隐藏它。
    /// </summary>
    public bool HideWhenEmpty { get; set; }

    /// <summary>
    /// 注册表构建完成后，可把子分类 ID 填进来，方便后续查树。
    /// </summary>
    public List<string> ChildCategoryIds { get; } = new();

    /// <summary>
    /// 注册表构建完成后，可把子物品 ID 填进来。
    /// </summary>
    public List<string> ChildItemIds { get; } = new();

    public CategoryDefinition()
    {
        IsAbstract = true;
    }
}
