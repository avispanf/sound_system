import copy
import json
import tempfile
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_player_smoke import REQUIRED, player_executable, validate_report, run, audit_assemblies


class PlayerReportTests(unittest.TestCase):
    def setUp(self):
        self.report = {"success": True, "unity": "2022.3.62f3", "dspAdvanced": True, "checks": sorted(REQUIRED)}

    def test_complete_report_is_accepted(self):
        validate_report(self.report, "2022.3.62f3")

    def test_incomplete_or_wrong_player_cannot_pass(self):
        mutations = [{"success": False}, {"dspAdvanced": False}, {"checks": ["simple playback admitted"]}, {"unity": "6000.3.21f1"}]
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                report = copy.deepcopy(self.report)
                report.update(mutation)
                with self.assertRaises(ValueError):
                    validate_report(report, "2022.3.62f3")

    def test_failed_run_does_not_leave_a_stale_success_report(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = Path(temporary)
            (fixture / ".audiomw-test-project").write_text("fixture")
            aggregate = fixture / "player-validation-Mono.json"
            aggregate.write_text('{"success": true}')
            (fixture / "player-build-Mono.json").write_text('{"success": false}')
            with self.assertRaises(ValueError):
                run(fixture, skip_build=True)
            self.assertFalse(aggregate.exists())

    def test_mono_build_rejects_editor_or_test_assembly_leaks(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = Path(temporary).resolve()
            player = fixture / "Player.app"
            managed = player / "Contents/Resources/Data/Managed"
            managed.mkdir(parents=True)
            (managed / "AudioMW.Runtime.dll").write_bytes(b"runtime")
            build = {"player": str(player), "backend": "Mono"}
            self.assertTrue(audit_assemblies(fixture, build)["managedAudit"])
            (managed / "AudioMW.Editor.dll").write_bytes(b"editor")
            with self.assertRaises(ValueError):
                audit_assemblies(fixture, build)

    def test_build_report_cannot_launch_an_external_executable(self):
        with self.assertRaises(ValueError):
            player_executable(Path("/tmp/fixture"), {"player": "/bin/sh"})
        self.assertEqual(Path("/tmp/fixture/Test.app/Contents/MacOS/AudioMWSmoke").resolve(), player_executable(Path("/tmp/fixture"), {"player": "/tmp/fixture/Test.app"}))
