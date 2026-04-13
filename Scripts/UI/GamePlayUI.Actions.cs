using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.SaveLoad;

namespace Test00_0410.UI;

public partial class GamePlayUI
{
    protected SecondaryAreaLayout? GetSelectedArea()
    {
        if (_gameManager == null)
        {
            return null;
        }

        SecondaryAreaLayout? area = _scenarioLayout.FindArea(_gameManager.PlayerProfile.UiState.SelectedAreaId);
        return area != null && IsAreaVisible(area)
            ? area
            : null;
    }

    protected GameplayActionContext? FindActionContextByEventId(string eventId)
    {
        return _scenarioLayout.FindActionContext(eventId);
    }

    protected bool IsAreaRunningIdle(SecondaryAreaLayout area)
    {
        if (_gameManager?.PlayerProfile.IdleState.IsRunning != true)
        {
            return false;
        }

        GameplayActionContext? context = FindActionContextByEventId(_gameManager.PlayerProfile.IdleState.ActiveEventId);
        return context.HasValue && string.Equals(context.Value.AreaId, area.Id, StringComparison.Ordinal);
    }

    protected int GetInteractableCount(SecondaryAreaLayout area)
    {
        if (_gameManager == null)
        {
            return 0;
        }

        int count = 0;
        foreach (SceneLayout scene in GetVisibleScenes(area))
        {
            foreach (string eventId in scene.EventIds)
            {
                EventDefinition? definition = _gameManager.EventRegistry.GetEvent(eventId);
                if (definition != null && ShouldShowEvent(definition) && !IsEventDisabled(definition))
                {
                    count++;
                }
            }
        }

        return count;
    }

    protected IEnumerable<PrimaryRegionLayout> GetVisibleRegions()
    {
        return _scenarioLayout.Regions.Where(IsRegionVisible);
    }

    protected IEnumerable<SecondaryAreaLayout> GetVisibleAreas(PrimaryRegionLayout region)
    {
        return region.Areas.Where(IsAreaVisible);
    }

    protected IEnumerable<SceneLayout> GetVisibleScenes(SecondaryAreaLayout area)
    {
        return area.Scenes.Where(IsSceneVisible);
    }

    protected bool IsRegionVisible(PrimaryRegionLayout region)
    {
        return _gameManager != null
            && ConditionEvaluator.AreAllConditionsMet(_gameManager.PlayerProfile, region.VisibilityConditions);
    }

    protected bool IsAreaVisible(SecondaryAreaLayout area)
    {
        if (_gameManager == null)
        {
            return false;
        }

        PrimaryRegionLayout? parentRegion = _scenarioLayout.Regions.FirstOrDefault(region => region.Areas.Contains(area));
        return parentRegion != null
            && IsRegionVisible(parentRegion)
            && ConditionEvaluator.AreAllConditionsMet(_gameManager.PlayerProfile, area.VisibilityConditions);
    }

    protected bool IsSceneVisible(SceneLayout scene)
    {
        if (_gameManager == null)
        {
            return false;
        }

        SecondaryAreaLayout? parentArea = _scenarioLayout.EnumerateAreas().FirstOrDefault(area => area.Scenes.Contains(scene));
        return parentArea != null
            && IsAreaVisible(parentArea)
            && ConditionEvaluator.AreAllConditionsMet(_gameManager.PlayerProfile, scene.VisibilityConditions);
    }

    protected string ResolveVisibleDefaultAreaId()
    {
        if (_scenarioLayout.FindArea(_scenarioLayout.DefaultAreaId) is SecondaryAreaLayout defaultArea
            && IsAreaVisible(defaultArea))
        {
            return defaultArea.Id;
        }

        return GetVisibleRegions()
            .SelectMany(GetVisibleAreas)
            .FirstOrDefault()?.Id ?? string.Empty;
    }

    protected List<string> GetAvailableTabIds()
    {
        List<string> tabs = new()
        {
            TabCurrentRegion,
            TabInventory,
            TabSkills,
            TabDictionary
        };

        if (_gameManager?.ActiveScenario?.EnableSystemTab == true)
        {
            tabs.Add(TabSystem);
        }

        return tabs;
    }

