# Runtime profiling and lifecycle checks

2026-11-05. These are local macOS editor measurements, not device budgets or
player-build results. Test code lives in RuntimeLoadTests / RuntimeLifecycleTests.

## Measured scenarios

Each warm measurement prewarms a 32-voice pool and runs 64 trigger/stop warmups,
then 2,000 synchronous tracked trigger/stop pairs. Debugger logging is disabled.
The Simple scenario resolves one clip; Layered resolves two parameter-bound
children; Switch resolves one matching child with initial local parameters.
Stop and cleanup are included. Clip import, source construction, warmup and
report serialization are outside the timed region.

The profiler counter is calibrated against a known 64 KiB managed allocation.
GC.GetAllocatedBytesForCurrentThread returned zero for that control on this
Unity Mono setup, so its zero was rejected. GC Allocated In Frame supplied a
usable counter on Unity 6. It is frame-wide, not an isolated thread counter;
background/editor allocations may be included. An unavailable measurement is
reported as -1, never as proof of zero allocations.

| Unity 6000.3.21f1, 2,000 pairs | Before instance/buffer reuse | After reuse |
|---|---:|---:|
| Simple, allocated bytes | 1,096,000 | 0 |
| Layered, allocated bytes | 1,464,000 | 0 |
| Switch, allocated bytes | 1,176,000 | 0 |
| Simple, elapsed ms | 5.252 | 4.932 |
| Layered, elapsed ms | 10.374 | 9.470 |
| Switch, elapsed ms | 7.412 | 5.914 |

These are individual observations on the same host; some validation runs had
another editor process active, so timings are not a controlled CPU benchmark. They do not establish a
stable speedup percentage, general zero-GC playback or frame-rate guarantees.
Cold starts, changing container complexity/binding counts, debug records,
new parameter keys, music transitions and optional loaders can still allocate.

A separate saturated burst starts 2,000 two-layer events without stopping
between triggers. With a 32-voice cap it retains 16 live logical instances,
32 physical voices and bounded cached instances after StopAll.

## Lifetime contract

Voice release notifications run after the surrounding runtime operation has
committed its collection changes. Natural completion is dispatched at the end
of the runtime tick. Callback code can start/stop audio; shutdown rejects new
requests while it flushes releases. A raw Voice remains a pooled object and
should not be retained; SoundHandle is the supported retained reference.

Playback caches retain at most MaxVoices logical instances and clear event,
clip, attachment and parameter references before caching. RebuildPool discards
the caches. Cooldown history is swept once per second and releases destroyed
or expired event entries. Attached 2D and 3D sounds end when their target is
lost, including scene unload. Runtime recreation cannot alias an old handle to
a new sound. Shutdown clears the duck release state immediately; normal VO
stop retains its authored release behaviour.

## Using Unity Profiler

Inspect AudioMW.Play and AudioMW.Tick scopes in CPU Usage alongside GC Alloc.
Keep debugger enabled when measuring a real authoring workflow, then compare
with it disabled to understand the logging cost. Capture player builds on the
actual target device before choosing voice budgets. CPU trigger timing does
not measure audio-thread DSP/mixer cost, resident audio memory or streaming IO.
