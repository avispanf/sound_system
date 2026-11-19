from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_remote_catalog import verify_transfer


class RemoteCatalogTests(unittest.TestCase):
    log = ("REMOTE_CATALOG interrupted=True released=True\n"
           "PACKED_PLAYER_RUN True unity=6000.3.21f1 provider=BundledAssetProvider sample=100 pcm=True retained=0 muted=True")
    catalog = {"name": "catalog.bin", "sent": 100, "size": 100}
    interrupted = {"name": "clip.bundle", "sent": 8, "size": 1000}
    complete = {"name": "clip.bundle", "sent": 1000, "size": 1000}

    def test_requires_download_recovery_and_native_playback(self):
        requests = [self.catalog, self.interrupted, self.complete]
        report = verify_transfer("6000.3.21f1", "catalog.bin", self.log, requests)
        self.assertEqual(report["bundleRequests"], requests[1:])
        for log, traffic in [
            (self.log, requests[1:]),
            (self.log, [self.catalog, self.complete]),
            (self.log, [self.catalog, self.complete, self.interrupted]),
            (self.log, [self.catalog, self.interrupted, dict(self.complete, name="other.bundle")]),
            (self.log.splitlines()[-1], requests),
            (self.log.replace("sample=100", "sample=0"), requests),
        ]:
            with self.subTest(log=log, traffic=traffic), self.assertRaises(RuntimeError):
                verify_transfer("6000.3.21f1", "catalog.bin", log, traffic)
