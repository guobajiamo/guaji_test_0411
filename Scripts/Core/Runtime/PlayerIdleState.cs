using System;
using System.Collections.Generic;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// Runtime state for idle loop execution.
/// </summary>
public class PlayerIdleState
{
    /// <summary>
    /// Active idle event id.
    /// </summary>
    public string ActiveEventId { get; set; } = string.Empty;

    /// <summary>
    /// Whether idle loop is currently active.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Accumulated progress in seconds for the active idle loop.
    /// </summary>
    public double AccumulatedProgressSeconds { get; set; }

    /// <summary>
    /// Fractional output carried across settlements.
    /// </summary>
    public double PendingOutputFraction { get; set; }

    /// <summary>
    /// Last timestamp when idle progress was advanced.
    /// </summary>
    public long LastProgressUnixSeconds { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// Offline settlement hard cap in seconds.
    /// </summary>
    public int OfflineSettlementCapSeconds { get; set; } = 28800;

    /// <summary>
    /// Indicates the active idle event is waiting for gathering node recovery.
    /// </summary>
    public bool IsWaitingForGatheringRecovery { get; set; }

    /// <summary>
    /// Per gathering node runtime state (keyed by idle event id).
    /// </summary>
    public Dictionary<string, GatheringNodeState> GatheringNodeStates { get; } = new();
}
