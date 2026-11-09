# BAD SHIFT — reference art pass

The user-supplied cafeteria image is the visual target. This pass changes the
isolated BAD SHIFT project only. It does not change AudioMW runtime middleware.

Implemented: fixed orthographic composition, cream background, warm matte palette,
coral wall band, cafe windows, exterior road and lilac car, cafe sign, desk and
notice board, expressive broccoli, lavender stockroom doorway, kitchen shelf,
picnic table and parasol, food crates, larger characters and refrigerator.
Skeletons wear mint aprons. Rounded mesh primitives soften new furniture edges.

Clay motion runs after skeletal animation. It adds restrained volume-preserving
squash/stretch, a damped landing spring and stepped deformation at 12 poses/second.
The existing skeletal clips remain smoothly evaluated. VO freezes staff deformation
and skeletal playback; the manager continues speaking gestures. Colliders never
inherit the visual squash. The fixed camera does not move the player AudioListener.

This is an implemented real-time interpretation, not a pixel-identical reproduction
of the illustration. The KayKit animation rig and stock clips remain, while the latest pass replaces
the visible skeleton geometry. Some furniture silhouettes still differ. It is not frame-by-frame claymation.
No new third-party art or licensing assumptions were introduced.

For a fresh prepared scene after ImproveBadShift.Run, run these authoring entry
points in order, with separate Unity processes and log files:

1. ReferenceBadShift.Run
2. ReferenceBadShift.Finish
3. ReferenceBadShift.FinalArt
4. ReferenceBadShift.Facing
5. ReferenceBadShift.FinalDetails
6. BuildAudioPlayground.BuildPlayer

These are one-time scene migrations, not a rebuild-on-every-import pipeline.
Use the saved scene for normal editing. Do not rerun them over hand-authored changes.
Run PlayMode tests after changing layout; taller character capsules are included
in route verification. Real audio and human listening approval remain pending.

## Completed furniture, lighting and response pass

FinishBadShiftArt.Run replaces the old cafe tables/chairs with complete rounded
furniture, authors a kitchen cabinet, completes the stockroom enclosure and sets
up the clay render profile. Run FinishBadShiftArt.FinalGrade afterwards; it assigns
the dedicated profile to the PC pipeline (replacing the template SampleSceneProfile
and its vignette), preserves the cream palette and builds the standalone player.
These steps run once after the reference migrations above. FinalGrade is repeatable.

The final grade uses a global layer-0 Volume and the same dedicated pipeline
profile, camera post-processing enabled, HDR support, SMAA High, SSAO intensity
0.85/radius 0.45, no tone compression, saturation -3 and contrast 0. Existing
AudioMW runtime code and the original Relay Field project are untouched.

The landing spring now uses an analytic damped solution and the visual squash
preserves volume exactly. Player sway is visual-only. Microwave shell/glass/button
react as one visual assembly while the fixed collision body stays in place.
Reaction clocks pause with gameplay; VO holds staff scale and skeletal motion.

Validation: 202/202 PlayMode tests in Unity 6000.3.21f1, including 15 demo tests.
Native walkthrough reached the microwave; X displayed the actual selected SFX clip.
Final macOS standalone build succeeded without errors. Human audio sign-off and
pixel-identical custom character modelling remain outside the completed art pass.

## Reference comparison, 2026-11-09

Compared the supplied illustration with a fresh 1600x1000 Unity camera render
(reference-review.png in the prepared project). CompleteReferenceDetails.Run is a
one-time migration after ArrangeBadShift: faceted broccoli crowns, hollow gold
megaphone, coffee cup and steam sign with cream lettering band, and a preparation
table with lower shelf, loose bread and bowl. The table and dishes use Rigidbody
collision and AudioMW impact playback. Capture renders the saved scene without HUD;
it does not certify animation or runtime interaction.

