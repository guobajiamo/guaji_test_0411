using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Test00_0410.Core.Definitions;

namespace Test00_0410.Systems;

/// <summary>
/// Buff/道具加成系统。
/// 对外提供统一倍率读取接口，支持常驻或限时倍率条目。
/// </summary>
public partial class BuffSystem : Node
{
    public sealed class ActiveBuffView
    {
        public string BuffId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public bool IsTimed { get; init; }

        public double RemainingSeconds { get; init; }
    }

    private sealed class BuffMultiplierEntry
    {
        public string BuffId { get; init; } = string.Empty;

        public string StatId { get; init; } = string.Empty;

        public double Multiplier { get; set; } = 1.0;

        public double? ExpireAtUnixSeconds { get; set; }
    }

    private sealed class BuffMetadataEntry
    {
        public string BuffId { get; init; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public double? ExpireAtUnixSeconds { get; set; }
    }

    private readonly Dictionary<string, BuffMultiplierEntry> _entriesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BuffMetadataEntry> _metadataByBuffId = new(StringComparer.Ordinal);

    public double GetMultiplier(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
        {
            return 1.0;
        }

        RemoveExpiredEntries();

        double result = 1.0;
        foreach (BuffMultiplierEntry entry in _entriesByKey.Values)
        {
            if (!string.Equals(entry.StatId, statId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!double.IsFinite(entry.Multiplier))
            {
                continue;
            }

            result *= entry.Multiplier;
        }

        return result;
    }

    public void RefreshActiveBuffs()
    {
        RemoveExpiredEntries();
    }

    public void SetPersistentMultiplier(string buffId, string statId, double multiplier)
    {
        UpsertEntry(buffId, statId, multiplier, null);
        UpsertMetadata(buffId, buffId, string.Empty, null);
    }

    public void SetTimedMultiplier(string buffId, string statId, double multiplier, double durationSeconds)
    {
        if (durationSeconds <= 0.0)
        {
            RemoveMultiplier(buffId, statId);
            return;
        }

        double expireAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + durationSeconds;
        UpsertEntry(buffId, statId, multiplier, expireAtUnixSeconds);
        UpsertMetadata(buffId, buffId, string.Empty, expireAtUnixSeconds);
    }

    public void ApplyTimedBuff(
        string buffId,
        string displayName,
        string description,
        double durationSeconds,
        bool extendDurationOnReapply,
        IReadOnlyList<BuffStatModifierDefinition> statModifiers)
    {
        if (string.IsNullOrWhiteSpace(buffId)
            || durationSeconds <= 0.0
            || statModifiers.Count == 0)
        {
            return;
        }

        double nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        double expireAtUnixSeconds = nowUnixSeconds + durationSeconds;
        if (extendDurationOnReapply
            && _metadataByBuffId.TryGetValue(buffId, out BuffMetadataEntry? existingMetadata)
            && existingMetadata.ExpireAtUnixSeconds.HasValue)
        {
            double baseline = Math.Max(nowUnixSeconds, existingMetadata.ExpireAtUnixSeconds.Value);
            expireAtUnixSeconds = baseline + durationSeconds;
        }

        UpsertMetadata(buffId, displayName, description, expireAtUnixSeconds);
        foreach (BuffStatModifierDefinition statModifier in statModifiers)
        {
            if (string.IsNullOrWhiteSpace(statModifier.StatId))
            {
                continue;
            }

            UpsertEntry(buffId, statModifier.StatId, statModifier.Multiplier, expireAtUnixSeconds);
        }
    }

    public IReadOnlyList<ActiveBuffView> GetActiveBuffs()
    {
        RemoveExpiredEntries();

        double nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return _metadataByBuffId.Values
            .Select(metadata =>
            {
                bool isTimed = metadata.ExpireAtUnixSeconds.HasValue;
                double remaining = isTimed
                    ? Math.Max(0.0, metadata.ExpireAtUnixSeconds!.Value - nowUnixSeconds)
                    : 0.0;
                return new ActiveBuffView
                {
                    BuffId = metadata.BuffId,
                    DisplayName = string.IsNullOrWhiteSpace(metadata.DisplayName) ? metadata.BuffId : metadata.DisplayName,
                    Description = metadata.Description,
                    IsTimed = isTimed,
                    RemainingSeconds = remaining
                };
            })
            .OrderByDescending(buff => buff.IsTimed)
            .ThenBy(buff => buff.IsTimed ? buff.RemainingSeconds : double.MaxValue)
            .ThenBy(buff => buff.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    public void RemoveMultiplier(string buffId, string statId)
    {
        _entriesByKey.Remove(BuildEntryKey(buffId, statId));
        RemoveMetadataIfOrphan(buffId);
    }

    public void RemoveBuff(string buffId)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return;
        }

        List<string> keys = _entriesByKey.Keys
            .Where(key => key.StartsWith($"{buffId}@@", StringComparison.Ordinal))
            .ToList();
        foreach (string key in keys)
        {
            _entriesByKey.Remove(key);
        }

        _metadataByBuffId.Remove(buffId);
    }

    private void UpsertEntry(string buffId, string statId, double multiplier, double? expireAtUnixSeconds)
    {
        if (string.IsNullOrWhiteSpace(buffId) || string.IsNullOrWhiteSpace(statId))
        {
            return;
        }

        _entriesByKey[BuildEntryKey(buffId, statId)] = new BuffMultiplierEntry
        {
            BuffId = buffId,
            StatId = statId,
            Multiplier = multiplier,
            ExpireAtUnixSeconds = expireAtUnixSeconds
        };
    }

    private void UpsertMetadata(string buffId, string displayName, string description, double? expireAtUnixSeconds)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return;
        }

