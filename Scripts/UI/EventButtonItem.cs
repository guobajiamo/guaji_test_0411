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
    private static readonly Color ProgressFillColor = new("#39ff88");
    private static readonly Color ProgressBackgroundColor = new("#102018");
    private static readonly Vector2 ButtonIdleScale = Vector2.One;
    private static readonly Vector2 ButtonHoverScale = new(1.00f, 1.03f);
    private static readonly Vector2 ButtonPressedScale = new(0.99f, 0.97f);

    public string EventId { get; private set; } = string.Empty;

    private Action<string>? _onPressed;
    private bool _isBound;
    private MainUiLayoutSettings _layoutSettings = new();
    private int _progressBarHeight = 10;

    private MarginContainer? _progressSlot;
    private ProgressBar? _progressBar;
    private Button? _button;
    private bool _isInteractionBound;
    private bool _isButtonHovered;
    private Tween? _buttonTween;
    private string _appliedStyleVariant = string.Empty;
    private bool _hasAppliedDisabledState;
    private bool _appliedDisabledState;
    private bool _progressStylesInitialized;

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

        EnsureInteractionFeedbackBindings();

        UpdateView(data, layoutSettings, progressBarHeight);
    }

    public void UpdateView(EventButtonViewData data, MainUiLayoutSettings layoutSettings, int progressBarHeight = 10)
    {
        _layoutSettings = layoutSettings;
        _progressBarHeight = progressBarHeight;
        EnsureStructure();

        EventId = data.EventId;
        TooltipText = string.Empty;
        CustomMinimumSize = new Vector2(0, layoutSettings.EventButtonMinHeight + progressBarHeight + 4);

        if (_progressSlot != null)
        {
            _progressSlot.CustomMinimumSize = new Vector2(0, progressBarHeight);
        }

        if (_progressBar != null)
        {
            bool shouldShowProgress = data.ShowProgressBar;
            if (_progressBar.Visible != shouldShowProgress)
            {
                _progressBar.Visible = shouldShowProgress;
            }

            double progressValue = Math.Max(0.0, Math.Min(100.0, data.ProgressRatio * 100.0));
            if (Math.Abs(_progressBar.Value - progressValue) > 0.02)
            {
                _progressBar.Value = progressValue;
            }

            if (Math.Abs(_progressBar.CustomMinimumSize.Y - progressBarHeight) > 0.01f)
            {
                _progressBar.CustomMinimumSize = new Vector2(0, progressBarHeight);
            }

            EnsureProgressStyles();
        }

        if (_button != null)
        {
            if (!string.Equals(_button.Text, data.DisplayName, StringComparison.Ordinal))
            {
                _button.Text = data.DisplayName;
            }

            if (!string.IsNullOrEmpty(_button.TooltipText))
            {
                _button.TooltipText = string.Empty;
            }

            if (!_hasAppliedDisabledState || _appliedDisabledState != data.IsDisabled)
            {
                _button.Disabled = data.IsDisabled;
                _appliedDisabledState = data.IsDisabled;
                _hasAppliedDisabledState = true;
                if (data.IsDisabled)
                {
                    _isButtonHovered = false;
                    AnimateButtonScale(ButtonIdleScale, 0.08);
                }
            }

            _button.CustomMinimumSize = new Vector2(0, layoutSettings.EventButtonMinHeight);
            _button.AddThemeFontSizeOverride("font_size", layoutSettings.BodyFontSize);
            if (!string.Equals(_appliedStyleVariant, data.StyleVariant, StringComparison.Ordinal))
            {
                UiImageThemeManager.ApplyButtonStyle(_button, data.StyleVariant);
                _appliedStyleVariant = data.StyleVariant;
            }
        }
    }

    public void SetLiveProgress(double progressRatio, bool isVisible)
    {
        EnsureStructure();
        if (_progressBar == null)
        {
            return;
        }

        if (_progressBar.Visible != isVisible)
        {
            _progressBar.Visible = isVisible;
        }

        double clampedValue = Math.Max(0.0, Math.Min(100.0, progressRatio * 100.0));
        if (Math.Abs(_progressBar.Value - clampedValue) > 0.02)
        {
            _progressBar.Value = clampedValue;
        }
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
        _progressSlot.GuiInput += OnProgressSlotGuiInput;
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
        EnsureProgressStyles();
        _progressSlot.AddChild(_progressBar);

        _button = new Button
        {
            Name = "ActionButton",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _button.Alignment = HorizontalAlignment.Center;
        _button.ActionMode = BaseButton.ActionModeEnum.Press;
        _button.FocusMode = FocusModeEnum.None;
        _button.Resized += OnButtonResized;
        root.AddChild(_button);
        UpdateButtonPivot();
    }

    private void OnPressedInternal()
    {
        _onPressed?.Invoke(EventId);
    }

    private void EnsureInteractionFeedbackBindings()
    {
        if (_isInteractionBound || _button == null)
        {
            return;
        }

        _button.MouseEntered += OnButtonMouseEntered;
        _button.MouseExited += OnButtonMouseExited;
        _button.ButtonDown += OnButtonDown;
        _button.ButtonUp += OnButtonUp;
        _isInteractionBound = true;
    }

    private static StyleBoxFlat CreateProgressFillStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = ProgressFillColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
    }

    private static StyleBoxFlat CreateProgressBackgroundStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = ProgressBackgroundColor,
            BorderColor = new Color("#203128"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
    }

    private void EnsureProgressStyles()
    {
        if (_progressBar == null || _progressStylesInitialized)
        {
            return;
        }

        _progressBar.AddThemeStyleboxOverride("fill", CreateProgressFillStyle());
        _progressBar.AddThemeStyleboxOverride("background", CreateProgressBackgroundStyle());
        _progressStylesInitialized = true;
    }

    private void OnProgressSlotGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton
            || mouseButton.ButtonIndex != MouseButton.Left
            || !mouseButton.Pressed)
        {
            return;
        }

        if (_button?.Disabled == true || string.IsNullOrWhiteSpace(EventId))
        {
            return;
        }

        AnimateButtonScale(ButtonPressedScale, 0.04);
        OnPressedInternal();
        CallDeferred(nameof(RecoverButtonScaleAfterManualPress));
        AcceptEvent();
    }

    private void OnButtonMouseEntered()
    {
        if (_button?.Disabled == true)
        {
            return;
        }

        _isButtonHovered = true;
        AnimateButtonScale(ButtonHoverScale, 0.08);
    }

    private void OnButtonMouseExited()
    {
        _isButtonHovered = false;
        AnimateButtonScale(ButtonIdleScale, 0.08);
    }

    private void OnButtonDown()
    {
        if (_button?.Disabled == true)
        {
            return;
        }

        AnimateButtonScale(ButtonPressedScale, 0.05);
    }

    private void OnButtonUp()
    {
        AnimateButtonScale(_isButtonHovered ? ButtonHoverScale : ButtonIdleScale, 0.06);
    }

    private void RecoverButtonScaleAfterManualPress()
    {
        AnimateButtonScale(_isButtonHovered ? ButtonHoverScale : ButtonIdleScale, 0.06);
    }

    private void OnButtonResized()
    {
        UpdateButtonPivot();
    }

    private void UpdateButtonPivot()
    {
        if (_button == null || !IsInstanceValid(_button))
        {
            return;
        }

        // Keep scale growth anchored at the bottom-center so hover feels upward instead of drifting sideways.
        _button.PivotOffset = new Vector2(_button.Size.X * 0.5f, _button.Size.Y);
    }

    private void AnimateButtonScale(Vector2 targetScale, double duration)
    {
        if (_button == null || !IsInstanceValid(_button))
        {
            return;
        }

        UpdateButtonPivot();
        _buttonTween?.Kill();
        _buttonTween = CreateTween();
        _buttonTween.SetEase(Tween.EaseType.Out);
        _buttonTween.SetTrans(Tween.TransitionType.Cubic);
        _buttonTween.TweenProperty(_button, "scale", targetScale, duration);
    }
}
