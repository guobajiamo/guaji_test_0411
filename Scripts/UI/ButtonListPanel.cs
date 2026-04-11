using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test00_0410.UI;

/// <summary>
/// 通用按钮列表面板。
/// 点击事件、挂机事件、一次性事件都可以复用这类容器。
/// </summary>
public partial class ButtonListPanel : VBoxContainer
{
    private Label? _titleLabel;
    private VBoxContainer? _listContainer;
    private MainUiLayoutSettings _layoutSettings = new();

    public string GroupId { get; set; } = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(string title, MainUiLayoutSettings layoutSettings)
    {
        _layoutSettings = layoutSettings;
        EnsureStructure();

        _titleLabel!.Text = title;
        _titleLabel.AddThemeFontSizeOverride("font_size", _layoutSettings.SectionHeaderFontSize);
        AddThemeConstantOverride("separation", _layoutSettings.PanelSpacing);
    }

    public void RebuildButtons(IEnumerable<EventButtonViewData> buttons, Action<string> onPressed)
    {
        EnsureStructure();
        List<EventButtonViewData> buttonList = buttons.ToList();

        if (CanReuseExistingButtons(buttonList))
        {
            for (int index = 0; index < buttonList.Count; index++)
            {
                EventButtonItem button = (EventButtonItem)_listContainer!.GetChild(index);
                button.UpdateView(buttonList[index], _layoutSettings);
            }
            return;
        }

        if (buttonList.Count == 0 && IsShowingEmptyState())
        {
            return;
        }

        ClearListContainer();

        if (buttonList.Count == 0)
        {
            Label emptyLabel = new()
            {
                Text = "当前没有可用按钮。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", _layoutSettings.BodyFontSize);
            _listContainer!.AddChild(emptyLabel);
            return;
        }

        foreach (EventButtonViewData buttonData in buttonList)
        {
            EventButtonItem button = new();
            button.BindEvent(buttonData, onPressed, _layoutSettings);
            _listContainer!.AddChild(button);
        }
    }

    private void EnsureStructure()
    {
        if (_titleLabel != null && _listContainer != null)
        {
            return;
        }

        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Text = "按钮组"
        };
        AddChild(_titleLabel);

        _listContainer = new VBoxContainer
        {
            Name = "ListContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _listContainer.AddThemeConstantOverride("separation", 6);
        AddChild(_listContainer);
    }

    private void ClearListContainer()
    {
        if (_listContainer == null)
        {
            return;
        }

        foreach (Node child in _listContainer.GetChildren())
        {
            _listContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    private bool CanReuseExistingButtons(IReadOnlyList<EventButtonViewData> buttonList)
    {
        if (_listContainer == null || buttonList.Count == 0 || _listContainer.GetChildCount() != buttonList.Count)
        {
            return false;
        }

        for (int index = 0; index < buttonList.Count; index++)
        {
            if (_listContainer.GetChild(index) is not EventButtonItem button || button.EventId != buttonList[index].EventId)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsShowingEmptyState()
    {
        return _listContainer != null
            && _listContainer.GetChildCount() == 1
            && _listContainer.GetChild(0) is Label;
    }
}
