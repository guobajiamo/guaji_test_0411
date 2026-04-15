using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 背包页。
/// 结构分为：顶部背包导航栏、左侧背包主界面、右侧物品详细信息。
/// </summary>
public partial class InventoryPanel : Control
{
    private const int StitchGridSlotCount = 240;
    private const int StitchLargeColumns = 8;
    private const int StitchSmallColumns = 16;
    private const int StitchLargeSlotSize = 92;
    private const int StitchSmallSlotSize = 46;

    private static readonly InventoryFilterTab[] FilterOrder =
    {
        InventoryFilterTab.All,
        InventoryFilterTab.Currency,
        InventoryFilterTab.Material,
        InventoryFilterTab.Consumable,
        InventoryFilterTab.Battle,
        InventoryFilterTab.Tool,
        InventoryFilterTab.Special,
        InventoryFilterTab.Valuable,
        InventoryFilterTab.Other,
        InventoryFilterTab.Junk
    };

    private readonly Dictionary<InventoryFilterTab, Button> _filterButtons = new();
    private readonly Dictionary<string, Button> _slotButtonsByKey = new(StringComparer.Ordinal);

    private GameManager? _gameManager;
    private LineEdit? _searchInput;
    private Button? _autoSortButton;
    private Button? _arrivalSortButton;
    private Button? _smallIconButton;
    private Button? _largeIconButton;
    private HFlowContainer? _filterButtonBar;
    private HFlowContainer? _legacySlotFlow;
    private GridContainer? _stitchSlotGrid;
    private ScrollContainer? _slotScroll;
    private ScrollContainer? _detailScroll;
    private RichTextLabel? _detailTitleLabel;
    private RichTextLabel? _detailBodyLabel;
    private Button? _favoriteToggleButton;
    private Button? _consumeButton;
    private Label? _emptyHintLabel;
    private string _selectedItemId = string.Empty;
    private readonly Dictionary<string, Texture2D?> _iconTextureCache = new(StringComparer.Ordinal);
    private int _pendingStitchGridReflowFrames;

    public event Action<string, IReadOnlyList<ItemInfoDisplayEntry>>? ItemDetailFocused;

    public override void _Ready()
    {
        EnsureStructure();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_pendingStitchGridReflowFrames <= 0 || !IsUsingStitchUiTheme())
        {
            return;
        }

        if (GetStitchGridAvailableWidth() > 1f)
        {
            _pendingStitchGridReflowFrames = 0;
            ReflowStitchGridCells();
            return;
        }

