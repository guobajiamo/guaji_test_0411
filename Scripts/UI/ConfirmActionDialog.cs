using Godot;
using System;

namespace Test00_0410.UI;

/// <summary>
/// 简单确认弹窗。
/// 用于读档覆盖提示、存档覆盖提示，以及预留功能提示。
/// </summary>
public partial class ConfirmActionDialog : Control
{
    private ColorRect? _overlay;
    private PanelContainer? _dialogPanel;
    private Label? _titleLabel;
    private RichTextLabel? _messageLabel;
    private Button? _confirmButton;
    private Button? _cancelButton;
    private Action? _onConfirm;
    private bool _useStitchStyle;

    public override void _Ready()
    {
        EnsureStructure();
        HideDialog();
    }

    public void Configure(MainUiLayoutSettings layoutSettings, bool useStitchStyle = false)
    {
        _useStitchStyle = useStitchStyle;
        EnsureStructure();
        _titleLabel!.AddThemeFontSizeOverride("font_size", layoutSettings.SectionHeaderFontSize);
        _messageLabel!.AddThemeFontSizeOverride("normal_font_size", layoutSettings.BodyFontSize);
        ApplyThemeStyles();
    }

    public void ShowDialog(string title, string message, string confirmText, string cancelText, Action onConfirm, bool showCancel = true)
    {
        EnsureStructure();
        _titleLabel!.Text = title;
        _messageLabel!.Text = message;
        _confirmButton!.Text = confirmText;
        _cancelButton!.Text = cancelText;
        _cancelButton.Visible = showCancel;
        _onConfirm = onConfirm;
        Visible = true;
    }

    public void HideDialog()
    {
        EnsureStructure();
        Visible = false;
        _onConfirm = null;
    }

    private void EnsureStructure()
    {
        if (_overlay != null
            && _dialogPanel != null
            && _titleLabel != null
            && _messageLabel != null
            && _confirmButton != null
            && _cancelButton != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 170;

        _overlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.55f),
            MouseFilter = MouseFilterEnum.Stop
        };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        CenterContainer center = new();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _dialogPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(420, 0)
        };
        center.AddChild(_dialogPanel);

        VBoxContainer content = new();
        content.AddThemeConstantOverride("separation", 10);
        _dialogPanel.AddChild(content);

        _titleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.AddChild(_titleLabel);

        _messageLabel = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = false,
            CustomMinimumSize = new Vector2(0, 80)
        };
        content.AddChild(_messageLabel);

        HBoxContainer buttons = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        buttons.AddThemeConstantOverride("separation", 10);
        content.AddChild(buttons);

        _cancelButton = new Button
        {
            Text = "取消",
            CustomMinimumSize = new Vector2(100, 40),
            ActionMode = BaseButton.ActionModeEnum.Press,
            FocusMode = FocusModeEnum.None
        };
        _cancelButton.Pressed += HideDialog;
        buttons.AddChild(_cancelButton);

        _confirmButton = new Button
        {
            Text = "确认",
            CustomMinimumSize = new Vector2(100, 40),
            ActionMode = BaseButton.ActionModeEnum.Press,
            FocusMode = FocusModeEnum.None
        };
        _confirmButton.Pressed += () =>
        {
            Action? confirmAction = _onConfirm;
            HideDialog();
            confirmAction?.Invoke();
        };
        buttons.AddChild(_confirmButton);

        ApplyThemeStyles();
    }

    private void ApplyThemeStyles()
    {
        if (_overlay == null
            || _dialogPanel == null
            || _titleLabel == null
            || _messageLabel == null
            || _confirmButton == null
            || _cancelButton == null)
        {
            return;
        }

        if (_useStitchStyle)
        {
            _overlay.Color = new Color(0, 0, 0, 0.46f);
            _dialogPanel.AddThemeStyleboxOverride("panel", StitchElementStyleLibrary.CreateLightDialogFrame());
            _titleLabel.AddThemeColorOverride("font_color", new Color("#224545"));
            _messageLabel.AddThemeColorOverride("default_color", new Color("#30332e"));
            UiImageThemeManager.ApplyButtonStyle(_confirmButton, "system_action");
            UiImageThemeManager.ApplyButtonStyle(_cancelButton, "system_return_menu");
        }
    }
}
