using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using Test00_0410.Autoload;
using Test00_0410.Core.SaveLoad;

namespace Test00_0410.UI;

/// <summary>
/// 游戏主菜单界面。
/// 游戏启动后先进入这里，再由玩家选择新游戏、读档、设置或测试入口。
/// </summary>
public partial class MainMenuUI : Control
{
    private GameManager? _gameManager;
    private MainUiLayoutSettings _layoutSettings = new();
    private Action? _onStartNewGame;
    private Action<string>? _onLoadSaveRequested;
    private Action? _onOpenTestGame;
    private Action? _onQuitRequested;
    private SaveSlotDialog? _loadDialog;
    private ConfirmActionDialog? _infoDialog;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(
        GameManager gameManager,
        MainUiLayoutSettings layoutSettings,
        Action onStartNewGame,
        Action<string> onLoadSaveRequested,
        Action onOpenTestGame,
        Action onQuitRequested)
    {
        _gameManager = gameManager;
        _layoutSettings = layoutSettings;
        _onStartNewGame = onStartNewGame;
        _onLoadSaveRequested = onLoadSaveRequested;
        _onOpenTestGame = onOpenTestGame;
        _onQuitRequested = onQuitRequested;
        EnsureStructure();
    }

    private void EnsureStructure()
    {
        if (GetNodeOrNull("Background") != null)
        {
            return;
        }

        Control background = UiImageThemeManager.CreateMenuBackground();
        background.Name = "Background";
        AddChild(background);

        MarginContainer margin = new()
        {
            Name = "Margin"
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 36);
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_right", 36);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        AddChild(margin);

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 18);
        margin.AddChild(root);

        HBoxContainer topRow = new()
        {
            Name = "TopRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(topRow);

        Label title = new()
        {
            Text = "游戏主菜单",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color("f8fafc"));
        topRow.AddChild(title);

        Button testButton = new()
        {
            Text = "进入游戏测试",
            TooltipText = "特殊调试入口。后期可以隐藏。",
            CustomMinimumSize = new Vector2(180, 48)
        };
        UiImageThemeManager.ApplyButtonStyle(testButton, "menu_test_entry");
        testButton.Pressed += () => _onOpenTestGame?.Invoke();
        topRow.AddChild(testButton);

        HBoxContainer centerRow = new()
        {
            Name = "CenterRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(centerRow);

        VBoxContainer menuColumn = new()
        {
            Name = "MenuColumn",
            CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        menuColumn.AddThemeConstantOverride("separation", 12);
        centerRow.AddChild(menuColumn);

        Button newGameButton = CreateMenuButton("新的游戏", "读取默认主线剧本，开始新的故事。");
        newGameButton.Pressed += () => _onStartNewGame?.Invoke();
        menuColumn.AddChild(newGameButton);

        Button loadButton = CreateMenuButton("读取存档", "打开 10 槽位读档列表。");
        loadButton.Pressed += ShowLoadDialog;
        menuColumn.AddChild(loadButton);

        Button settingsButton = CreateMenuButton("游戏设置", "设置界面接口已预留，本次先不实现具体功能。");
        settingsButton.Pressed += ShowSettingsPlaceholder;
        menuColumn.AddChild(settingsButton);

        Button quitButton = CreateMenuButton("退出游戏", "关闭当前游戏程序。");
        quitButton.Pressed += () => _onQuitRequested?.Invoke();
        menuColumn.AddChild(quitButton);

        Control filler = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        centerRow.AddChild(filler);

        _loadDialog = new SaveSlotDialog
        {
            Name = "MainMenuLoadDialog"
        };
        _loadDialog.Configure(_layoutSettings);
        AddChild(_loadDialog);

        _infoDialog = new ConfirmActionDialog
        {
            Name = "MainMenuInfoDialog"
        };
        _infoDialog.Configure(_layoutSettings);
        AddChild(_infoDialog);
    }

    private Button CreateMenuButton(string text, string tooltipText)
    {
        Button button = new()
        {
            Text = text,
            TooltipText = tooltipText,
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.AddThemeFontSizeOverride("font_size", _layoutSettings.BodyFontSize + 2);
        UiImageThemeManager.ApplyButtonStyle(button, "menu_primary");
        return button;
    }

    private void ShowLoadDialog()
    {
        if (_gameManager == null || _loadDialog == null)
        {
            return;
        }

        _loadDialog.ShowDialog(
            "读取存档",
            BuildSlotViewData(_gameManager.GetStorySaveSlotSummaries(), true),
            slotIndex =>
            {
                SaveSlotSummary summary = _gameManager.GetStorySaveSlotSummaries()[slotIndex - 1];
                if (!summary.Exists)
                {
                    return;
                }

                _loadDialog.HideDialog();
                _onLoadSaveRequested?.Invoke(summary.FilePath);
            });
    }

    private void ShowSettingsPlaceholder()
    {
        _infoDialog?.ShowDialog(
            "游戏设置",
            "设置界面接口已预留，本次暂不实现具体设置项。",
            "我知道了",
            "关闭",
            () => { },
            false);
    }

    private static IReadOnlyList<SaveSlotViewData> BuildSlotViewData(IReadOnlyList<SaveSlotSummary> summaries, bool loadOnlyExisting)
    {
        List<SaveSlotViewData> viewData = new();
        foreach (SaveSlotSummary summary in summaries)
        {
            string summaryText = summary.Exists
                ? $"剧本：{summary.ScenarioDisplayName}\n保存时间：{FormatSavedTime(summary.SavedAtUnixSeconds)}"
                : "空存档位";

            viewData.Add(new SaveSlotViewData
            {
                SlotIndex = summary.SlotIndex,
                Title = $"存档位 {summary.SlotIndex:00}（{summary.FileName}）",
                Summary = summaryText,
                TooltipText = summary.Exists
                    ? $"读取该存档位。\n路径：{summary.FilePath}"
                    : $"这个存档位当前为空。\n路径：{summary.FilePath}",
                IsEnabled = loadOnlyExisting ? summary.Exists : true
            });
        }

        return viewData;
    }

    private static string FormatSavedTime(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return "未知";
        }

        DateTimeOffset dateTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
