# CompleteTimer

A timer system for mid-core and larger games (farming, RPG, puzzle games with Lives): multi-stage timers, production queues, self-regenerating resources (Lives/Energy), correct offline progress against real time, and persistence through DataFlow.

- **One scheduler** owns every timer in a min-heap ordered by deadline. Each frame it only looks at the top of the heap.
- **It stores absolute moments (UTC ms), never remaining time.** Remaining time is always derived from `now`, so killing the app, saving early or late, or changing `timeScale` cannot change the result.
- **Events are not lost.** An event with no listener is kept and delivered as soon as one registers. A finished timer stays `Completed` until it is `Release`d.
- **The core is plain C#** (`noEngineReferences`) and is testable in EditMode with a fake clock.

## Folder layout

| Folder | Assembly | Role |
|---|---|---|
| `CompleteTimer/` (this folder) | `...TimeServices.CompleteTimer` (`noEngineReferences`) | Plain C# core |
| `CompleteTimer/Clock/` | same | `ITimeProvider`, `TimerClock`, `ClockAnomaly` |
| `CompleteTimer/Scheduling/` | same | `TimerScheduler`, `TimerSpec`, `TimerHandle`, `TimerEvent`, `ITimerListener`, `TimerState`, snapshots; `TimerMinHeap` and `TimerRecord` are internal |
| `CompleteTimer/Production/` | same | `ProductionQueue`, `ProductionQueueRegistry`, snapshots |
| `CompleteTimer/Regeneration/` | same | `RegenerationCounter`, `RegenerationCounterRegistry`, snapshots |
| `CompleteTimer/Tests/Editor/` | `...CompleteTimer.Tests` | EditMode tests and benchmarks |
| `../CompleteTimerIntegration/` | Assembly-CSharp (no asmdef) | Unity, VContainer and DataFlow integration: `CompleteTimerRegistration`, `CompleteTimerRuntime`, `CompleteTimerDataController`, `TimerSaveDataV1`, `TimerSaveMapper` |

`CompleteTimerIntegration/` must stay a **sibling** of this folder, never nested inside it. The asmdef here has `noEngineReferences: true`, so any file that uses UnityEngine, VContainer or MessagePack and sits inside this folder would fail to compile.

### Namespaces

Namespaces follow the same convention as UISystem: `<asmdef rootNamespace>.<folder path under Assets>`. For example, the types in `CompleteTimer/Scheduling/` live in:

```
DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer
    .DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
```

(written as one dotted name). `Clock`, `Production` and `Regeneration` work the same way, and the tests use the `...CompleteTimer.Tests` root plus their folder path. `CompleteTimerIntegration/` has no asmdef, so its namespace is just its folder path: `DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimerIntegration`.

## Quick start

### Registration

```csharp
protected override void Configure(IContainerBuilder builder)
{
    DataFlowScope dataFlow = builder.AddDataFlow();
    builder.AddCompleteTimer(dataFlow);   // clock, scheduler, registries, save controller, runtime
}
```

`CompleteTimerRuntime` registers the scheduler's tick, checks clock drift every frame and handles `Application.focusChanged`. A working example: `Assets/Scripts/Test/SampleProjectLifetimeScope.cs`.

### Single-stage and multi-stage timers

```csharp
// Single stage: 60 seconds.
scheduler.TryStart(new TimerSpec { Key = "crop/12", Channel = CropChannel, DurationMs = 60_000 }, out TimerHandle h);

// A crop with three stages: 1', 3', 8' (12' in total).
scheduler.TryStart(new TimerSpec
{
    Key = "crop/13",
    Channel = CropChannel,
    StageDurationsMs = new long[] { 60_000, 180_000, 480_000 }
}, out TimerHandle tree);

scheduler.AddChannelListener(CropChannel, this);   // this : ITimerListener

public void OnTimerEvent(in TimerEvent e)
{
    // e.Type: StageChanged or Completed. e.AtMs is the theoretical moment of the event.
    // e.IsLate: delivered more than 1 second after AtMs (offline catch-up); you may skip animations.
    // e.IsReplay: re-delivery of an event that was already delivered, or never got delivered, in a previous session.
}
```

