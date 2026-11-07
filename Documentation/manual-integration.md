# Unity 6 gameplay integration: Relay Field

Relay Field is a separate, editable 3D collection game built on the installed Unity
3D URP template. It integrates the packaged AudioMW runtime into movement,
surface-dependent footsteps, landing, positional impacts, pickups, queued VO,
parameter ducking, adaptive music and scene lifetime. It is an original test game,
not Starter Assets or a Unity Microgame. No development scene is modified.

## Reproduce

Use Unity 6000.3.21f1 and an AudioMW release tarball. The installed template used
here is `com.unity.template.3d-cross-platform-17.0.14.tgz`. Dependencies are updated
to URP 17.3.0 and Input System 1.20.0 for this editor.

```sh
python3 Tools/prepare_playground.py --destination /absolute/empty/Playground --template /absolute/template.tgz --archive /absolute/AudioMW.tgz
```

Then run Unity with `-batchmode -nographics -projectPath /absolute/empty/Playground
-executeMethod BuildAudioPlayground.Run -logFile /absolute/setup.log`.
The setup imports the packaged demo, creates editable Data assets and a playable
`Assets/Playground/RelayField.unity`, and writes `playground-setup.json` using the
real library audit. It only runs in a project carrying `.audiomw-playground`.
It recreates the generated scene and Data assets: do not rerun setup after
customizing them. The Python preparation refuses every nonempty destination.

For the reviewed-mix integration tests, create the saved project brief and apply
its explicit fixture mix plan after generating the base scene. On a fresh, marked
development fixture, run Unity separately with `-executeMethod ValidateProjectSetup.Run`
and then `-executeMethod ValidateMixSetup.Run` (same projectPath/log arguments).
These development commands test approval/execution without prompting and are not
an authorization path for agents in customer projects. For interactive use, open
Project Setup and Mix Setup and approve their reviews in Unity instead.

Open RelayField and press Play. The project README describes controls and a
15-minute listening route. AudioMW is installed as `Packages/AudioMW.tgz`, so the
project remains usable after moving it away from the development repository.
For a development macOS Mono application, use the same Unity invocation with
`-executeMethod BuildAudioPlayground.BuildPlayer`; output is `Build/RelayField.app`.

## Boundaries and validation

The gameplay scripts and scene setup live under `Tools/ManualPlayground`, outside
the distributed middleware package. The music duck adapter explicitly calls
MusicPlayer.SetVolume, while ambience ducking uses SoundParameter bindings.
The listener follows the player's ears and takes orientation from the camera.
A bank lease covers scene lifetime; restart shuts down playback and disposes the
lease before the new scene acquires it.

Five PlayMode integration tests require the reviewed mix installation and cover accepted impacts and real limit rejection,
VO startup and eventual voice count, completion, repeat music requests, bounded
bank ownership after scene reload, and a real fall producing one landing cue
without grounded jitter. Run with `-runTests -testPlatform PlayMode -testFilter
RelayFieldIntegrationTests -testResults /absolute/results.xml`. The tests load the generated scene and exercise the real middleware runtime;
the completion test moves the player between relays programmatically. The mix test
checks real mixer attenuation and recovery and absence of duplicate source ducking.

The fixture uses synthesized audio from the existing demo. It does not prove
credible footsteps, speech, musical composition or loudness by ear. Replace clips
with redistributable recordings/stems before signing off listening quality.
Native desktop operation, automated runtime tests and the user's ear-check are
separate pieces of evidence. This fixture is not a Store submission or proof of
Windows/mobile/IL2CPP/real Addressables behavior.

## Reviewed mixer update (preview.16)

The updated prepared fixture installs 14 asset routes and a RelayMix profile through
the reviewed Mix Setup executor. Music and ambience use dedicated mixer duck buses;
the legacy per-source/music-adapter duck stages are removed or bypassed explicitly.
The macOS Mono Player was rebuilt. Its F8 snapshots include active-profile state
and both bus gains; -6 dB during VO and 0 dB after release were observed natively.
This remains synthetic placeholder content and is not a human listening sign-off.
See [mix-setup.md](mix-setup.md) for installation and ownership boundaries.
