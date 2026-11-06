import json
from pathlib import Path
import sys
import tarfile
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_package import DOCS, build, collect


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        source = self.root / "Assets" / "AudioMW"
        sample = self.root / "Assets" / "AudioMW Demo"
        source.mkdir(parents=True)
        sample.mkdir()
        (self.root / "Documentation").mkdir()
        (self.root / "Tools").mkdir()
        (self.root / "Tools" / "PackageREADME.md").write_text("Preview")
        for name in DOCS:
            (self.root / "Documentation" / name).write_text(name)
        (source / "package.json").write_text(json.dumps({"name": "com.avispanf.audiomw", "version": "0.4.0"}))
        (sample / "Demo.unity").write_text("%YAML 1.1\n")

    def tearDown(self):
        self.temp.cleanup()

    def test_repeatable_archive_contains_declared_sample(self):
        first = self.root / "first.tgz"
        second = self.root / "second.tgz"
        build(self.root, first, "0.5.0-preview.1")
        build(self.root, second, "0.5.0-preview.1")
        self.assertEqual(first.read_bytes(), second.read_bytes())
        with tarfile.open(first) as archive:
            manifest = json.load(archive.extractfile("package/package.json"))
            metadata = archive.extractfile("package/README.md.meta").read()
            self.assertRegex(metadata, rb"guid: [a-f0-9]{32}")
            self.assertIn("package/" + manifest["samples"][0]["path"] + "/Demo.unity", archive.getnames())

    def test_missing_dependency_rejects_archive(self):
        (self.root / "Assets" / "AudioMW Demo" / "Demo.unity").write_text("guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
        with self.assertRaisesRegex(ValueError, "Unshipped asset reference"):
            collect(self.root)

    def test_duplicate_guids_reject_archive(self):
        source = self.root / "Assets" / "AudioMW"
        for name in ("A.meta", "B.meta"):
            (source / name).write_text("guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n")
        with self.assertRaisesRegex(ValueError, "Duplicate GUID"):
            collect(self.root)

    def test_symlinks_cannot_include_external_content(self):
        (self.root / "Assets" / "AudioMW" / "link").symlink_to(self.root / "Tools")
        with self.assertRaisesRegex(ValueError, "symlinks"):
            collect(self.root)


if __name__ == "__main__":
    unittest.main()
