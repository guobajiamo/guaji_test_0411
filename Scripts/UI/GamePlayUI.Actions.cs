using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Test00_0410.Core.Definitions;
using Test00_0410.Core.Enums;
using Test00_0410.Core.Helpers;
using Test00_0410.Core.Runtime;
using Test00_0410.Core.SaveLoad;
using Test00_0410.Systems;

namespace Test00_0410.UI;

public partial class GamePlayUI
{
    private sealed partial class CornerBracketOverlayControl : Control
    {
        private readonly Color _color;
        private readonly float _cornerRadius;
        private readonly float _thickness;
        private readonly float _panelOutset;

        public CornerBracketOverlayControl(Color color, float cornerRadius, float thickness, float panelOutset)
        {
            _color = color;
            _cornerRadius = cornerRadius;
            _thickness = thickness;
            _panelOutset = panelOutset;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 4;
        }

        public override void _Ready()
        {
            QueueRedraw();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationResized)
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            Rect2 panelRect = new(
                _panelOutset,
                _panelOutset,
                Mathf.Max(1.0f, Size.X - (_panelOutset * 2.0f)),
                Mathf.Max(1.0f, Size.Y - (_panelOutset * 2.0f)));

            float outerRadius = Mathf.Clamp(_cornerRadius, _thickness, Mathf.Min(panelRect.Size.X, panelRect.Size.Y) * 0.5f);

            Vector2 topLeftCenter = new(panelRect.Position.X + outerRadius, panelRect.Position.Y + outerRadius);
            Vector2 topRightCenter = new(panelRect.End.X - outerRadius, panelRect.Position.Y + outerRadius);
            Vector2 bottomLeftCenter = new(panelRect.Position.X + outerRadius, panelRect.End.Y - outerRadius);
            Vector2 bottomRightCenter = new(panelRect.End.X - outerRadius, panelRect.End.Y - outerRadius);

            float leftX = panelRect.Position.X;
            float rightX = panelRect.End.X;
            float topY = panelRect.Position.Y;
            float bottomY = panelRect.End.Y;

            int pointCount = Mathf.Max(16, Mathf.RoundToInt(outerRadius * 2.2f));

            DrawArc(topLeftCenter, outerRadius, Mathf.Pi, Mathf.Pi * 1.5f, pointCount, _color, _thickness, true);
            DrawArc(topRightCenter, outerRadius, Mathf.Pi * 1.5f, Mathf.Pi * 2.0f, pointCount, _color, _thickness, true);
            DrawArc(bottomLeftCenter, outerRadius, Mathf.Pi * 0.5f, Mathf.Pi, pointCount, _color, _thickness, true);
            DrawArc(bottomRightCenter, outerRadius, 0.0f, Mathf.Pi * 0.5f, pointCount, _color, _thickness, true);

            DrawLine(
                new Vector2(leftX, panelRect.Position.Y + outerRadius),
                new Vector2(leftX, panelRect.End.Y - outerRadius),
                _color,
                _thickness,
                true);
            DrawLine(
                new Vector2(rightX, panelRect.Position.Y + outerRadius),
                new Vector2(rightX, panelRect.End.Y - outerRadius),
                _color,
                _thickness,
                true);