    protected string GetTabText(string tabId)
    {
        return tabId switch
        {
            TabCurrentRegion => _theme.CurrentRegionTabText,
            TabInventory => _theme.InventoryTabText,
            TabSkills => _theme.SkillsTabText,
            TabDictionary => _theme.DictionaryTabText,
            TabSystem => _theme.SystemTabText,
            _ => tabId
        };
    }

    protected bool ShouldShowEvent(EventDefinition definition)
    {
        if (_gameManager == null)
        {
            return false;
        }

        return definition.Type switch
        {
            EventType.IdleLoop => _gameManager.IdleSystem?.ShouldShowEvent(definition.Id) == true
                || _gameManager.IdleSystem?.IsRunningEvent(definition.Id) == true,
            EventType.OneshotClick => _gameManager.ClickEventSystem?.ShouldShowEvent(definition.Id) == true,
            EventType.RepeatableClick => _gameManager.ClickEventSystem?.ShouldShowEvent(definition.Id) == true,
            _ => false
        };
    }

    protected EventButtonViewData BuildEventButtonData(EventDefinition definition)
    {
        string description = Translate(definition.DescriptionKey);
        string hoverInfo = !string.IsNullOrWhiteSpace(definition.HoverInfoKey)
            ? Translate(definition.HoverInfoKey)
            : description;

        EventButtonViewData data = new()
        {
            EventId = definition.Id,
            DisplayName = Translate(definition.NameKey),
            Description = description,
            TooltipText = BuildEventTooltipText(definition, hoverInfo),
            IsDisabled = IsEventDisabled(definition),
            ProgressRatio = definition.Type == EventType.IdleLoop
                ? _gameManager?.IdleSystem?.GetProgressRatio(definition.Id) ?? 0.0
                : 0.0,
            ShowProgressBar = definition.Type == EventType.IdleLoop
                && _gameManager?.IdleSystem?.IsRunningEvent(definition.Id) == true,
            StyleVariant = definition.Type switch
            {
                EventType.OneshotClick => "event_oneshot",
                EventType.RepeatableClick => "event_click",
                EventType.IdleLoop => "event_idle",
                _ => "event_click"
            }
        };

        if (definition.Type == EventType.IdleLoop && _gameManager?.IdleSystem?.IsRunningEvent(definition.Id) == true)
        {
            data.DisplayName = $"停止{data.DisplayName}";
            data.IsDisabled = false;
        }

        return data;
    }

    protected bool IsEventDisabled(EventDefinition definition)
    {
        if (_gameManager == null)
        {
            return true;
        }

        return definition.Type switch
        {
            EventType.IdleLoop => _gameManager.IdleSystem?.CanStartIdleEvent(definition.Id) != true
                && _gameManager.IdleSystem?.IsRunningEvent(definition.Id) != true,
            _ => _gameManager.ClickEventSystem?.CanTriggerEvent(definition.Id) != true
        };
    }

    protected void OnEventButtonPressed(string eventId)
    {
        if (_gameManager == null)
        {
            return;
        }

        EventDefinition? definition = _gameManager.EventRegistry.GetEvent(eventId);
        if (definition == null)
        {
            return;
        }

        if (definition.Type == EventType.IdleLoop)
        {
            HandleIdleEvent(definition);
            RefreshAllPanels();
            return;
        }

        if (definition.Dialog != null && (definition.Dialog.HasConfirmButton || definition.Dialog.HasChoices))
        {
            ShowDialogForEvent(definition);
            return;
        }

        bool success = _gameManager.ClickEventSystem?.TryTriggerEvent(eventId) == true;
        if (!success)
        {
            _gameManager.AddGameLog($"事件执行失败：{Translate(definition.NameKey)}");
        }

        RefreshAllPanels();
    }

    protected void HandleIdleEvent(EventDefinition definition)
    {
        if (_gameManager?.IdleSystem == null)
        {
            return;
        }

        if (_gameManager.IdleSystem.IsRunningEvent(definition.Id))
        {
            _gameManager.IdleSystem.StopIdleEvent();
            _gameManager.AddGameLog($"已停止挂机：{Translate(definition.NameKey)}");
            return;
        }

        string previousIdleEventId = _gameManager.PlayerProfile.IdleState.ActiveEventId;
        bool hadRunningIdle = _gameManager.PlayerProfile.IdleState.IsRunning
            && !string.IsNullOrWhiteSpace(previousIdleEventId);
        bool success = _gameManager.IdleSystem.StartIdleEvent(definition.Id);
        if (success && hadRunningIdle)
        {
            _gameManager.AddGameLog($"已切换挂机：{_gameManager.GetEventDisplayName(previousIdleEventId)} -> {Translate(definition.NameKey)}");
            return;
        }

        _gameManager.AddGameLog(success
            ? $"已开始挂机：{Translate(definition.NameKey)}"
            : $"挂机启动失败：{Translate(definition.NameKey)}");
    }

