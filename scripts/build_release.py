"""Deterministic VPM source package builder; Python 3 standard library only."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE_FILES = ('package.json', 'LICENSE', 'README.md', 'AGENTS.md', 'CHANGELOG.md', 'THIRD-PARTY.md')
LISTING_URL = 'https://raw.githubusercontent.com/tobenot/clientsim-avatar-library/main/vpm/index.json'


def package_members(root=ROOT):
    files = [root / p for p in PACKAGE_FILES]
    for directory in ('Editor', 'Tools~'):
        files.extend(p for p in (root / directory).rglob('*')
                     if p.is_file() and '__pycache__' not in p.parts and p.suffix not in ('.pyc', '.pyo'))
    # Only importer-side metas (the ~ folder is ignored by Unity).
    files.extend(root / (p + '.meta') for p in PACKAGE_FILES if p != 'package.json')
    files.append(root / 'Editor.meta')
    paths = sorted({p.relative_to(root).as_posix() for p in files})
    for name in paths:
        path = root / name
        if not path.is_file() or path.is_symlink():
            raise ValueError('Missing or symbolic package source: ' + name)
        if name.endswith(('.bundle', '.fbx', '.prefab', '.unity', '.png', '.jpg')):
            raise ValueError('Unexpected appearance/project payload: ' + name)
    return paths


def build(root=ROOT, output=None):
    root = Path(root)
    output = Path(output or root / 'dist')
    manifest = json.loads((root / 'package.json').read_text(encoding='utf-8'))
    name, version = manifest['name'], manifest['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', version):
        raise ValueError('Invalid SemVer')
    filename = f'{name}-{version}.zip'
    expected_url = f'https://github.com/tobenot/clientsim-avatar-library/releases/download/v{version}/{filename}'
    if manifest['url'] != expected_url:
        raise ValueError('Manifest URL does not match versioned release asset')
    if manifest.get('license') != 'MIT' or not manifest['author'].get('email'):
        raise ValueError('License or VPM author field missing')
    output.mkdir(parents=True, exist_ok=True)
    archive = output / filename
    members = package_members(root)
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for member in members:
            item = zipfile.ZipInfo(member, date_time=(1980, 1, 1, 0, 0, 0))
            item.compress_type = zipfile.ZIP_DEFLATED
            item.external_attr = 0o100644 << 16
            z.writestr(item, (root / member).read_bytes(), compresslevel=9)
    sha = hashlib.sha256(archive.read_bytes()).hexdigest()
    listing_path = root / 'vpm/index.json'
    previous = json.loads(listing_path.read_text()) if listing_path.exists() else {}
    versions = previous.get('packages', {}).get(name, {}).get('versions', {})
    versions[version] = dict(manifest, zipSHA256=sha)
    listing = dict(name='ClientSim Avatar Library', id='io.github.tobenot.clientsim-avatar-library.listing',
                   url=LISTING_URL, author=manifest['author']['email'],
                   packages={name: dict(versions=versions)})
    listing_path.parent.mkdir(exist_ok=True)
    listing_path.write_text(json.dumps(listing, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'package.json').write_bytes((root / 'package.json').read_bytes())
    sums = f'{sha}  {filename}\n{hashlib.sha256((output / "package.json").read_bytes()).hexdigest()}  package.json\n'
    (output / 'SHA256SUMS').write_text(sums, encoding='utf-8')
    with zipfile.ZipFile(archive) as z:
        if z.testzip() is not None or json.loads(z.read('package.json')) != manifest:
            raise ValueError('Built archive validation failed')
    return dict(archive=str(archive), bytes=archive.stat().st_size, sha256=sha, members=members, listing=str(listing_path))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    print(json.dumps(build(output=args.output), indent=2))


if __name__ == '__main__':
    main()
