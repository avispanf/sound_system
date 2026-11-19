import argparse
import hashlib
import json
import os
import re
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
from threading import Thread
from urllib.parse import unquote, urlsplit

from prepare_test_project import prepare


def verify_transfer(unity, catalog_name, log, traffic):
    native = next((line for line in log.splitlines() if line.startswith("PACKED_PLAYER_RUN True ")), "")
    sample = re.search(r" sample=(\d+) pcm=True retained=0 muted=True", native)
    if not sample or int(sample[1]) <= 0 or "unity=" + unity + " " not in native:
        raise RuntimeError("Native playback evidence is missing.")
    if not any(r["name"] == catalog_name and 0 < r["sent"] == r["size"] for r in traffic):
        raise RuntimeError("No HTTP catalog transfer recorded.")
    bundles = [request for request in traffic if request["name"].endswith(".bundle")]
    interrupted = {r["name"] for r in bundles[:-1] if 0 < r["sent"] < r["size"]}
    if not bundles or bundles[-1]["name"] not in interrupted or bundles[-1]["sent"] != bundles[-1]["size"] or "REMOTE_CATALOG interrupted=True released=True" not in log:
        raise RuntimeError("No successful retry of the interrupted bundle.")
    return {"unity": unity, "catalog": catalog_name, "bundleRequests": bundles, "player": native}


def run(directory, editor, archive, unity, addressables):
    directory.mkdir(parents=True, exist_ok=True)
    if any(directory.iterdir()):
        raise ValueError("Use an empty output directory for a new remote catalog run.")
    fixture = directory / "project"
    prepare(fixture, unity, archive, addressables)
    output = fixture / "ServerData"
    marker = directory / "retry.marker"
    traffic = []

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def do_GET(self):
            name = unquote(urlsplit(self.path).path).lstrip("/")
            if not name or Path(name).name != name:
                self.send_error(404)
                return
            path = output / name
            if not path.is_file():
                self.send_error(404)
                return
            data = path.read_bytes()
            interrupt = name.endswith(".bundle") and not marker.exists()
            size = min(len(data) // 2, 8192) if interrupt else len(data)
            self.send_response(200)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(data[:size])
            self.wfile.flush()
            self.close_connection = True
            traffic.append({"name": name, "size": len(data), "sent": size})

    base = [editor, "-batchmode", "-projectPath", fixture]
    with ThreadingHTTPServer(("127.0.0.1", 0), Handler) as server:
        origin = "http://127.0.0.1:" + str(server.server_port)
        env = dict(os.environ, AUDIOMW_CATALOG_ORIGIN=origin)
        worker = Thread(target=server.serve_forever, daemon=True)
        worker.start()
        try:
            for method, log in [("ValidateAddressables.RunPacked", "local-build"), ("BuildCatalogPlayer.BuildRemote", "remote-build")]:
                subprocess.run(base + ["-executeMethod", method, "-logFile", directory / (log + ".log")],
                               check=True, timeout=450, env=env)
            catalogs = [p for p in output.glob("catalog*") if p.suffix in (".json", ".bin")]
            if len(catalogs) != 1:
                raise RuntimeError("Expected one remote catalog, found " + str(catalogs))
            env.update(AUDIOMW_REMOTE_CATALOG=origin + "/" + catalogs[0].name, AUDIOMW_RETRY_MARKER=str(marker))
            player = fixture / "CatalogPlayer/AudioMW Packed Catalog.app/Contents/MacOS/AudioMW Packed Catalog"
            subprocess.run([player, "-batchmode", "-logFile", directory / "player.log"], check=True, timeout=100, env=env)
        finally:
            server.shutdown()
            worker.join()
            (directory / "http.json").write_text(json.dumps(traffic, indent=2) + "\n")
    log = (directory / "player.log").read_text()
    report = verify_transfer(unity, catalogs[0].name, log, traffic)
    report.update(addressables=addressables, archive=archive.name, sha256=hashlib.sha256(archive.read_bytes()).hexdigest())
    (directory / "verification.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    for field in ("directory", "editor", "archive"):
        parser.add_argument("--" + field, type=Path, required=True)
    parser.add_argument("--unity-version", required=True)
    parser.add_argument("--addressables-version", required=True)
    args = parser.parse_args()
    run(args.directory.resolve(), args.editor.resolve(), args.archive.resolve(), args.unity_version, args.addressables_version)
