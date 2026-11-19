# Reproduce compatibility tests

Create an isolated package-only project from the repository root:

```sh
python3 Tools/prepare_test_project.py --destination /path/to/empty/AudioMWTest --unity-version 2022.3.62f3
```

The destination must be empty or carry the generator's ownership marker.
Re-running replaces only the copied AudioMW package and generated manifest /
version settings. Use a separate directory for each Unity version. The fixture
uses Test Framework 1.1.33 for 2022.3 or 1.6.0 for Unity 6 plus the required
built-in modules; it does not copy URP, MCP or other development packages.

Run the selected editor with `-batchmode -projectPath <fixture> -runTests
-testPlatform EditMode -testResults <absolute.xml> -logFile <absolute.log>`.
Repeat with PlayMode. Do not run two editors on the same fixture concurrently.

GitHub Actions assembles and checks the package in a separate license-free job, then prepares this fixture before the Unity version matrix.
It still requires working Unity licensing credentials. Local results do not
prove a cloud run or Linux image availability. Configuration follows the
[GameCI test runner](https://game.ci/docs/github/test-runner/).

Without `--archive`, this fixture checks the source package under Assets. That mode is not an import test of a UPM archive or Asset Store download.

For distribution checks, pass `--archive /absolute/path/to/audiomw.tgz` to the
fixture generator. This installs the actual tarball with Package Manager and
enables the package tests through testables. See distribution.md for sample
import validation. CI now uses this archive route as well.

## Desktop Player validation

Use the archive route, then run from the repository root:

```sh
python3 Tools/run_player_smoke.py --fixture /absolute/fixture --editor /absolute/Unity --backend Mono
```

The generated fixture contains `BuildPlayerSmoke.Run` and the runtime smoke
component. The command imports and validates the package sample, creates a
separate serialized validation scene, builds a non-development desktop Player
and runs fourteen runtime checks. Mono uses Low stripping. `--backend IL2CPP`
uses High stripping and requires both the Unity build support module and native
compiler toolchain. macOS requires full Xcode; Command Line Tools alone did not
satisfy the local IL2CPP builder. `--target` also accepts StandaloneLinux64 and
StandaloneWindows64, but their build support and native host execution are required.

The runtime requires advancing DSP time, actual AudioSource playback, Simple /
Layered / Switch behaviour, captured rejection state, handle stop, quantized music
scheduling, cancellation and shutdown. Output gain is muted; no listening approval
is implied. It writes a fresh report for each run and fails on a missing check,
frozen DSP clock, wrong Unity version or nonzero Player exit. Build results and
logs remain in the fixture. `--skip-build` deliberately reruns the last build for
the chosen backend. A failed run removes the old aggregate success report.

UPM paths are relative to the fixture's Packages folder when possible. This
preserves them when moving the workspace into a CI container; archives on a
different Windows drive retain an absolute fallback. Desktop Player checks are
currently a local command, not a cloud CI runtime pass.

## Addressables packed catalog

Prepare a separate generated project for the installed tarball. On Unity
2022.3.62f3 use Addressables 1.21.21. On Unity 6000.3.21f1 use 2.9.1.

```sh
python3 Tools/prepare_test_project.py --destination /absolute/catalog-fixture --archive /absolute/audiomw.tgz --unity-version 6000.3.21f1 --addressables-version 2.9.1
/absolute/Unity -batchmode -projectPath /absolute/catalog-fixture -executeMethod ValidateAddressables.RunPacked -logFile /absolute/catalog.log
```

Allow the editor to exit after its asynchronous Play Mode checks. The probe
builds a local bundle containing the package sample's bass WAV and loads it
through AudioMW. It checks the resolved provider, both clients receiving the
same clip, release after the last client, and a subsequent load. A successful
run writes `addressables-packed-validation.json` at the fixture root. The
existing `ValidateAddressables.Run` remains available for the compile-only check.

On Unity 6000.3.21f1, Addressables 1.21.21 with Scriptable Build Pipeline 2.6.1
failed at CreateBuiltInShadersBundle before building the catalog. The packed
check with Addressables 2.9.1 passed on the same editor.

To check the same local bundle in a native macOS Mono Player, continue in that
fixture after the packed Editor check succeeds:

```sh
/absolute/Unity -batchmode -projectPath /absolute/catalog-fixture -executeMethod BuildCatalogPlayer.Build -logFile /absolute/catalog-build.log
"/absolute/catalog-fixture/CatalogPlayer/AudioMW Packed Catalog.app/Contents/MacOS/AudioMW Packed Catalog" -batchmode -logFile /absolute/catalog-player.log
```

Check the Player exit status and the `PACKED_PLAYER_RUN True` line. The Player
resolves the bundled provider, reads non-silent PCM from the sample, starts
a tracked AudioMW voice with output muted, checks that its sample position
advances and releases the retained address.