        _pendingStitchGridReflowFrames--;
        if (_pendingStitchGridReflowFrames <= 0)
        {
            ReflowStitchGridCells();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && Visible)
        {
            RefreshInventory();
            ScheduleStitchGridReflow(20);
        }
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
            ShowNoDataState("背包尚未绑定 GameManager。");
            return;
        }

        PlayerProfile profile = _gameManager.PlayerProfile;
        PlayerInventory inventory = profile.Inventory;
        inventory.EnsureRuntimeSlotOrder();
        RefreshSortButtons(profile.UiState.InventorySortMode);
        RefreshFilterButtons(profile.UiState.InventoryFilterTab);
        RefreshIconModeButtons();

        List<InventorySlotView> slots = BuildVisibleSlots(profile);
        EnsureSelectedItem(slots);

        RebuildSlotButtons(slots);
        RefreshDetailPanel();
        ScheduleStitchGridReflow();
        CallDeferred(nameof(UpdateScrollableAreaVisibility));
    }

    /// <summary>
    /// 供后续“右侧详细属性组件化”继续复用。
    /// </summary>
    public IReadOnlyList<ItemInfoDisplayEntry> GetItemDetailEntries(string itemId)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(itemId))
        {
            return Array.Empty<ItemInfoDisplayEntry>();
        }

        PlayerInventory inventory = _gameManager.PlayerProfile.Inventory;
        ItemDefinition? item = _gameManager.ItemRegistry.GetItem(itemId);
        if (item == null)
        {
            return BuildUnknownDetailEntries(itemId, inventory.GetItemAmount(itemId));
        }

        PlayerItemState state = inventory.GetOrCreateItemState(itemId);
        List<ItemInfoDisplayEntry> entries = ItemInfoFormatter.BuildDetailEntries(
            item,
            inventory.Stacks.GetValueOrDefault(itemId),
            state,
            _gameManager.TranslateText,
            ResolveCategoryDisplayName);

        if (!item.IsStackable)
        {
            int quantity = inventory.GetItemAmount(itemId);
            entries.Add(new ItemInfoDisplayEntry
            {
                FieldId = "slot_count",
                Label = "占用格数",
                Value = quantity.ToString(CultureInfo.InvariantCulture)
            });
        }

        return entries;
    }

    private void EnsureStructure()
    {
        if (_searchInput != null
            && _autoSortButton != null
            && _arrivalSortButton != null
            && _smallIconButton != null
            && _largeIconButton != null
            && _filterButtonBar != null
            && (_legacySlotFlow != null || _stitchSlotGrid != null)
            && _slotScroll != null
            && _detailScroll != null
            && _detailTitleLabel != null
            && _detailBodyLabel != null
            && _favoriteToggleButton != null
            && _consumeButton != null
            && _emptyHintLabel != null)
        {
            return;
        }

        if (GetChildCount() > 0)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);

        VBoxContainer root = new()
        {
            Name = "InventoryRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        root.AddChild(BuildNavigationBar());
        root.AddChild(BuildMainArea());
    }

    private Control BuildNavigationBar()
    {
        PanelContainer navPanel = new()
        {
            Name = "InventoryNavBar",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        navPanel.AddThemeStyleboxOverride(
            "panel",
            IsUsingStitchUiTheme()
                ? CreatePanelStyle("#f5f4ef", "#d9dbd2", 2)
                : CreatePanelStyle("#2a1205", "#ff224d", 2));

        VBoxContainer navRoot = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        navRoot.AddThemeConstantOverride("separation", 6);
        navPanel.AddChild(navRoot);

        HBoxContainer searchRow = new()
        {
            Name = "SearchRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        searchRow.AddThemeConstantOverride("separation", 8);
        navRoot.AddChild(searchRow);

        Label searchLabel = new() { Text = "物品搜索：" };
        searchLabel.AddThemeColorOverride("font_color", GetSearchLabelColor());
        searchLabel.AddThemeFontSizeOverride("font_size", 18);
        searchRow.AddChild(searchLabel);

        _searchInput = new LineEdit
        {
            Name = "SearchInput",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "输入关键字筛选物品"
        };
        _searchInput.TextChanged += _ => RefreshInventory();
        searchRow.AddChild(_searchInput);

        _autoSortButton = new Button
        {
            Name = "AutoSortButton",
            Text = "整理背包(自动排序)",
            CustomMinimumSize = new Vector2(168, 34)
        };
        UiImageThemeManager.ApplyButtonStyle(_autoSortButton, "system_action");
        _autoSortButton.Pressed += ApplyAutoSort;
        searchRow.AddChild(_autoSortButton);

        _arrivalSortButton = new Button
        {
            Name = "ArrivalSortButton",
            Text = "入袋顺序",
            CustomMinimumSize = new Vector2(112, 34)
        };
        UiImageThemeManager.ApplyButtonStyle(_arrivalSortButton, "system_action");
        _arrivalSortButton.Pressed += ApplyArrivalOrder;
        searchRow.AddChild(_arrivalSortButton);

        _smallIconButton = new Button
        {
            Name = "SmallIconModeButton",
            Text = "小图标",
            CustomMinimumSize = new Vector2(94, 34)
        };
        UiImageThemeManager.ApplyButtonStyle(_smallIconButton, "special_action");
        _smallIconButton.Pressed += () => SetInventoryIconMode(false);
        searchRow.AddChild(_smallIconButton);

        _largeIconButton = new Button
        {
            Name = "LargeIconModeButton",
            Text = "大图标",
            CustomMinimumSize = new Vector2(94, 34)
        };
        UiImageThemeManager.ApplyButtonStyle(_largeIconButton, "special_action");
        _largeIconButton.Pressed += () => SetInventoryIconMode(true);
        searchRow.AddChild(_largeIconButton);

        HBoxContainer filterRow = new()
        {
            Name = "FilterRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        filterRow.AddThemeConstantOverride("separation", 8);
        navRoot.AddChild(filterRow);

        Label filterLabel = new() { Text = "标签：" };
        filterLabel.AddThemeColorOverride("font_color", GetFilterLabelColor());
        filterLabel.AddThemeFontSizeOverride("font_size", 24);
        filterRow.AddChild(filterLabel);

        _filterButtonBar = new HFlowContainer
        {
            Name = "FilterButtonBar",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _filterButtonBar.AddThemeConstantOverride("h_separation", 4);
        _filterButtonBar.AddThemeConstantOverride("v_separation", 4);
        filterRow.AddChild(_filterButtonBar);
        BuildFilterButtons();

        return navPanel;
    }

    private Control BuildMainArea()
    {
        HSplitContainer split = new()
        {
            Name = "InventoryMainSplit",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = new[] { IsUsingStitchUiTheme() ? 640 : 670 }
        };

        PanelContainer leftPanel = new()
        {
            Name = "InventoryMainPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        leftPanel.AddThemeStyleboxOverride(
            "panel",
            IsUsingStitchUiTheme()
                ? CreatePanelStyle("#e2e3db", "#d9dbd2", 2)
                : CreatePanelStyle("#07150b", "#00ff5a", 2));
        split.AddChild(leftPanel);

        VBoxContainer leftRoot = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        leftPanel.AddChild(leftRoot);

        _slotScroll = new ScrollContainer
        {
            Name = "SlotScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        leftRoot.AddChild(_slotScroll);

        if (IsUsingStitchUiTheme())
        {
            _stitchSlotGrid = new GridContainer
            {
                Name = "SlotGrid",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                Columns = GetStitchGridColumns()
            };
            _stitchSlotGrid.AddThemeConstantOverride("h_separation", 8);
            _stitchSlotGrid.AddThemeConstantOverride("v_separation", 8);
            _slotScroll.AddChild(_stitchSlotGrid);
        }
        else
        {
            _legacySlotFlow = new HFlowContainer
            {
                Name = "SlotFlow",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _legacySlotFlow.AddThemeConstantOverride("h_separation", 8);
            _legacySlotFlow.AddThemeConstantOverride("v_separation", 10);
            _slotScroll.AddChild(_legacySlotFlow);
        }

        _emptyHintLabel = new Label
        {
            Name = "EmptyHintLabel",
            Text = "当前筛选条件下没有可显示道具。",
            Visible = false
        };
        _emptyHintLabel.AddThemeColorOverride("font_color", GetEmptyHintColor());
        _emptyHintLabel.AddThemeFontSizeOverride("font_size", 16);
        leftRoot.AddChild(_emptyHintLabel);

        PanelContainer rightPanel = new()
        {
            Name = "ItemDetailPanel",
            CustomMinimumSize = new Vector2(IsUsingStitchUiTheme() ? 360 : 275, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rightPanel.AddThemeStyleboxOverride(
            "panel",
            IsUsingStitchUiTheme()
                ? CreatePanelStyle("#f9f8f3", "#d9dbd2", 2)
                : CreatePanelStyle("#06150b", "#00ff5a", 2));
        split.AddChild(rightPanel);

        VBoxContainer detailRoot = new()
        {
            Name = "ItemDetailRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detailRoot.AddThemeConstantOverride("separation", 8);
        rightPanel.AddChild(detailRoot);

        HBoxContainer detailHeader = new()
        {
            Name = "ItemDetailHeader",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        detailHeader.AddThemeConstantOverride("separation", 6);
        detailRoot.AddChild(detailHeader);

        _favoriteToggleButton = new Button
        {
            Name = "FavoriteToggleButton",
            Text = "☆",
            CustomMinimumSize = new Vector2(44, 40)
        };
        _favoriteToggleButton.Pressed += ToggleFavoriteForSelectedItem;
        detailHeader.AddChild(_favoriteToggleButton);

        _detailTitleLabel = new RichTextLabel
        {
            Name = "ItemDetailTitleLabel",
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _detailTitleLabel.AddThemeFontSizeOverride("normal_font_size", IsUsingStitchUiTheme() ? 24 : 19);
        _detailTitleLabel.AddThemeColorOverride("default_color", IsUsingStitchUiTheme() ? new Color("#2f3b39") : new Color("#f8fafc"));
        detailHeader.AddChild(_detailTitleLabel);

        _consumeButton = new Button
        {
            Name = "ConsumeButton",
            Text = "食用",
            Visible = false,
            Disabled = true,
            CustomMinimumSize = new Vector2(86, 32)
        };
        _consumeButton.Pressed += ConsumeSelectedItem;
        detailRoot.AddChild(_consumeButton);

        _detailScroll = new ScrollContainer
        {
            Name = "ItemDetailScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        detailRoot.AddChild(_detailScroll);

        _detailBodyLabel = new RichTextLabel
        {
            Name = "ItemDetailBodyLabel",
            BbcodeEnabled = false,
            FitContent = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _detailBodyLabel.AddThemeFontSizeOverride("normal_font_size", IsUsingStitchUiTheme() ? 20 : 17);
        _detailBodyLabel.AddThemeColorOverride("default_color", IsUsingStitchUiTheme() ? new Color("#4f5f5b") : new Color("#e6ecf6"));
        _detailScroll.AddChild(_detailBodyLabel);

        return split;
    }

    private void BuildFilterButtons()
    {
        if (_filterButtonBar == null)
        {
            return;
        }

        foreach (Node child in _filterButtonBar.GetChildren())
        {
            _filterButtonBar.RemoveChild(child);
            child.QueueFree();
        }

        _filterButtons.Clear();
        foreach (InventoryFilterTab filterTab in FilterOrder)
        {
            InventoryFilterTab selected = filterTab;
            Button button = new()
            {
                Text = GetFilterLabel(filterTab),
                CustomMinimumSize = new Vector2(72, 32)
            };
            UiImageThemeManager.ApplyButtonStyle(button, "special_action");
            button.Pressed += () => ApplyFilter(selected);
            _filterButtonBar.AddChild(button);
            _filterButtons[filterTab] = button;
        }
    }

    private void RefreshFilterButtons(InventoryFilterTab activeFilter)
    {
        foreach (KeyValuePair<InventoryFilterTab, Button> pair in _filterButtons)
        {
            InventoryFilterTab filter = pair.Key;
            Button button = pair.Value;
            bool isActive = filter == activeFilter;
            button.Text = isActive ? $"{GetFilterLabel(filter)}*" : GetFilterLabel(filter);
            Color resolvedColor = isActive ? GetFilterButtonActiveColor() : GetFilterButtonInactiveColor();
            button.AddThemeColorOverride("font_color", resolvedColor);
            button.AddThemeColorOverride("font_hover_color", resolvedColor);
            button.AddThemeColorOverride("font_pressed_color", resolvedColor);
        }
    }

    private void RefreshSortButtons(InventorySortMode sortMode)
    {
        if (_arrivalSortButton == null || _autoSortButton == null)
        {
            return;
        }

        _arrivalSortButton.Text = sortMode == InventorySortMode.ArrivalOrder
            ? "入袋顺序*"
            : "入袋顺序";
        _autoSortButton.Text = sortMode == InventorySortMode.AutoByTypeThenDefinition
            ? "整理背包(自动排序)*"
            : "整理背包(自动排序)";
    }

    private void RefreshIconModeButtons()
    {
        if (_smallIconButton == null || _largeIconButton == null)
        {
            return;
        }

        bool useLarge = UseLargeInventoryIcons();
        _largeIconButton.Text = useLarge ? "大图标*" : "大图标";
        _smallIconButton.Text = useLarge ? "小图标" : "小图标*";

        Color activeColor = IsUsingStitchUiTheme() ? new Color("#224545") : new Color("#ffe36e");
        Color inactiveColor = IsUsingStitchUiTheme() ? new Color("#5d605a") : new Color("#ffb4b4");

        _largeIconButton.AddThemeColorOverride("font_color", useLarge ? activeColor : inactiveColor);
        _largeIconButton.AddThemeColorOverride("font_hover_color", useLarge ? activeColor : inactiveColor);
        _largeIconButton.AddThemeColorOverride("font_pressed_color", useLarge ? activeColor : inactiveColor);

        _smallIconButton.AddThemeColorOverride("font_color", useLarge ? inactiveColor : activeColor);
        _smallIconButton.AddThemeColorOverride("font_hover_color", useLarge ? inactiveColor : activeColor);
        _smallIconButton.AddThemeColorOverride("font_pressed_color", useLarge ? inactiveColor : activeColor);
    }

    private void SetInventoryIconMode(bool useLarge)
    {
        if (_gameManager == null)
        {
            return;
        }

        if (_gameManager.PlayerProfile.UiState.InventoryUseLargeIcons == useLarge)
        {
            RefreshIconModeButtons();
            return;
        }

        _gameManager.PlayerProfile.UiState.InventoryUseLargeIcons = useLarge;
        RefreshInventory();
    }

    private bool UseLargeInventoryIcons()
    {
        return _gameManager?.PlayerProfile.UiState.InventoryUseLargeIcons ?? true;
    }

    private List<InventorySlotView> BuildVisibleSlots(PlayerProfile profile)
    {
        if (_gameManager == null)
        {
            return new List<InventorySlotView>();
        }

        PlayerInventory inventory = profile.Inventory;
        InventoryFilterTab filterTab = profile.UiState.InventoryFilterTab;
        string keyword = _searchInput?.Text.Trim() ?? string.Empty;
        List<InventorySlotView> slots = new();

        foreach (string itemId in inventory.GetActiveItemIdsByDisplayOrder())
        {
            int quantity = inventory.GetItemAmount(itemId);
            if (quantity <= 0)
            {
                continue;
            }

            ItemDefinition? item = _gameManager.ItemRegistry.GetItem(itemId);
            if (!IsVisibleByFilter(itemId, item, filterTab, keyword))
            {
                continue;
            }

            if (item == null || item.IsStackable)
            {
                slots.Add(new InventorySlotView(itemId, 0, FormatStackableQuantity(quantity)));
                continue;
            }

            for (int index = 0; index < quantity; index++)
            {
                slots.Add(new InventorySlotView(itemId, index + 1, string.Empty));
            }
        }

        return slots;
    }

    private bool IsVisibleByFilter(string itemId, ItemDefinition? item, InventoryFilterTab filterTab, string keyword)
    {
        if (_gameManager == null)
        {
            return false;
        }

        string displayName = item?.GetDisplayName(_gameManager.TranslateText) ?? _gameManager.GetItemDisplayName(itemId);
        if (!string.IsNullOrWhiteSpace(keyword)
            && displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
            && itemId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (filterTab == InventoryFilterTab.All)
        {
            return true;
        }

        PlayerItemState state = _gameManager.PlayerProfile.Inventory.GetOrCreateItemState(itemId);
        ItemTag tags = item?.Tags ?? ItemTag.None;

        return filterTab switch
        {
            InventoryFilterTab.Currency => HasAnyTag(tags, ItemTag.Currency),
            InventoryFilterTab.Material => HasAnyTag(tags, ItemTag.Material),
            InventoryFilterTab.Consumable => HasAnyTag(tags, ItemTag.Consumable),
            InventoryFilterTab.Battle => HasAnyTag(tags, ItemTag.Battle | ItemTag.Weapon | ItemTag.Armor | ItemTag.Helmet),
            InventoryFilterTab.Tool => HasAnyTag(tags, ItemTag.Tool),
            InventoryFilterTab.Special => HasAnyTag(tags, ItemTag.Special | ItemTag.QuestItem),
            InventoryFilterTab.Valuable => HasAnyTag(tags, ItemTag.Valuable),
            InventoryFilterTab.Junk => state.IsJunkMarked || HasAnyTag(tags, ItemTag.Junk),
            InventoryFilterTab.Other => IsOtherCategory(tags, state),
            _ => true
        };
    }

    private static bool IsOtherCategory(ItemTag tags, PlayerItemState state)
    {
        if (state.IsJunkMarked || HasAnyTag(tags, ItemTag.Junk))
        {
            return false;
        }

        ItemTag majorTags = ItemTag.Currency
            | ItemTag.Material
            | ItemTag.Consumable
            | ItemTag.Battle
            | ItemTag.Tool
            | ItemTag.Special
            | ItemTag.Valuable;
        return (tags & majorTags) == ItemTag.None;
    }

    private static bool HasAnyTag(ItemTag value, ItemTag target)
    {
        return (value & target) != ItemTag.None;
    }

    private void EnsureSelectedItem(IReadOnlyList<InventorySlotView> slots)
    {
        if (slots.Count == 0)
        {
            _selectedItemId = string.Empty;
            return;
        }

        bool exists = slots.Any(slot => string.Equals(slot.ItemId, _selectedItemId, StringComparison.Ordinal));
        if (!exists)
        {
            _selectedItemId = slots[0].ItemId;
        }
    }

    private void RebuildSlotButtons(IReadOnlyList<InventorySlotView> slots)
    {
        if (_gameManager == null || _emptyHintLabel == null)
        {
            return;
        }

        if (IsUsingStitchUiTheme())
        {
            RebuildStitchSlotGrid(slots);
            return;
        }

        RebuildLegacySlotButtons(slots);
    }

    private void RebuildLegacySlotButtons(IReadOnlyList<InventorySlotView> slots)
    {
        if (_legacySlotFlow == null || _gameManager == null || _emptyHintLabel == null)
        {
            return;
        }

        foreach (Node child in _legacySlotFlow.GetChildren())
        {
            _legacySlotFlow.RemoveChild(child);
            child.QueueFree();
        }

        _slotButtonsByKey.Clear();
        _emptyHintLabel.Visible = slots.Count == 0;
        if (slots.Count == 0)
        {
            return;
        }

        bool useLarge = UseLargeInventoryIcons();
        int slotHeight = useLarge ? 54 : 42;
        int fontSize = useLarge ? 20 : 17;
        float widthScale = useLarge ? 1.2f : 1.0f;

        foreach (InventorySlotView slot in slots)
        {
            ItemDefinition? item = _gameManager.ItemRegistry.GetItem(slot.ItemId);
            string itemName = item?.GetDisplayName(_gameManager.TranslateText) ?? _gameManager.GetItemDisplayName(slot.ItemId);
            string caption = string.IsNullOrWhiteSpace(slot.DisplayQuantityText)
                ? itemName
                : $"{itemName}({slot.DisplayQuantityText})";
            string slotKey = slot.GetSlotKey();

            Button button = new()
            {
                Name = $"Slot_{slotKey}",
                Text = caption,
                TooltipText = BuildTooltip(slot.ItemId),
                SizeFlagsHorizontal = 0,
                SizeFlagsVertical = 0,
                CustomMinimumSize = new Vector2(EstimateButtonWidth(itemName, item?.IsStackable ?? true, widthScale), slotHeight),
                ClipText = false
            };
            button.AddThemeFontSizeOverride("font_size", fontSize);
            button.AddThemeColorOverride("font_color", new Color("#ffe8e8"));
            button.AddThemeColorOverride("font_hover_color", new Color("#fff8e6"));
            button.AddThemeColorOverride("font_pressed_color", new Color("#fff8e6"));
            button.AddThemeStyleboxOverride("normal", CreateLegacySlotStyle(false));
            button.AddThemeStyleboxOverride("hover", CreateLegacySlotStyle(true));
            button.AddThemeStyleboxOverride("pressed", CreateLegacySlotStyle(true));
            button.Alignment = HorizontalAlignment.Center;
            button.Pressed += () => SelectItem(slot.ItemId);
            _legacySlotFlow.AddChild(button);
            _slotButtonsByKey[slotKey] = button;
        }

        ApplySelectionVisual();
    }

    private void RebuildStitchSlotGrid(IReadOnlyList<InventorySlotView> slots)
    {
        if (_stitchSlotGrid == null || _gameManager == null || _emptyHintLabel == null)
        {
            return;
        }

        foreach (Node child in _stitchSlotGrid.GetChildren())
        {
            _stitchSlotGrid.RemoveChild(child);
            child.QueueFree();
        }

        _slotButtonsByKey.Clear();
        _stitchSlotGrid.Columns = GetStitchGridColumns();
        _emptyHintLabel.Visible = false;

        List<InventorySlotView> gridItems = slots.Take(StitchGridSlotCount).ToList();
        int slotSize = GetStitchGridSlotSize();

        for (int index = 0; index < StitchGridSlotCount; index++)
        {
            if (index < gridItems.Count)
            {
                InventorySlotView slot = gridItems[index];
                AddStitchItemCell(slot, slotSize);
            }
            else
            {
                AddStitchEmptyCell(slotSize);
            }
        }

        ApplySelectionVisual();
        ScheduleStitchGridReflow();
    }

    private void ReflowStitchGridCells()
    {
        if (!IsUsingStitchUiTheme() || _stitchSlotGrid == null)
        {
            return;
        }

        int slotSize = GetStitchGridSlotSize();
        foreach (Node child in _stitchSlotGrid.GetChildren())
        {
            if (child is not Button button)
            {
                continue;
            }

            button.CustomMinimumSize = new Vector2(slotSize, slotSize);
        }

        CallDeferred(nameof(UpdateScrollableAreaVisibility));
    }

    private void AddStitchEmptyCell(int slotSize)
    {
        if (_stitchSlotGrid == null)
        {
            return;
        }

        Button button = new()
        {
            Disabled = true,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(slotSize, slotSize),
            SizeFlagsHorizontal = SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Fill
        };
        button.AddThemeStyleboxOverride("normal", CreateStitchGridSlotStyle(false, false, true));
        button.AddThemeStyleboxOverride("disabled", CreateStitchGridSlotStyle(false, false, true));
        _stitchSlotGrid.AddChild(button);
    }

    private void AddStitchItemCell(InventorySlotView slot, int slotSize)
    {
        if (_stitchSlotGrid == null || _gameManager == null)
        {
            return;
        }

        ItemDefinition? item = _gameManager.ItemRegistry.GetItem(slot.ItemId);
        string itemName = item?.GetDisplayName(_gameManager.TranslateText) ?? _gameManager.GetItemDisplayName(slot.ItemId);
        string slotKey = slot.GetSlotKey();

        Button button = new()
        {
            Name = $"Slot_{slotKey}",
            TooltipText = BuildTooltip(slot.ItemId),
            CustomMinimumSize = new Vector2(slotSize, slotSize),
            SizeFlagsHorizontal = SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.Fill,
            ClipText = true,
            Alignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Center
        };
        button.AddThemeStyleboxOverride("normal", CreateStitchGridSlotStyle(false, false));
        button.AddThemeStyleboxOverride("hover", CreateStitchGridSlotStyle(true, false));
        button.AddThemeStyleboxOverride("pressed", CreateStitchGridSlotStyle(true, false));

        Texture2D? iconTexture = TryLoadInventoryItemIcon(item);
        if (iconTexture != null)
        {
            button.Icon = iconTexture;
            button.ExpandIcon = true;
            button.IconAlignment = HorizontalAlignment.Center;
            button.Text = string.Empty;
        }
        else
        {
            button.Text = itemName;
            button.AddThemeFontSizeOverride("font_size", GetAdaptiveGridTextFontSize(itemName));
            button.AddThemeColorOverride("font_color", new Color("#30332e"));
            button.AddThemeColorOverride("font_hover_color", new Color("#224545"));
            button.AddThemeColorOverride("font_pressed_color", new Color("#224545"));
        }

        if (!string.IsNullOrWhiteSpace(slot.DisplayQuantityText))
        {
            Label countLabel = new()
            {
                Text = $"x{slot.DisplayQuantityText}",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = MouseFilterEnum.Ignore
            };
            countLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            countLabel.OffsetLeft = 4;
            countLabel.OffsetTop = 4;
            countLabel.OffsetRight = -4;
            countLabel.OffsetBottom = -2;
            countLabel.AddThemeFontSizeOverride("font_size", UseLargeInventoryIcons() ? 11 : 9);
            countLabel.AddThemeColorOverride("font_color", new Color("#5d605a"));
            button.AddChild(countLabel);
        }

        button.Pressed += () => SelectItem(slot.ItemId);
        _stitchSlotGrid.AddChild(button);
        _slotButtonsByKey[slotKey] = button;
    }

    private void SelectItem(string itemId)
    {
        _selectedItemId = itemId;
        ApplySelectionVisual();
        RefreshDetailPanel();
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            ItemDetailFocused?.Invoke(itemId, GetItemDetailEntries(itemId));
        }
    }

    private void ApplySelectionVisual()
    {
        if (_gameManager == null)
        {
            return;
        }

        foreach ((string slotKey, Button button) in _slotButtonsByKey)
        {
            bool selected = slotKey.StartsWith($"{_selectedItemId}#", StringComparison.Ordinal);
            if (IsUsingStitchUiTheme())
            {
                button.Modulate = Colors.White;
                button.AddThemeStyleboxOverride("normal", CreateStitchGridSlotStyle(false, selected));
                button.AddThemeStyleboxOverride("hover", CreateStitchGridSlotStyle(true, selected));
                button.AddThemeStyleboxOverride("pressed", CreateStitchGridSlotStyle(true, selected));
                continue;
            }

            button.Modulate = selected ? new Color("#ffd27a") : Colors.White;
        }
    }

    private void RefreshDetailPanel()
    {
        if (_gameManager == null
            || _detailTitleLabel == null
            || _detailBodyLabel == null
            || _favoriteToggleButton == null
            || _consumeButton == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedItemId))
        {
            _favoriteToggleButton.Disabled = true;
            _favoriteToggleButton.Text = "☆";
            _favoriteToggleButton.AddThemeColorOverride("font_color", new Color("#7f7f7f"));
            _consumeButton.Visible = false;
            _consumeButton.Disabled = true;
            _consumeButton.TooltipText = string.Empty;
            _detailTitleLabel.Text = "[color=#9aa5b1]未选择物品[/color]";
            _detailBodyLabel.Text = "点击左侧任意道具按钮后，这里会显示该道具的详细信息。";
            return;
        }

        PlayerInventory inventory = _gameManager.PlayerProfile.Inventory;
        PlayerItemState state = inventory.GetOrCreateItemState(_selectedItemId);
        ItemDefinition? item = _gameManager.ItemRegistry.GetItem(_selectedItemId);
        string displayName = item?.GetDisplayName(_gameManager.TranslateText) ?? _gameManager.GetItemDisplayName(_selectedItemId);

        _favoriteToggleButton.Disabled = false;
        _favoriteToggleButton.Text = state.IsFavorite ? "★" : "☆";
        _favoriteToggleButton.AddThemeColorOverride("font_color", state.IsFavorite ? new Color("#ffd34d") : new Color("#7f7f7f"));

        bool canConsume = item?.CanConsumeFromInventory == true
            && item.ConsumeBuff != null
            && inventory.GetItemAmount(_selectedItemId) > 0;
        _consumeButton.Visible = canConsume;
        _consumeButton.Disabled = !canConsume;
        _consumeButton.TooltipText = canConsume
            ? (string.IsNullOrWhiteSpace(item!.ConsumeBuff!.Description) ? "点击后消耗 1 个并触发对应 Buff。" : item.ConsumeBuff.Description)
            : string.Empty;

        _detailTitleLabel.Text = state.IsFavorite
            ? $"{displayName} [font_size=16](已收藏)[/font_size]"
            : displayName;

        IReadOnlyList<ItemInfoDisplayEntry> entries = GetItemDetailEntries(_selectedItemId);
        _detailBodyLabel.Text = ItemInfoFormatter.BuildTooltipText(entries);
        CallDeferred(nameof(UpdateScrollableAreaVisibility));
    }

    private void ConsumeSelectedItem()
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(_selectedItemId))
        {
            return;
        }

        ItemDefinition? item = _gameManager.ItemRegistry.GetItem(_selectedItemId);
        if (item?.CanConsumeFromInventory != true || item.ConsumeBuff == null)
        {
            return;
        }

        if (!_gameManager.SettlementService.TryRemoveItem(_selectedItemId, 1))
        {
            _gameManager.AddGameLog($"食用失败：{_gameManager.GetItemDisplayName(_selectedItemId)} 数量不足。");
            return;
        }

        ConsumableBuffDefinition buff = item.ConsumeBuff;
        _gameManager.BuffSystem?.ApplyTimedBuff(
            buff.BuffId,
            buff.DisplayName,
            buff.Description,
            buff.DurationSeconds,
            buff.ExtendDurationOnReapply,
            buff.StatModifiers);
        _gameManager.BuffSystem?.RefreshActiveBuffs();
        _gameManager.AddGameLog($"已食用：{item.GetDisplayName(_gameManager.TranslateText)}");
        RefreshInventory();
    }

    private void ToggleFavoriteForSelectedItem()
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(_selectedItemId))
        {
            return;
        }

        PlayerInventory inventory = _gameManager.PlayerProfile.Inventory;
        List<string> orderedIds = inventory.GetActiveItemIdsByDisplayOrder();
        if (!orderedIds.Contains(_selectedItemId, StringComparer.Ordinal))
        {
            return;
        }

        PlayerItemState state = inventory.GetOrCreateItemState(_selectedItemId);
        bool makeFavorite = !state.IsFavorite;
        state.IsFavorite = makeFavorite;

        orderedIds.Remove(_selectedItemId);
        if (makeFavorite)
        {
            orderedIds.Insert(0, _selectedItemId);
            _gameManager.AddGameLog($"已收藏道具：{_gameManager.GetItemDisplayName(_selectedItemId)}");
        }
        else
        {
            int firstNonFavoriteIndex = orderedIds.FindIndex(itemId => !inventory.GetOrCreateItemState(itemId).IsFavorite);
            if (firstNonFavoriteIndex < 0)
            {
                firstNonFavoriteIndex = orderedIds.Count;
            }

            orderedIds.Insert(firstNonFavoriteIndex, _selectedItemId);
            _gameManager.AddGameLog($"已取消收藏：{_gameManager.GetItemDisplayName(_selectedItemId)}");
        }

        inventory.ApplyDisplayOrder(orderedIds);
        RefreshInventory();
    }

    private void ApplyFilter(InventoryFilterTab filterTab)
    {
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.PlayerProfile.UiState.InventoryFilterTab = filterTab;
        RefreshInventory();
    }

    private void ApplyArrivalOrder()
    {
        if (_gameManager == null)
        {
            return;
        }

        PlayerInventory inventory = _gameManager.PlayerProfile.Inventory;
        List<string> nonFavorite = inventory.GetActiveItemIdsByArrivalOrder()
            .Where(itemId => !inventory.GetOrCreateItemState(itemId).IsFavorite)
            .ToList();
        inventory.ApplyDisplayOrder(BuildOrderWithFavoritesLeading(nonFavorite));
        _gameManager.PlayerProfile.UiState.InventorySortMode = InventorySortMode.ArrivalOrder;
        _gameManager.AddGameLog("背包顺序已切换为入袋顺序（收藏道具保持前置）。");
        RefreshInventory();
    }

    private void ApplyAutoSort()
    {
        if (_gameManager == null)
        {
            return;
        }

        PlayerInventory inventory = _gameManager.PlayerProfile.Inventory;
        List<string> nonFavorite = inventory.GetActiveItemIdsByDisplayOrder()
            .Where(itemId => !inventory.GetOrCreateItemState(itemId).IsFavorite)
            .OrderBy(itemId => GetCategorySortPath(itemId), StringComparer.Ordinal)
            .ThenBy(itemId => _gameManager.ItemRegistry.GetItem(itemId)?.DefinitionOrder ?? int.MaxValue)
            .ThenBy(itemId => _gameManager.ItemRegistry.GetItem(itemId)?.SourceFileOrder ?? int.MaxValue)
            .ThenBy(itemId => _gameManager.ItemRegistry.GetItem(itemId)?.SourceEntryOrder ?? int.MaxValue)
            .ThenBy(itemId => itemId, StringComparer.Ordinal)
            .ToList();
        inventory.ApplyDisplayOrder(BuildOrderWithFavoritesLeading(nonFavorite));
        _gameManager.PlayerProfile.UiState.InventorySortMode = InventorySortMode.AutoByTypeThenDefinition;
        _gameManager.AddGameLog("背包已执行自动排序（收藏道具保持前置）。");
        RefreshInventory();
    }

    private List<string> BuildOrderWithFavoritesLeading(IReadOnlyList<string> nonFavoriteOrder)
    {
        if (_gameManager == null)
        {
            return nonFavoriteOrder.ToList();
        }

        PlayerInventory inventory = _gameManager.PlayerProfile.Inventory;
        List<string> favorites = inventory.GetActiveItemIdsByDisplayOrder()
            .Where(itemId => inventory.GetOrCreateItemState(itemId).IsFavorite)
            .ToList();
        favorites.AddRange(nonFavoriteOrder);
        return favorites;
    }

    private string BuildTooltip(string itemId)
    {
        if (_gameManager == null)
        {
            return itemId;
        }

        ItemDefinition? item = _gameManager.ItemRegistry.GetItem(itemId);
        if (item == null)
        {
            return $"未注册物品：{itemId}";
        }

        PlayerInventory inventory = _gameManager.PlayerProfile.Inventory;
        PlayerItemState state = inventory.GetOrCreateItemState(itemId);
        List<ItemInfoDisplayEntry> entries = ItemInfoFormatter.BuildHoverEntries(
            item,
            inventory.Stacks.GetValueOrDefault(itemId),
            state,
            _gameManager.TranslateText,
            ResolveCategoryDisplayName);
        return ItemInfoFormatter.BuildTooltipText(entries);
    }

    private string ResolveCategoryDisplayName(string categoryId)
    {
        if (_gameManager == null || string.IsNullOrWhiteSpace(categoryId))
        {
            return string.IsNullOrWhiteSpace(categoryId) ? "未分类" : categoryId;
        }

        CategoryDefinition? category = _gameManager.ItemRegistry.GetCategory(categoryId);
        return category == null
            ? categoryId
            : category.GetDisplayName(_gameManager.TranslateText);
    }

    private string GetCategorySortPath(string itemId)
    {
        if (_gameManager == null)
        {
            return $"999999:{itemId}";
        }

        ItemDefinition? item = _gameManager.ItemRegistry.GetItem(itemId);
        if (item == null)
        {
            return $"999999:{itemId}";
        }

        List<string> parts = new();
        string currentId = item.ParentId;
        int guard = 0;
        while (!string.IsNullOrWhiteSpace(currentId) && guard < 12)
        {
            CategoryDefinition? category = _gameManager.ItemRegistry.GetCategory(currentId);
            if (category == null)
            {
                break;
            }

            parts.Insert(0, $"{category.DefinitionOrder:000000}");
            currentId = category.ParentId;
            guard++;
        }

        if (parts.Count == 0)
        {
            parts.Add("999999");
        }

        return $"{string.Join(".", parts)}:{item.ParentId}";
    }

    private static string GetFilterLabel(InventoryFilterTab filterTab)
    {
        return filterTab switch
        {
            InventoryFilterTab.All => "全部",
            InventoryFilterTab.Currency => "货币",
            InventoryFilterTab.Material => "素材",
            InventoryFilterTab.Consumable => "消耗品",
            InventoryFilterTab.Battle => "战斗",
            InventoryFilterTab.Tool => "工具",
            InventoryFilterTab.Special => "特殊",
            InventoryFilterTab.Valuable => "贵重物",
            InventoryFilterTab.Other => "其他",
            InventoryFilterTab.Junk => "垃圾",
            _ => filterTab.ToString()
        };
    }

    private static string FormatStackableQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            return "0个";
        }

        if (quantity >= 1_000_000)
        {
            int value = quantity / 1_000_000;
            return value <= 9 ? $"{value}百万" : "百万+";
        }

        if (quantity >= 100_000)
        {
            return $"{quantity / 100_000}十万";
        }

        if (quantity >= 10_000)
        {
            return $"{quantity / 10_000}万";
        }

        if (quantity >= 1_000)
        {
            return $"{quantity / 1_000}千";
        }

        if (quantity >= 100)
        {
            return $"{quantity / 100}百";
        }

        if (quantity >= 10)
        {
            return $"{quantity / 10}十";
        }

        return $"{quantity}个";
    }

    private static float EstimateButtonWidth(string itemName, bool isStackable, float widthScale = 1.0f)
    {
        string widthSample = isStackable ? $"{itemName}(百万+)" : itemName;
        double paddedLength = widthSample.Length + 1.5;
        return Math.Max(84f, (float)Math.Ceiling(paddedLength * 16.0 * Math.Max(0.7f, widthScale)));
    }

    private static StyleBoxFlat CreatePanelStyle(string bgColor, string borderColor, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(bgColor),
            BorderColor = new Color(borderColor),
            BorderWidthBottom = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth
        };
    }

    private StyleBoxFlat CreateLegacySlotStyle(bool highlight)
    {
        return new StyleBoxFlat
        {
            BgColor = highlight ? new Color("#4a0000") : new Color("#220000"),
            BorderColor = new Color("#ff2d2d"),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
    }

    private StyleBoxFlat CreateStitchGridSlotStyle(bool highlight, bool selected, bool isEmpty = false)
    {
        Color background = isEmpty
            ? new Color("#f5f4ef")
            : selected
                ? new Color("#d2e5f3")
                : (highlight ? new Color("#f5f4ef") : new Color("#ffffff"));
        Color border = selected
            ? new Color("#426464")
            : (highlight ? new Color("#b7dbdb") : new Color("#d9dbd2"));

        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
    }

    private int GetStitchGridColumns()
    {
        return UseLargeInventoryIcons() ? StitchLargeColumns : StitchSmallColumns;
    }

    private int GetStitchGridSlotSize()
    {
        int fallback = UseLargeInventoryIcons() ? StitchLargeSlotSize : StitchSmallSlotSize;
        if (_stitchSlotGrid == null)
        {
            return fallback;
        }

        float availableWidth = GetStitchGridAvailableWidth();

        int columns = Math.Max(1, GetStitchGridColumns());
        int separation = _stitchSlotGrid.GetThemeConstant("h_separation");
        float effectiveWidth = availableWidth - (columns - 1) * separation;
        if (effectiveWidth <= 0f)
        {
            return fallback;
        }

        int sizeByWidth = (int)Math.Floor(effectiveWidth / columns);
        return Math.Max(fallback, sizeByWidth);
    }

    private float GetStitchGridAvailableWidth()
    {
        if (_stitchSlotGrid == null)
        {
            return 0f;
        }

        float availableWidth = _stitchSlotGrid.Size.X;
        if (availableWidth <= 1f && _slotScroll != null)
        {
            availableWidth = _slotScroll.Size.X - 6f;
        }

        return Math.Max(0f, availableWidth);
    }

    private void ScheduleStitchGridReflow(int maxFrames = 10)
    {
        if (!IsUsingStitchUiTheme() || _stitchSlotGrid == null)
        {
            return;
        }

        _pendingStitchGridReflowFrames = Math.Max(_pendingStitchGridReflowFrames, maxFrames);
        CallDeferred(nameof(ReflowStitchGridCells));
    }

    private void UpdateScrollableAreaVisibility()
    {
        UpdateScrollBarVisibility(_slotScroll);
        UpdateScrollBarVisibility(_detailScroll);
    }

    private static void UpdateScrollBarVisibility(ScrollContainer? scrollContainer)
    {
        if (scrollContainer == null)
        {
            return;
        }

        VScrollBar vScroll = scrollContainer.GetVScrollBar();
        if (vScroll == null)
        {
            return;
        }

        bool canScroll = vScroll.MaxValue > vScroll.Page + 0.5f;
        vScroll.Visible = canScroll;
        vScroll.Modulate = canScroll ? Colors.White : new Color(1f, 1f, 1f, 0f);
    }

    private int GetAdaptiveGridTextFontSize(string text)
    {
        int length = string.IsNullOrWhiteSpace(text) ? 1 : text.Length;
        int maxFont = UseLargeInventoryIcons() ? 20 : 13;
        int minFont = UseLargeInventoryIcons() ? 11 : 8;
        int sizeByLength = length switch
        {
            <= 3 => maxFont,
            <= 5 => maxFont - 2,
            <= 7 => maxFont - 4,
            <= 10 => maxFont - 6,
            _ => minFont
        };
        return Math.Clamp(sizeByLength, minFont, maxFont);
    }

    private Texture2D? TryLoadInventoryItemIcon(ItemDefinition? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.IconTexturePath))
        {
            return null;
        }

        string path = item.IconTexturePath.Trim();
        if (_iconTextureCache.TryGetValue(path, out Texture2D? cached))
        {
            return cached;
        }

        if (!ResourceLoader.Exists(path))
        {
            _iconTextureCache[path] = null;
            return null;
        }

        Texture2D? loaded = ResourceLoader.Load<Texture2D>(path);
        _iconTextureCache[path] = loaded;
        return loaded;
    }

    private bool IsUsingStitchUiTheme()
    {
        return string.Equals(
            UiImageThemeManager.GetThemeMode(),
            GameplayUiConfigLoader.UiThemeModeStitch,
            StringComparison.Ordinal);
    }

    private Color GetSearchLabelColor()
    {
        return IsUsingStitchUiTheme() ? new Color("#5d605a") : new Color("#ffe36e");
    }

    private Color GetFilterLabelColor()
    {
        return IsUsingStitchUiTheme() ? new Color("#426464") : new Color("#ff3a3a");
    }

    private Color GetEmptyHintColor()
    {
        return IsUsingStitchUiTheme() ? new Color("#6a7b6e") : new Color("#f0e27a");
    }

    private Color GetFilterButtonActiveColor()
    {
        return IsUsingStitchUiTheme() ? new Color("#0f4f4c") : new Color("#ffe36e");
    }

    private Color GetFilterButtonInactiveColor()
    {
        return IsUsingStitchUiTheme() ? new Color("#7b8a84") : new Color("#ff3a3a");
    }

    private void ShowNoDataState(string message)
    {
        if (_detailTitleLabel != null)
        {
            _detailTitleLabel.Text = "[color=#9aa5b1]未加载背包[/color]";
        }

        if (_detailBodyLabel != null)
        {
            _detailBodyLabel.Text = message;
        }

        if (_emptyHintLabel == null)
        {
            return;
        }

        if (_legacySlotFlow != null)
        {
            foreach (Node child in _legacySlotFlow.GetChildren())
            {
                _legacySlotFlow.RemoveChild(child);
                child.QueueFree();
            }
        }

        if (_stitchSlotGrid != null)
        {
            foreach (Node child in _stitchSlotGrid.GetChildren())
            {
                _stitchSlotGrid.RemoveChild(child);
                child.QueueFree();
            }
        }

        _emptyHintLabel.Visible = true;
        _emptyHintLabel.Text = message;
        CallDeferred(nameof(UpdateScrollableAreaVisibility));
    }

    private static IReadOnlyList<ItemInfoDisplayEntry> BuildUnknownDetailEntries(string itemId, int quantity)
    {
        return new List<ItemInfoDisplayEntry>
        {
            new()
            {
                FieldId = "item_id",
                Label = "物品ID",
                Value = itemId
            },
            new()
            {
                FieldId = "quantity",
                Label = "数量",
                Value = quantity.ToString(CultureInfo.InvariantCulture)
            },
            new()
            {
                FieldId = "extra",
                Label = string.Empty,
                Value = "该物品未注册到静态配置，当前仅保留运行时库存数据。"
            }
        };
    }

    private sealed class InventorySlotView
    {
        public string ItemId { get; }

        public int SlotIndex { get; }

        public string DisplayQuantityText { get; }

        public InventorySlotView(string itemId, int slotIndex, string displayQuantityText)
        {
            ItemId = itemId;
            SlotIndex = slotIndex;
            DisplayQuantityText = displayQuantityText;
        }

        public string GetSlotKey()
        {
            return $"{ItemId}#{SlotIndex}";
        }
    }
}
