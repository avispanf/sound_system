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
