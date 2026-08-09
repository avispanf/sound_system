# Memory, tiers and auditing

## Platform tiers

An `AudioTierConfig` bundles the settings that differ between a phone and a
desktop build: voice ceiling, how many voices to prewarm, DSP buffer size,
output sample rate, real voice count and feature toggles.

```csharp
AudioSystem.ApplyTier(mobileTier);
```

Three presets are available from code as starting points:

| Preset | Voices | DSP buffer | Sample rate | Spatial |
|---|---|---|---|---|
| Mobile 2D | 16 | 1024 | 24 kHz | off |
| Standard 3D | 32 | 512 | 48 kHz | on |
| High-End | 96 | 256 | 48 kHz | on |

Applying a tier rewrites the audio configuration and rebuilds the voice pool.
**Do this at boot, before anything plays**: changing the audio configuration
restarts Unity's audio system and stops every playing source. The applier skips
the reset entirely when nothing would actually change, so calling it twice with
the same tier is harmless.

Prewarming creates voice objects up front so the first busy moment of a level
does not allocate. It costs memory in exchange for a flatter frame time.

## Banks

A `SoundBank` lists events and loads their clips together.

```csharp
AudioSystem.LoadBank(levelBank);
AudioSystem.UnloadBank(previousBank);
```

Loading walks every event in the bank, collects clips from both simple and
blend containers, deduplicates them and calls `LoadAudioData`. Unloading
reverses it. There is no build step and no bank file: the bank is an ordinary
asset listing events.

## Streamed clips

Clips that should not ship inside the build are referenced by address rather
than by object. Resolution goes through `IAudioAssetLoader`, so the package
itself has no dependency on any particular loading system.

```csharp
AudioSystem.AssetLoader = new AddressableAudioAssetLoader();
AudioSystem.LoadBank(levelBank);

yield return new WaitUntil(() => levelBank.IsStreamingComplete);
```

The Addressables backend requires the `AUDIOMW_ADDRESSABLES` define and the
Addressables package. Without a loader assigned, addressed clips simply stay
unresolved and the bank reports itself complete — nothing throws.

To integrate a different system, implement four methods: `CanLoad`,
`LoadAsync`, `Release` and `ReleaseAll`.

## Import auditor

`Window > AudioMW > Import Auditor` scans every clip in the project and reports:

- streamed clips shorter than two seconds, which cost a disk read per play
- clips longer than fifteen seconds decompressed into memory
- medium clips streamed where compressed in memory usually fits better
- streaming clips with preload enabled, which defeats streaming
- short clips loading in background, which can miss their first play
- stereo clips used spatially without Force To Mono, doubling memory for panning
  that is thrown away
- sample rates above 48 kHz, which rarely survive the output device

Load types can be corrected in bulk. Estimated resident memory accounts for
streaming, mono folding and compressed-in-memory.

## Loudness audit

`Window > AudioMW > Loudness Audit` measures integrated loudness and true peak
to ITU-R BS.1770 and compares each clip against a target, so a library mastered
over months stays coherent.

Results export to CSV. Clips that are not set to Decompress On Load cannot be
read and are reported as unreadable rather than measured as silence.
