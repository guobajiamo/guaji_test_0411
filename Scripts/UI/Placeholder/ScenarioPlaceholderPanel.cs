using Godot;
using System.Collections.Generic;

namespace Test00_0410.UI.Placeholder;

/// <summary>
/// 通用占位页。
/// 给战斗、教学、成就这类暂未实装的页签统一提供一个可配置的说明面板。
/// </summary>
public partial class ScenarioPlaceholderPanel : Control
{
    private Label? _titleLabel;
    private VBoxContainer? _contentRoot;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(string title, IReadOnlyList<string> lines, Color accentColor)
    {
        EnsureStructure();

        if (_titleLabel != null)
        {
            _titleLabel.Text = string.IsNullOrWhiteSpace(title) ? "预留页签" : title;
            _titleLabel.AddThemeColorOverride("font_color", accentColor);
        }

        if (_contentRoot == null)
        {
            return;
        }

        foreach (Node child in _contentRoot.GetChildren())
        {
            _contentRoot.RemoveChild(child);
            child.QueueFree();
        }

        foreach (string line in lines)
        {
            Label label = new()
            {
                Text = line,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", new Color("#e8edf7"));
            _contentRoot.AddChild(label);
        }
    }

    private void EnsureStructure()
    {
        if (_titleLabel != null && _contentRoot != null)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);

        MarginContainer margin = new()
        {
            Name = "Margin"
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Text = "预留页签"
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 24);
        _titleLabel.AddThemeColorOverride("font_color", new Color("#ffe2a6"));
        root.AddChild(_titleLabel);

        ScrollContainer scroll = new()
        {
            Name = "Scroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        root.AddChild(scroll);

        _contentRoot = new VBoxContainer
        {
            Name = "ContentRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _contentRoot.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_contentRoot);
    }
}
