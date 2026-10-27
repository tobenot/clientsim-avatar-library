"""Source/package checks, not a substitute for Unity runtime verification."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('build_release', ROOT / 'scripts/build_release.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PackageChecks(unittest.TestCase):
    def test_editor_only_assembly_and_dependencies(self):
        assembly = json.loads((ROOT / 'Editor/Luobei.ClientSimAvatarLibrary.Editor.asmdef').read_text())
        self.assertEqual(assembly['includePlatforms'], ['Editor'])
        self.assertIn('VRC.ClientSim', assembly['references'])
        manifest = json.loads((ROOT / 'package.json').read_text())
        self.assertIn('com.vrchat.worlds', manifest['vpmDependencies'])
        self.assertEqual(manifest['license'], 'MIT')
        self.assertIn('"' + manifest['version'] + '"', (ROOT / 'Editor/ExternalActors.cs').read_text())

    def test_deterministic_source_zip_and_listing(self):
        with tempfile.TemporaryDirectory(prefix='clientsim-package-') as tmp:
            first = module.build(output=Path(tmp) / 'one')
            second = module.build(output=Path(tmp) / 'two')
            self.assertEqual(first['sha256'], second['sha256'])
            with zipfile.ZipFile(first['archive']) as z:
                self.assertIsNone(z.testzip())
                self.assertIn('package.json', z.namelist())
                self.assertIn('Tools~/actors.py', z.namelist())
                self.assertNotIn('device.py', z.namelist())
                self.assertTrue(all(not n.startswith(('/', '../')) for n in z.namelist()))
            listing = json.loads((ROOT / 'vpm/index.json').read_text())
            manifest = json.loads((ROOT / 'package.json').read_text())
            published = listing['packages'][manifest['name']]['versions'][manifest['version']]
            self.assertEqual(published['zipSHA256'], first['sha256'])

    def test_meta_guids_unique(self):
        guids = []
        for p in ROOT.rglob('*.meta'):
            if 'dist' in p.parts:
                continue
            guid = next(line.split(':', 1)[1].strip() for line in p.read_text().splitlines() if line.startswith('guid:'))
            self.assertEqual(len(guid), 32)
            guids.append(guid)
        self.assertEqual(len(guids), len(set(guids)))
        self.assertGreater(len(guids), 0)


if __name__ == '__main__':
    unittest.main()
