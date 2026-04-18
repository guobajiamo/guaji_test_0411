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
            Assert(mainTabs.GetChildren().Any(child => child.Name == "当前场景"), "中间区域应该存在“当前场景”页。");
            Assert(mainTabs.GetChildren().Any(child => child.Name == "装备"), "中间区域应该存在“装备”页。");
            Assert(mainTabs.GetChildren().Any(child => child.Name == "任务"), "中间区域应该存在“任务”页。");
            Assert(mainTabs.GetChildren().Any(child => child.Name == "教学"), "中间区域应该存在“教学”页。");
            Assert(mainTabs.GetChildren().Any(child => child.Name == "成就"), "中间区域应该存在“成就”页。");
            Assert(!HasButtonContaining(mainUi, "图鉴"), "图鉴页签当前不应显示。");
            Assert(HasButtonContaining(mainUi, "当前场景"), "当前场景页签未显示。");
            Assert(HasButtonContaining(mainUi, "装备"), "装备页签未显示。");
            Assert(HasButtonContaining(mainUi, "任务"), "任务页签未显示。");
            Assert(HasButtonContaining(mainUi, "教学"), "教学页签未显示。");
            Assert(HasButtonContaining(mainUi, "成就"), "成就页签未显示。");

            Label topBarLabel = mainUi.FindChild("TopBarScenarioLabel", true, false) as Label
                ?? throw new InvalidOperationException("顶部主线任务标签未创建。");
            Assert(topBarLabel.Text.Contains("初入人间之里", StringComparison.Ordinal), "顶部栏没有显示当前主线任务。");

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
            Assert(ReadTooltipText(confirmButton).Contains("金币", StringComparison.Ordinal), "单按钮确认项未显示奖励悬浮说明。");
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
            Assert(ReadTooltipText(acceptButton).Contains("金币", StringComparison.Ordinal), "接受馈赠按钮未显示正确悬浮说明。");
            Assert(ReadTooltipText(declineButton).Contains("包子", StringComparison.Ordinal), "婉拒谢礼按钮未显示正确悬浮说明。");

            bool choiceOk = gameManager.ClickEventSystem?.TryTriggerDialogChoice("evt_stranger_reward_offer", "evt_accept_gift_gold", true) ?? false;
            Assert(choiceOk, "双选项分支事件执行失败。");
            Assert(gameManager.PlayerProfile.Economy.Gold >= 30, "接受馈赠后未获得金币。");
            Assert(gameManager.PlayerProfile.CompletedEventIds.Contains("evt_stranger_reward_offer"), "弹窗源事件未被标记为已完成。");

            typedMainUi.RefreshAllPanels();
            EmitPressOnButton(mainUi, "任务");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Button claimRewardButton = mainUi.FindChild("ClaimRewardButton", true, false) as Button
                ?? throw new InvalidOperationException("任务领奖按钮未创建。");
            Assert(claimRewardButton.Visible && !claimRewardButton.Disabled, "完成首个主线任务后，任务页没有出现可领取奖励按钮。");
            int goldBeforeQuestReward = gameManager.PlayerProfile.Economy.Gold;
            claimRewardButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(gameManager.PlayerProfile.Economy.Gold == goldBeforeQuestReward + 20, "领取主线任务奖励后金币没有增加。");
            Assert(gameManager.PlayerProfile.UnlockedAchievementIds.Contains("ach_testgame_story_intro"), "领取主线任务奖励后没有解锁对应成就占位。");

            Assert(gameManager.IdleSystem?.ShouldShowEvent("evt_idle_fishing") == true, "挂机钓鱼按钮应当默认显示。");
            Assert(gameManager.IdleSystem?.CanStartIdleEvent("evt_idle_fishing") != true, "挂机钓鱼在没有鱼竿和鱼饵时不应可点击。");
            Assert(gameManager.ClickEventSystem?.ShouldShowEvent("evt_empty_hook_fishing") != true, "姜太公钓鱼在前置显示条件未满足前不应显示。");

            typedMainUi.RefreshAllPanels();
            Assert(!HasButtonContaining(mainUi, "挂机钓鱼"), "当前不在玄武涧时，不应在中间区域看到挂机钓鱼按钮。");
            Assert(!HasButtonContaining(mainUi, "姜太公钓鱼"), "姜太公钓鱼在前置显示条件未满足前不应显示按钮。");

            bool getRodOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_claim_fishing_rod") ?? false;
            Assert(getRodOk, "领取鱼竿事件执行失败。");
            Assert(gameManager.PlayerProfile.Inventory.HasItem("tool_fishing_rod"), "领取鱼竿后背包中没有鱼竿。");
            typedMainUi.RefreshAllPanels();
            Assert(topBarLabel.Text.Contains("山涧钓手", StringComparison.Ordinal), "拿到鱼竿并完成前序任务后，顶部主线任务没有切换到下一阶段。");
            Assert(gameManager.IdleSystem?.CanStartIdleEvent("evt_idle_fishing") != true, "只有鱼竿没有鱼饵时，挂机钓鱼仍应保持置灰。");
            Assert(gameManager.ClickEventSystem?.ShouldShowEvent("evt_empty_hook_fishing") != true, "只拿到鱼竿还没有对话时，姜太公钓鱼仍不应显示。");

            bool getBaitOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_get_worm_bait") ?? false;
            Assert(getBaitOk, "获取蚯蚓鱼饵事件执行失败。");
            Assert(gameManager.PlayerProfile.Inventory.GetItemAmount("bait_worm") == 3, "获取蚯蚓鱼饵后数量不正确。");
            Assert(gameManager.IdleSystem?.CanStartIdleEvent("evt_idle_fishing") == true, "同时拥有鱼竿和鱼饵后，挂机钓鱼应可点击。");
            typedMainUi.RefreshAllPanels();
            Assert(gameManager.PlayerProfile.UiState.HasNewMarker("genbu_glen"), "非当前区域首次出现可互动按钮后，没有给对应二级区域挂上 New 提示。");
            Assert(HasRegionEntryText(mainUi, "玄武涧"), "当折叠的一级区域下出现 New 时，没有自动展开对应一级区域。");
            Assert(HasAreaNewMarker(mainUi, "玄武涧"), "左侧区域栏没有在“玄武涧”右侧显示 New 提示。");

            EmitPressOnButton(mainUi, "玄武涧");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Label currentRegionTitleLabel = mainUi.FindChild("CurrentRegionTitleLabel", true, false) as Label
                ?? throw new InvalidOperationException("当前区域标题未创建。");
            Assert(currentRegionTitleLabel.Text.Contains("玄武涧", StringComparison.Ordinal), "切换到“玄武涧”后，中间区域标题未同步更新。");
            Assert(!gameManager.PlayerProfile.UiState.HasNewMarker("genbu_glen"), "切换到带有 New 的区域后，提示没有自动清除。");

            bool talkOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_talk_to_taigongwang") ?? false;
            Assert(talkOk, "与太公望对话事件执行失败。");
            Assert(gameManager.PlayerProfile.CompletedEventIds.Contains("evt_talk_to_taigongwang"), "与太公望对话未写入已完成事件历史。");
            Assert(gameManager.ClickEventSystem?.ShouldShowEvent("evt_empty_hook_fishing") == true, "完成对话后，姜太公钓鱼应当显示。");
            Assert(gameManager.ClickEventSystem?.CanTriggerEvent("evt_empty_hook_fishing") == true, "完成对话并持有鱼竿后，姜太公钓鱼应可点击。");
            Assert(!gameManager.PlayerProfile.UiState.HasNewMarker("genbu_glen"), "如果按钮是在当前区域首次可互动，不应再次出现 New 提示。");

            typedMainUi.RefreshAllPanels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Button idleFishingButton = FindButtonContaining(mainUi, "挂机钓鱼");
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

            bool idleStartOk = gameManager.IdleSystem?.StartIdleEvent("evt_idle_chop_apple_tree") ?? false;
            Assert(idleStartOk, "挂机砍苹果树事件启动失败。");
            typedMainUi.RefreshAllPanels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(gameManager.PlayerProfile.IdleState.IsRunning, "挂机砍苹果树启动后未进入运行状态。");
            Assert(string.Equals(gameManager.PlayerProfile.IdleState.ActiveEventId, "evt_idle_chop_apple_tree", StringComparison.Ordinal), "挂机砍苹果树启动后记录的活动事件不正确。");

            bool clickWhileIdleOk = gameManager.ClickEventSystem?.TryTriggerEvent("evt_get_worm_bait") ?? false;
            Assert(clickWhileIdleOk, "挂机进行中执行点击事件失败。");
            Assert(gameManager.PlayerProfile.IdleState.IsRunning, "点击事件错误中断了挂机。");
            Assert(string.Equals(gameManager.PlayerProfile.IdleState.ActiveEventId, "evt_idle_chop_apple_tree", StringComparison.Ordinal), "点击事件错误切换了挂机目标。");

            Assert(gameManager.EventRegistry.Events.Count >= 11, "测试剧本事件配置未成功加载完整。");

            gameManager.IdleSystem?.StopIdleEvent();
            typedMainUi.RefreshAllPanels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            foreach (string snackItemId in new[] { "mat_peach", "mat_plum", "mat_cucumber", "mat_chili" })
            {
                gameManager.SettlementService.AddItem(snackItemId, 1);
            }
            gameManager.PlayerProfile.Inventory.ApplyDisplayOrder(
                new[] { "mat_peach", "mat_plum", "mat_cucumber", "mat_chili" }
                    .Concat(gameManager.PlayerProfile.Inventory.GetActiveItemIdsByDisplayOrder()
                        .Where(itemId => itemId != "mat_peach"
                            && itemId != "mat_plum"
                            && itemId != "mat_cucumber"
                            && itemId != "mat_chili")));
            typedMainUi.RefreshAllPanels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            foreach (string snackItemId in new[] { "mat_peach", "mat_plum", "mat_cucumber", "mat_chili" })
            {
                ConsumeSnackFromInventory(mainUi, snackItemId);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            Assert(gameManager.BuffSystem?.GetActiveBuffs().Any(buff => string.Equals(buff.BuffId, "buff_plum_double_drop", StringComparison.Ordinal)) == true, "After consuming snacks, BuffSystem did not register buff_plum_double_drop.");
            Assert(gameManager.BuffSystem?.GetActiveBuffs().Any(buff => string.Equals(buff.BuffId, "buff_peach_focus", StringComparison.Ordinal)) == true, "After consuming snacks, BuffSystem did not register buff_peach_focus.");
            Assert(gameManager.BuffSystem?.GetActiveBuffs().Any(buff => string.Equals(buff.BuffId, "buff_cucumber_refreshing", StringComparison.Ordinal)) == true, "After consuming snacks, BuffSystem did not register buff_cucumber_refreshing.");
            Assert(gameManager.BuffSystem?.GetActiveBuffs().Any(buff => string.Equals(buff.BuffId, "buff_chili_burning", StringComparison.Ordinal)) == true, "After consuming snacks, BuffSystem did not register buff_chili_burning.");
            Assert(gameManager.PlayerProfile.UiState.SelectedSnackItemIds.Count == 4, "After consuming snacks, the selected snack queue count was not 4.");
            string plumBuffDisplayName = gameManager.ItemRegistry.GetItem("mat_plum")?.ConsumeBuff?.DisplayName
                ?? throw new InvalidOperationException("mat_plum consume buff display name is missing.");
            Assert(ContainsLabelText(mainUi, plumBuffDisplayName), "After consuming mat_plum, the status panel did not show the plum buff label.");

            EmitPressOnMainTab(mainUi, "battle");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(string.Equals(gameManager.PlayerProfile.UiState.SelectedTabId, "battle", StringComparison.Ordinal), "After consuming mat_plum, clicking the battle tab did not switch to battle.");
            Control battlePageRootAfterSnack = mainUi.FindChild("BattlePageRoot", true, false) as Control
                ?? throw new InvalidOperationException("BattlePageRoot was not found after consuming mat_plum.");
            Assert(battlePageRootAfterSnack.Visible, "BattlePageRoot was not visible after consuming mat_plum.");

            EmitPressOnMainTab(mainUi, "system");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(string.Equals(gameManager.PlayerProfile.UiState.SelectedTabId, "system", StringComparison.Ordinal), "After consuming mat_plum, clicking the system tab did not switch to system.");

            PressReturnToMainMenuButton(mainUi);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ConfirmActionDialog confirmDialogAfterSnack = mainUi.FindChild("ConfirmDialog", true, false) as ConfirmActionDialog
                ?? throw new InvalidOperationException("ConfirmDialog was not found after requesting return to main menu.");
            Assert(confirmDialogAfterSnack.Visible, "Return-to-main-menu confirm dialog did not become visible after consuming mat_plum.");
            PressConfirmDialogPrimaryButton(confirmDialogAfterSnack);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(mainScene.FindChild("MainMenuUI", true, false) != null, "After consuming mat_plum, confirming return did not show MainMenuUI.");

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
        Control clickableControl = FindClickableControl(root, displayText);
        if (clickableControl is Button button)
        {
            button.EmitSignal(Button.SignalName.Pressed);
            return;
        }

        InputEventMouseButton clickEvent = new()
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true
        };
        clickableControl.EmitSignal(Control.SignalName.GuiInput, clickEvent);
    }

    private static void EmitPressOnMainTab(Node root, string tabId)
    {
        HBoxContainer tabBar = root.FindChild("MainTabBar", true, false) as HBoxContainer
            ?? throw new InvalidOperationException("MainTabBar was not found.");

        Button[] buttons = tabBar.GetChildren().OfType<Button>().ToArray();
        Button targetButton = tabId switch
        {
            "battle" => buttons.ElementAtOrDefault(3),
            "system" => buttons.LastOrDefault(),
            _ => throw new InvalidOperationException($"Unsupported main tab id: {tabId}")
        }
            ?? throw new InvalidOperationException($"Main tab button was not found for tab id: {tabId}");
        targetButton.EmitSignal(Button.SignalName.Pressed);
    }

    private static void PressReturnToMainMenuButton(Node root)
    {
        Control currentVisiblePage = GetCurrentVisibleMainTabPage(root);
        Button returnButton = currentVisiblePage.FindChildren("*", "Button", true, false)
            .OfType<Button>()
            .Where(button => button.IsVisibleInTree())
            .ElementAtOrDefault(2)
            ?? throw new InvalidOperationException("Return-to-main-menu button was not found in SystemPanel.");
        returnButton.EmitSignal(Button.SignalName.Pressed);
    }

    private static void PressConfirmDialogPrimaryButton(Node root)
    {
        Button confirmButton = root.FindChildren("*", "Button", true, false)
            .OfType<Button>()
            .Where(button => button.IsVisibleInTree())
            .LastOrDefault()
            ?? throw new InvalidOperationException("Primary confirm button was not found in ConfirmActionDialog.");
        confirmButton.EmitSignal(Button.SignalName.Pressed);
    }

    private static Control GetCurrentVisibleMainTabPage(Node root)
    {
        Control pageHost = root.FindChild("MainTabs", true, false) as Control
            ?? throw new InvalidOperationException("MainTabs was not found.");
        return pageHost.GetChildren()
            .OfType<Control>()
            .FirstOrDefault(control => control.IsVisibleInTree())
            ?? throw new InvalidOperationException("No visible main tab page was found.");
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

    private static void ConsumeSnackFromInventory(Node mainUi, string itemId)
    {
        MainUI typedMainUi = mainUi as MainUI
            ?? throw new InvalidOperationException("MainUI type resolution failed while consuming snack.");
        GameManager gameManager = GameManager.Instance ?? throw new InvalidOperationException("GameManager is not initialized.");
        GD.Print($"[UiFeatureSmokeTest] preparing snack consume: {itemId}");
        gameManager.PlayerProfile.UiState.SelectedTabId = "inventory";
        typedMainUi.RefreshAllPanels();
        GD.Print($"[UiFeatureSmokeTest] inventory refreshed for: {itemId}");

        Button slotButton = mainUi.FindChild($"Slot_{itemId}#0", true, false) as Button
            ?? throw new InvalidOperationException($"Snack inventory slot was not found: {itemId}");
        GD.Print($"[UiFeatureSmokeTest] slot found for: {itemId}");
        slotButton.EmitSignal(Button.SignalName.Pressed);

        Button consumeButton = mainUi.FindChild("ConsumeButton", true, false) as Button
            ?? throw new InvalidOperationException("ConsumeButton was not found in InventoryPanel.");
        Assert(consumeButton.Visible && !consumeButton.Disabled, $"ConsumeButton was not enabled after selecting {itemId}.");
        GD.Print($"[UiFeatureSmokeTest] consume button ready for: {itemId}");
        consumeButton.EmitSignal(Button.SignalName.Pressed);
        GD.Print($"[UiFeatureSmokeTest] consume emitted for: {itemId}");
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

    private static Control FindClickableControl(Node root, string displayText)
    {
        foreach (Node child in root.FindChildren("*", "", true, false))
        {
            if (child is Button button && button.Text.Contains(displayText, StringComparison.Ordinal))
            {
                return button;
            }

            if (child is Label label && label.Text.Contains(displayText, StringComparison.Ordinal))
            {
                Node? current = label;
                while (current != null)
                {
                    if (current is Control control && control.MouseFilter == Control.MouseFilterEnum.Stop)
                    {
                        return control;
                    }

                    current = current.GetParent();
                }
            }
        }

        throw new InvalidOperationException($"未找到可点击控件：{displayText}");
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

    private static bool ContainsLabelText(Node root, string displayText)
    {
        return root.FindChildren("*", "Label", true, false)
            .OfType<Label>()
            .Any(label => label.Visible && label.Text.Contains(displayText, StringComparison.Ordinal));
    }

    private static bool HasAreaNewMarker(Node root, string areaDisplayText)
    {
        Node? areaNode = FindRegionEntryNode(root, areaDisplayText);
        if (areaNode == null)
        {
            return false;
        }

        Node? row = areaNode.GetParent();
        if (row == null)
        {
            return false;
        }

        return row.GetChildren()
            .OfType<Label>()
            .Any(label => string.Equals(label.Text, "New", StringComparison.Ordinal));
    }

    private static bool HasRegionEntryText(Node root, string displayText)
    {
        return FindRegionEntryNode(root, displayText) != null;
    }

    private static Node? FindRegionEntryNode(Node root, string displayText)
    {
        foreach (Node child in root.FindChildren("*", "", true, false))
        {
            switch (child)
            {
                case Button button when button.Text.Contains(displayText, StringComparison.Ordinal):
                    return button;
                case Label label when label.Text.Contains(displayText, StringComparison.Ordinal):
                    return label;
            }
        }

        return null;
    }

    private static string ReadTooltipText(Control control)
    {
        if (!string.IsNullOrWhiteSpace(control.TooltipText))
        {
            return control.TooltipText;
        }

        if (!control.HasMeta("__nb_tooltip_text"))
        {
            return string.Empty;
        }

        Variant metaValue = control.GetMeta("__nb_tooltip_text");
        return metaValue.VariantType == Variant.Type.Nil
            ? string.Empty
            : metaValue.AsString();
    }
}
