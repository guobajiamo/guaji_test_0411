using Godot;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 物品图鉴/字典面板。
/// 专门用于显示“玩家已经获得过的物品”的详细资料。
/// </summary>
public partial class DictionaryPanel : Control
{
    private RichTextLabel? _contentLabel;
    private GameManager? _gameManager;
    private string _lastDictionarySignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureStructure();
    }

    public void RefreshDictionary()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            _contentLabel!.Clear();
            _contentLabel.AppendText("图鉴面板尚未绑定 GameManager。");
            return;
        }

        var acquiredItems = _gameManager.ItemRegistry.Items.Values
            .Where(item =>
            {
                PlayerItemState state = _gameManager.PlayerProfile.Inventory.GetOrCreateItemState(item.Id);
                return state.IsAcquired;
            })
            .OrderBy(item => item.DefinitionOrder)
            .ThenBy(item => item.Id)
            .ToList();

        string nextSignature = string.Join("|", acquiredItems.Select(item => item.Id));
        if (nextSignature == _lastDictionarySignature)
        {
            return;
        }

        _lastDictionarySignature = nextSignature;

        if (acquiredItems.Count == 0)
        {
            _contentLabel!.Clear();
            _contentLabel.AppendText("当前还没有已解锁的图鉴内容。");
            return;
        }

        _contentLabel!.Clear();
        foreach (ItemDefinition item in acquiredItems)
        {
            string name = item.GetDisplayName(_gameManager.TranslateText);
            string description = item.GetDisplayDescription(_gameManager.TranslateText);
            string detail = string.IsNullOrWhiteSpace(item.DetailDescriptionKey)
                ? description
                : _gameManager.TranslateText(item.DetailDescriptionKey);

            _contentLabel.AppendText($"{name}\n{detail}\n\n");
        }
    }

    private void EnsureStructure()
    {
        if (_contentLabel != null)
        {
            return;
        }

        _contentLabel = new RichTextLabel
        {
            Name = "ContentLabel",
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _contentLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_contentLabel);
    }
}
