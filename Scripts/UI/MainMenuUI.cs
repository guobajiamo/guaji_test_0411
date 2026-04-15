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
    private const string MainMenuButtonAnimBoundMetaKey = "__main_menu_anim_bound";
    private static readonly Vector2 MenuButtonIdleScale = Vector2.One;
    private static readonly Vector2 MenuButtonHoverScale = new(1.00f, 1.03f);
    private static readonly Vector2 MenuButtonPressedScale = new(0.99f, 0.97f);
    private const float MainMenuPrimaryButtonScale = 2.0f;

    private GameManager? _gameManager;
    private MainUiLayoutSettings _layoutSettings = new();
    private Action? _onStartNewGame;
    private Action<string>? _onLoadSaveRequested;
    private Action? _onOpenTestGame;
    private Action? _onQuitRequested;
    private SaveSlotDialog? _loadDialog;
    private ConfirmActionDialog? _infoDialog;
    private readonly Dictionary<Button, Tween> _menuButtonTweens = new();
    private readonly HashSet<Button> _hoveredMenuButtons = new();

    public override void _Ready()
    {
        EnsureStructure();
    }

    public override void _ExitTree()
    {
        foreach (Tween tween in _menuButtonTweens.Values)
        {
            if (IsInstanceValid(tween))
            {
                tween.Kill();
            }
        }

        _menuButtonTweens.Clear();
        _hoveredMenuButtons.Clear();
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
        BindMainMenuButtonFeedback(testButton);
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
            CustomMinimumSize = new Vector2(320 * MainMenuPrimaryButtonScale, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        menuColumn.AddThemeConstantOverride("separation", (int)(12 * MainMenuPrimaryButtonScale));
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
        bool useStitchStyle = string.Equals(
            UiImageThemeManager.GetThemeMode(),
            GameplayUiConfigLoader.UiThemeModeStitch,
            StringComparison.Ordinal);
        _loadDialog.Configure(_layoutSettings, useStitchStyle);
        AddChild(_loadDialog);

        _infoDialog = new ConfirmActionDialog
        {
            Name = "MainMenuInfoDialog"
        };
        _infoDialog.Configure(_layoutSettings, useStitchStyle);
        AddChild(_infoDialog);
    }

    private Button CreateMenuButton(string text, string tooltipText)
    {
        Button button = new()
        {
            Text = text,
            TooltipText = tooltipText,
            CustomMinimumSize = new Vector2(0, 52 * MainMenuPrimaryButtonScale),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.AddThemeFontSizeOverride("font_size", (int)Math.Round((_layoutSettings.BodyFontSize + 2) * MainMenuPrimaryButtonScale));
        button.ActionMode = BaseButton.ActionModeEnum.Press;
        button.FocusMode = FocusModeEnum.None;
        UiImageThemeManager.ApplyButtonStyle(button, "menu_primary");
        BindMainMenuButtonFeedback(button);
        return button;
    }

    private void BindMainMenuButtonFeedback(Button button)
    {
        if (button.HasMeta(MainMenuButtonAnimBoundMetaKey))
        {
            return;
        }

        button.SetMeta(MainMenuButtonAnimBoundMetaKey, true);
        UpdateMainMenuButtonPivot(button);
        button.Resized += () => UpdateMainMenuButtonPivot(button);
        button.MouseEntered += () =>
        {
            if (!IsInstanceValid(button) || button.Disabled)
            {
                return;
            }

            _hoveredMenuButtons.Add(button);
            AnimateMainMenuButtonScale(button, MenuButtonHoverScale, 0.08);
        };
        button.MouseExited += () =>
        {
            if (!IsInstanceValid(button))
            {
                return;
            }

            _hoveredMenuButtons.Remove(button);
            AnimateMainMenuButtonScale(button, MenuButtonIdleScale, 0.08);
        };
        button.ButtonDown += () =>
        {
            if (!IsInstanceValid(button) || button.Disabled)
            {
                return;
            }

            AnimateMainMenuButtonScale(button, MenuButtonPressedScale, 0.05);
        };
        button.ButtonUp += () =>
        {
            if (!IsInstanceValid(button))
            {
                return;
            }

            AnimateMainMenuButtonScale(
                button,
                _hoveredMenuButtons.Contains(button) ? MenuButtonHoverScale : MenuButtonIdleScale,
                0.06);
        };
    }

    private static void UpdateMainMenuButtonPivot(Button button)
    {
        if (!IsInstanceValid(button))
        {
            return;
        }

        button.PivotOffset = new Vector2(button.Size.X * 0.5f, button.Size.Y);
    }

    private void AnimateMainMenuButtonScale(Button button, Vector2 targetScale, double duration)
    {
        if (!IsInstanceValid(button))
        {
            return;
        }

        UpdateMainMenuButtonPivot(button);
        if (_menuButtonTweens.TryGetValue(button, out Tween? oldTween) && IsInstanceValid(oldTween))
        {
            oldTween.Kill();
        }

        Tween tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(button, "scale", targetScale, duration);
        _menuButtonTweens[button] = tween;
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
