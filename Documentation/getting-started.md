# Getting started

## 1. Create a sound event

Right click in the Project window and choose **Create > AudioMW > Sound Event**,
or select several audio clips and use **Assets > AudioMW > Create Sound Event
From Selection**.

Fill in the inspector:

- **Clips** — one or more clips. With several clips, pick a selection mode.
- **Selection mode** — `RandomNoRepeat` avoids playing the same clip twice in a
  row and is the right default for footsteps, impacts and other repeated sounds.
- **Volume / pitch randomisation** — small ranges here remove the machine-gun
  effect from repeated one shots.
- **Spatial blend** — `0` for UI and music, `1` for world sounds.

## 2. Play it

```csharp
using AudioMW;

public class Footsteps : MonoBehaviour
{
    [SerializeField] private SoundEvent step;

    private void OnStep()
    {
        AudioSystem.PlayAttached(step, transform);
    }
}
```

Three entry points cover most cases:

| Call | Use for |
|---|---|
| `AudioSystem.Play(evt)` | 2D sounds, UI, non-positional |
| `AudioSystem.PlayAtPosition(evt, pos)` | one shots at a fixed world position |
| `AudioSystem.PlayAttached(evt, transform)` | sounds that follow a moving object |

`PlayAttached` stops the voice automatically if the target is destroyed, which
avoids sounds stranded at the world origin.

For designer-driven setups add a **Sound Emitter** component instead of writing
code, and call `Play` from an animation event or UnityEvent.

## 3. Add a parameter

Create a **SoundParameter** asset with a range, for example `0` to `1` for a
tension value or `0` to `120` for vehicle speed.

On the sound event, add a parameter binding: pick the parameter, choose whether
it drives volume or pitch, and draw a curve. **The curve output is a multiplier**
applied on top of the event's base value, so a flat curve at `1` changes nothing
and a curve ending at `2` doubles the pitch.

Drive it from gameplay:

```csharp
AudioSystem.SetParameter(speed, rigidbody.linearVelocity.magnitude);
```

Per-voice overrides are available when one instance needs its own value:

```csharp
Voice voice = AudioSystem.PlayAttached(engine, car.transform);
AudioSystem.SetVoiceParameter(voice, speed, car.Speed);
```

A per-voice value always wins over the global one.

## 4. Play music

Create a **Music Track**, set tempo and time signature, assign an intro clip and
a loop clip, then:

```csharp
AudioSystem.PlayMusic(track);
```

The intro and loop are scheduled on the audio thread ahead of time, so the join
is sample accurate rather than frame accurate. See
[music.md](music.md) for layers, transitions and stingers.

## 5. Manage memory

Group events into a **Sound Bank** and load or unload them around level
boundaries:

```csharp
AudioSystem.LoadBank(levelBank);
AudioSystem.UnloadBank(previousBank);
```

Banks here are a convenience over Unity's own clip loading. There is no build
step and no separate bank file: the bank is an ordinary asset listing events.

## 6. Check your work

- **Window > AudioMW > Event Debugger** — every play request with the reason it
  was rejected and the full volume chain.
- **Window > AudioMW > Import Auditor** — load types, memory and import
  mistakes.
- **Window > AudioMW > Loudness Audit** — integrated LUFS and true peak per clip.
- **Window > AudioMW > Audio Browser** — navigate every AudioMW asset, preview
  waveforms and read per-clip loudness without leaving the window.
- Add an **Audio Debug HUD** component to see live voices in the running game.

## 7. Pick a platform tier

Apply a tier at boot, before anything plays, to set the voice ceiling, DSP
buffer size and feature toggles for the target platform:

```csharp
AudioSystem.ApplyTier(mobileTier);
```

See [optimisation.md](optimisation.md) for tiers, banks, streamed clips and the
auditing tools.
