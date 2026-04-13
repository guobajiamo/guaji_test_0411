using Godot;
using System;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.UI;

namespace Test00_0410.Debug;

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
            AppRoot appRoot = mainScene as AppRoot ?? throw new InvalidOperationException("Main 场景根节点不是 AppRoot。");
            Node? mainMenu = mainScene.FindChild("MainMenuUI", true, false);
            Assert(mainMenu != null, "启动后没有先进入主菜单。");

            appRoot.OpenTestGame();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Node mainUi = mainScene.FindChild("MainUI", true, false)
                ?? throw new InvalidOperationException("进入测试剧本后没有创建 MainUI。");
            MainUI typedMainUi = mainUi as MainUI ?? throw new InvalidOperationException("MainUI 类型解析失败。");

            Assert(mainUi.FindChild("CollapsedLogDock", true, false) != null, "底部日志区域未创建。");

            Control mainTabs = mainUi.FindChild("MainTabs", true, false) as Control
                ?? throw new InvalidOperationException("主标签内容容器不存在。");
            Assert(mainTabs.GetChildren().All(child => child.Name != "日志"), "日志页不应该存在于中间区域。");
            Assert(mainTabs.GetChildren().Any(child => child.Name == "系统"), "测试剧本应该显示系统页。");
            Assert(mainTabs.GetChildren().Any(child => child.Name == "当前区域"), "中间区域应该存在“当前区域”页。");

            Assert(HasButtonContaining(mainUi, "人间之里"), "默认可见的一级区域“人间之里”未显示。");
            Assert(HasButtonContaining(mainUi, "魔法之森"), "默认可见的一级区域“魔法之森”未显示。");
            Assert(HasButtonContaining(mainUi, "博丽神社"), "默认可见的一级区域“博丽神社”未显示。");
            Assert(HasButtonContaining(mainUi, "妖怪之山"), "默认可见的一级区域“妖怪之山”未显示。");
            Assert(HasButtonContaining(mainUi, "红魔馆"), "默认可见的一级区域“红魔馆”未显示。");
            Assert(HasButtonContaining(mainUi, "迷途竹林"), "默认可见的一级区域“迷途竹林”未显示。");
            Assert(!HasButtonContaining(mainUi, "旧地狱"), "带解锁条件的一级区域“旧地狱”不应默认显示。");

            EmitPressOnButton(mainUi, "迷途竹林");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(!HasButtonContaining(mainUi, "永远亭"), "带显示条件的二级区域“永远亭”不应默认显示。");

            bool pickupOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_pickup_stone_axe") ?? false;
            Assert(pickupOk, "获取石斧事件执行失败。");
            Assert(gameManager.PlayerProfile.Inventory.HasItem("tool_stone_axe"), "石斧未进入背包。");
            typedMainUi.RefreshAllPanels();

            EventDialogPanel eventDialogPanel = mainUi.FindChild("EventDialogPanel", true, false) as EventDialogPanel
                ?? throw new InvalidOperationException("事件弹窗面板未创建。");
            Button dismissButton = eventDialogPanel.FindChild("DismissButton", true, false) as Button
                ?? throw new InvalidOperationException("“我再想想”按钮未创建。");

            EmitPressOnButton(mainUi, "阅读伐木心得");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(eventDialogPanel.Visible, "单按钮事件弹窗未弹出。");
            Assert(!dismissButton.Visible, "单按钮强制确认弹窗不应显示“我再想想”按钮。");
            Button confirmButton = FindDialogButtonByExactText(eventDialogPanel, "我知道了");
            Assert(confirmButton.TooltipText.Contains("金币", StringComparison.Ordinal), "单按钮确认项未显示奖励悬浮说明。");
            confirmButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(!eventDialogPanel.Visible, "单按钮强制确认事件点击确认后弹窗未关闭。");
            Assert(gameManager.PlayerProfile.CompletedEventIds.Contains("evt_read_chopping_notice"), "单按钮强制确认事件在确认后未被正确完成。");

            EmitPressOnButton(mainUi, "路人的谢礼");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(eventDialogPanel.Visible, "双选项事件弹窗未弹出。");
            Assert(!dismissButton.Visible, "双选项强制选择弹窗不应显示“我再想想”按钮。");
            Button acceptButton = FindDialogButtonByExactText(eventDialogPanel, "接受馈赠");
            Button declineButton = FindDialogButtonByExactText(eventDialogPanel, "婉拒谢礼");
            Assert(acceptButton.TooltipText.Contains("金币", StringComparison.Ordinal), "接受馈赠按钮未显示正确悬浮说明。");
            Assert(declineButton.TooltipText.Contains("包子", StringComparison.Ordinal), "婉拒谢礼按钮未显示正确悬浮说明。");

            bool choiceOk = gameManager.ClickEventSystem?.TryTriggerDialogChoice("evt_stranger_reward_offer", "evt_accept_gift_gold", true) ?? false;
            Assert(choiceOk, "双选项分支事件执行失败。");
            Assert(gameManager.PlayerProfile.Economy.Gold >= 30, "接受馈赠后未获得金币。");
            Assert(gameManager.PlayerProfile.CompletedEventIds.Contains("evt_stranger_reward_offer"), "弹窗源事件未被标记为已完成。");

            EmitPressOnButton(mainUi, "妖怪之山");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            EmitPressOnButton(mainUi, "玄武涧");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Label currentRegionTitleLabel = mainUi.FindChild("CurrentRegionTitleLabel", true, false) as Label
                ?? throw new InvalidOperationException("当前区域标题未创建。");
            Assert(currentRegionTitleLabel.Text.Contains("玄武涧", StringComparison.Ordinal), "切换到“玄武涧”后，中间区域标题未同步更新。");

            Assert(gameManager.IdleSystem?.ShouldShowEvent("evt_idle_fishing") == true, "挂机钓鱼按钮应当默认显示。");
            Assert(gameManager.IdleSystem?.CanStartIdleEvent("evt_idle_fishing") != true, "挂机钓鱼在没有鱼竿和鱼饵时不应可点击。");
            Assert(gameManager.ClickEventSystem?.ShouldShowEvent("evt_empty_hook_fishing") != true, "姜太公钓鱼在前置显示条件未满足前不应显示。");

            typedMainUi.RefreshAllPanels();
            Button idleFishingButton = FindButtonContaining(mainUi, "挂机钓鱼");
            Assert(idleFishingButton.Disabled, "挂机钓鱼在条件未满足时应显示为置灰按钮。");
            Assert(!HasButtonContaining(mainUi, "姜太公钓鱼"), "姜太公钓鱼在前置显示条件未满足前不应显示按钮。");

            bool getRodOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_claim_fishing_rod") ?? false;
            Assert(getRodOk, "领取鱼竿事件执行失败。");
            Assert(gameManager.PlayerProfile.Inventory.HasItem("tool_fishing_rod"), "领取鱼竿后背包中没有鱼竿。");
            Assert(gameManager.IdleSystem?.CanStartIdleEvent("evt_idle_fishing") != true, "只有鱼竿没有鱼饵时，挂机钓鱼仍应保持置灰。");
            Assert(gameManager.ClickEventSystem?.ShouldShowEvent("evt_empty_hook_fishing") != true, "只拿到鱼竿还没有对话时，姜太公钓鱼仍不应显示。");

            bool getBaitOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_get_worm_bait") ?? false;
            Assert(getBaitOk, "获取蚯蚓鱼饵事件执行失败。");
            Assert(gameManager.PlayerProfile.Inventory.GetItemAmount("bait_worm") == 3, "获取蚯蚓鱼饵后数量不正确。");
            Assert(gameManager.IdleSystem?.CanStartIdleEvent("evt_idle_fishing") == true, "同时拥有鱼竿和鱼饵后，挂机钓鱼应可点击。");

            bool talkOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_talk_to_taigongwang") ?? false;
            Assert(talkOk, "与太公望对话事件执行失败。");
            Assert(gameManager.PlayerProfile.CompletedEventIds.Contains("evt_talk_to_taigongwang"), "与太公望对话未写入已完成事件历史。");
            Assert(gameManager.ClickEventSystem?.ShouldShowEvent("evt_empty_hook_fishing") == true, "完成对话后，姜太公钓鱼应当显示。");
            Assert(gameManager.ClickEventSystem?.CanTriggerEvent("evt_empty_hook_fishing") == true, "完成对话并持有鱼竿后，姜太公钓鱼应可点击。");

            typedMainUi.RefreshAllPanels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            idleFishingButton = FindButtonContaining(mainUi, "挂机钓鱼");
            Assert(!idleFishingButton.Disabled, "同时拥有鱼竿和鱼饵后，挂机钓鱼按钮应点亮。");
            Assert(HasButtonContaining(mainUi, "姜太公钓鱼"), "完成显示前置条件后，姜太公钓鱼按钮应出现。");

            int fishBeforeEmptyHook = gameManager.PlayerProfile.Inventory.GetItemAmount("mat_carp");
            bool emptyHookOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_empty_hook_fishing") ?? false;
            Assert(emptyHookOk, "姜太公钓鱼事件执行失败。");
            Assert(gameManager.PlayerProfile.Inventory.GetItemAmount("mat_carp") == fishBeforeEmptyHook + 1, "姜太公钓鱼后没有获得鲤鱼。");

            gameManager.PlayerProfile.Inventory.TryRemoveItem("bait_worm", 2);
            Assert(gameManager.PlayerProfile.Inventory.GetItemAmount("bait_worm") == 1, "测试前整理鱼饵数量失败。");

            bool idleFishingOk = gameManager.IdleSystem?.StartIdleEvent("evt_idle_fishing") ?? false;
            Assert(idleFishingOk, "挂机钓鱼事件启动失败。");
            typedMainUi.RefreshAllPanels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Control currentRegionSceneList = mainUi.FindChild("CurrentRegionSceneList", true, false) as Control
                ?? throw new InvalidOperationException("当前区域场景列表未创建。");
            Control characterStatusPanel = mainUi.FindChild("CharacterStatusPanel", true, false) as Control
                ?? throw new InvalidOperationException("人物状态栏未创建。");
            Assert(HasVisibleProgressBar(currentRegionSceneList), "挂机钓鱼启动后，事件按钮区的进度条没有显示。");
            Assert(HasVisibleProgressBar(characterStatusPanel), "挂机钓鱼启动后，人物状态栏的目标进度条没有显示。");

            int fishBeforeIdle = gameManager.PlayerProfile.Inventory.GetItemAmount("mat_carp");
            gameManager.PlayerProfile.IdleState.AccumulatedProgressSeconds = 6.1;
            gameManager.IdleSystem?._Process(0.0);
            Assert(gameManager.PlayerProfile.Inventory.GetItemAmount("mat_carp") > fishBeforeIdle, "挂机钓鱼结算后没有获得鲤鱼。");
            Assert(gameManager.PlayerProfile.Inventory.GetItemAmount("bait_worm") == 0, "挂机钓鱼结算后没有扣除最后一个鱼饵。");

            gameManager.IdleSystem?._Process(0.0);
            Assert(gameManager.PlayerProfile.IdleState.IsRunning == false, "鱼饵耗尽后，挂机钓鱼没有自动停止。");
            Assert(string.IsNullOrWhiteSpace(gameManager.PlayerProfile.IdleState.ActiveEventId), "鱼饵耗尽后，挂机事件记录没有清空。");

            EmitPressOnButton(mainUi, "魔法之森");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            EmitPressOnButton(mainUi, "魔法之森外围");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(currentRegionTitleLabel.Text.Contains("魔法之森外围", StringComparison.Ordinal), "切换到“魔法之森外围”后，中间区域标题未同步更新。");

            bool idleStartOk = gameManager.IdleSystem?.StartIdleEvent("evt_idle_chop") ?? false;
            Assert(idleStartOk, "挂机砍树事件启动失败。");
            typedMainUi.RefreshAllPanels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(gameManager.PlayerProfile.IdleState.IsRunning, "挂机砍树启动后未进入运行状态。");
            Assert(string.Equals(gameManager.PlayerProfile.IdleState.ActiveEventId, "evt_idle_chop", StringComparison.Ordinal), "挂机砍树启动后记录的活动事件不正确。");

            bool clickWhileIdleOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_click_chop") ?? false;
            Assert(clickWhileIdleOk, "挂机进行中执行点击砍树事件失败。");
            Assert(gameManager.PlayerProfile.IdleState.IsRunning, "点击事件错误中断了挂机。");
            Assert(string.Equals(gameManager.PlayerProfile.IdleState.ActiveEventId, "evt_idle_chop", StringComparison.Ordinal), "点击事件错误切换了挂机目标。");

            Assert(gameManager.EventRegistry.Events.Count >= 11, "测试剧本事件配置未成功加载完整。");

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

    private static void EmitPressOnButton(Node root, string displayText)
    {
        FindButtonContaining(root, displayText).EmitSignal(Button.SignalName.Pressed);
    }

    private static Button FindDialogButtonByExactText(Node root, string displayText)
    {
        foreach (Node child in root.FindChildren("*", "Button", true, false))
        {
            if (child is Button button && string.Equals(button.Text, displayText, StringComparison.Ordinal))
            {
                return button;
            }
        }

        throw new InvalidOperationException($"未找到弹窗按钮：{displayText}");
    }

    private static Button FindButtonContaining(Node root, string displayText)
    {
        foreach (Node child in root.FindChildren("*", "Button", true, false))
        {
            if (child is Button button && button.Text.Contains(displayText, StringComparison.Ordinal))
            {
                return button;
            }
        }

        throw new InvalidOperationException($"未找到按钮：{displayText}");
    }

    private static bool HasButtonContaining(Node root, string displayText)
    {
        foreach (Node child in root.FindChildren("*", "Button", true, false))
        {
            if (child is Button button && button.Text.Contains(displayText, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasVisibleProgressBar(Node root)
    {
        return root.FindChildren("*", "ProgressBar", true, false)
            .OfType<ProgressBar>()
            .Any(bar => bar.Visible);
    }
}
