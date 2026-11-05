# Gain authoring in decibels

Event, music-track and voice-line volume, and child-event gain, now use a native
fader with a numeric dB field. The underlying serialized floats remain linear
amplitude values. Opening an Inspector, rebinding it or updating the package does
not convert or rewrite existing values. The existing Volume and Gain APIs remain
linear and keep their original 0–1 range.

| Setting | Linear amplitude | Meaning |
|---|---:|---|
| 0 dB | 1 | Original level |
| about -6.02 dB | 0.5 | Half the amplitude |
| -20 dB | 0.1 | One tenth of the amplitude |
| -80 dB | 0.0001 | Quiet, still nonzero |
| −∞ | 0 | Full silence |

The fader spans -80 to 0 dB. Type a lower numeric value to retain a quieter level;
the fader floor is not a mute threshold. Type `-inf`, `-infinity`, `-∞` or `mute`
for silence. The field displays −∞ for a stored zero and — for mixed selections.
Invalid text/NaN is rejected. Positive dB input is limited to 0 dB because these
asset controls retain their existing unity-gain ceiling. Number input follows the
current locale and also accepts an invariant decimal point. These are amplitude
gain controls, not LUFS measurements, automatic normalization or a peak limiter.

The control supports native SerializedProperty binding, multi-object edits and
Undo/Redo. Selecting a mixed group and typing the first object's existing value
still applies that value to the entire selection. The IMGUI drawer follows the
same storage and conversion contract for custom inspectors using IMGUI.

## Per-trigger variation

New events from Assets > Create > AudioMW > Sound Event, Create From Selection,
the setup window and SoundEvent.CreateRuntime start with **Decibels** variation.
In Sound > Variation, Min gain / Max gain are relative dB offsets. For example,
a -12 dB event with -3 to +3 dB variation resolves between -15 and -9 dB. Random
selection is uniform in dB; parent containers resolve their own random offset
once per instance and multiply it into the selected children.

Each event's randomized base gain still clamps to 0–1 before inheritance, as it
did previously. Positive offsets do not create asset gain above 0 dB. A zero base
stays silent even with positive variation. Offsets can be authored from -80 to
+24 dB. Inverted ranges are evaluated in ascending order and produce an Inspector
message asking the author to fix the range.

Existing serialized events retain **Linear Offsets** when the new mode field is
absent, including their original random sequence and additive/clamped behaviour.
Raw ScriptableObject.CreateInstance<SoundEvent>() also starts with that serialized
default; use CreateRuntime for the new-event defaults. Switching variation units
is explicit and uses independent ranges. Old offsets are kept when selecting
Decibels, so switching back restores them. Changing units does not mathematically
translate one distribution into the other.

## Code

```csharp
soundEvent.VolumeDb = -12f;
soundEvent.VolumeRandomDbMin = -2f;
soundEvent.VolumeRandomDbMax = 2f;
soundEvent.VolumeVariationMode = VolumeVariationMode.Decibels;
track.VolumeDb = -6f;
voiceLine.VolumeDb = -3f;
child.GainDb = -6f;
AudioSystem.Music.SetVolumeDb(-12f);
```

MusicPlayer.VolumeDb reports its runtime multiplier. SetVolumeDb does not change
the MusicTrack asset. AudioGain.ToDecibels/FromDecibels convert amplitude ratios
using 20 log10 / 10^(dB/20); conversions themselves can describe positive gain.
Zero or nonpositive input to ToDecibels is negative infinity. FromDecibels maps
negative infinity and NaN to zero. The asset/runtime dB setters retain their
normal 0–1 volume clamps.

## Diagnose the final level

Event Debugger retains the linear chain and adds a dB view. Both include base
(inherited/randomized event gain), parameter gain, blend weight and occlusion.
FinalVolumeDb describes the actual AudioSource.volume at capture time. If the
product exceeds 1, the record also reports the unclamped product and the 0 dB
volume ceiling. This is a source-volume clamp, not rendered-signal peak analysis.
A muted factor is displayed as −∞ rather than a rounded linear zero.

Debugger entries are captured at the trigger (or an explicit Record call), not
continuous meter snapshots. Later parameter/occlusion changes are visible in a
new capture, not retroactively in older records. Mixer processing, spatial
attenuation, the source waveform, master volume and DSP transition envelopes
remain outside this per-voice source-volume chain.

## UI basis and verification

The combined fader and numeric entry follow Unity's
[Slider](https://www.foundations.unity.com/components/slider),
[Numeric Field](https://www.foundations.unity.com/components/numeric-field) and
[Label](https://www.foundations.unity.com/components/label) guidance. Native binding
follows Unity's [custom field examples](https://github.com/Unity-Technologies/ui-toolkit-manual-code-examples).

EditMode checks cover conversion, an old-format YAML asset with the new fields
removed, deterministic variation, silence, low-level preservation, actual panel
binding, text/fader multi-edit and Undo/Redo. PlayMode checks cover inherited dB
gain, runtime-only music gain, silence diagnostics, occlusion and volume clamping.
The new Inspector was visually checked in Unity 6 dark theme at 320 and 400
points wide. Light theme, screen-reader behaviour, full IMGUI interaction and
human listening remain separate acceptance work.
