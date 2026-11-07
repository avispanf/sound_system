# AudioMW release readiness

AudioMW is not yet ready for Asset Store submission. The package now has local
Editor, installed-UPM and desktop Player evidence, but listening, final demo
content and broader platform validation remain open.

## Verified locally on 2026-11-07

| Gate | Evidence | Scope |
| --- | --- | --- |
| Installed UPM regression suite | 255 EditMode + 181 PlayMode per editor | Unity 2022.3.62f3 and 6000.3.21f1, preview.13; previous runtime tranche |
| Sample import and references | Successful before each Player build | Delivered preview.13 tarball, generated isolated projects |
| Desktop Player build and run | 14 checks per Player, exit 0, DSP advances | macOS, Mono, Low stripping, both editors |
| Assembly separation | Only AudioMW.Runtime.dll in the Mono Player | No AudioMW Editor or test DLLs |
| Validation tooling | 14 Python tests | Packaging, portable fixtures, report completeness, stale-result handling, assembly leaks |

The Player smoke checks cover serialized clips/graphs, real AudioSource playback,
Simple/Layered/Switch, captured local Switch state, handle stop, quantized music,
repeat-destination scheduling, cancellation and shutdown. Output gain is muted.
These checks do not establish sound quality or musical timing by ear.

## Manual gameplay fixture

Relay Field now provides a separate Unity 6 URP game for hands-on integration and
listening. Its library audit reports 20 assets with zero errors and warnings;
four PlayMode gameplay tests pass in Unity 6000.3.21f1. The test fixture uses the
existing preview.14 archive and leaves the middleware runtime unchanged.
See [manual-integration.md](manual-integration.md) for reproduction and boundaries.
The user listening pass remains open.

## Open release gates

- IL2CPP with High stripping: Mac modules are installed for both local editors,
  but native builds require full Xcode. The installed Command Line Tools did not
  satisfy the builder. IL2CPP has no successful local Player result yet.
- Windows/Linux and target mobile/device runs, real Addressables catalogs,
  audio-device recovery and broader accessibility checks.
- Real redistributable demo audio, listening of transitions/crossfades/ducking,
  final product identity, commercial licensing review and store materials.
- Cloud Unity test results: the license-free package job is separate from Unity
  jobs, which still need valid licensing credentials. Local results do not prove
  cloud success or availability of a given CI image.

## Repeat the technical gates

Use the repository scripts described in [compatibility-testing.md](compatibility-testing.md).
Build a fresh archive, prepare an isolated project with `--archive`, then run
`Tools/run_player_smoke.py` with the correct editor and backend. Never build from
the development Sound System scene. Keep the build report, Player report, archive
hash, editor version, backend and stripping level together for each candidate.
