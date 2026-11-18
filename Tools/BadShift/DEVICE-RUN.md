# BAD SHIFT device run

Build the dedicated Development Player from the prepared BAD SHIFT fixture using Unity with the target platform module installed. Use an unused absolute output path. Android produces an ARM64 APK. iOS exports a project for Xcode. Both use com.avispanf.audiomw.devicecheck.

    Unity -batchmode -projectPath /absolute/AudioMW-BadShift -buildTarget Android -executeMethod BadShiftDeviceBuild.Run -deviceRunOutput /absolute/device-check.apk -quit
    Unity -batchmode -projectPath /absolute/AudioMW-BadShift -buildTarget iOS -executeMethod BadShiftDeviceBuild.Run -deviceRunOutput /absolute/DeviceCheckXcode -quit

Install the Android APK via adb. For iOS open Unity-iPhone.xcodeproj, select a development team for the Unity-iPhone target and run on a paired phone. The test starts muted. Keep the app in the foreground through the baseline, open door, closed door and reopened phases. When prompted, background the app for five seconds and reopen it. Tap **Copy JSON report**. Each run also writes a unique AudioMW-device-*.json under Application.persistentDataPath.

A complete device check has workloadComplete, mixObserved, pauseObserved and resumeVerified set, an empty error, and interruptedDuringLoad clear. Resume verification checks that the DSP clock and the running tap sample cursor both advance while the music transport is active. Rerun if the app was interrupted during the workload. Keep separate reports for each device, build and audio configuration.

Samples every quarter second record physical and virtual voices, steals, process-wide Unity allocated memory, and door obstruction while traffic, adaptive music, VO and multiple spatial loop sources overlap. DSP CPU is recorded only when a percentage counter is exposed. A value of -1 means unavailable. Device performance and listening acceptance each require their own evidence.
