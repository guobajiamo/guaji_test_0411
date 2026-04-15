using Godot;
using System;
using System.Collections.Generic;

namespace Test00_0410.UI;

/// <summary>
/// 10 槽位存档列表弹窗。
/// 主菜单读档、游戏内保存、游戏内读档都会复用它。
/// </summary>
public partial class SaveSlotDialog : Control
{
    private ColorRect? _overlay;
    private PanelContainer? _dialogPanel;
    private Label? _titleLabel;
    private VBoxContainer? _slotList;
    private Button? _closeButton;
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
        _titleLabel!.AddThemeFontSizeOverride("font_size", layoutSettings.HeaderFontSize);
        ApplyThemeStyles();
    }

    public void ShowDialog(string title, IReadOnlyList<SaveSlotViewData> slots, Action<int> onSlotPressed)
    {
        EnsureStructure();
        _titleLabel!.Text = title;
        ClearSlotList();

        foreach (SaveSlotViewData slot in slots)
        {
            Button slotButton = new()
            {
                Text = $"{slot.Title}\n{slot.Summary}",
                TooltipText = slot.TooltipText,
                Disabled = !slot.IsEnabled,
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 64),
                ActionMode = BaseButton.ActionModeEnum.Press,
                FocusMode = FocusModeEnum.None
            };
            if (_useStitchStyle)
            {
                UiImageThemeManager.ApplyButtonStyle(slotButton, "event_click");
                slotButton.AddThemeStyleboxOverride("normal", StitchElementStyleLibrary.CreateLightSubPanelFrame());
                slotButton.AddThemeStyleboxOverride("hover", StitchElementStyleLibrary.CreateLightSubPanelFrame());
                slotButton.AddThemeStyleboxOverride("pressed", StitchElementStyleLibrary.CreateLightSubPanelFrame());
                slotButton.AddThemeColorOverride("font_color", slot.IsEnabled ? new Color("#30332e") : new Color("#797c75"));
                slotButton.AddThemeColorOverride("font_hover_color", new Color("#224545"));
                slotButton.AddThemeColorOverride("font_pressed_color", new Color("#224545"));
            }
            slotButton.Pressed += () => onSlotPressed(slot.SlotIndex);
            _slotList!.AddChild(slotButton);
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
        if (_overlay != null && _dialogPanel != null && _titleLabel != null && _slotList != null && _closeButton != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 150;

        _overlay = new ColorRect
        {
            Name = "Overlay",
            Color = new Color(0, 0, 0, 0.55f),
            MouseFilter = MouseFilterEnum.Stop
        };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        MarginContainer margin = new();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 48);
        margin.AddThemeConstantOverride("margin_top", 48);
        margin.AddThemeConstantOverride("margin_right", 48);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(margin);

        _dialogPanel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        margin.AddChild(_dialogPanel);

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        _dialogPanel.AddChild(content);

        HBoxContainer header = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(header);

        _titleLabel = new Label
        {
            Name = "TitleLabel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        header.AddChild(_titleLabel);

        _closeButton = new Button
        {
            Name = "CloseButton",
            Text = "关闭",
            CustomMinimumSize = new Vector2(90, 38),
            ActionMode = BaseButton.ActionModeEnum.Press,
            FocusMode = FocusModeEnum.None
        };
        _closeButton.Pressed += HideDialog;
        header.AddChild(_closeButton);

        ScrollContainer scroll = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(scroll);

        _slotList = new VBoxContainer
        {
            Name = "SlotList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _slotList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_slotList);

        ApplyThemeStyles();
    }

    private void ApplyThemeStyles()
    {
        if (_overlay == null || _dialogPanel == null || _titleLabel == null || _closeButton == null)
        {
            return;
        }

        if (_useStitchStyle)
        {
            _overlay.Color = new Color(0, 0, 0, 0.46f);
            _dialogPanel.AddThemeStyleboxOverride("panel", StitchElementStyleLibrary.CreateLightDialogFrame());
            _titleLabel.AddThemeColorOverride("font_color", new Color("#224545"));
            UiImageThemeManager.ApplyButtonStyle(_closeButton, "system_return_menu");
        }
    }

    private void ClearSlotList()
    {
        if (_slotList == null)
        {
            return;
        }

        foreach (Node child in _slotList.GetChildren())
        {
            _slotList.RemoveChild(child);
            child.QueueFree();
        }
    }
}

/// <summary>
/// 存档槽位显示数据。
/// </summary>
public sealed class SaveSlotViewData
{
    public int SlotIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string TooltipText { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
}
