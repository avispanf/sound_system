# BAD SHIFT — reference art pass

The user-supplied cafeteria image is the visual target. This pass changes the
isolated BAD SHIFT project only. It does not change AudioMW runtime middleware.

Implemented: fixed orthographic composition, cream background, warm matte palette,
coral wall band, cafe windows, exterior road and lilac car, cafe sign, desk and
notice board, expressive broccoli, lavender stockroom doorway, kitchen shelf,
picnic table and parasol, food crates, larger characters and refrigerator.
Skeletons wear mint aprons. Rounded mesh primitives soften new furniture edges.

Clay motion runs after skeletal animation. It adds restrained volume-preserving
squash/stretch, a damped landing spring and stepped deformation at 16 poses/second.
The existing skeletal clips remain smoothly evaluated. VO freezes staff deformation
and skeletal playback; the manager continues speaking gestures. Colliders never
inherit the visual squash. The fixed camera does not move the player AudioListener.

This is an implemented real-time interpretation, not a pixel-identical reproduction
of the illustration. Original KayKit skeleton topology, stock animation clips and
some furniture silhouettes remain different. It is not frame-by-frame claymation.
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

Remaining visual differences: stock skeleton faces/body proportions rather than
the illustration's simplified skulls; stock skeletal clips plus procedural clay
motion rather than authored stop-motion; simpler materials, illumination and
furniture silhouettes. The UI is an interactive demo overlay rather than the reference poster's
title, palette swatches and two inset illustrations. Full reference parity is
not claimed. Audio still uses placeholders.

Validation for this pass: full PlayMode suite 223/223 passed; after selecting the whole-loaf model, all five layout/physics checks passed again. The full suite includes the guided route. Camera renders verify appearance, not animation quality or human listening.