UI only **queries** what it currently shows; there is no per-frame callback:

```csharp
scheduler.GetRemainingMs(h);          scheduler.GetProgress01(h);
scheduler.GetCurrentStage(tree);      scheduler.GetStageRemainingMs(tree);
scheduler.GetStageProgress01(tree);   scheduler.GetEndUtcMs(tree);
```

The stage queries are **computed from `now`** and do not read dispatch state, so they are correct in the window between `Restore` and the first `Tick`.

Controls: `Pause`, `Resume`, `SpeedUp(h, ms)`, `Delay(h, ms)`, `SkipCurrentStage(h)`, `CompleteNow(h)`, `Cancel(h)`, `Release(h)`. `SpeedUp`, `SkipCurrentStage` and `CompleteNow` are **synchronous**: the events fire immediately, without waiting for a `Tick`.

### Production queues

```csharp
ProductionQueue bakery = queues.Create("bakery", capacity: 4, channel: BuildingChannel);
bakery.TryEnqueue("bread", durationMs: 120_000);      // starts immediately if the queue is idle
bakery.OnItemCompleted += (queue, itemId, atMs) => { /* update the UI */ };

List<ProductionQueueOutput> done = new();
bakery.ClaimOutputs(done);                             // collect finished items
```

Only the active item owns a timer. When it finishes, the next item starts at the previous item's `AtMs` (not at "now"), so after a long offline gap the whole queue drains in a single frame with every item's moment exactly right.

### Lives / Energy

```csharp
RegenerationCounter lives = regenerations.Create("lives", max: 5, intervalMs: 30 * 60_000);

long now = scheduler.NowMs;
lives.TryConsume(1, now);
lives.GetCurrent(now);
lives.GetNextRegenRemainingMs(now);
lives.OnRegenerated += (gained, current, lastAtMs) => { };
```

This is deliberately not a multi-stage timer: players spend Lives while a regeneration interval is partly elapsed, and that progress must not be lost. `RegenerationCounter` uses integer arithmetic and keeps the remainder (`AnchorMs += n × IntervalMs`), so accumulated error is zero. Being offline for 70 minutes with 1/5 Lives yields 3/5, and the next Life is exactly 20 minutes away. Any offline gap costs O(1), because there is only one timer, for the next regeneration tick.

## Timer lifecycle

`Running` → `Completed` → (`Release`) → `Free`. `Pause` moves a timer from `Running` to `Paused`, and `Resume` moves it back to `Running`.

- `Completed` is a **state**, not a one-shot event. The timer stays in the data and is reported again every session (`IsReplay`) until gameplay calls `Release(h)`.
- `Release` frees the key, bumps `Version` so every old handle becomes invalid, and returns the slot to the free list for reuse. The next save no longer contains that timer.
- **Fire-and-forget timers** (buffs, cooldowns, expiring quests): set `TimerSpec.AutoRelease = true`. The scheduler releases the timer once its `Completed` event has been delivered to at least one listener. If delivery was not possible, the timer keeps `Completed` and is replayed next session.
- `ProductionQueue` and `RegenerationCounter` release their own timers.
- Forgetting `Release` makes the data grow. `CompletedCount` reports how many timers are in that state, and `OnWarning` fires when it exceeds `CompletedWarningThreshold` (default 1000).
- Stale handles, and handles that point at a slot that was never used, do not throw: queries return defaults and `GetState` returns `Free`.

## Delivery guarantees

The semantics are **at-least-once**. **Grant rewards on `Release`/`Claim`, never inside the callback**, because the same event can arrive again (`IsReplay`).

1. **Buffer:** an event with no listener (per-timer or per-channel) is kept in `AtMs` order. `AddChannelListener` and `SetListener` immediately deliver the matching pending events.
2. **Replay:** on `Restore`, a `Completed` timer that has not been released emits `Completed` again with `IsLate` and `IsReplay`. A `Running` timer that expired while offline is handled in the first `Tick`, including every `StageChanged` it skipped, in `AtMs` order.
3. **Direct queries:** `GetState`, `GetCurrentStage` and `GetEndUtcMs` are always correct and never depend on events.

