using Godot;

namespace Test00_0410.UI;

/// <summary>
/// 单个物品槽 UI。
/// 用于显示名称、数量、收藏状态和点击事件。
/// </summary>
public partial class ItemSlotUI : Button
{
    public string ItemId { get; private set; } = string.Empty;

    public void BindItem(string itemId, string displayName, int quantity)
    {
        ItemId = itemId;
        Text = $"{displayName} x{quantity}";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, 40);
        Alignment = HorizontalAlignment.Left;
    }
}
