using Godot;
using System;
using Test00_0410.Autoload;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Runtime;

namespace Test00_0410.UI;

/// <summary>
/// 右侧角色状态栏。
/// 以后会持续显示金币、技能等级、当前挂机状态、区域进度等核心信息。
/// </summary>
public partial class CharacterStatusPanel : Control
{
    private Label? _statusLabel;
    private GameManager? _gameManager;

    public override void _Ready()
    {
        EnsureStructure();
    }

    public void Configure(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureStructure();
    }

    public void RefreshStatus()
    {
        EnsureStructure();

        if (_gameManager == null)
        {
            _statusLabel!.Text = "状态栏尚未绑定 GameManager。";
            return;
        }

        PlayerProfile profile = _gameManager.PlayerProfile;
        PlayerSkillState choppingState = profile.GetOrCreateSkillState("skill_chopping");
        ItemDefinition? currentTool = _gameManager.GetBestOwnedTool(ItemTag.Axe);
        string currentToolName = currentTool == null ? "无" : currentTool.GetDisplayName(_gameManager.TranslateText);

        string idleText = "当前未挂机";
        double progressRatio = 0.0;
        if (_gameManager.IdleSystem != null && profile.IdleState.IsRunning && !string.IsNullOrWhiteSpace(profile.IdleState.ActiveEventId))
        {
            idleText = $"当前挂机：{_gameManager.GetEventDisplayName(profile.IdleState.ActiveEventId)}";
            progressRatio = _gameManager.IdleSystem.GetProgressRatio(profile.IdleState.ActiveEventId);
        }

        _statusLabel!.Text =
            $"金币：{profile.Economy.Gold}\n" +
            $"当前工具：{currentToolName}\n" +
            $"砍树等级：Lv.{choppingState.Level}\n" +
            $"砍树经验：{FormatExpValue(choppingState.StoredExp)}\n" +
            $"{idleText}\n" +
            $"当前读条进度：{progressRatio:P0}";
    }

    private void EnsureStructure()
    {
        if (_statusLabel != null)
        {
            return;
        }

        PanelContainer panel = new()
        {
            Name = "StatusPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        _statusLabel = new Label
        {
            Name = "StatusLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddChild(_statusLabel);
    }

    private static string FormatExpValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString("0")
            : value.ToString("0.###");
    }
}
