import argparse
import json
from pathlib import Path
import shutil
import tarfile


def prepare(destination, template, archive):
    destination = destination.resolve()
    if destination.exists() and any(destination.iterdir()):
        raise ValueError("Use an empty destination. Existing playground edits are never overwritten.")
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(template) as package:
        for member in package.getmembers():
            prefix = "package/ProjectData~/"
            if not member.name.startswith(prefix) or not member.isfile():
                continue
            relative = Path(member.name[len(prefix):])
            if not relative.parts or relative.parts[0] not in ("Assets", "Packages", "ProjectSettings") or ".." in relative.parts:
                continue
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(package.extractfile(member).read())
    source = Path(__file__).resolve().parent / "ManualPlayground"
    shutil.copytree(source / "Runtime", destination / "Assets/PlaygroundRuntime")
    shutil.copytree(source / "Editor", destination / "Assets/Editor/PlaygroundSetup")
    shutil.copytree(source / "Tests", destination / "Assets/PlaygroundTests")
    (destination / "Packages").mkdir(exist_ok=True)
    (destination / "ProjectSettings").mkdir(exist_ok=True)
    shutil.copy2(archive, destination / "Packages/AudioMW.tgz")
    manifest = {"dependencies": {"com.unity.render-pipelines.universal": "17.3.0", "com.unity.inputsystem": "1.20.0", "com.unity.test-framework": "1.6.0", "com.avispanf.audiomw": "file:AudioMW.tgz"}, "testables": ["com.avispanf.audiomw"]}
    for module in ("audio", "physics", "physics2d", "imgui", "jsonserialize", "ui", "uielements"):
        manifest["dependencies"]["com.unity.modules." + module] = "1.0.0"
    (destination / "Packages/manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    (destination / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.3.21f1\n")
    (destination / ".audiomw-playground").write_text("AudioMW generated Unity 6 URP manual integration playground\n")
    (destination / ".gitignore").write_text("Library/\nTemp/\nObj/\nLogs/\nUserSettings/\nBuild/\nBuilds/\n")
    shutil.copy2(source / "README.md", destination / "README.md")
    print(destination)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--template", type=Path, required=True)
    parser.add_argument("--archive", type=Path, required=True)
    args = parser.parse_args()
    prepare(args.destination, args.template, args.archive)
