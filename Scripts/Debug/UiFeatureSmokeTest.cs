using Godot;
using System;
using System.Linq;
using Test00_0410.Autoload;

namespace Test00_0410.Debug;

/// <summary>
/// UI 新功能烟雾测试。
/// 用来快速检查这次新增的日志区、弹窗事件配置和基础节点结构是否正常。
/// </summary>
public partial class UiFeatureSmokeTest : Node
{
    public override async void _Ready()
    {
        try
        {
            PackedScene mainSceneResource = GD.Load<PackedScene>("res://Scenes/Main.tscn");
            Node mainScene = mainSceneResource.Instantiate();
            AddChild(mainScene);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            GameManager gameManager = GameManager.Instance ?? throw new InvalidOperationException("GameManager 未初始化。");
            Node mainUi = mainScene.GetNode("MainUI");

            Node? collapsedLogDock = mainUi.FindChild("CollapsedLogDock", true, false);
            Assert(collapsedLogDock != null, "底部日志区未创建。");

            Godot.TabContainer mainTabs = mainUi.FindChild("MainTabs", true, false) as Godot.TabContainer
                ?? throw new InvalidOperationException("主标签容器不存在。");
            Assert(mainTabs.GetChildren().All(child => child.Name != "日志"), "日志标签仍然存在于中间区域。");

            bool pickupOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_pickup_stone_axe") ?? false;
            Assert(pickupOk, "获取石斧事件执行失败。");
            Assert(gameManager.PlayerProfile.Inventory.HasItem("tool_stone_axe"), "石斧未进入背包。");

            bool choiceOk = gameManager.ClickEventSystem?.TryTriggerDialogChoice(
                "evt_stranger_reward_offer",
                "evt_accept_gift_gold",
                true) ?? false;
            Assert(choiceOk, "双选项分支事件执行失败。");
            Assert(gameManager.PlayerProfile.Economy.Gold >= 30, "接受馈赠后未获得金币。");
            Assert(gameManager.PlayerProfile.CompletedEventIds.Contains("evt_stranger_reward_offer"), "弹窗源事件未被标记为已完成。");

            Node? eventDialogPanel = mainUi.FindChild("EventDialogPanel", true, false);
            Assert(eventDialogPanel != null, "事件弹窗面板未创建。");
            Assert(gameManager.EventRegistry.Events.Count >= 7, "新增加的事件配置未成功载入。");

            GD.Print("[UiFeatureSmokeTest] all checks passed");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[UiFeatureSmokeTest] {exception}");
            GetTree().Quit(1);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
