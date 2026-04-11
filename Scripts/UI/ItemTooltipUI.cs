using Godot;

namespace Test00_0410.UI;

/// <summary>
/// 物品悬浮信息框。
/// 鼠标移到物品上时，可以显示更详细的描述。
/// </summary>
public partial class ItemTooltipUI : PanelContainer
{
    public void ShowItemTooltip(string title, string description)
    {
        Visible = true;
        // 这里后续会把 title 和 description 写到内部的 Label 上。
    }

    public void HideTooltip()
    {
        Visible = false;
    }
}
