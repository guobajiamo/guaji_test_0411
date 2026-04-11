using Godot;

namespace Test00_0410.UI;

/// <summary>
/// 自定义标签页容器。
/// 如果后续发现 Godot 原生 TabContainer 不够用，就在这里扩展。
/// </summary>
public partial class TabContainer : Godot.TabContainer
{
    public void RegisterDefaultTabs()
    {
        // 这里后续会统一注册“背包、属性、战斗、任务、字典、成就”等页签。
    }
}
