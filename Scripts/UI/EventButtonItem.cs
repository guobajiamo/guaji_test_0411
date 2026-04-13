using Godot;
using System;

namespace Test00_0410.UI;

/// <summary>
/// 单个事件按钮组件。
/// 统一封装“上方进度条槽位 + 下方按钮”这套结构，
/// 这样无论一次性事件、点击事件还是挂机事件，都复用同一套布局。
/// </summary>
public partial class EventButtonItem : Control
{
    public string EventId { get; private set; } = string.Empty;

    private Action<string>? _onPressed;
    private bool _isBound;
    private MainUiLayoutSettings _layoutSettings = new();
    private int _progressBarHeight = 10;

    private MarginContainer? _progressSlot;
    private ProgressBar? _progressBar;
    private Button? _button;

    public Button? ActionButton => _button;

    public Control? ProgressSlot => _progressSlot;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void BindEvent(
        EventButtonViewData data,
        Action<string> onPressed,
        MainUiLayoutSettings layoutSettings,
        int progressBarHeight = 10)
    {
        _onPressed = onPressed;
        _layoutSettings = layoutSettings;
        _progressBarHeight = progressBarHeight;

        EnsureStructure();
        if (!_isBound && _button != null)
        {
            _button.Pressed += OnPressedInternal;
            _isBound = true;
        }

        UpdateView(data, layoutSettings, progressBarHeight);
    }

    public void UpdateView(EventButtonViewData data, MainUiLayoutSettings layoutSettings, int progressBarHeight = 10)
    {
        _layoutSettings = layoutSettings;
        _progressBarHeight = progressBarHeight;
        EnsureStructure();

        EventId = data.EventId;
        TooltipText = string.IsNullOrWhiteSpace(data.TooltipText) ? data.Description : data.TooltipText;
        CustomMinimumSize = new Vector2(0, layoutSettings.EventButtonMinHeight + progressBarHeight + 4);

        if (_progressSlot != null)
        {
            _progressSlot.CustomMinimumSize = new Vector2(0, progressBarHeight);
        }

        if (_progressBar != null)
        {
            _progressBar.Visible = data.ShowProgressBar;
            _progressBar.Value = Math.Max(0.0, Math.Min(100.0, data.ProgressRatio * 100.0));
            _progressBar.CustomMinimumSize = new Vector2(0, progressBarHeight);
            _progressBar.AddThemeColorOverride("fill", new Color("#8a63ff"));
            _progressBar.AddThemeColorOverride("background", new Color("#241c43"));
        }

        if (_button != null)
        {
            _button.Text = data.DisplayName;
            _button.TooltipText = TooltipText;
            _button.Disabled = data.IsDisabled;
            _button.CustomMinimumSize = new Vector2(0, layoutSettings.EventButtonMinHeight);
            _button.AddThemeFontSizeOverride("font_size", layoutSettings.BodyFontSize);
            UiImageThemeManager.ApplyButtonStyle(_button, data.StyleVariant);
        }
    }

    public void SetLiveProgress(double progressRatio, bool isVisible)
    {
        EnsureStructure();
        if (_progressBar == null)
        {
            return;
        }

        _progressBar.Visible = isVisible;
        _progressBar.Value = Math.Max(0.0, Math.Min(100.0, progressRatio * 100.0));
    }

    private void EnsureStructure()
    {
        if (_progressSlot != null && _progressBar != null && _button != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 4);
        AddChild(root);

        _progressSlot = new MarginContainer
        {
            Name = "ProgressSlot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _progressBarHeight),
            MouseFilter = MouseFilterEnum.Stop
        };
        root.AddChild(_progressSlot);

        _progressBar = new ProgressBar
        {
            Name = "ProgressBar",
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _progressBarHeight),
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _progressBar.SetAnchorsPreset(LayoutPreset.FullRect);
        _progressSlot.AddChild(_progressBar);

        _button = new Button
        {
            Name = "ActionButton",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _button.Alignment = HorizontalAlignment.Center;
        root.AddChild(_button);
    }

    private void OnPressedInternal()
    {
        _onPressed?.Invoke(EventId);
    }
}
