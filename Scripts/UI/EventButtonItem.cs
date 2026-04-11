using Godot;
using System;

namespace Test00_0410.UI;

/// <summary>
/// 单个事件按钮 UI。
/// 如果是挂机事件，它还可以额外显示一个进度条。
/// </summary>
public partial class EventButtonItem : Button
{
    public string EventId { get; set; } = string.Empty;
    private Action<string>? _onPressed;
    private bool _isBound;

    public void BindEvent(EventButtonViewData data, Action<string> onPressed, MainUiLayoutSettings layoutSettings)
    {
        _onPressed = onPressed;
        if (!_isBound)
        {
            Pressed += OnPressedInternal;
            _isBound = true;
        }

        UpdateView(data, layoutSettings);
    }

    public void UpdateView(EventButtonViewData data, MainUiLayoutSettings layoutSettings)
    {
        EventId = data.EventId;
        Text = BuildDisplayText(data);
        TooltipText = string.IsNullOrWhiteSpace(data.TooltipText) ? data.Description : data.TooltipText;
        Disabled = data.IsDisabled;
        CustomMinimumSize = new Vector2(0, layoutSettings.EventButtonMinHeight);
        AddThemeFontSizeOverride("font_size", layoutSettings.BodyFontSize);
    }

    public void SetProgress(double progress)
    {
        // 这一版先把进度直接显示在按钮文本里，避免额外的复杂 UI 依赖。
        // 后续如果你想换成真正的 ProgressBar，可以在这里继续扩展。
    }

    private static string BuildDisplayText(EventButtonViewData data)
    {
        if (data.ProgressRatio > 0.0)
        {
            return $"{data.DisplayName} ({data.ProgressRatio:P0})";
        }

        return data.DisplayName;
    }

    private void OnPressedInternal()
    {
        _onPressed?.Invoke(EventId);
    }
}
