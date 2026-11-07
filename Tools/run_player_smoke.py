import argparse
import json
from pathlib import Path
import subprocess
import sys
import uuid


REQUIRED = {
    "serialized clip reference", "simple playback admitted", "native AudioSource advances",
    "handle stop releases voice", "serialized layered graph plays two voices",
    "unmatched switch rejected", "diagnostic record available", "actual local switch state captured",
    "matched switch plays", "serialized music starts", "quantized music scheduled",
    "repeat destination keeps boundary", "pending music cancelled", "shutdown releases playback",
}


def validate_report(report, unity_version):
    if report.get("success") is not True:
        raise ValueError("Player reported failure: " + str(report.get("error")))
    if report.get("unity") != unity_version:
        raise ValueError("Build and player Unity versions differ")
    if report.get("dspAdvanced") is not True:
        raise ValueError("Player DSP clock did not advance")
    missing = REQUIRED - set(report.get("checks", []))
    if missing:
        raise ValueError("Missing player checks: " + ", ".join(sorted(missing)))


def player_executable(fixture, build):
    fixture = fixture.resolve()
    player = Path(build["player"]).resolve()
    if fixture not in player.parents:
        raise ValueError("Player must be inside the generated fixture")
    if player.suffix == ".app":
        player = player / "Contents" / "MacOS" / "AudioMWSmoke"
    player = player.resolve()
    if fixture not in player.parents:
        raise ValueError("Player executable resolves outside the fixture")
    return player


def audit_assemblies(fixture, build):
    player = Path(build["player"]).resolve()
    player_executable(fixture, build)
    if build["backend"] != "Mono":
        return {"backend": build["backend"], "managedAudit": False}
    managed = player / "Contents/Resources/Data/Managed" if player.suffix == ".app" else player.parent / "AudioMWSmoke_Data/Managed"
    names = sorted(path.name for path in managed.glob("AudioMW*.dll"))
    if "AudioMW.Runtime.dll" not in names or any(name != "AudioMW.Runtime.dll" for name in names):
        raise ValueError("Unexpected AudioMW player assemblies: " + str(names))
    return {"backend": "Mono", "managedAudit": True, "assemblies": names}


def run(fixture, editor=None, target="StandaloneOSX", skip_build=False, backend="Mono"):
    fixture = fixture.resolve()
    if not (fixture / ".audiomw-test-project").is_file():
        raise ValueError("Use a generated AudioMW fixture")
    aggregate_path = fixture / ("player-validation-" + backend + ".json")
    aggregate_path.unlink(missing_ok=True)
    build_path = fixture / ("player-build-" + backend + ".json")
    if not skip_build:
        if editor is None:
            raise ValueError("Pass --editor or --skip-build")
        build_path.unlink(missing_ok=True)
        subprocess.run([str(editor), "-batchmode", "-projectPath", str(fixture), "-executeMethod", "BuildPlayerSmoke.Run", "--audiomw-target", target, "--audiomw-backend", backend, "-logFile", str(fixture / ("player-build-" + backend + ".log"))], check=True, timeout=1200)
    build = json.loads(build_path.read_text())
    if build.get("success") is not True:
        raise ValueError("Player build did not succeed")
    player = player_executable(fixture, build)
    audit = audit_assemblies(fixture, build)
    report_path = fixture / ("player-run-" + uuid.uuid4().hex + ".json")
    with (fixture / ("player-console-" + backend + ".log")).open("w") as console:
        result = subprocess.run([str(player), "-batchmode", "-logFile", str(fixture / ("player-run-" + backend + ".log")), "--audiomw-report", str(report_path)], stdout=console, stderr=subprocess.STDOUT, timeout=60)
    report = json.loads(report_path.read_text())
    validate_report(report, build["unity"])
    if result.returncode != 0:
        raise ValueError("Player exit code: " + str(result.returncode))
    aggregate = {"success": True, "build": build, "player": report, "report": str(report_path), "assemblyAudit": audit}
    aggregate_path.write_text(json.dumps(aggregate, indent=2) + "\n")
    return aggregate


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--fixture", type=Path, required=True)
    parser.add_argument("--editor", type=Path)
    parser.add_argument("--target", default="StandaloneOSX", choices=["StandaloneOSX", "StandaloneLinux64", "StandaloneWindows64"])
    parser.add_argument("--backend", choices=["Mono", "IL2CPP"], default="Mono")
    parser.add_argument("--skip-build", action="store_true")
    args = parser.parse_args()
    try:
        result = run(args.fixture, args.editor, args.target, args.skip_build, args.backend)
        print(json.dumps(result, indent=2))
    except (ValueError, KeyError, OSError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
