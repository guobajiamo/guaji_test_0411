using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test00_0410.UI;

/// <summary>
/// 运行日志面板。
/// 折叠状态只显示最近几行，展开状态显示全部内容。
/// </summary>
public partial class LogPanel : Control
{
    private const int CollapsedLineCount = 5;

    private TextEdit? _textEdit;
    private bool _isExpanded;
    private string _lastMessageSignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void SetDisplayMode(bool isExpanded)
    {
        _isExpanded = isExpanded;
        _lastMessageSignature = string.Empty;
    }

    public void SetMessages(IEnumerable<string> messages)
    {
        EnsureStructure();

        List<string> messageList = messages.ToList();
        if (!_isExpanded && messageList.Count > CollapsedLineCount)
        {
            messageList = messageList.Skip(messageList.Count - CollapsedLineCount).ToList();
        }

        string nextText = string.Join("\n", messageList);
        if (nextText == _lastMessageSignature)
        {
            return;
        }

        _lastMessageSignature = nextText;
        _textEdit!.Text = nextText;
        _textEdit.SetCaretLine(Math.Max(0, _textEdit.GetLineCount() - 1));
    }

    private void EnsureStructure()
    {
        if (_textEdit != null)
        {
            return;
        }

        _textEdit = new TextEdit
        {
            Name = "LogTextEdit",
            Editable = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _textEdit.AddThemeColorOverride("background_color", new Color("#10161a"));
        _textEdit.AddThemeColorOverride("font_readonly_color", new Color("#dae2e8"));
        _textEdit.AddThemeColorOverride("font_color", new Color("#dae2e8"));
        _textEdit.AddThemeColorOverride("caret_color", new Color("#d6a34a"));
        _textEdit.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_textEdit);
    }
}
