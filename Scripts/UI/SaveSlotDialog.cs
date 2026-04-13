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
    private Label? _titleLabel;
    private VBoxContainer? _slotList;
    private Button? _closeButton;

    public override void _Ready()
    {
        EnsureStructure();
        HideDialog();
    }

    public void Configure(MainUiLayoutSettings layoutSettings)
    {
        EnsureStructure();
        _titleLabel!.AddThemeFontSizeOverride("font_size", layoutSettings.HeaderFontSize);
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
                CustomMinimumSize = new Vector2(0, 64)
            };
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
        if (_overlay != null && _titleLabel != null && _slotList != null && _closeButton != null)
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

        PanelContainer panel = new()
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        margin.AddChild(panel);

        VBoxContainer content = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

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
            CustomMinimumSize = new Vector2(90, 38)
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
