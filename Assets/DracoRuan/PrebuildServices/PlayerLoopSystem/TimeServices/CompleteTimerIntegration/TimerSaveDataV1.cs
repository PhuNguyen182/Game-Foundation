using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.LocalData;
using MessagePack;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.CompleteTimerIntegration
{
    /// <summary>One persisted timer entry, mirroring <see cref="Scheduling.TimerEntrySnapshot"/>.</summary>
    [Serializable]
    [MessagePackObject]
    public sealed class TimerEntryV1
    {
        [Key(0)] public string Key { get; set; }
        [Key(1)] public int Channel { get; set; }
        [Key(2)] public int State { get; set; }
        [Key(3)] public long StartMs { get; set; }
        [Key(4)] public long DurationMs { get; set; }
        [Key(5)] public long PausedAtMs { get; set; }
        [Key(6)] public long[] StageEnds { get; set; }
        [Key(7)] public int DispatchedStage { get; set; }
        [Key(8)] public bool AutoRelease { get; set; }
        [Key(9)] public long CompletedAtMs { get; set; }
        [Key(10)] public bool CompletedDelivered { get; set; }
    }

    /// <summary>One persisted production-queue item, mirroring <see cref="Production.ProductionQueueItemSnapshot"/>.</summary>
    [Serializable]
    [MessagePackObject]
    public sealed class ProductionQueueItemEntryV1
    {
        [Key(0)] public string ItemId { get; set; }
        [Key(1)] public long DurationMs { get; set; }
    }

    /// <summary>One persisted production-queue output, mirroring <see cref="Production.ProductionQueueOutputSnapshot"/>.</summary>
    [Serializable]
    [MessagePackObject]
    public sealed class ProductionQueueOutputEntryV1
    {
        [Key(0)] public string ItemId { get; set; }
        [Key(1)] public long AtMs { get; set; }
    }

    /// <summary>One persisted production queue, mirroring <see cref="Production.ProductionQueueSnapshot"/>.</summary>
    [Serializable]
    [MessagePackObject]
    public sealed class ProductionQueueEntryV1
    {
        [Key(0)] public string QueueKey { get; set; }
        [Key(1)] public int Capacity { get; set; }
        [Key(2)] public int Channel { get; set; }
        [Key(3)] public List<ProductionQueueItemEntryV1> Items { get; set; } = new();
        [Key(4)] public List<ProductionQueueOutputEntryV1> Outputs { get; set; } = new();
        [Key(5)] public string ActiveTimerKey { get; set; }
        [Key(6)] public long Sequence { get; set; }
        [Key(7)] public bool IsPaused { get; set; }
    }

    /// <summary>One persisted regeneration counter, mirroring <see cref="Regeneration.RegenerationSnapshot"/>.</summary>
    [Serializable]
    [MessagePackObject]
    public sealed class RegenerationEntryV1
    {
        [Key(0)] public string Key { get; set; }
        [Key(1)] public long Stored { get; set; }
        [Key(2)] public long Max { get; set; }
        [Key(3)] public long IntervalMs { get; set; }
        [Key(4)] public long AnchorMs { get; set; }
    }

    /// <summary>
    /// Complete Timer save data, schema version 1.
    /// </summary>
    /// <remarks>
    /// One class per schema version, never edited after release - see <c>RiseProgressDataV1</c> for
    /// the pattern this follows. <see cref="LastSeenUtcMs"/> persists <c>TimerClock.LastSeenUtcMs</c>
    /// so <c>TimerClock.ApplyFloor</c> can reject a device clock rolled back between sessions.
    /// </remarks>
    [Serializable]
    [MessagePackObject]
    public sealed class TimerSaveDataV1 : IGameData
    {
        [Key(0)] public List<TimerEntryV1> Timers { get; set; } = new();
        [Key(1)] public List<ProductionQueueEntryV1> Queues { get; set; } = new();
        [Key(2)] public long LastSeenUtcMs { get; set; }
        [Key(3)] public List<RegenerationEntryV1> Regenerations { get; set; } = new();
    }
}
