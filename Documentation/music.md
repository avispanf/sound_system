# Adaptive music

## The clock

`MusicPlayer` runs a `MusicClock` driven by `AudioSettings.dspTime`, not by
frame time. Everything the music system schedules is placed on that timeline
ahead of the audio thread reaching it, which is why joins and transitions land
sample accurately even when the frame rate stutters.

Durations are computed as `samples / frequency` in double precision. The float
`AudioClip.length` property accumulates enough error over a long track to make a
loop join audible.

Subscribe to the grid to drive gameplay from the music:

```csharp
AudioSystem.Music.BeatTick += beat => flash.Pulse();
AudioSystem.Music.BarTick += bar => spawner.NextWave();
```

## Vertical layers

A music track has an optional base loop and any number of layers. All of them
are scheduled on the same cursor, so the stems stay locked together.

Each layer carries a parameter, a weight curve and a fade time. Set the
parameter and the layers move:

```csharp
AudioSystem.SetParameter(intensity, 0.8f);
```

A practical arrangement for combat:

| Layer | Curve | Fade |
|---|---|---|
| base loop | always on | — |
| pad | rises from 0.25 | 1.2 s |
| percussion | rises from 0.0 | 0.9 s |
| lead | rises from 0.5 | 0.6 s |

Longer fades on sustained material and shorter fades on rhythmic material keep
the transition from sounding mechanical.

Channel index `0` is always the base loop; layers follow in declaration order.
`GetLayerWeight` and `GetLayerName` expose the live state for debugging.

## Horizontal transitions

```csharp
AudioSystem.TransitionMusic(combat, MusicQuantization.Bar);
```

The incoming track is scheduled at the next bar line and the outgoing sources
are given a scheduled end time at exactly the same instant, so there is no gap
and no overlap. On arrival the clock restarts at the boundary and adopts the new
tempo and signature.

`MusicQuantization.Immediate` transitions as soon as possible, `Beat` waits for
the next beat, `Bar` for the next bar. Bar is usually what you want for section
changes; beat suits fast reactive switches.

A pending transition can be replaced by another call, and `StopMusic` cancels
it. The intro clip of the incoming track is skipped during a transition — intros
belong at the start of a piece, not at every section change.

## Markers

Beats and bars are a metric grid, not a musical one. A section often wants to
change at the end of a phrase, which may sit anywhere inside the loop. Markers
name those points.

Add markers to a track as a list of bar and beat positions in the inspector,
then quantise to them:

```csharp
AudioSystem.TransitionMusic(combat, MusicQuantization.Marker);
AudioSystem.PlayStinger(hitClip, MusicQuantization.Marker);
```

The clock resolves the nearest marker ahead of the playhead, wrapping into the
next loop cycle when the playhead has passed them all. A track with no usable
markers falls back to the bar grid, so marker quantisation is always safe to
request.

React to markers in gameplay:

```csharp
AudioSystem.Music.MarkerReached += marker => director.OnCue(marker.Name);
```

## Stingers

```csharp
AudioSystem.PlayStinger(victoryClip, MusicQuantization.Beat, 0.8f);
```

Stingers play on their own source pool and never disturb the music channels or
compete with gameplay sounds for voices. They are quantised to the running grid,
so a stinger fired at an arbitrary moment still lands in time.

## Ducking under dialogue

The voice over director drives a parameter from `0` to `1` while a line plays.
Bind that parameter wherever you want the ducking to happen:

- on a music layer's weight curve, to thin the arrangement
- on a sound event's volume binding, to duck a specific sound
- on a mixer routing profile, to duck a whole bus in decibels

```csharp
AudioSystem.VoiceOver.DuckParameter = voiceDuck;
AudioSystem.VoiceOver.DuckFadeSeconds = 0.25f;
```

Because ducking is a parameter rather than a fixed snapshot, it can be selective:
music and ambience drop while important gameplay cues stay where they are.
