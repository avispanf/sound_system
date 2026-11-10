# Nearby audio visualization

The top Audio button and Tab cycle labels → details → off. Labels are on by default; Sound on/off remains independent. Reset restores the default visualization. Silent development launches continue to simulate audio and show labels without output.

Labels use actual successful spatial SoundEvent records, anchored at their playback position. Each lasts 0.85 seconds, rises slightly and fades; distance fade starts at 4 metres and ends at 7 metres or the event max distance. Offscreen sources are hidden, never pinned to a screen edge. Repeated identical events within 0.1 seconds and 0.7 metres combine into ×N. At most 12 recent pulses are retained; overlapping labels use up to four lanes and otherwise disappear. HUD and detail panels take priority.

Details adds the existing journal and source explanation. Connection lines fade between 3 and 5 metres and between 140 and 260 logical screen pixels. Both ends must be visible in the play area. No distant line crosses the level. The source explanation hides beyond 5 metres.

The labels are event telemetry, not a microphone reading or proof of audible output. Loops, non-spatial events, rejected requests, uncaptured/zero gains, direct AudioSource playback, MusicTrack and VoiceLine playback are not represented by these short labels. The detail journal/music/ducking readouts retain their existing scope. Re-enabling visualization does not replay old history; diagnostic sequence IDs prevent buffer trimming or Clear from replaying events.

## Validation

Targeted PlayMode: 19/19 passed, including lifetime, distance filtering, mode cycling, history suppression and diagnostic buffer behavior. Full-suite and native evidence are recorded in AUDIO-VISUALS-VERIFICATION.json in the delivered project. Testing is silent; this is not an ear check.
