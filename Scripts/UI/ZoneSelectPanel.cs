using Godot;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 区域选择面板。
/// 用于切换不同危险区域或副本区域。
/// </summary>
public partial class ZoneSelectPanel : Control
{
    private RichTextLabel? _contentLabel;
    private GameManager? _gameManager;
    private string _lastZoneSignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureStructure();
    }

    public void RefreshZones()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            _contentLabel!.Clear();
            _contentLabel.AppendText("区域面板尚未绑定 GameManager。");
            return;
        }

        string nextSignature = string.Join("|", _gameManager.ZoneRegistry.Zones.Values
            .OrderBy(item => item.Id)
            .Select(zone =>
            {
                PlayerZoneState state = _gameManager.PlayerProfile.GetOrCreateZoneState(zone.Id);
                return $"{zone.Id}:{state.IsUnlocked}:{state.ExplorationPercent:0.###}:{state.ClearCount}";
            }));
        if (nextSignature == _lastZoneSignature)
        {
            return;
        }

        _lastZoneSignature = nextSignature;
        _contentLabel!.Clear();
        foreach (ZoneDefinition zone in _gameManager.ZoneRegistry.Zones.Values.OrderBy(item => item.Id))
        {
            PlayerZoneState state = _gameManager.PlayerProfile.GetOrCreateZoneState(zone.Id);
            _contentLabel.AppendText($"{_gameManager.TranslateText(zone.NameKey)}\n");
            _contentLabel.AppendText($"是否解锁：{(state.IsUnlocked ? "已解锁" : "未解锁")}\n");
            _contentLabel.AppendText($"探索度：{state.ExplorationPercent:0}%\n");
            _contentLabel.AppendText($"通关次数：{state.ClearCount}/{zone.MaxClearCount}\n\n");
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
            ScrollActive = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _contentLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_contentLabel);
    }
}
