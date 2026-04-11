using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 排序辅助工具。
/// 主要处理默认顺序、重新编号以及备用排序规则。
/// </summary>
public static class SortingHelper
{
    public static int GetSparseOrderByIndex(int index, int step = 100)
    {
        return (index + 1) * step;
    }

    public static void ReIndexDefinitionOrder(IList<NodeDefinitionBase> nodes, int step = 100)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            nodes[index].DefinitionOrder = GetSparseOrderByIndex(index, step);
        }
    }

    public static List<NodeDefinitionBase> SortNodes(IEnumerable<NodeDefinitionBase> nodes)
    {
        return nodes
            .OrderBy(node => node.DefinitionOrder)
            .ThenBy(node => node.Id)
            .ToList();
    }

    /// <summary>
    /// 计算玩家界面的最终显示顺序。
    /// 如果玩家没有手动改过顺序，就回退到静态定义顺序。
    /// </summary>
    public static int GetEffectiveDisplayOrder(NodeDefinitionBase node, PlayerItemState? state)
    {
        return state?.PlayerDisplayOrder ?? node.DefinitionOrder;
    }

    /// <summary>
    /// 玩家界面中的推荐排序规则：
    /// 1. 收藏置顶
    /// 2. 同级内高稀有度优先
    /// 3. 玩家自定义顺序优先，没有则走定义顺序
    /// 4. 最后再按 ID 兜底
    /// </summary>
    public static List<TNode> SortNodesForDisplay<TNode>(
        IEnumerable<TNode> nodes,
        Func<string, PlayerItemState?> playerStateResolver,
        Func<string, int>? rarityWeightResolver = null)
        where TNode : NodeDefinitionBase
    {
        return nodes
            .OrderByDescending(node => playerStateResolver(node.Id)?.IsFavorite ?? false)
            .ThenByDescending(node => rarityWeightResolver?.Invoke(node.Id) ?? 0)
            .ThenBy(node => GetEffectiveDisplayOrder(node, playerStateResolver(node.Id)))
            .ThenBy(node => node.Id)
            .ToList();
    }
}