            DrawLine(
                new Vector2(panelRect.Position.X + outerRadius, topY),
                new Vector2(panelRect.End.X - outerRadius, topY),
                _color,
                _thickness,
                true);
            DrawLine(
                new Vector2(panelRect.Position.X + outerRadius, bottomY),
                new Vector2(panelRect.End.X - outerRadius, bottomY),
                _color,
                _thickness,
                true);
        }
    }

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
        List<string> orderedTabs = new()
        {
            TabCurrentRegion,
            TabInventory,
            TabSkills,
            TabBattle,
            TabEquipment,
            TabQuest,
            TabTutorial,
            TabAchievement,
            TabDictionary,
            TabSystem
        };

        return orderedTabs.Where(IsTabVisible).ToList();
    }

    protected string GetTabText(string tabId)
    {
        if (string.Equals(tabId, TabCurrentRegion, StringComparison.Ordinal)
            && _gameManager?.PlayerProfile.UiState.IsSkillSidebarMode == true)
        {
            SkillDefinition? selectedSkill = GetSelectedSkillDefinition();
            return selectedSkill == null
                ? "技能相关"
                : $"{Translate(selectedSkill.NameKey)}相关";
        }

        if (_scenarioTabs.TryGetValue(tabId, out ScenarioTabDefinition? tabDefinition)
            && !string.IsNullOrWhiteSpace(tabDefinition.Title))
        {
            return tabDefinition.Title;
        }

        return tabId switch
        {
            TabCurrentRegion => _theme.CurrentRegionTabText,
            TabInventory => _theme.InventoryTabText,
            TabSkills => _theme.SkillsTabText,
            TabBattle => "战斗",
            TabEquipment => "装备",
            TabQuest => "任务",
            TabTutorial => "教学",
            TabAchievement => "成就",
            TabDictionary => _theme.DictionaryTabText,
            TabSystem => _theme.SystemTabText,
            _ => tabId
        };
    }

    protected bool IsTabVisible(string tabId)
    {
        if (string.Equals(tabId, TabSystem, StringComparison.Ordinal))
        {
            return _gameManager?.ActiveScenario?.EnableSystemTab == true;
        }

        if (_scenarioTabs.TryGetValue(tabId, out ScenarioTabDefinition? definition))
        {
            return definition.Visible;
        }

        return tabId switch
        {
            TabCurrentRegion => true,
            TabInventory => true,
            TabSkills => true,
            TabBattle => true,
            TabDictionary => true,
            _ => false
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
        bool isIdleRunning = definition.Type == EventType.IdleLoop
            && _gameManager?.IdleSystem?.IsRunningEvent(definition.Id) == true;
        bool isGatheringWaiting = definition.Type == EventType.IdleLoop
            && _gameManager?.IdleSystem?.IsWaitingForGatheringRecovery(definition.Id) == true;

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
                && isIdleRunning
                && !isGatheringWaiting,
            StyleVariant = definition.Type switch
            {
                EventType.OneshotClick => "event_oneshot",
                EventType.RepeatableClick => "event_click",
                EventType.IdleLoop => "event_idle",
                _ => "event_click"
            }
        };

        if (definition.Type == EventType.IdleLoop && isIdleRunning)
        {
            data.DisplayName = IsCollectionIdleEvent(definition)
                ? isGatheringWaiting
                    ? $"{data.DisplayName}(等待中)"
                    : $"{data.DisplayName}(采集中)"
                : $"停止{data.DisplayName}";
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
            string previousIdleEventId = _gameManager.PlayerProfile.IdleState.ActiveEventId;
            bool wasIdleRunning = _gameManager.PlayerProfile.IdleState.IsRunning;
            HandleIdleEvent(definition);
            bool isIdleStateChanged = wasIdleRunning != _gameManager.PlayerProfile.IdleState.IsRunning
                || !string.Equals(previousIdleEventId, _gameManager.PlayerProfile.IdleState.ActiveEventId, StringComparison.Ordinal);
            if (!isIdleStateChanged)
            {
                RefreshCurrentRegionPage();
                RefreshLeftRegionTree();
                RefreshStatusAndLogs();
            }

            return;
        }

        if (TryOpenBattlePageFromEvent(definition))
        {
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

        ScheduleActionDrivenRefresh();
    }

    private bool TryOpenBattlePageFromEvent(EventDefinition definition)
    {
        if (_gameManager == null || !TryResolveBattleEncounterId(definition, out string encounterId))
        {
            return false;
        }

        GameplayActionContext? actionContext = FindActionContextByEventId(definition.Id);
        PlayerUiState uiState = _gameManager.PlayerProfile.UiState;
        uiState.IsSkillSidebarMode = true;
        uiState.SelectedSkillId = ResolvePreferredBattleSkillId();
        uiState.SetBattleSelection(
            actionContext?.AreaId ?? _gameManager.PlayerProfile.UiState.SelectedAreaId,
            actionContext?.SceneId ?? string.Empty,
            definition.Id,
            encounterId,
            fromSceneEntry: true);
        uiState.SelectedTabId = TabBattle;
        PrepareBattleTabLayout();
        RefreshAllPanels();
        return true;
    }

    private string ResolvePreferredBattleSkillId()
    {
        if (_gameManager == null)
        {
            return string.Empty;
        }

        if (IsBattleSkillId(_gameManager.PlayerProfile.UiState.SelectedSkillId))
        {
            return _gameManager.PlayerProfile.UiState.SelectedSkillId;
        }

        string? learnedBattleSkillId = _gameManager.SkillRegistry.Skills.Values
            .Where(static skill => string.Equals(skill.GroupId, "battle", StringComparison.Ordinal))
            .OrderBy(skill => skill.GroupOrder)
            .ThenBy(skill => skill.SkillOrder)
            .ThenBy(skill => skill.SourceFileOrder)
            .ThenBy(skill => skill.SourceEntryOrder)
            .Select(skill => new
            {
                skill.Id,
                Level = _gameManager.PlayerProfile.GetOrCreateSkillState(skill.Id).Level
            })
            .FirstOrDefault(entry => entry.Level > 0)
            ?.Id;
        if (!string.IsNullOrWhiteSpace(learnedBattleSkillId))
        {
            return learnedBattleSkillId;
        }

        return _gameManager.SkillRegistry.GetSkill(BattleEquipmentTypeCatalog.SlashPassiveSkillId) != null
            ? BattleEquipmentTypeCatalog.SlashPassiveSkillId
            : _gameManager.SkillRegistry.Skills.Values
                .Where(static skill => string.Equals(skill.GroupId, "battle", StringComparison.Ordinal))
                .OrderBy(skill => skill.GroupOrder)
                .ThenBy(skill => skill.SkillOrder)
                .ThenBy(skill => skill.SourceFileOrder)
                .ThenBy(skill => skill.SourceEntryOrder)
                .Select(skill => skill.Id)
                .FirstOrDefault()
                ?? string.Empty;
    }

    private static bool TryResolveBattleEncounterId(EventDefinition definition, out string encounterId)
    {
        encounterId = definition.Effects
            .FirstOrDefault(effect => effect.EffectType == EventEffectType.StartBattle)?
            .TargetId?
            .Trim()
            ?? string.Empty;
        return !string.IsNullOrWhiteSpace(encounterId);
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

                        ScheduleActionDrivenRefresh();
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

                    ScheduleActionDrivenRefresh();
                }
            });
        }

        _eventDialogPanel.ShowDialog(
            Translate(definition.NameKey),
            Translate(dialog.BodyTextKey),
            buttons,
            dialog.ShowCancelButton);
        CallDeferred(nameof(HarvestNonBlockingTooltipBindings));
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

        string rewardText = definition.Type == EventType.IdleLoop
            ? BuildIdleDropSummary(definition)
            : BuildEffectSummary(definition);
        if (!string.IsNullOrWhiteSpace(rewardText))
        {
            lines.Add($"获得：{rewardText}");
        }

        string resourceText = BuildGatheringResourceSummary(definition);
        if (!string.IsNullOrWhiteSpace(resourceText))
        {
            lines.Add(resourceText);
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
        parts.AddRange(definition.Rewards.Select(reward =>
        {
            string chanceText = reward.DropChance >= 0.999999
                ? string.Empty
                : $" ({Math.Clamp(reward.DropChance, 0.0, 1.0) * 100.0:0.#}%)";
            return $"{_gameManager.GetItemDisplayName(reward.ItemId)} x{reward.Amount}{chanceText}";
        }));

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
                case EventEffectType.UnlockAchievement:
                    parts.Add($"解锁成就：{effect.TargetId}");
                    break;
                case EventEffectType.LearnSkill:
                    parts.Add($"习得技能：{GetSkillDisplayName(effect.TargetId)}");
                    break;
                case EventEffectType.SwitchToSkillMode:
                    parts.Add($"切换到技能模式：{GetSkillDisplayName(effect.TargetId)}");
                    break;
            }
        }

        return string.Join("，", parts);
    }

    protected string BuildIdleDropSummary(EventDefinition definition)
    {
        if (_gameManager == null)
        {
            return string.Empty;
        }

        List<string> parts = new();
        HashSet<string> dropItemIds = new(StringComparer.Ordinal);
        SkillDefinition? skillDefinition = _gameManager.SkillRegistry.GetSkill(definition.LinkedSkillId);
        bool hasPrimaryRewardOverride = skillDefinition != null && definition.Rewards.Any(reward =>
            string.Equals(reward.ItemId, skillDefinition.PrimaryOutputItemId, StringComparison.Ordinal)
            && reward.DropChance >= 0.999999);
        if (skillDefinition != null && !hasPrimaryRewardOverride)
        {
            var skillState = _gameManager.PlayerProfile.GetOrCreateSkillState(skillDefinition.Id);
            SkillLevelEntry? levelEntry = skillDefinition.GetLevelEntry(skillState.Level);
            if (levelEntry != null && levelEntry.Output > 0.0 && !string.IsNullOrWhiteSpace(skillDefinition.PrimaryOutputItemId))
            {
                string outputText = Math.Abs(levelEntry.Output - Math.Round(levelEntry.Output)) < 0.0001
                    ? Math.Round(levelEntry.Output).ToString("0", CultureInfo.InvariantCulture)
                    : levelEntry.Output.ToString("0.###", CultureInfo.InvariantCulture);
                parts.Add($"{_gameManager.GetItemDisplayName(skillDefinition.PrimaryOutputItemId)} x{outputText} (100%)");
                dropItemIds.Add(skillDefinition.PrimaryOutputItemId);
            }
        }

        foreach (EventRewardEntry reward in definition.Rewards)
        {
            string chanceText = reward.DropChance >= 0.999999
                ? "100%"
                : $"{Math.Clamp(reward.DropChance, 0.0, 1.0) * 100.0:0.#}%";
            parts.Add($"{_gameManager.GetItemDisplayName(reward.ItemId)} x{reward.Amount} ({chanceText})");
            dropItemIds.Add(reward.ItemId);
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        int rarityLevel = 1;
        if (dropItemIds.Count > 0)
        {
            Rarity highestRarity = dropItemIds
                .Select(itemId => _gameManager.ItemRegistry.GetItem(itemId)?.BaseRarity ?? Rarity.Common)
                .DefaultIfEmpty(Rarity.Common)
                .Max();
            rarityLevel = (int)highestRarity + 1;
        }

        return $"采集点稀有度：{rarityLevel}级\n掉落：{string.Join("，", parts)}";
    }

    protected string BuildGatheringResourceSummary(EventDefinition definition)
    {
        if (_gameManager?.IdleSystem == null || definition.Type != EventType.IdleLoop || !definition.HasResourceCap)
        {
            return string.Empty;
        }

        if (!_gameManager.IdleSystem.TryGetGatheringNodeView(definition.Id, out IdleSystem.GatheringNodeView nodeView))
        {
            return string.Empty;
        }

        string fullText = nodeView.SecondsToFull <= 0.0
            ? "采集次数已回满。"
            : $"还剩{FormatDurationText(nodeView.SecondsToFull)}回满采集次数。";
        return $"可采集次数/上限={nodeView.AvailableAmount}/{nodeView.Capacity}，{fullText}";
    }

    protected bool IsCollectionIdleEvent(EventDefinition definition)
    {
        if (_gameManager == null || definition.Type != EventType.IdleLoop)
        {
            return false;
        }

        SkillDefinition? skillDefinition = _gameManager.SkillRegistry.GetSkill(definition.LinkedSkillId);
        return skillDefinition?.IsCollectionSkill == true;
    }

    protected static string FormatDurationText(double secondsValue)
    {
        int seconds = Math.Max(0, (int)Math.Ceiling(secondsValue));
        if (seconds >= 3600)
        {
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            return minutes > 0 ? $"{hours}小时{minutes}分" : $"{hours}小时";
        }

        if (seconds >= 60)
        {
            int minutes = seconds / 60;
            int leftSeconds = seconds % 60;
            return leftSeconds > 0 ? $"{minutes}分{leftSeconds}秒" : $"{minutes}分";
        }

        return $"{seconds}秒";
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
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ShadowColor = new Color(0, 0, 0, 0.32f),
            ShadowSize = 10,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8
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
            BorderWidthLeft = active ? 4 : 3,
            BorderWidthTop = active ? 4 : 3,
            BorderWidthRight = active ? 4 : 3,
            BorderWidthBottom = active ? 4 : 3,
            CornerRadiusTopLeft = _theme.CornerRadius,
            CornerRadiusTopRight = _theme.CornerRadius,
            CornerRadiusBottomLeft = _theme.CornerRadius,
            CornerRadiusBottomRight = _theme.CornerRadius,
            ShadowColor = new Color(0, 0, 0, active ? 0.24f : 0.16f),
            ShadowSize = active ? 6 : 4,
            ContentMarginLeft = 12,
            ContentMarginTop = 6,
            ContentMarginRight = 12,
            ContentMarginBottom = 6
        };
    }

    protected void ApplyLogActionButtonStyle(Button button)
    {
        button.AddThemeStyleboxOverride("normal", CreateTabStyle(false));
        button.AddThemeStyleboxOverride("hover", CreateTabStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateTabStyle(true));
        if (IsUsingStitchUiTheme())
        {
            button.AddThemeColorOverride("font_color", new Color("#224545"));
            button.AddThemeColorOverride("font_hover_color", new Color("#183737"));
            button.AddThemeColorOverride("font_pressed_color", new Color("#183737"));
            button.AddThemeColorOverride("font_focus_color", new Color("#183737"));
            button.AddThemeColorOverride("font_disabled_color", new Color("#797c75"));
            return;
        }

        button.AddThemeColorOverride("font_color", new Color("#f8fafc"));
        button.AddThemeColorOverride("font_hover_color", new Color("#ffffff"));
        button.AddThemeColorOverride("font_pressed_color", new Color("#ffffff"));
        button.AddThemeColorOverride("font_focus_color", new Color("#ffffff"));
        button.AddThemeColorOverride("font_disabled_color", new Color("#9ca3af"));
    }

    protected void ApplyRuntimeTooltipTheme()
    {
        if (!IsUsingStitchUiTheme())
        {
            Theme = null;
            return;
        }

        Theme runtimeTheme = new();
        runtimeTheme.SetStylebox("panel", "TooltipPanel", StitchElementStyleLibrary.CreateDeepTooltipFrame(Math.Max(10, _theme.CornerRadius - 4)));
        runtimeTheme.SetColor("font_color", "TooltipLabel", new Color("#f4f9ff"));
        runtimeTheme.SetFontSize("font_size", "TooltipLabel", Math.Max(14, _theme.BodyFontSize - 5));
        Theme = runtimeTheme;
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

    protected StyleBoxFlat CreateSceneHeaderStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.92f, 0.94f, 0.98f, 0.24f),
            BorderColor = new Color(0.96f, 0.98f, 1.0f, 0.18f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ShadowColor = new Color(0, 0, 0, 0.26f),
            ShadowSize = 5,
            ContentMarginLeft = 6,
            ContentMarginTop = 1,
            ContentMarginRight = 6,
            ContentMarginBottom = 1
        };
    }

    protected Label CreateSceneHeaderTextLayer(string text, Color color, int xOffset, int yOffset, int fontSize)
    {
        Label label = new()
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.OffsetLeft = xOffset;
        label.OffsetTop = yOffset - 2;
        label.OffsetRight = xOffset;
        label.OffsetBottom = yOffset - 2;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    protected Control CreateCornerBracketOverlay(Color color, float thickness = 1.5f)
    {
        float strokeThickness = Mathf.Max(1.5f, thickness);
        float effectiveCornerRadius = Mathf.Max(_theme.CornerRadius, strokeThickness + 2.0f);
        float overlayOutset = strokeThickness;

        CornerBracketOverlayControl overlay = new(color, effectiveCornerRadius, strokeThickness, overlayOutset);
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.OffsetLeft = -overlayOutset;
        overlay.OffsetTop = -overlayOutset;
        overlay.OffsetRight = overlayOutset;
        overlay.OffsetBottom = overlayOutset;
        return overlay;
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
        label.AddThemeColorOverride("font_color", IsUsingStitchUiTheme() ? new Color("#5d605a") : new Color("#d7dde5"));
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
