using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Runtime;

namespace Test00_0410.Core.Helpers;

public sealed class ItemInfoDisplayEntry
{
    public string FieldId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 统一的物品信息渲染器。
/// 用于“背包悬浮窗”和“详细属性面板”两类展示接口。
/// </summary>
public static class ItemInfoFormatter
{
    private static readonly string[] DefaultHoverFields =
    {
        "name",
        "description",
        "acquisition_hint",
        "base_rarity",
        "sell_price",
        "tags"
    };

    private static readonly string[] DefaultDetailFields =
    {
        "item_id",
        "name",
        "category",
        "description",
        "detail_description",
        "quantity",
        "base_rarity",
        "is_stackable",
        "base_max_stack",
        "buy_price",
        "sell_price",
        "has_durability",
        "max_durability",
        "tool_log_yield",
        "tool_chop_speed",
        "tags",
        "acquisition_hint"
    };

    public static IReadOnlyList<string> GetDefaultHoverFieldIds()
    {
        return DefaultHoverFields;
    }

    public static IReadOnlyList<string> GetDefaultDetailFieldIds()
    {
        return DefaultDetailFields;
    }

    public static List<ItemInfoDisplayEntry> BuildHoverEntries(
        ItemDefinition item,
        ItemStack? stack,
        PlayerItemState? state,
        Func<string, string>? translator = null,
        Func<string, string>? categoryNameResolver = null)
    {
        return BuildEntries(
            item,
            stack,
            state,
            item.HoverInfoFields,
            item.HoverExtraLines,
            DefaultHoverFields,
            translator,
            categoryNameResolver);
    }

    public static List<ItemInfoDisplayEntry> BuildDetailEntries(
        ItemDefinition item,
        ItemStack? stack,
        PlayerItemState? state,
        Func<string, string>? translator = null,
        Func<string, string>? categoryNameResolver = null)
    {
        return BuildEntries(
            item,
            stack,
            state,
            item.DetailInfoFields,
            item.DetailExtraLines,
            DefaultDetailFields,
            translator,
            categoryNameResolver);
    }

