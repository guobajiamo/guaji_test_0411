using System;

namespace Test00_0410.Core.Runtime;

/// <summary>
/// 玩家挂机运行时状态。
/// 这是挂机系统的唯一真相源，避免系统层自己再偷偷维护一份重复状态。
/// </summary>
public class PlayerIdleState
{
    /// <summary>
    /// 当前正在运行的挂机事件 ID。
    /// </summary>
    public string ActiveEventId { get; set; } = string.Empty;

    /// <summary>
    /// 是否处于挂机进行中。
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 当前读条已经积累了多少秒。
    /// 这是在线运行和离线补算都会修改的核心字段。
    /// </summary>
    public double AccumulatedProgressSeconds { get; set; }

    /// <summary>
    /// 挂机产出的小数余量。
    /// 因为技能表里的单次产出不一定是整数，所以这里把未满 1 的部分先存起来，
    /// 下次结算时再继续累计，避免长期挂机时损失精度。
    /// </summary>
    public double PendingOutputFraction { get; set; }

    /// <summary>
    /// 上一次真正推进挂机进度的时间戳。
    /// 恢复存档时，可以拿它来计算离线经过了多久。
    /// </summary>
    public long LastProgressUnixSeconds { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// 为了避免离线挂机无限膨胀，这里预留一个最大离线补算秒数。
    /// 例如 8 小时 = 28800 秒。
    /// </summary>
    public int OfflineSettlementCapSeconds { get; set; } = 28800;
}
