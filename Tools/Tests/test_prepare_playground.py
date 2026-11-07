from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import sys
import tarfile
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from prepare_playground import prepare


class PlaygroundPreparationTests(unittest.TestCase):
    def test_template_is_filtered_and_package_install_is_portable(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            template = root / "template.tgz"
            with tarfile.open(template, "w:gz") as package:
                for name in ("Assets/Template.txt", "ProjectSettings/ProjectSettings.asset", "Library/Cache", "Assets/../../escape.txt"):
                    item = tarfile.TarInfo("package/ProjectData~/" + name)
                    item.size = 4
                    package.addfile(item, io.BytesIO(b"test"))
                link = tarfile.TarInfo("package/ProjectData~/Assets/Link")
                link.type = tarfile.SYMTYPE
                link.linkname = "/tmp"
                package.addfile(link)
            archive = root / "audio.tgz"
            archive.write_bytes(b"package archive")
            destination = root / "Project"
            with redirect_stdout(io.StringIO()):
                prepare(destination, template, archive)
            manifest = json.loads((destination / "Packages/manifest.json").read_text())
            self.assertEqual("file:AudioMW.tgz", manifest["dependencies"]["com.avispanf.audiomw"])
            self.assertEqual(archive.read_bytes(), (destination / "Packages/AudioMW.tgz").read_bytes())
            self.assertTrue((destination / "Assets/Template.txt").is_file())
            self.assertTrue((destination / "Assets/PlaygroundRuntime/AudioMWPlayground.cs").is_file())
            self.assertTrue((destination / "Assets/PlaygroundTests/RelayFieldIntegrationTests.cs").is_file())
            self.assertFalse((destination / "Library").exists())
            self.assertFalse((destination / "Assets/Link").exists())
            self.assertFalse((root / "escape.txt").exists())
            self.assertTrue((destination / ".audiomw-playground").exists())

    def test_existing_playground_edits_are_never_overwritten(self):
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary)
            user_file = destination / "user.scene"
            user_file.write_text("keep my work")
            with self.assertRaises(ValueError):
                prepare(destination, destination / "missing-template", destination / "missing-archive")
            self.assertEqual("keep my work", user_file.read_text())
            self.assertEqual([user_file], list(destination.iterdir()))
