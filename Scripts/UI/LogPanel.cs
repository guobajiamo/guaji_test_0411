using Godot;
using System;
using System.Collections.Generic;

namespace Test00_0410.UI;

/// <summary>
/// 日志面板。
/// 开发期可显示调试日志，后期也可显示给玩家看的行动记录。
/// </summary>
public partial class LogPanel : Control
{
    private TextEdit? _textEdit;
    private string _lastMessageSignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void SetMessages(IEnumerable<string> messages)
    {
        EnsureStructure();
        string nextText = string.Join("\n", messages);
        if (nextText == _lastMessageSignature)
        {
            return;
        }

        _lastMessageSignature = nextText;
        _textEdit!.Text = nextText;
        _textEdit.SetCaretLine(Math.Max(0, _textEdit.GetLineCount() - 1));
    }

    public void AppendLog(string message)
    {
        EnsureStructure();
        if (!string.IsNullOrWhiteSpace(_textEdit!.Text))
        {
            _textEdit.Text += "\n";
        }

        _textEdit.Text += message;
        _lastMessageSignature = _textEdit.Text;
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
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _textEdit.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_textEdit);
    }
}
