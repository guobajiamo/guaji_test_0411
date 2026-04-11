using Godot;
using System.Linq;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 势力面板。
/// 负责显示势力好感度、可访问 NPC 和相关事件入口。
/// </summary>
public partial class FactionPanel : Control
{
    private RichTextLabel? _contentLabel;
    private GameManager? _gameManager;
    private string _lastFactionSignature = string.Empty;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureStructure();
    }

    public void RefreshFactions()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            _contentLabel!.Clear();
            _contentLabel.AppendText("势力面板尚未绑定 GameManager。");
            return;
        }

        string nextSignature = string.Join("|", _gameManager.FactionRegistry.Factions.Values
            .OrderBy(item => item.Id)
            .Select(faction =>
            {
                PlayerFactionState state = _gameManager.PlayerProfile.GetOrCreateFactionState(faction.Id);
                return $"{faction.Id}:{state.Reputation}:{state.HasPeaceAgreement}";
            }));
        if (nextSignature == _lastFactionSignature)
        {
            return;
        }

        _lastFactionSignature = nextSignature;
        _contentLabel!.Clear();
        foreach (FactionDefinition faction in _gameManager.FactionRegistry.Factions.Values.OrderBy(item => item.Id))
        {
            PlayerFactionState state = _gameManager.PlayerProfile.GetOrCreateFactionState(faction.Id);
            _contentLabel.AppendText($"{_gameManager.TranslateText(faction.NameKey)}\n");
            _contentLabel.AppendText($"声望：{state.Reputation}/{faction.MaxReputation}\n");
            _contentLabel.AppendText($"和平建交：{(state.HasPeaceAgreement ? "已达成" : "未达成")}\n\n");
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
