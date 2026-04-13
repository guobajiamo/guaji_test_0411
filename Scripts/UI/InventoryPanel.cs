using Godot;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 背包面板。
/// 用来显示物品列表、收藏标记、垃圾标记等状态。
/// </summary>
public partial class InventoryPanel : Control
{
    private Label? _summaryLabel;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _itemListContainer;
    private GameManager? _gameManager;
    private string _lastInventorySignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureStructure();
    }

    public void RefreshInventory()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            _summaryLabel!.Text = "背包尚未绑定 GameManager。";
            return;
        }

        var orderedItems = SortingHelper.SortNodesForDisplay(
            _gameManager.ItemRegistry.Items.Values,
            itemId =>
            {
                _gameManager.PlayerProfile.Inventory.ItemStates.TryGetValue(itemId, out PlayerItemState? state);
                return state;
            },
            itemId => (int)(_gameManager.ItemRegistry.GetItem(itemId)?.BaseRarity ?? 0));

        List<string> signatureParts = new();
        int shownCount = 0;
        foreach (ItemDefinition item in orderedItems)
        {
            int quantity = _gameManager.PlayerProfile.Inventory.GetItemAmount(item.Id);
            PlayerItemState state = _gameManager.PlayerProfile.Inventory.GetOrCreateItemState(item.Id);
            if (!state.IsAcquired && quantity <= 0)
            {
                continue;
            }

            shownCount++;
            signatureParts.Add($"{item.Id}:{quantity}:{state.IsAcquired}:{state.IsFavorite}:{state.IsJunkMarked}");
        }

        string nextSignature = string.Join("|", signatureParts);
        _summaryLabel!.Text = $"已显示 {shownCount} 种物品。";
        if (nextSignature == _lastInventorySignature)
        {
            return;
        }

        _lastInventorySignature = nextSignature;
        ClearItemList();

        foreach (ItemDefinition item in orderedItems)
        {
            int quantity = _gameManager.PlayerProfile.Inventory.GetItemAmount(item.Id);
            PlayerItemState state = _gameManager.PlayerProfile.Inventory.GetOrCreateItemState(item.Id);
            if (!state.IsAcquired && quantity <= 0)
            {
                continue;
            }

            ItemSlotUI slot = new();
            slot.BindItem(item.Id, item.GetDisplayName(_gameManager.TranslateText), quantity);
            string detailText = string.IsNullOrWhiteSpace(item.DetailDescriptionKey)
                ? item.GetDisplayDescription(_gameManager.TranslateText)
                : _gameManager.TranslateText(item.DetailDescriptionKey);
            string acquisitionHint = string.IsNullOrWhiteSpace(item.AcquisitionHintKey)
                ? "暂未填写获取途径说明。"
                : _gameManager.TranslateText(item.AcquisitionHintKey);
            slot.TooltipText =
                $"{detailText}\n" +
                $"获取提示：{acquisitionHint}\n" +
                $"稀有度：{item.BaseRarity}\n" +
                $"售价：{item.SellPrice}";
            _itemListContainer!.AddChild(slot);
        }
    }

    private void EnsureStructure()
    {
        if (_summaryLabel != null && _scrollContainer != null && _itemListContainer != null)
        {
            return;
        }

        VBoxContainer root = new()
        {
            Name = "Root",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        _summaryLabel = new Label
        {
            Name = "SummaryLabel",
            Text = "背包内容"
        };
        _summaryLabel.AddThemeColorOverride("font_color", new Color("#f0e0b8"));
        _summaryLabel.AddThemeFontSizeOverride("font_size", 18);
        root.AddChild(_summaryLabel);

        _scrollContainer = new ScrollContainer
        {
            Name = "ScrollContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        root.AddChild(_scrollContainer);

        _itemListContainer = new VBoxContainer
        {
            Name = "ItemListContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _itemListContainer.AddThemeConstantOverride("separation", 6);
        _scrollContainer.AddChild(_itemListContainer);
    }

    private void ClearItemList()
    {
        if (_itemListContainer == null)
        {
            return;
        }

        foreach (Node child in _itemListContainer.GetChildren())
        {
            _itemListContainer.RemoveChild(child);
            child.QueueFree();
        }
    }
}