Each due timer is dispatched right after it is popped (the per-timer listener first, then the channel listener), so offline ordering is globally correct and callbacks may safely create, cancel or release timers. `maxEventsPerTick` (default 10,000) caps the number of events per `Tick` so one frame cannot stall.

## Clock and time tampering

`TimerClock : ITimeProvider` anchors to a monotonic source (a `Stopwatch` by default), so changing the device clock during a session does not make timers jump. The time source can be replaced by any other `ITimeProvider` (for example server time).

| Situation | Behavior |
|---|---|
| App closed or killed | `StartMs` is unchanged and `now` advances by exactly the offline time |
| `Pause`, then offline | `Resume` shifts the moments by `now − PausedAtMs`, so the timer stood still for the whole pause |
| Device deep sleep, monotonic source stops | `ReanchorFromDevice()` on focus regain, plus a per-frame drift check (`CheckDrift`, tolerance `DriftToleranceMs`, default 2000) |
| Clock set backwards | `now` never goes backwards (`ApplyFloor(LastSeenUtcMs)` on load, clamped during the session). Timers stand still and `OnAnomaly(Backward)` fires |
| Clock set forwards | **Cannot be detected without a server.** The jump is accepted, and `OnAnomaly(ForwardJump)` fires when it exceeds the tolerance |
| With a server | `SyncWithServer(ms)` anchors to server time. After returning from the background `IsServerSynced` becomes `false` and `OnResyncRequired` fires; the game may block harvesting or speed-ups until it resyncs |

Anything that costs real money or gems should be gated on server time, because the device clock cannot be trusted against cheating.

## Persistence

`CompleteTimerDataController` (id `complete_timer`, schema version 1) saves through DataFlow using MessagePack.

- The scheduler and registries are the runtime source of truth. `Data` is only a snapshot, refreshed in `OnBeforeSave` right before a flush.
- Every structural change (create, cancel, pause, complete, release, ...) raises `OnStructureChanged` and marks the data dirty. `SaveScheduler` by default only flushes on suspend or quit (enable periodic flushing with `AddDataFlow(autoSaveIntervalSeconds)`), so **a gem-spending action must call `CompleteTimerDataController.SaveNow()`** together with the wallet save, to avoid a mismatch that could duplicate items.
- `Restore` skips corrupt entries (missing key, no stages, duplicate key) and reports them through `OnWarning`.
- `TimerSaveMapper` overwrites the existing `TimerEntryV1` instances instead of creating new ones, so repeated saves do not allocate while the timer count is unchanged.

Two things to keep in mind when using snapshots:

- `TimerEntrySnapshot` is a struct. Its `StageEnds` array is **shared with the scheduler and immutable**: read it, never write into it.
- **Durations are copied into the timer when it is created.** A designer changing config later will not move the finish time of crops that are already planted. Apply changes retroactively with a schema migration.

To change the data shape, add `TimerSaveDataV2` and a migrator; never edit `V1` once it has shipped.

## Performance

Numbers come from `Tests/Editor/TimerSchedulerBenchmarkTests.cs`: median of 10 runs in the Unity Editor (Mono). There are no IL2CPP or on-device measurements yet.

| | 100,000 timers | 2,000 timers |
|---|---|---|
| Construct the scheduler | 0.7 ms, 6.2 MB | 0.03 ms, 130 KB |
| First `TryStart` (creates the records) | ~59 ms, ~15 MB | ~1 ms, ~0.4 MB |
| Tick with nothing due | ~22 ns/tick, 0 B | same |
| One `Tick` completing every timer | 29 ms | 0.47 ms |
| `CaptureSnapshot` | 5.9 ms, 0 B | 0.12 ms, 0 B |
| `Release` | 15 ms | 0.29 ms |
| `GetRemainingMs`, `TryGetHandle` | ~60–80 ns/call | same |

