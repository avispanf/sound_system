# AudioMW — containers, limits and music states

## Create a sound

Select clips or a folder in Project, then use **Window > AudioMW > Create Sound
Event**. OneShot, Footsteps, UI and Ambience are editable starting presets.
They create an asset with Undo support; they do not modify imported clips.
The existing Assets context command remains available.

Use the Inspector to edit an event. Audio Browser searches and previews source
clips only. Event gain, routing, layering and parameter behaviour are evaluated
by the runtime in Play Mode.

## Container semantics

| Mode | Behaviour |
|---|---|
| Simple | Select one clip: random, no immediate repeat, sequential, weighted random or shuffle bag |
| Blend | Start valid clip layers; a normalized parameter drives their weight curves |
| Layered | Start child events together; children can themselves be containers |
| Switch | Round the state parameter to an integer and start all children matching that state |

Switch selection occurs at trigger time. Changing a parameter on an existing
handle updates continuous bindings and blend weights; it does not replace its
selected child. Multiple matching rows deliberately produce layers. No matching
row produces no playback. Pass initial local parameters for per-emitter state.

Weighted selection uses clip order. Missing weights default to 1; nonpositive
or nonfinite weights disable a choice. All disabled choices reject the play.
Shuffle bag visits each index once before refill and avoids a boundary repeat
when there is more than one clip. Selection history belongs to the SoundEvent
asset and is shared across its callers; local parameters belong to a playback
instance. There is no per-emitter random-history mode yet.

Parent gain, pitch, random offsets and parameter bindings multiply into every
leaf. Leaf events own clips, loop, attenuation, mixer routing and virtualization.
A child-row gain multiplies its subtree. Cycles, depth beyond 32 and plans larger
than the voice pool reject the entire request before allocating or stealing.
Invalid individual clips/layers are skipped where the mode permits; the event
must resolve at least one playable layer.

## Lifecycle and limits

One playback instance owns all resolved layers, including virtual layers.
Stopping a SoundHandle stops the whole instance. The handle stays valid if its
first layer ends, if voices are stolen, and across virtual/physical changes;
IsPlaying becomes false when the instance ends. IsVirtual means at least one
layer is virtual; Voice returns a currently physical layer, or null.

Use SoundHandle for retained references. A raw Voice is a pooled object and
must not be retained across reuse or virtualization. Built-in emitter,
scatterer, spline and occluder components use handles. SoundEmitter.Stop stops
all plays owned by that emitter; scatterer stop also stops its owned instances.

Event limits, per-emitter limits and shared ConcurrencyGroup limits count
logical instances, not individual layers. Zero disables an event limit. Calls
with no attached Transform share one emitter bucket, even at different world
positions. Cooldown is global per root event, measured in unscaled seconds.
Root-event policies apply when it is triggered; nested children's admission
policies do not run again inside the group.

RejectNewest keeps existing instances. StealOldest, StealQuietest and
StealLowestPriority choose existing whole instances. Quietest compares the
largest current AudioSource gain in each instance (virtual-only instances score
zero); it is not a psychoacoustic audibility estimate. Unity priority 0 is most
important. LowestPriority chooses the largest numeric priority among existing
candidates; it does not protect them against a lower-priority incoming event.

All constraints and required physical slots are evaluated before selected
victims are stopped. A request cannot steal its own newly created layers.
Virtual promotion uses only free slots and waits if the pool is full. Virtual
playback advances at its current parameter-adjusted pitch even with no listener.
Local parameters, blend definitions and inherited bindings survive promotion.
StopAll and pool rebuild clear physical and virtual instances.

```csharp
var local = new ParameterStore();
local.Set(surfaceParameter, 2);
SoundHandle footstep = AudioSystem.PlayTrackedAttached(footstepEvent, transform, local);
footstep.SetParameter(intensityParameter, 0.7f);
footstep.Stop();
```

## Music states

Create **AudioMW > Music State Profile**. Give each state a unique name and
track asset. The transition rules reference source/destination names and override
the default quantization. Unknown names, duplicate names/tracks/routes and
unplayable tracks are rejected with Inspector feedback.

Add MusicStateController to a scene object and assign the profile. Call
SetState(string) from a UnityEvent or gameplay code; TrySetState returns success.
Music Transitions opens a Play Mode audition window showing the current track,
pending track and countdown. Repeating the same pending request does not
reschedule it. Rules resolve from the currently playing state, not a pending
state that has not yet started.

Transitions use the existing music scheduler: start from silence includes the
intro; transitioning to another state enters its body. This tranche does not
add crossfade envelopes or prove audible/sample-accurate results under frame
stalls. Human listening, rapid replacement under load and target-device capture
remain release gates.
