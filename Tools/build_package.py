import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import tarfile


SAMPLE = {"displayName": "Demo Scene", "description": "Interactive demo with synthesized placeholder audio.", "path": "Samples~/Demo"}
DOCS = ("getting-started.md", "api-reference.md", "music.md", "optimisation.md", "authoring-and-runtime.md", "ui-design.md", "distribution.md", "runtime-profiling.md", "loading-and-pause.md", "gain-authoring.md")


def collect(repository, version=None):
    source = repository / "Assets" / "AudioMW"
    manifest = json.loads((source / "package.json").read_text())
    if version:
        if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version):
            raise ValueError("Invalid package version")
        manifest["version"] = version
    manifest["samples"] = [SAMPLE]
    files = {}
    for root, prefix in ((source, ""), (repository / "Assets" / "AudioMW Demo", "Samples~/Demo/")):
        for path in sorted(root.rglob("*")):
            if path.is_symlink():
                raise ValueError("Package inputs must not contain symlinks: " + str(path))
            if path.is_file() and not path.name.startswith("."):
                files[prefix + path.relative_to(root).as_posix()] = path.read_bytes()
    files["package.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    files["README.md"] = (repository / "Tools" / "PackageREADME.md").read_bytes()
    for name in DOCS:
        files["Documentation~/" + name] = (repository / "Documentation" / name).read_bytes()
    guids = {}
    for name, data in files.items():
        if name.endswith(".meta"):
            match = re.search(rb"^guid: ([a-f0-9]{32})$", data, re.M)
            if match:
                guid = match[1].decode()
                if guid in guids:
                    raise ValueError("Duplicate GUID: " + name + " and " + guids[guid])
                guids[guid] = name
    for name, data in files.items():
        if name.endswith((".unity", ".asset", ".mat", ".prefab")):
            for guid in re.findall(rb"guid: ([a-f0-9]{32})", data):
                value = guid.decode()
                if not value.startswith("0000000000000000") and value not in guids:
                    raise ValueError("Unshipped asset reference " + value + " in " + name)
    if not any(name.endswith(".unity") for name in files if name.startswith(SAMPLE["path"] + "/")):
        raise ValueError("The declared sample must contain a scene")
    return manifest, files


def build(repository, output, version=None):
    manifest, files = collect(repository, version)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("wb") as raw:
        with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as zipped:
            with tarfile.open(fileobj=zipped, mode="w", format=tarfile.USTAR_FORMAT) as archive:
                for name, data in sorted(files.items()):
                    entry = tarfile.TarInfo("package/" + name)
                    entry.size = len(data)
                    entry.mode = 0o644
                    entry.mtime = 0
                    archive.addfile(entry, io.BytesIO(data))
    result = {"name": manifest["name"], "version": manifest["version"], "files": len(files), "sha256": hashlib.sha256(output.read_bytes()).hexdigest()}
    output.with_suffix(".json").write_text(json.dumps(result, indent=2) + "\n")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--version")
    args = parser.parse_args()
    print(json.dumps(build(Path(__file__).resolve().parents[1], args.output.resolve(), args.version)))
