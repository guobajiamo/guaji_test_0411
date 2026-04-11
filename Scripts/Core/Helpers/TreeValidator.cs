using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;

namespace Test00_0410.Core.Helpers;

/// <summary>
/// 树结构校验工具。
/// 用来检查重复 ID、缺失父节点、循环依赖、排序冲突等常见问题。
/// </summary>
public static class TreeValidator
{
    public static List<string> ValidateDefinitionSets(IEnumerable<CategoryDefinition> categories, IEnumerable<ItemDefinition> items)
    {
        List<NodeDefinitionBase> combinedNodes = categories
            .Cast<NodeDefinitionBase>()
            .Concat(items)
            .ToList();

        List<string> messages = new();
        ValidateDuplicateIds(combinedNodes, messages);
        return messages;
    }

    public static List<string> ValidateNodeTree(IEnumerable<NodeDefinitionBase> nodes)
    {
        List<string> messages = new();
        List<NodeDefinitionBase> nodeList = nodes.ToList();

        ValidateDuplicateIds(nodeList, messages);
        ValidateMissingParents(nodeList, messages);
        ValidateCircularReference(nodeList, messages);
        ValidateOrderConflicts(nodeList, messages);

        return messages;
    }

    private static void ValidateDuplicateIds(IEnumerable<NodeDefinitionBase> nodes, List<string> messages)
    {
        foreach (IGrouping<string, NodeDefinitionBase> group in nodes.GroupBy(node => node.Id).Where(group => group.Count() > 1))
        {
            messages.Add($"[Error] 存在重复 ID: {group.Key}");
        }
    }

    private static void ValidateMissingParents(IEnumerable<NodeDefinitionBase> nodes, List<string> messages)
    {
        HashSet<string> allIds = nodes.Select(node => node.Id).ToHashSet();

        foreach (NodeDefinitionBase node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ParentId) && !allIds.Contains(node.ParentId))
            {
                messages.Add($"[Error] 节点 {node.Id} 的父节点不存在: {node.ParentId}");
            }
        }
    }

    private static void ValidateCircularReference(IEnumerable<NodeDefinitionBase> nodes, List<string> messages)
    {
        Dictionary<string, NodeDefinitionBase> map = nodes.ToDictionary(node => node.Id, node => node);

        foreach (NodeDefinitionBase node in nodes)
        {
            HashSet<string> visited = new();
            string currentId = node.Id;
            string parentId = node.ParentId;

            while (!string.IsNullOrWhiteSpace(parentId))
            {
                if (!visited.Add(parentId))
                {
                    messages.Add($"[Error] 检测到循环依赖，起点节点: {currentId}");
                    break;
                }

                if (!map.TryGetValue(parentId, out NodeDefinitionBase? parentNode))
                {
                    break;
                }

                parentId = parentNode.ParentId;
            }
        }
    }

    private static void ValidateOrderConflicts(IEnumerable<NodeDefinitionBase> nodes, List<string> messages)
    {
        foreach (IGrouping<string, NodeDefinitionBase> siblingGroup in nodes.GroupBy(node => node.ParentId))
        {
            foreach (IGrouping<int, NodeDefinitionBase> orderGroup in siblingGroup.GroupBy(node => node.DefinitionOrder).Where(group => group.Count() > 1))
            {
                string ids = string.Join(", ", orderGroup.Select(node => node.Id));
                messages.Add($"[Warning] 同级节点排序重复，parent={siblingGroup.Key}, order={orderGroup.Key}, ids={ids}");
            }
        }
    }
}