- Each frame reads the clock once and looks at the top of the heap (O(1)); a due timer is popped (O(log N)). The hot path (`Tick`, queries, `Release`, `CaptureSnapshot`) does not allocate.
- The heap keeps the sort key inside each node and a separate position table, so sifting never has to read a `TimerRecord`. That is what makes mass completion fast.
- `TimerRecord` objects are created lazily when a slot is first handed out, so a large `initialCapacity` only costs the pointer array.
- Single-stage timers with the same `DurationMs` share one `StageEnds` array (up to 1,024 distinct durations). This helps when many timers have the same length; when every duration is different it costs about 100 KB once.
- Memory is measured with `ProfilerRecorder` ("GC Allocated In Frame"). Do not use `GC.GetAllocatedBytesForCurrentThread`: it always returns 0 on Unity's Mono.

A farming game or RPG typically has a few hundred simultaneous timers; at that size every operation stays under 1 ms.

## Known limitations

- **Not thread-safe.** Call it from the main thread.
- `TryStart` returns `false` when the key already exists or the duration is ≤ 0.
- Forward clock changes are not blocked without a server (see the clock section).
- `SpeedUp` and `Delay` adjust the **total** time, and every later moment moves by the same amount. For a percentage speed-up, compute `ms` from `GetRemainingMs` or `GetStageRemainingMs` in game code.
- There are no stages that require an extra action to pass (for example watering). This can be added as a pause-at-boundary option inside the scheduler, because gameplay cannot pause at a boundary that is already in the past.
- Saving `Queues` and `Regenerations` still allocates new objects on every save; only `Timers` are reused.

## Tests

The tests live in `Tests/Editor/` (EditMode, using `FakeClock`, which sets both the device time and the monotonic source by hand):

| File | Covers |
|---|---|
| `TimerSchedulerTests` | Completion at the right moment, pause/resume, speed-up, safe callbacks, duplicate keys, offline catch-up, `maxEventsPerTick` |
| `MultiStageTierTests` | Three-stage crop: stage queries, `SkipCurrentStage`, pausing across offline time |
| `RealTimeElapsedTests` | Remaining time matches real time regardless of when the save happened |
| `DeliveryGuaranteeTests` | Event buffering, late listener registration, replay |
| `ReleaseLifecycleTests` | `Release`, `AutoRelease`, warning about leftover `Completed` timers |
| `TimerSnapshotTests` | Capture/Restore, corrupt entries |
| `TimerSchedulerCapacityTests` | Capacity growth, slot reuse, forged handles, ordering on equal deadlines |
| `ProductionQueueTests`, `RegenerationCounterTests`, `TimerClockTests` | One per component |
| `TimerSchedulerBenchmarkTests` | 100k and 2k timer benchmarks with exact timing and allocation |

Run them from the Test Runner (EditMode), or with the Editor open:

```bash
unity command run_tests --mode EditMode --filter "DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer"
```

The benchmark prints its results with `TestContext.WriteLine`; read them from `TestResults.xml` (in the project's `LocalLow` folder). After changing the scheduler, re-run the benchmark as well to catch allocation regressions: it asserts that the hot path stays under 1 B per operation.

The integration layer (`CompleteTimerIntegration/`) lives in Assembly-CSharp, so it has no permanent unit tests yet.

## Notes for maintainers

- Repo style: `this.` for member access, `_camelCase` for private fields, XML comments that explain *why*, no LINQ on the hot path.
- `System.Collections.Generic.PriorityQueue` does not exist in .NET Standard 2.1 (Unity 6000.3), which is why the heap is hand-written.
- The core never uses Unity's `Debug`; warnings go through the `OnWarning` callback, and the integration layer forwards them to `Debug.LogWarning`.
- After changing `NextDeadlineMs` of a record that is in the heap, call `_heap.Update` right away. The heap keeps a copy of the deadline in its node and will not notice on its own.
- `TimerRecord.StageEnds` is immutable: replace the whole array, never write into an element. Shared single-stage arrays and snapshots that hand the array out both rely on this.
- Clock drift (deep sleep, clock changes) is hard to reproduce by hand; add a test with `FakeClock` instead of trying it on a device.
- The previous implementation (`TimerOrchestrator` and its related files) was deleted and rewritten from scratch because of fundamental bugs: converting seconds to milliseconds with `TimeSpan.Milliseconds` (which only returns the fractional part), subtracting the elapsed time again every frame and overwriting the result, new timers never being registered for ticking or saving, and removing a timer inside a callback breaking the iteration loop.