        _metadataByBuffId[buffId] = new BuffMetadataEntry
        {
            BuffId = buffId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? buffId : displayName,
            Description = description,
            ExpireAtUnixSeconds = expireAtUnixSeconds
        };
    }

    private void RemoveExpiredEntries()
    {
        if (_entriesByKey.Count == 0 && _metadataByBuffId.Count == 0)
        {
            return;
        }

        double nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        List<string> expiredEntryKeys = _entriesByKey
            .Where(pair => pair.Value.ExpireAtUnixSeconds.HasValue && pair.Value.ExpireAtUnixSeconds.Value <= nowUnixSeconds)
            .Select(pair => pair.Key)
            .ToList();
        foreach (string expiredEntryKey in expiredEntryKeys)
        {
            _entriesByKey.Remove(expiredEntryKey);
        }

        List<string> expiredMetadataKeys = _metadataByBuffId
            .Where(pair => pair.Value.ExpireAtUnixSeconds.HasValue && pair.Value.ExpireAtUnixSeconds.Value <= nowUnixSeconds)
            .Select(pair => pair.Key)
            .ToList();
        foreach (string expiredMetadataKey in expiredMetadataKeys)
        {
            _metadataByBuffId.Remove(expiredMetadataKey);
        }

        List<string> orphanMetadataKeys = _metadataByBuffId.Keys
            .Where(buffId => !_entriesByKey.Values.Any(entry => string.Equals(entry.BuffId, buffId, StringComparison.Ordinal)))
            .ToList();
        foreach (string orphanMetadataKey in orphanMetadataKeys)
        {
            _metadataByBuffId.Remove(orphanMetadataKey);
        }
    }

    private void RemoveMetadataIfOrphan(string buffId)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            return;
        }

        bool stillHasEntries = _entriesByKey.Values.Any(entry => string.Equals(entry.BuffId, buffId, StringComparison.Ordinal));
        if (!stillHasEntries)
        {
            _metadataByBuffId.Remove(buffId);
        }
    }

    private static string BuildEntryKey(string buffId, string statId)
    {
        return $"{buffId}@@{statId}";
    }
}
