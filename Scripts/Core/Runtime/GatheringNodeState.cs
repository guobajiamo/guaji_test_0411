using System;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 单个采集点（按 idle event_id 绑定）的运行时资源状态。
/// </summary>
public class GatheringNodeState
{
    public string EventId { get; set; } = string.Empty;

    public int AvailableAmount { get; set; }

    public double LastRecoverUnixSeconds { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