    protected void ShowDialogForEvent(EventDefinition definition)
    {
        if (_gameManager == null || _eventDialogPanel == null || definition.Dialog == null)
        {
            return;
        }

        EventDialogDefinition dialog = definition.Dialog;
        List<EventDialogPanel.DialogButtonConfig> buttons = new();

        if (dialog.HasChoices)
        {
            foreach (EventDialogChoiceDefinition choice in dialog.Choices)
            {
                buttons.Add(new EventDialogPanel.DialogButtonConfig
                {
                    Text = choice.ButtonText,
                    TooltipText = BuildChoiceTooltip(choice.TargetEventId),
                    OnPressed = () =>
                    {
                        bool success = _gameManager.ClickEventSystem?.TryTriggerDialogChoice(
                            definition.Id,
                            choice.TargetEventId,
                            dialog.ConsumeSourceEventOnChoice) == true;

                        if (!success)
                        {
                            _gameManager.AddGameLog($"分支选择执行失败：{choice.ButtonText}");
                        }

                        RefreshAllPanels();
                    }
                });
            }
        }
        else
        {
            buttons.Add(new EventDialogPanel.DialogButtonConfig
            {
                Text = dialog.ConfirmButtonText,
                TooltipText = BuildSingleConfirmTooltip(definition),
                OnPressed = () =>
                {
                    bool success = _gameManager.ClickEventSystem?.TryTriggerEvent(definition.Id) == true;
                    if (!success)
                    {
                        _gameManager.AddGameLog($"事件执行失败：{Translate(definition.NameKey)}");
                    }

                    RefreshAllPanels();
                }
            });
        }

        _eventDialogPanel.ShowDialog(
            Translate(definition.NameKey),
            Translate(dialog.BodyTextKey),
            buttons,
            dialog.ShowCancelButton);
    }

    protected string BuildChoiceTooltip(string targetEventId)
    {
        if (_gameManager == null)
        {
            return string.Empty;
        }

        EventDefinition? targetDefinition = _gameManager.EventRegistry.GetEvent(targetEventId);
        if (targetDefinition == null)
        {
            return "未找到该选项对应的目标事件。";
        }

        string info = !string.IsNullOrWhiteSpace(targetDefinition.HoverInfoKey)
            ? Translate(targetDefinition.HoverInfoKey)
            : Translate(targetDefinition.DescriptionKey);
        return BuildEventTooltipText(targetDefinition, info);
    }

    protected string BuildSingleConfirmTooltip(EventDefinition definition)
    {
        string info = !string.IsNullOrWhiteSpace(definition.HoverInfoKey)
            ? Translate(definition.HoverInfoKey)
            : Translate(definition.DescriptionKey);
        return BuildEventTooltipText(definition, info);
    }

