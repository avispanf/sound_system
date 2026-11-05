import argparse
import json
from pathlib import Path
import re
import shutil


def prepare(destination, unity_version, archive=None):
    if not re.fullmatch(r"(?:2022\.3|6000\.\d+)\.\d+[abfp]\d+", unity_version):
        raise ValueError("Use a supported full Unity version, for example 2022.3.62f3.")
    if archive is not None:
        archive = archive.resolve(strict=True)
    repository = Path(__file__).resolve().parents[1]
    destination = destination.resolve()
    marker = destination / ".audiomw-test-project"
    if destination.exists() and any(destination.iterdir()) and not marker.is_file():
        raise ValueError("Destination must be empty or an AudioMW-generated test project.")
    source = repository / "Assets" / "AudioMW"
    if destination == repository or destination in repository.parents or destination == source or source in destination.parents:
        raise ValueError("The source repository cannot be a test-project destination.")
    destination.mkdir(parents=True, exist_ok=True)
    marker.write_text("AudioMW generated compatibility fixture\n")
    package = destination / "Assets" / "AudioMW"
    if package.exists():
        shutil.rmtree(package)
    if archive is None:
        shutil.copytree(source, package)
    dependencies = {"com.unity.test-framework": "1.1.33" if unity_version.startswith("2022.") else "1.6.0"}
    for module in ("audio", "physics", "physics2d", "imgui", "jsonserialize", "ui", "uielements"):
        dependencies["com.unity.modules." + module] = "1.0.0"
    manifest = {"dependencies": dependencies}
    if archive is not None:
        dependencies["com.avispanf.audiomw"] = "file:" + archive.as_posix()
        manifest["testables"] = ["com.avispanf.audiomw"]
        validation = destination / "Assets" / "Editor" / "AudioMWDistributionValidation"
        validation.mkdir(parents=True, exist_ok=True)
        shutil.copy2(repository / "Tools" / "PackageValidation" / "ValidateDistribution.cs", validation)
    (destination / "Packages").mkdir(exist_ok=True)
    (destination / "Packages" / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    (destination / "ProjectSettings").mkdir(exist_ok=True)
    (destination / "ProjectSettings" / "ProjectVersion.txt").write_text("m_EditorVersion: " + unity_version + "\n")
    print("Prepared AudioMW test project: " + str(destination))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--unity-version", required=True)
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    prepare(args.destination, args.unity_version, args.archive)
