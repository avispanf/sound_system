# Sound Event workflow — Design 02

The native implementation follows CODEX-HANDOFF.md and Design 02 from the supplied
AudioMW UI prototype, checked against the current package on 2026-11-05. HTML,
JavaScript, sample records and browser audio are not shipped in the Unity package.

## Find a source and edit its event

Open **Window > AudioMW > Sounds**. Music and Diagnostics remain separate native,
dockable windows; existing menu paths also work. The Tools menu links loudness,
import audit and Play Mode change review. Browser remains read-only.

Click an event once to send it to Unity selection and update an ordinary unlocked
Inspector. The Browser keeps its own highlighted asset. Pin Inspector explicitly
opens Unity's native focused property editor. Its asset name is displayed in the
Browser; changing Browser selection does not retarget the pin. Follow selection
closes that owned focused window and returns to ordinary selection. Other native
Inspector locks are respected. A pin is also visible when a search returns no
results. Closing a focused window clears its Browser pin status.

Search, type-filter mask, selected asset GUID, source index and pane width are
serialized on the EditorWindow. Native split-view state also persists. A missing
search result clears old details and offers Reset search and filters. A missing
library offers event creation. Full names and asset paths are available through
tooltips; the overview title wraps.

## Source preview

Choose one row in Source clips. The selected source owns the waveform, duration,
sample rate, channel count, LUFS/true-peak analysis and PCM-size estimate. Nested
containers list all authored branches, including Switch alternatives; the list is
not a simulation of the event's currently selected branch. Cycles are bounded.
Music lists intro/body/layers and VO lists its clip.

Preview source clip plays the original file using Unity's editor preview API.
**Original file · No event processing** stays visible. Source changes, changing
the asset, hiding the selected asset through filters and closing the Browser stop
preview. A native direct AudioClip can have unloaded PCM and still support editor
preview; this was observed in Unity 6. Decompressed direct clips can be read with
Unity's existing GetData path for waveform/analysis without modifying import
settings. Loading/failed and missing sources display their reason.

Addressed references remain gated by the existing SoundBank resolution/data-loading
contract. Browser does not start an asset-loader request to make them playable.
For an unavailable addressed source, the Browser finds real banks containing the
event, shows their loading state and links to their Inspector. Streaming/compressed
clips do not get invented waveforms or loudness values when PCM cannot be read.
Waveform textures keep the package's GUID-keyed cache.

Event audition remains an explicit Play Mode task. No clip preview button claims
to reproduce random selection, parent/child gain, switch choice, blend curves,
routing, virtualization or spatial processing.

## Inspector

Playback, its mode-specific content, Gain and Pitch ratio stay visible. Sound is
a heading, not a foldout. Simple retains direct clips, addressed references and
all selection modes. Layered/Switch retain child events and per-child dB gain;
Switch still chooses its state at trigger time. Blend retains its full curves.
Only leaves show Loop, mixer routing, pause opt-out and spatial settings.

Variation, Spatial audio, Playback limits, Parameter bindings and Addressed clips
have summaries computed from the selected serialized settings. Examples include
Off, 3D, 2 / emitter and Unlimited. Relevant mixed values show Mixed. Expanding a
section removes its compact summary. Gain includes a fader, precise dB text and
Reset to 0 dB, including a recovery path from −∞. Existing linear storage and
legacy variation semantics remain unchanged. Editing uses native binding and Undo.

## Create and connect

Select AudioClips or their folder in Project and choose Create event. With no
clips, creation is disabled and the next step is explained. One-shot, Footsteps,
UI and Ambience are presets for the same SoundEvent type. Footsteps starts with
Shuffle bag, -3 dB gain, ±2 dB variation and two instances per emitter. UI starts
2D; Ambience loops and opts into virtualization. All fields remain editable.
The menu/creation window share the same creation controller, and no global
SaveAssets call writes unrelated dirty assets.

After creating an asset, Unity selects it. In **Use this sound in Unity**, choose
a scene GameObject and explicitly click Assign event. This assigns the first
SoundEmitter on that object or adds one with Undo. Existing assignments and prefab
instance overrides are undoable. The action rejects persistent prefab assets and
requires one event; it does not attach a component just because an asset is selected.

## Diagnose a real rejection

Diagnostics records reference the actual SoundEvent. Instance rejection captures
scope, actual count and configured limit; cooldown captures remaining seconds;
voice-capacity rejection captures requested/available/capacity values. Missing
context remains generic rather than displaying fabricated zero counts. Accepted
source-volume zero is labelled accepted, not rejected.

Selecting a record shows its captured reason, then Open event settings opens a
native property editor at the relevant Playback limits/cooldown field. Unknown
or destroyed event targets disable navigation. Gain details are collapsed and
marked Captured; changing filters clears detail from records no longer present.
The existing list is retained in this tranche; a richer Diagnostics table and the
remaining Music screen refinements are subsequent work.

## Validation and boundaries

EditMode coverage includes presets, graph/source identity, addressed-bank discovery,
empty searches, serialized Browser restoration, native pin/follow, reset and Undo,
summary mixed states, section navigation, preview lifetime, emitter creation and
prefab overrides. PlayMode coverage includes actual emitter rejection counts,
remaining cooldown and accepted mute, plus the existing runtime suites.

Native Unity 6 checks include source switching/preview stop, event value retention,
pin independence, empty search, dark/light themes, Sounds at 620 points and
Inspector at 300/360 points. Test assets use the package's generated demo WAVs;
they are not production footsteps or human listening approval. Automated test
results, actual native screenshots and remaining checks are recorded separately.
