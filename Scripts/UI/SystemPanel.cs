using Godot;
using System;

namespace Test00_0410.UI;

/// <summary>
/// 游戏内系统页。
/// 当前用于统一承载正式剧本和测试剧本的存读档入口。
/// </summary>
public partial class SystemPanel : Control
{
    private string _titleText = "系统功能";
    private string _tipText = "这里放置系统相关操作入口。";
    private string _saveButtonText = "保存";
    private string _loadButtonText = "读取";
    private string _returnButtonText = "返回主菜单";
    private Action? _onSaveRequested;
    private Action? _onLoadRequested;
    private Action? _onReturnToMenuRequested;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(
        string titleText,
        string tipText,
        string saveButtonText,
        string loadButtonText,
        string returnButtonText,
        Action onSaveRequested,
        Action onLoadRequested,
        Action onReturnToMenuRequested)
    {
        _titleText = titleText;
        _tipText = tipText;
        _saveButtonText = saveButtonText;
        _loadButtonText = loadButtonText;
        _returnButtonText = returnButtonText;
        _onSaveRequested = onSaveRequested;
        _onLoadRequested = onLoadRequested;
        _onReturnToMenuRequested = onReturnToMenuRequested;
        RebuildStructure();
    }

    private void EnsureStructure()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        ScrollContainer scroll = new()
        {
            Name = "ScrollContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        MarginContainer margin = new()
        {
            Name = "Margin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        scroll.AddChild(margin);

        VBoxContainer root = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        Label title = new()
        {
            Text = _titleText,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color("#d7fff3"));
        root.AddChild(title);

        Label tip = new()
        {
            Text = _tipText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        tip.AddThemeFontSizeOverride("font_size", 16);
        tip.AddThemeColorOverride("font_color", new Color("#cef6e7"));
        root.AddChild(tip);

        Button saveButton = CreateActionButton(_saveButtonText, "system_action");
        saveButton.Pressed += () => _onSaveRequested?.Invoke();
        root.AddChild(saveButton);

        Button loadButton = CreateActionButton(_loadButtonText, "system_action");
        loadButton.Pressed += () => _onLoadRequested?.Invoke();
        root.AddChild(loadButton);

        Button returnButton = CreateActionButton(_returnButtonText, "system_return_menu");
        returnButton.TooltipText = "返回主菜单前建议先手动保存存档。";
        returnButton.Pressed += () => _onReturnToMenuRequested?.Invoke();
        root.AddChild(returnButton);
    }

    private void RebuildStructure()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        EnsureStructure();
    }

    private static Button CreateActionButton(string text, string styleKey)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 48)
        };
        UiImageThemeManager.ApplyButtonStyle(button, styleKey);
        return button;
    }
}
