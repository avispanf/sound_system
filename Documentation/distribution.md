# Build and install a development package

From the repository root:

```sh
python3 Tools/build_package.py --output /path/to/com.avispanf.audiomw-preview.tgz --version 0.5.0-preview.1
```

The builder assembles the source package, the development demo under Samples~/Demo
and user documentation under Documentation~. Source assets stay in their existing
development folders. The source manifest intentionally declares no samples; the
assembled archive declares the sample it actually contains. Direct Git installation
of Assets/AudioMW gives the engine and tools; use the assembled archive for the demo.

The builder preserves GUIDs and rejects duplicate GUIDs and unresolved serialized
asset references. Tar entries and gzip metadata are normalized for repeatable bytes.
The sidecar JSON records the archive hash, version and file count.

In Unity Package Manager choose + > Add package from tarball, select the .tgz,
then import Demo Scene from Samples. Open the imported AudioMW Demo.unity.
No development-project render pipeline or project settings are included. The demo
uses a bundled unlit shader rather than a URP package material.

The scene and audio are still demonstration placeholders. A clean install is a
technical gate; release licensing, real audio, listening and platform builds are
separate outstanding gates. This builder produces a UPM archive, not an Asset Store
submission archive.

See Unity's [tarball installation](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-tarball.html)
and [sample layout](https://docs.unity3d.com/2022.3/Documentation/Manual/cus-samples.html).

To reproduce the installation check, prepare a fixture with `--archive <tgz>`
and run Unity with `-batchmode -projectPath <fixture> -executeMethod
ValidateDistribution.Run -logFile <log>`. The validator imports the declared
sample using Package Manager, opens its scene and checks missing components,
serialized references, shader import and the shipped Browser stylesheet.
It writes distribution-validation.json in the fixture. Then run the package's
EditMode and PlayMode tests using the same fixture.
