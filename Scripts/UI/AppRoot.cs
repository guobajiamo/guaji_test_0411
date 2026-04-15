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
    private bool _isWindowSizeHandlerBound;

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
    /// UI 设计基准切换为 2K（2560x1440），并在运行时根据窗口比例自适应缩放策略：
    /// 1) 常规横屏采用 Expand，尽量利用额外可视区域；
    /// 2) 竖屏采用 KeepWidth，优先保证阅读宽度和布局稳定。
    /// </summary>
    private void ConfigureAdaptiveWindow()
    {
        _rootWindow = GetWindow();
        if (_rootWindow == null)
        {
            return;
        }

        Vector2I designSize = GetDesignSize();
        _rootWindow.ContentScaleSize = designSize;
        _rootWindow.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        _rootWindow.ContentScaleFactor = 1.0f;
        if (!_isWindowSizeHandlerBound)
        {
            _rootWindow.SizeChanged += UpdateAdaptiveScale;
            _isWindowSizeHandlerBound = true;
        }

        if (!IsMobilePlatform())
        {
            _rootWindow.Unresizable = false;
            _rootWindow.Borderless = false;
            _rootWindow.Size = designSize;
            _rootWindow.MinSize = new Vector2I(
                Math.Max(1280, designSize.X / 2),
                Math.Max(720, designSize.Y / 2));
        }

        UpdateAdaptiveScale();
    }

    /// <summary>
    /// 根据当前窗口比例切换缩放策略，不做额外倍数放大/缩小（保持 ContentScaleFactor=1）。
    /// </summary>
    private void UpdateAdaptiveScale()
    {
        if (_rootWindow == null)
        {
            return;
        }

        _rootWindow.ContentScaleAspect = ResolveAdaptiveAspect(_rootWindow.Size);
        _rootWindow.ContentScaleFactor = 1.0f;
    }

    private Vector2I GetDesignSize()
    {
        return new Vector2I(
            Math.Max(1280, _layoutSettings.WindowBaseWidth),
            Math.Max(720, _layoutSettings.WindowBaseHeight));
    }

    private static Window.ContentScaleAspectEnum ResolveAdaptiveAspect(Vector2I windowSize)
    {
        if (windowSize.Y <= 0)
        {
            return Window.ContentScaleAspectEnum.Expand;
        }

        float aspect = (float)windowSize.X / windowSize.Y;
        return aspect < 1.2f
            ? Window.ContentScaleAspectEnum.KeepWidth
            : Window.ContentScaleAspectEnum.Expand;
    }

    private static bool IsMobilePlatform()
    {
        return OS.HasFeature("mobile")
            || OS.GetName().Equals("Android", StringComparison.OrdinalIgnoreCase)
            || OS.GetName().Equals("iOS", StringComparison.OrdinalIgnoreCase);
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
