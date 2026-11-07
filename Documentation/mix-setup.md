# Reviewed mixer setup

Window > AudioMW > Mix Setup extends Project Setup with an executable, local review.
A saved project setup profile is required. Reading assets, changing draft roles,
preparing a review and Reject do not write assets or scenes. Approve and apply is
the explicit mutation boundary; automation should present the same review and wait
for the developer to approve in Unity. Development fixtures may call the executor
directly to test it, which is not agent permission to alter users' projects.

## Workflow

1. Save your scene and audio assets. Select the saved project setup and read its
   source folder. Music Tracks and VO Lines receive draft roles; Sound Events start
   Unchanged. Confirm the category of every event, including nested leaf events.
2. Choose an existing output folder, a new installation name, and a GameObject in
   the saved scene. Leave the mixer empty to copy the standard native Unity mixer.
   Reusing a mixer requires the compatible group paths and exposed names below;
   this version does not insert groups/effects into arbitrary existing graphs.
3. Choose an existing global 0–1/default-0 VO duck parameter, or leave it empty to
   create one. Review music/ambience attenuation, attack and release. The defaults
   are creative starting points, not platform loudness requirements.
4. Prepare routing review. Every selected asset shows its old/new route and Why.
   Existing non-default routing requires the explicit replacement option.
5. Approve and apply creates assets, assigns selected routes, installs an
   AudioMixScope and saves the target scene. Reject leaves the project untouched.
   Draft edits, reloads, input edits and changed destinations require a new review.
6. Enter Play Mode, queue dialogue and check both audible recovery and mixer gain.
   Use version control to review the persisted scene and asset changes.

## Standard mixer

The template was authored in Unity 2022.3's native Audio Mixer and is copied using
AssetDatabase.CopyAsset. No mixer serialization rewriting, private editor APIs or
external binary tooling is used.

| Route | Group path |
| --- | --- |
| Music | Master/Music/MusicDuck |
| SFX | Master/SFX |
| Ambience | Master/Ambience/AmbienceDuck |
| VO | Master/VO |
| UI | Master/UI |

Only MusicDuck and AmbienceDuck volume are exposed as AudioMW_MusicDuckDb and
AudioMW_AmbienceDuckDb. Their snapshot defaults are 0 dB. Category/user faders and
Master remain separate. No compressor, limiter, automatic master gain, loudness
normalization, import setting changes or platform compliance claim is included.

The generated MixerRoutingProfile maps one VO envelope to the two duck gains.
AudioMixProfile supplies the envelope parameter and separate attack/release.
AudioMixScope acquires the mix at Start before ordinary gameplay Start, and releases
it on destruction. Additive scenes can share the same profile through reference
counted leases; a different simultaneous profile is rejected. Shutdown invalidates
old leases. The routing bindings are copied for the session, so later asset edits
do not change the installed binding list. Runtime ownership of the dedicated duck
parameters is exclusive; don't write them with another script or snapshot override.
Existing routing profiles targeting those same parameters are rejected at acquire.
On last release, the routing is removed, duck overrides are cleared back to the
mixer snapshot, and the previous VO envelope configuration is restored if still
owned. These gains must not double as user volume settings.

## Avoid duplicate ducking

Existing per-source volume bindings remain unless their row explicitly requests
removal. That option is limited to events routed to Music/Ambience and removes only
Volume bindings for the selected existing duck parameter. Other parameters, pitch,
clip gain and variation remain. Existing game-code duck adapters need a corresponding
integration change; the planner does not rewrite gameplay code. Relay Field bypasses
its legacy music duck adapter when ActiveMixProfile is present and keeps its mute.
Stingers and arbitrary AudioSources are not automatically discovered or routed.

## Persistence and failure handling

The installation receipt records the reviewed request and hashes of the installed
assets, routed assets, setup profile and scene. Repeating an unchanged installation
is a no-op. Edits to tracked content block reapplication; they are preserved rather
than merged or overwritten. Review those edits manually or use a new output name.
This is an initial-install workflow, not an in-place installation updater.

The executor checks the review again immediately before writing. It backs up only
selected source asset files and the target scene under Library/AudioMW/Transactions,
uses Unity Undo for object changes and removes newly created assets on a caught
failure. Rollback is tested after source routes have been saved. Inspect scene dirty
state after a failure. If restoration itself fails, the error points to retained
backups. This is not a crash-proof filesystem transaction or a promise that one
ordinary Undo restores every saved file. Commit before applying in a real project.
