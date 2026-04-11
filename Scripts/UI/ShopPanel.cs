using Godot;

namespace Test00_0410.UI;

/// <summary>
/// 商店面板。
/// 以后会支持商店折叠、展开，以及以物易物的价格展示。
/// </summary>
public partial class ShopPanel : Control
{
    public void OpenShop(string npcId)
    {
        Visible = true;
        // 这里后续会加载指定 NPC 的商品列表。
    }

    public void CloseShop()
    {
        Visible = false;
    }
}
