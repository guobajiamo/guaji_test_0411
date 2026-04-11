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
    private HBoxContainer? _buttonRow;

    public override void _Ready()
    {
        EnsureStructure();
        HideDialog();
    }

    public void Configure(MainUiLayoutSettings layoutSettings)
    {
        EnsureStructure();
        _dialogPanel!.CustomMinimumSize = new Vector2(layoutSettings.DialogMinWidth, 0);
        _titleLabel!.AddThemeFontSizeOverride("font_size", layoutSettings.SectionHeaderFontSize);
        _bodyLabel!.AddThemeFontSizeOverride("normal_font_size", layoutSettings.BodyFontSize);
    }

    public void ShowDialog(string title, string bodyText, IReadOnlyList<DialogButtonConfig> buttons)
    {
        EnsureStructure();

        _titleLabel!.Text = title;
        _bodyLabel!.Text = bodyText;

        ClearButtonRow();
        foreach (DialogButtonConfig buttonConfig in buttons)
        {
            Button button = new()
            {
                Text = buttonConfig.Text,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(120, 42)
            };
            button.Pressed += () =>
            {
                HideDialog();
                buttonConfig.OnPressed?.Invoke();
            };
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
        if (_overlay != null && _dialogPanel != null && _titleLabel != null && _bodyLabel != null && _buttonRow != null)
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

        _buttonRow = new HBoxContainer
        {
            Name = "ButtonRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _buttonRow.AddThemeConstantOverride("separation", 12);
        content.AddChild(_buttonRow);
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

        public Action? OnPressed { get; set; }
    }
}
