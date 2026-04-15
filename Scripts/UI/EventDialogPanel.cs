using Godot;
using System;
using System.Collections.Generic;

namespace Test00_0410.UI;

/// <summary>
/// 中央事件弹窗面板。
/// 用于显示一次性事件的说明文本，并提供一个或两个按钮让玩家确认选择。
/// </summary>
public partial class EventDialogPanel : Control
{
    private ColorRect? _overlay;
    private PanelContainer? _dialogPanel;
    private Label? _titleLabel;
    private RichTextLabel? _bodyLabel;
    private CenterContainer? _dismissRow;
    private Button? _dismissButton;
    private HBoxContainer? _buttonRow;
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
        _dialogPanel!.CustomMinimumSize = new Vector2(layoutSettings.DialogMinWidth, 0);
        _titleLabel!.AddThemeFontSizeOverride("font_size", layoutSettings.SectionHeaderFontSize);
        _bodyLabel!.AddThemeFontSizeOverride("normal_font_size", layoutSettings.BodyFontSize);
        _dismissButton!.AddThemeFontSizeOverride("font_size", layoutSettings.BodyFontSize);
        ApplyThemeStyles();
    }

    public void ShowDialog(string title, string bodyText, IReadOnlyList<DialogButtonConfig> buttons, bool showCancelButton = true)
    {
        EnsureStructure();

        _titleLabel!.Text = title;
        _bodyLabel!.Text = bodyText;
        _dismissButton!.Text = "我再想想";
        _dismissButton.TooltipText = "我还没做好准备，晚点再说。";
        _dismissRow!.Visible = showCancelButton;
        _dismissButton.Visible = showCancelButton;

        ClearButtonRow();
        foreach (DialogButtonConfig buttonConfig in buttons)
        {
            Button button = new()
            {
                Text = buttonConfig.Text,
                TooltipText = buttonConfig.TooltipText,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(120, 42),
                ActionMode = BaseButton.ActionModeEnum.Press,
                FocusMode = FocusModeEnum.None
            };
            button.Pressed += () =>
            {
                HideDialog();
                buttonConfig.OnPressed?.Invoke();
            };
            if (_useStitchStyle)
            {
                UiImageThemeManager.ApplyButtonStyle(button, "event_oneshot");
                button.AddThemeColorOverride("font_color", new Color("#f4f9ff"));
                button.AddThemeColorOverride("font_hover_color", new Color("#f4f9ff"));
                button.AddThemeColorOverride("font_pressed_color", new Color("#f4f9ff"));
            }
            _buttonRow!.AddChild(button);
        }

        Visible = true;
    }

    public void HideDialog()
    {
        EnsureStructure();
        Visible = false;
    }

    private void EnsureStructure()
    {
        if (_overlay != null
            && _dialogPanel != null
            && _titleLabel != null
            && _bodyLabel != null
            && _dismissRow != null
            && _dismissButton != null
            && _buttonRow != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 200;

        _overlay = new ColorRect
        {
            Name = "Overlay",
            Color = new Color(0, 0, 0, 0.55f),
            MouseFilter = MouseFilterEnum.Stop
        };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        CenterContainer centerContainer = new()
        {
            Name = "CenterContainer",
            MouseFilter = MouseFilterEnum.Stop
        };
        centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centerContainer);

        _dialogPanel = new PanelContainer
        {
            Name = "DialogPanel",
            MouseFilter = MouseFilterEnum.Stop
        };
        centerContainer.AddChild(_dialogPanel);

        VBoxContainer content = new()
        {
            Name = "Content",
            CustomMinimumSize = new Vector2(0, 220)
        };
        content.AddThemeConstantOverride("separation", 12);
        _dialogPanel.AddChild(content);

        _titleLabel = new Label
        {
            Name = "TitleLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        content.AddChild(_titleLabel);

        _bodyLabel = new RichTextLabel
        {
            Name = "BodyLabel",
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(_bodyLabel);

        _dismissRow = new CenterContainer
        {
            Name = "DismissRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(_dismissRow);

        _dismissButton = new Button
        {
            Name = "DismissButton",
            Text = "我再想想",
            CustomMinimumSize = new Vector2(140, 40),
            ActionMode = BaseButton.ActionModeEnum.Press,
            FocusMode = FocusModeEnum.None
        };
        _dismissButton.Pressed += HideDialog;
        _dismissRow.AddChild(_dismissButton);

        _buttonRow = new HBoxContainer
        {
            Name = "ButtonRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _buttonRow.AddThemeConstantOverride("separation", 12);
        content.AddChild(_buttonRow);

        ApplyThemeStyles();
    }

    private void ApplyThemeStyles()
    {
        if (_overlay == null || _dialogPanel == null || _titleLabel == null || _bodyLabel == null || _dismissButton == null)
        {
            return;
        }

        if (_useStitchStyle)
        {
            _overlay.Color = new Color(0, 0, 0, 0.46f);
            _dialogPanel.AddThemeStyleboxOverride("panel", StitchElementStyleLibrary.CreateLightDialogFrame());
            _titleLabel.AddThemeColorOverride("font_color", new Color("#224545"));
            _bodyLabel.AddThemeColorOverride("default_color", new Color("#30332e"));
            UiImageThemeManager.ApplyButtonStyle(_dismissButton, "system_return_menu");
            _dismissButton.AddThemeColorOverride("font_color", new Color("#fbf8f8"));
            _dismissButton.AddThemeColorOverride("font_hover_color", new Color("#fbf8f8"));
            _dismissButton.AddThemeColorOverride("font_pressed_color", new Color("#fbf8f8"));
        }
    }

    private void ClearButtonRow()
    {
        if (_buttonRow == null)
        {
            return;
        }

        foreach (Node child in _buttonRow.GetChildren())
        {
            _buttonRow.RemoveChild(child);
            child.QueueFree();
        }
    }

    public sealed class DialogButtonConfig
    {
        public string Text { get; set; } = string.Empty;

        public string TooltipText { get; set; } = string.Empty;

        public Action? OnPressed { get; set; }
    }
}
