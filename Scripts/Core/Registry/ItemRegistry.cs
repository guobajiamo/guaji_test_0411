using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Helpers;

namespace Test00_0410.Core.Registry;

/// <summary>
/// 物品注册表。
/// 它负责集中保存分类和物品定义，并在启动时构建树形关系。
/// </summary>
public class ItemRegistry
{
    private readonly Dictionary<string, CategoryDefinition> _categories = new();
    private readonly Dictionary<string, ItemDefinition> _items = new();
    private readonly Dictionary<string, NodeDefinitionBase> _nodes = new();
    private readonly Dictionary<string, List<string>> _childrenByParent = new();
    private readonly List<string> _lastValidationMessages = new();

    public IReadOnlyDictionary<string, CategoryDefinition> Categories => _categories;

    public IReadOnlyDictionary<string, ItemDefinition> Items => _items;

    public IReadOnlyDictionary<string, NodeDefinitionBase> Nodes => _nodes;

    public IReadOnlyList<string> LastValidationMessages => _lastValidationMessages;

    /// <summary>
    /// 一次性装载所有静态定义。
    /// </summary>
    public void LoadDefinitions(IEnumerable<CategoryDefinition> categories, IEnumerable<ItemDefinition> items)
    {
        List<CategoryDefinition> categoryList = categories.ToList();
        List<ItemDefinition> itemList = items.ToList();

        _categories.Clear();
        _items.Clear();
        _nodes.Clear();
        _lastValidationMessages.Clear();

        _lastValidationMessages.AddRange(TreeValidator.ValidateDefinitionSets(categoryList, itemList));

        foreach (CategoryDefinition category in categoryList)
        {
            if (_categories.ContainsKey(category.Id) || _nodes.ContainsKey(category.Id))
            {
                continue;
            }

            _categories[category.Id] = category;
            _nodes[category.Id] = category;
        }

        foreach (ItemDefinition item in itemList)
        {
            if (_items.ContainsKey(item.Id) || _nodes.ContainsKey(item.Id))
            {
                continue;
            }

            _items[item.Id] = item;
            _nodes[item.Id] = item;
        }

        BuildTree();
        _lastValidationMessages.AddRange(TreeValidator.ValidateNodeTree(_nodes.Values));
    }

    /// <summary>
    /// 根据 ParentId 构建父子关系。
    /// </summary>
    public void BuildTree()
    {
        _childrenByParent.Clear();

        foreach (CategoryDefinition category in _categories.Values)
        {
            category.ChildCategoryIds.Clear();
            category.ChildItemIds.Clear();
        }

        foreach (NodeDefinitionBase node in _nodes.Values)
        {
            if (!_childrenByParent.ContainsKey(node.ParentId))
            {
                _childrenByParent[node.ParentId] = new List<string>();
            }

            _childrenByParent[node.ParentId].Add(node.Id);

            if (string.IsNullOrWhiteSpace(node.ParentId) || !_categories.TryGetValue(node.ParentId, out CategoryDefinition? parentCategory))
            {
                continue;
            }

            if (node is CategoryDefinition)
            {
                parentCategory.ChildCategoryIds.Add(node.Id);
            }
            else
            {
                parentCategory.ChildItemIds.Add(node.Id);
            }
        }
    }

    /// <summary>
    /// 调用通用校验器，返回警告和错误信息。
    /// </summary>
    public List<string> Validate()
    {
        return _lastValidationMessages.ToList();
    }

    public CategoryDefinition? GetCategory(string id)
    {
        return _categories.GetValueOrDefault(id);
    }

    public ItemDefinition? GetItem(string id)
    {
        return _items.GetValueOrDefault(id);
    }

    public NodeDefinitionBase? GetNode(string id)
    {
        return _nodes.GetValueOrDefault(id);
    }

    /// <summary>
    /// 获取某个父节点下的所有子节点，并按默认顺序排序。
    /// </summary>
    public List<NodeDefinitionBase> GetChildren(string parentId)
    {
        if (!_childrenByParent.TryGetValue(parentId, out List<string>? childIds))
        {
            return new List<NodeDefinitionBase>();
        }

        return childIds
            .Select(id => GetNode(id))
            .Where(node => node != null)
            .Cast<NodeDefinitionBase>()
            .OrderBy(node => node.DefinitionOrder)
            .ThenBy(node => node.Id)
            .ToList();
    }

    public void DumpTreeToLog()
    {
        TreePrinter.PrintToGodotConsole(this);
    }

    public void DumpTreeToFile(string filePath)
    {
        TreePrinter.WriteToFile(this, filePath);
    }

    /// <summary>
    /// 把树结构打印到运行时可写目录。
    /// 导出后依然能正常工作，不依赖只读的 res://。
    /// </summary>
    public void DumpTreeToDefaultRuntimeFile()
    {
        TreePrinter.WriteToFile(this, RuntimePathHelper.ItemTreeDumpPath);
    }
}
