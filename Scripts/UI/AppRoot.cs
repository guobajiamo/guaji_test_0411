using Godot;
using System;
using Test00_0410.Autoload;

namespace Test00_0410.UI;

/// <summary>
/// 应用根界面控制器。
/// 负责在“主菜单”和“游戏测试/主线游戏界面”之间切换。
/// </summary>
public partial class AppRoot : Control
{
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenuUI.tscn";
    private const string MainUiScenePath = "res://Scenes/UI/MainUI.tscn";
    private const string LayoutSettingsPath = "res://Resources/UI/main_ui_layout.tres";

    private Control? _screenHost;
    private MainUiLayoutSettings _layoutSettings = new();
    private Window? _rootWindow;

    public override void _Ready()
    {
        LoadLayoutSettings();
        EnsureStructure();
        ConfigureAdaptiveWindow();
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        MainMenuUI mainMenu = InstantiateScene<MainMenuUI>(MainMenuScenePath);
        mainMenu.Configure(
            GameManager.Instance!,
            _layoutSettings,
            StartDefaultStory,
            LoadStoryFromMenu,
            OpenTestGame,
            () => GetTree().Quit());
        ReplaceScreen(mainMenu);
    }

    public void OpenTestGame()
    {
        if (GameManager.Instance?.StartTestScenario() != true)
        {
            return;
        }

        ReplaceScreen(InstantiateScene<MainUI>(MainUiScenePath));
    }

    public void StartDefaultStory()
    {
        if (GameManager.Instance?.StartDefaultStoryScenario() != true)
        {
            return;
        }

        ReplaceScreen(InstantiateScene<MainUI>(MainUiScenePath));
    }

    public void LoadStoryFromMenu(string path)
    {
        if (GameManager.Instance?.LoadGameFromPath(path) != true)
        {
            return;
        }

        ReplaceScreen(InstantiateScene<MainUI>(MainUiScenePath));
    }

    private void LoadLayoutSettings()
    {
        _layoutSettings = ResourceLoader.Load<MainUiLayoutSettings>(LayoutSettingsPath) ?? new MainUiLayoutSettings();
    }

    private void EnsureStructure()
    {
        if (_screenHost != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        _screenHost = new Control
        {
            Name = "ScreenHost"
        };
        _screenHost.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_screenHost);
    }

    /// <summary>
    /// 当前先把 UI 设计尺寸固定在 1920x1080。
    /// 小窗口时整体缩小，大窗口时不主动放大超过 1 倍，尽量避免图片 UI 被拉糊。
    /// </summary>
    private void ConfigureAdaptiveWindow()
    {
        _rootWindow = GetWindow();
        Vector2I designSize = new(_layoutSettings.WindowBaseWidth, _layoutSettings.WindowBaseHeight);

        _rootWindow.Unresizable = false;
        _rootWindow.Borderless = false;
        _rootWindow.Size = designSize;
        _rootWindow.MinSize = new Vector2I(960, 540);
        _rootWindow.ContentScaleSize = designSize;
        _rootWindow.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        _rootWindow.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        _rootWindow.SizeChanged += UpdateAdaptiveScale;
        UpdateAdaptiveScale();
    }

    /// <summary>
    /// 当前 UI 设计基准固定为 1920x1080。
    /// 窗口缩小时按比例压缩，窗口放大时不超过 1 倍，尽量避免 PNG UI 被放大糊掉。
    /// </summary>
    private void UpdateAdaptiveScale()
    {
        if (_rootWindow == null)
        {
            return;
        }

        Vector2I designSize = new(_layoutSettings.WindowBaseWidth, _layoutSettings.WindowBaseHeight);
        _rootWindow.ContentScaleFactor = MathF.Min(
            1.0f,
            MathF.Min(
                (float)_rootWindow.Size.X / designSize.X,
                (float)_rootWindow.Size.Y / designSize.Y));
    }

    private void ReplaceScreen(Control nextScreen)
    {
        if (_screenHost == null)
        {
            return;
        }

        foreach (Node child in _screenHost.GetChildren())
        {
            _screenHost.RemoveChild(child);
            child.QueueFree();
        }

        nextScreen.Name = nextScreen is MainMenuUI ? "MainMenuUI" : "MainUI";
        nextScreen.SetAnchorsPreset(LayoutPreset.FullRect);
        _screenHost.AddChild(nextScreen);
    }

    private static T InstantiateScene<T>(string path) where T : Control
    {
        PackedScene scene = GD.Load<PackedScene>(path);
        return scene.Instantiate<T>();
    }
}
