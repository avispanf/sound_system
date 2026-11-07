from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from prepare_test_project import prepare, archive_dependency


class FixtureTests(unittest.TestCase):
    def test_tarball_path_is_portable_and_player_helpers_are_copied(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / "package.tgz"
            archive.write_bytes(b"fixture placeholder")
            destination = root / "Project"
            with redirect_stdout(io.StringIO()):
                prepare(destination, "2022.3.62f3", archive)
            manifest = json.loads((destination / "Packages/manifest.json").read_text())
            dependency = manifest["dependencies"]["com.avispanf.audiomw"]
            self.assertEqual("file:../../package.tgz", dependency)
            self.assertEqual(archive.resolve(), (destination / "Packages" / dependency[5:]).resolve())
            self.assertTrue((destination / "Assets/AudioMWPlayerValidation/AudioMWPlayerSmoke.cs").is_file())
            self.assertTrue((destination / "Assets/Editor/AudioMWDistributionValidation/BuildPlayerSmoke.cs").is_file())

    def test_cross_drive_archives_keep_an_absolute_fallback(self):
        with patch("prepare_test_project.os.path.relpath", side_effect=ValueError("different drives")):
            self.assertEqual("file:/archive/package.tgz", archive_dependency(Path("/project"), Path("/archive/package.tgz")))

    def test_non_owned_directory_is_preserved(self):
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary)
            user_file = destination / "user.txt"
            user_file.write_text("keep")
            with self.assertRaises(ValueError):
                prepare(destination, "2022.3.62f3")
            self.assertEqual("keep", user_file.read_text())
            self.assertFalse((destination / ".audiomw-test-project").exists())