    public static string BuildTooltipText(IEnumerable<ItemInfoDisplayEntry> entries)
    {
        return string.Join("\n", entries
            .Select(entry => string.IsNullOrWhiteSpace(entry.Label)
                ? entry.Value
                : $"{entry.Label}：{entry.Value}")
            .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static List<ItemInfoDisplayEntry> BuildEntries(
        ItemDefinition item,
        ItemStack? stack,
        PlayerItemState? state,
        IReadOnlyList<string> configuredFields,
        IReadOnlyList<string> extraLines,
        IReadOnlyList<string> fallbackFields,
        Func<string, string>? translator,
        Func<string, string>? categoryNameResolver)
    {
        List<string> fields = configuredFields.Count > 0
            ? configuredFields.Where(field => !string.IsNullOrWhiteSpace(field)).ToList()
            : fallbackFields.ToList();

        List<ItemInfoDisplayEntry> entries = new();
        foreach (string field in fields)
        {
            if (TryBuildEntry(item, stack, state, field, translator, categoryNameResolver, out ItemInfoDisplayEntry? entry)
                && entry != null)
            {
                entries.Add(entry);
            }
        }

        foreach (string rawLine in extraLines)
        {
            string line = TranslateText(translator, rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            entries.Add(new ItemInfoDisplayEntry
            {
                FieldId = "extra",
                Label = string.Empty,
                Value = line
            });
        }

        return entries;
    }

    private static bool TryBuildEntry(
        ItemDefinition item,
        ItemStack? stack,
        PlayerItemState? state,
        string fieldId,
        Func<string, string>? translator,
        Func<string, string>? categoryNameResolver,
        out ItemInfoDisplayEntry? entry)
    {
        string normalized = fieldId.Trim().ToLowerInvariant();
        int quantity = stack?.Quantity ?? 0;

        entry = normalized switch
        {
            "item_id" => CreateEntry(fieldId, "物品ID", item.Id),
            "parent_id" => CreateEntry(fieldId, "父分类ID", item.ParentId),
            "category" => CreateEntry(fieldId, "所属分类", ResolveCategoryName(item.ParentId, categoryNameResolver)),
            "name" => CreateEntry(fieldId, "名称", item.GetDisplayName(translator)),
            "description" => CreateEntry(fieldId, "简介", item.GetDisplayDescription(translator)),
            "detail_description" => CreateEntry(fieldId, "详细描述", ResolveDetailDescription(item, translator)),
            "acquisition_hint" => CreateEntry(fieldId, "获取提示", ResolveAcquisitionHint(item, translator)),
            "quantity" => CreateEntry(fieldId, "数量", quantity.ToString(CultureInfo.InvariantCulture)),
            "base_rarity" => CreateEntry(fieldId, "稀有度", item.BaseRarity.ToString()),
            "current_rarity" => CreateEntry(fieldId, "当前稀有度", (stack?.CurrentRarity ?? item.BaseRarity).ToString()),
            "is_stackable" => CreateEntry(fieldId, "可堆叠", item.IsStackable ? "是" : "否"),
            "base_max_stack" => CreateEntry(fieldId, "最大堆叠", item.IsStackable ? "无限" : "1"),
            "buy_price" => CreateEntry(fieldId, "买入价", item.BuyPrice.ToString(CultureInfo.InvariantCulture)),
            "sell_price" => CreateEntry(fieldId, "卖出价", item.SellPrice.ToString(CultureInfo.InvariantCulture)),
            "has_durability" => CreateEntry(fieldId, "启用耐久", item.HasDurability ? "是" : "否"),
            "max_durability" => CreateEntry(fieldId, "最大耐久", item.MaxDurability.ToString(CultureInfo.InvariantCulture)),
            "current_durability" => CreateEntry(fieldId, "当前耐久", (stack?.CurrentDurability ?? 0).ToString(CultureInfo.InvariantCulture)),
            "tool_log_yield" => CreateEntry(fieldId, "产出倍率", item.ToolBonuses.LogYieldMultiplier.ToString("0.##", CultureInfo.InvariantCulture)),
            "tool_chop_speed" => CreateEntry(fieldId, "速度倍率", item.ToolBonuses.ChopSpeedMultiplier.ToString("0.##", CultureInfo.InvariantCulture)),
            "tags" => CreateEntry(fieldId, "标签", ResolveTagText(item.Tags)),
            "is_favorite" => CreateEntry(fieldId, "收藏标记", (state?.IsFavorite ?? false) ? "是" : "否"),
            "is_junk" => CreateEntry(fieldId, "垃圾标记", (state?.IsJunkMarked ?? false) ? "是" : "否"),
            "arrival_order" => CreateEntry(fieldId, "入袋序号", (state?.AcquiredSequence ?? 0).ToString(CultureInfo.InvariantCulture)),
            "display_order" => CreateEntry(fieldId, "显示序号", (state?.PlayerDisplayOrder ?? 0).ToString(CultureInfo.InvariantCulture)),
            _ => null
        };

        return entry != null && !string.IsNullOrWhiteSpace(entry.Value);
    }

    private static ItemInfoDisplayEntry CreateEntry(string fieldId, string label, string value)
    {
        return new ItemInfoDisplayEntry
        {
            FieldId = fieldId,
            Label = label,
            Value = value
        };
    }

    private static string ResolveCategoryName(string categoryId, Func<string, string>? categoryNameResolver)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return "未分类";
        }

        return categoryNameResolver?.Invoke(categoryId) ?? categoryId;
    }

    private static string ResolveDetailDescription(ItemDefinition item, Func<string, string>? translator)
    {
        if (!string.IsNullOrWhiteSpace(item.DetailDescriptionKey))
        {
            return TranslateText(translator, item.DetailDescriptionKey);
        }

        return item.GetDisplayDescription(translator);
    }

    private static string ResolveAcquisitionHint(ItemDefinition item, Func<string, string>? translator)
    {
        if (!string.IsNullOrWhiteSpace(item.AcquisitionHintKey))
        {
            return TranslateText(translator, item.AcquisitionHintKey);
        }

        return "暂无获取途径说明。";
    }

    private static string ResolveTagText(ItemTag tags)
    {
        if (tags == ItemTag.None)
        {
            return "无";
        }

        return string.Join(" / ", Enum.GetValues<ItemTag>()
            .Where(tag => tag != ItemTag.None && (tags & tag) == tag)
            .Select(tag => tag.ToString()));
    }

    private static string TranslateText(Func<string, string>? translator, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return translator?.Invoke(text) ?? text;
    }
}