    protected string BuildEventTooltipText(EventDefinition definition, string baseText)
    {
        List<string> lines = new();
        if (!string.IsNullOrWhiteSpace(baseText))
        {
            lines.Add(baseText);
        }

        string costText = BuildCostSummary(definition);
        if (!string.IsNullOrWhiteSpace(costText))
        {
            lines.Add($"消耗：{costText}");
        }

        string rewardText = BuildEffectSummary(definition);
        if (!string.IsNullOrWhiteSpace(rewardText))
        {
            lines.Add($"获得：{rewardText}");
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    protected string BuildCostSummary(EventDefinition definition)
    {
        if (_gameManager == null || definition.Costs.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("，", definition.Costs.Select(cost => $"{_gameManager.GetItemDisplayName(cost.ItemId)} x{cost.Amount}"));
    }

    protected string BuildEffectSummary(EventDefinition definition)
    {
        if (_gameManager == null)
        {
            return string.Empty;
        }

        List<string> parts = new();
        parts.AddRange(definition.Rewards.Select(reward => $"{_gameManager.GetItemDisplayName(reward.ItemId)} x{reward.Amount}"));

        foreach (EventEffectEntry effect in definition.Effects)
        {
            switch (effect.EffectType)
            {
                case EventEffectType.GrantItem:
                    parts.Add($"{_gameManager.GetItemDisplayName(effect.TargetId)} x{effect.IntValue}");
                    break;
                case EventEffectType.RemoveItem:
                    parts.Add($"失去 {_gameManager.GetItemDisplayName(effect.TargetId)} x{effect.IntValue}");
                    break;
                case EventEffectType.GrantGold:
                    parts.Add($"金币 x{effect.IntValue}");
                    break;
                case EventEffectType.RemoveGold:
                    parts.Add($"失去金币 x{effect.IntValue}");
                    break;
                case EventEffectType.GrantSkillExp:
                    parts.Add($"{GetSkillDisplayName(effect.TargetId)}经验 +{effect.IntValue}");
                    break;
                case EventEffectType.AddFactionReputation:
                    parts.Add($"势力声望 {effect.TargetId} +{effect.IntValue}");
                    break;
                case EventEffectType.UnlockZone:
                    parts.Add($"解锁区域：{effect.TargetId}");
                    break;
                case EventEffectType.CompleteEvent:
                    parts.Add($"完成事件：{effect.TargetId}");
                    break;
                case EventEffectType.CompleteQuest:
                    parts.Add($"完成任务：{effect.TargetId}");
                    break;
            }
        }

        return string.Join("，", parts);
    }

    protected string GetSkillDisplayName(string skillId)
    {
        if (_gameManager == null)
        {
            return skillId;
        }

        SkillDefinition? definition = _gameManager.SkillRegistry.GetSkill(skillId);
        return definition == null ? skillId : Translate(definition.NameKey);
    }

    protected void TryLevelUpSkill(string skillId)
    {
        if (_gameManager?.SkillSystem == null)
        {
            return;
        }

        bool success = _gameManager.SkillSystem.TryLevelUp(skillId);
        _gameManager.AddGameLog(success
            ? $"技能升级成功：{GetSkillDisplayName(skillId)}"
            : $"技能升级失败：{GetSkillDisplayName(skillId)}");
        RefreshAllPanels();
    }

    protected void ShowStorySaveDialog()
    {
        if (_gameManager == null || _storySaveDialog == null)
        {
            return;
        }

        _storySaveDialog.ShowDialog(
            "保存存档",
            BuildSlotViewData(_gameManager.GetStorySaveSlotSummaries(), false),
            slotIndex =>
            {
                SaveSlotSummary summary = _gameManager.GetStorySaveSlotSummaries()[slotIndex - 1];
                if (summary.Exists)
                {
                    _confirmDialog?.ShowDialog("覆盖存档确认", $"存档位 {slotIndex:00} 已有内容，是否覆盖？", "确认覆盖", "取消", () => SaveStorySlot(slotIndex));
                    return;
                }

                SaveStorySlot(slotIndex);
            });
    }

    protected void SaveStorySlot(int slotIndex)
    {
        if (_gameManager == null)
        {
            return;
        }

        string path = _gameManager.GetStorySaveSlotPath(slotIndex);
        if (_gameManager.SaveGameToPath(path))
        {
            ShowStorySaveDialog();
        }

        RefreshAllPanels();
    }

    protected void ShowStoryLoadDialog()
    {
        if (_gameManager == null || _storyLoadDialog == null)
        {
            return;
        }

        _storyLoadDialog.ShowDialog(
            "读取存档",
            BuildSlotViewData(_gameManager.GetStorySaveSlotSummaries(), true),
            slotIndex =>
            {
                SaveSlotSummary summary = _gameManager.GetStorySaveSlotSummaries()[slotIndex - 1];
                if (!summary.Exists)
                {
                    return;
                }

                _confirmDialog?.ShowDialog("读取存档确认", "读取存档将覆盖当前游戏进度，是否继续？", "确认读取", "取消", () => LoadStorySlot(summary.FilePath));
            });
    }

    protected void LoadStorySlot(string path)
    {
        if (_gameManager?.LoadGameFromPath(path) == true)
        {
            _storyLoadDialog?.HideDialog();
            _eventDialogPanel?.HideDialog();
            RefreshAllPanels();
        }
    }

    protected void SaveQuickTestGame()
    {
        if (_gameManager?.SaveGame() == true)
        {
            RefreshAllPanels();
        }
    }

    protected void ConfirmQuickLoadTestGame()
    {
        _confirmDialog?.ShowDialog("读取测试存档", "读取测试存档将覆盖当前测试进度，是否继续？", "确认读取", "取消", LoadQuickTestGame);
    }

    protected void LoadQuickTestGame()
    {
        if (_gameManager?.LoadGame() == true)
        {
            RefreshAllPanels();
        }
    }

    protected void RequestReturnToMainMenu()
    {
        _confirmDialog?.ShowDialog(
            "返回主菜单",
            "返回主菜单前建议先手动保存存档。是否现在返回？",
            "返回主菜单",
            "取消",
            () =>
            {
                SetLogExpanded(false);
                _eventDialogPanel?.HideDialog();
                _storyLoadDialog?.HideDialog();
                _storySaveDialog?.HideDialog();
                FindAppRoot()?.ShowMainMenu();
            });
    }

    protected AppRoot? FindAppRoot()
    {
        Node? current = this;
        while (current != null)
        {
            if (current is AppRoot appRoot)
            {
                return appRoot;
            }

            current = current.GetParent();
        }

        return null;
    }

    protected string Translate(string keyOrText)
    {
        return string.IsNullOrWhiteSpace(keyOrText) ? string.Empty : _gameManager?.TranslateText(keyOrText) ?? keyOrText;
    }

    protected StyleBoxFlat CreatePanelStyle(PanelVisual visual)
    {
        Color background = visual.BaseColor;
        background.A = visual.Alpha;

        Color border = visual.BorderColor;
        border.A = visual.BorderAlpha;

        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ShadowColor = new Color(0, 0, 0, 0.32f),
            ShadowSize = 10,
            ContentMarginLeft = 14,
            ContentMarginTop = 12,
            ContentMarginRight = 14,
            ContentMarginBottom = 12
        };
    }

    protected StyleBoxFlat CreateTabStyle(bool active)
    {
        Color background = active ? _theme.Tabs.ActiveColor : _theme.Tabs.NormalColor;
        background.A = active ? _theme.Tabs.ActiveAlpha : _theme.Tabs.NormalAlpha;

        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = _theme.Tabs.BorderColor,
            BorderWidthLeft = active ? 2 : 1,
            BorderWidthTop = active ? 2 : 1,
            BorderWidthRight = active ? 2 : 1,
            BorderWidthBottom = active ? 2 : 1,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ShadowColor = new Color(0, 0, 0, active ? 0.24f : 0.16f),
            ShadowSize = active ? 6 : 4,
            ContentMarginLeft = 14,
            ContentMarginTop = 8,
            ContentMarginRight = 14,
            ContentMarginBottom = 8
        };
    }

    protected StyleBoxFlat CreateFlatStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6
        };
    }

    protected Label CreateHintLabel(string text)
    {
        Label label = new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", _theme.BodyFontSize);
        label.AddThemeColorOverride("font_color", new Color("#d7dde5"));
        return label;
    }

    protected static void ClearContainer(Node container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    protected static IReadOnlyList<SaveSlotViewData> BuildSlotViewData(IReadOnlyList<SaveSlotSummary> summaries, bool loadOnlyExisting)
    {
        List<SaveSlotViewData> viewData = new();
        foreach (SaveSlotSummary summary in summaries)
        {
            string summaryText = summary.Exists
                ? $"剧本：{summary.ScenarioDisplayName}\n保存时间：{FormatSavedTime(summary.SavedAtUnixSeconds)}"
                : "空存档位";

            viewData.Add(new SaveSlotViewData
            {
                SlotIndex = summary.SlotIndex,
                Title = $"存档位 {summary.SlotIndex:00}（{summary.FileName}）",
                Summary = summaryText,
                TooltipText = summary.Exists
                    ? $"读取该存档位。\n路径：{summary.FilePath}"
                    : $"这个存档位当前为空。\n路径：{summary.FilePath}",
                IsEnabled = loadOnlyExisting ? summary.Exists : true
            });
        }

        return viewData;
    }

    protected static string FormatSavedTime(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return "未知";
        }

        DateTimeOffset dateTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
