#!/usr/bin/env python3
"""Read-only, cross-platform verification of the 2026-10-20 handoff baseline."""
import argparse
import hashlib
import json
import re
from pathlib import Path


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=Path.cwd())
    parser.add_argument('--manifest', type=Path,
                        default=Path(__file__).resolve().parent.parent / 'FILE_MANIFEST.json')
    args = parser.parse_args()
    root = args.project_root.resolve()
    manifest = json.loads(args.manifest.read_text(encoding='utf-8'))
    errors = []
    for entry in manifest['files']:
        relative = Path(entry['project_path'])
        path = (root / relative).resolve()
        if relative.is_absolute() or root not in path.parents:
            errors.append(('INVALID_PATH', str(relative)))
        elif not path.is_file():
            errors.append(('MISSING', str(relative)))
        elif path.stat().st_size != entry['size'] or sha256(path) != entry['sha256']:
            errors.append(('CHANGED', str(relative)))
    for entry in manifest.get('unity_guids', []):
        path = root / entry['meta_path']
        match = re.search(r'^guid:\s*(\w+)\s*$', path.read_text(encoding='utf-8'), re.M) if path.is_file() else None
        if match is None or match.group(1) != entry['guid']:
            errors.append(('GUID_MISMATCH', entry['meta_path']))
    for status, path in errors[:50]:
        print(status, path)
    if len(errors) > 50:
        print('... additional mismatches:', len(errors) - 50)
    print(json.dumps({'files_checked': len(manifest['files']), 'mismatches': len(errors),
                      'status': 'PASS' if not errors else 'REVIEW_REQUIRED',
                      'read_only': True}, ensure_ascii=False))
    if errors:
        print('Compare changes before restoring anything; legitimate later edits also differ from this baseline.')
    return 1 if errors else 0


if __name__ == '__main__':
    raise SystemExit(main())