The current composition includes the cafe street/car/sign, manager desk,
lavender bathroom, dancing employee platform, parasol table, food crates,
kitchen cabinet, microwave, shelf and two-door fridge. Three evenly spaced dining
rows, the dark background, bathroom fixtures and appliance controls intentionally
follow later user requests instead of the original illustration.

At that checkpoint, the remaining differences included stock skeleton faces, smooth stock clips, lighting and furniture silhouettes. See the subsequent character pass below for the current state. The UI is an interactive demo overlay rather than the reference poster's
title, palette swatches and two inset illustrations. Full reference parity is
not claimed. Audio still uses placeholders.

Validation for this pass: full PlayMode suite 223/223 passed; after selecting the whole-loaf model, all five layout/physics checks passed again. The full suite includes the guided route. Camera renders verify appearance, not animation quality or human listening.

## Clay characters and studio lighting

ClayCharacterPass.Run then FinishShapes replace visible stock skeleton geometry
with cream rounded skulls, oval ink eyes, simple articulated bones, mittens and
rib cages. Original renderers remain disabled; the original rig and animation
controllers continue driving the new geometry. Aprons now attach to chest and hip
bones. AlwaysAnimate prevents hidden source meshes from stopping the rig.

BadShiftClayFace adds staggered blinking, head turns, delayed-looking head tilt
and volume-preserving squash at twelve pose samples per second. The dancer's
head response follows the actual Combat music state. Body deformation and landing
compression are stronger; physical colliders are untouched. Voice-over holds both
staff faces, and pause holds all procedural clocks. These are procedural clay
accents over continuous skeletal clips, not fully authored stop-motion clips.

Lighting uses a warm key, softer/lighter shadows, a cool shadowless fill and
trilight ambient colors. Existing URP grade and camera post-processing stay intact.
The dark blue-gray background follows the user's later direction. This changes
real-time lighting rather than recreating the reference's offline softbox rendering.

Reproduction requires com.unity.modules.imageconversion 1.0.0 for PlayMode visual
capture. ClayCharacterPass is guarded to run only on the isolated prepared demo.
Run the two entry points once after CompleteReferenceDetails. The prepared archive
already contains the generated meshes, materials and authored scene.

Character-pass validation: 225/225 PlayMode tests passed on 2026-11-09, including geometry/rig separation, Combat dance rendering and face freeze/resume checks. Inspected clay-idle-a.png, clay-idle-b.png and clay-dance.png captured from the live PlayMode camera. Human acoustic QA remains pending.

The rebuilt macOS standalone launched successfully; its new characters and scene presentation were visually checked in the native BAD SHIFT window. Build completed with zero errors.

## Proportions and distance-driven gait

ClayGaitPass.Run (once after ClayCharacterPass) raises the visual pelvis by 0.43
world units, replaces the short leg presentation with longer slim segments,
reduces head size to 90 percent and narrows/shortens the apron. Colliders and
CharacterController movement are preserved. Bone poses are restored before Animator
evaluation, then visual posing runs after body deformation; offsets do not accumulate.

Player gait phase follows measured displacement, not elapsed time. Stance feet
retain world anchors, while swing feet arc toward a ground-projected landing point.
Airborne motion and employee dance retain the underlying clip's foot targets.
Torso twist/lean and delayed head sway add clay comedy; the contact trajectory stays
continuous. Landings and teleports reinitialize anchors. The player footstep sound
now fires on swing-to-stance contact through the existing local surface parameters;
projects without this presentation component retain their original stride playback.

PlayMode visual captures now run after LateUpdate, so they include the final visual
pose rather than the temporarily restored Animator source pose. Tests exercise
stance-foot stability during actual CharacterController movement, pause, and absence
of cumulative hip offset. The result is procedural stylized animation, not an exact
copy of the illustration or fully authored stop-motion animation.

Validation on 2026-11-09: full suite 227/227 passed. After removing idle foot recentering, four focused actor/gait tests passed again, including fixed stance feet during movement and after stopping. Inspected post-LateUpdate walking captures. The whole demo route remains covered by the full suite; perceptual footstep/audio review is still manual.
