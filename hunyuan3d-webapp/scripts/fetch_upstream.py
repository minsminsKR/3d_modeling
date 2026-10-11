"""Restore pinned upstream code and reviewed patches; never reset an existing checkout."""
from __future__ import annotations
import argparse
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def git(*args: str, cwd: Path | None = None, check: bool = True):
    return subprocess.run(['git', *args], cwd=cwd, check=check, capture_output=True, text=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--only', nargs='+', help='Names under vendor/, e.g. TRELLIS.2 trellis-extensions')
    parser.add_argument('--check', action='store_true', help='Check existing sources without downloading or modifying them')
    args = parser.parse_args()
    manifest = json.loads((ROOT / 'upstream/pins.json').read_text())
    for item in manifest['repositories']:
        if args.only and item['path'].split('/')[1] not in args.only:
            continue
        repo = (ROOT / item['path']).resolve()
        if not repo.is_relative_to(ROOT / 'vendor'):
            raise ValueError('Upstream path escapes vendor/')
        if not (repo / '.git').exists():
            if args.check:
                raise RuntimeError(f'Missing upstream source: {item["path"]}')
            if repo.exists():
                raise RuntimeError(f'Refusing to overwrite a non-Git directory: {repo}')
            repo.parent.mkdir(parents=True, exist_ok=True)
            git('clone', '--no-checkout', '--filter=blob:none', item['url'], str(repo))
            git('checkout', '--detach', item['revision'], cwd=repo)
        actual = git('rev-parse', 'HEAD', cwd=repo).stdout.strip()
        if actual != item['revision']:
            raise RuntimeError(f'{item["path"]}: expected {item["revision"]}, found {actual}; preserve edits and use a fresh directory')
        if not args.check:
            git('submodule', 'update', '--init', '--recursive', cwd=repo)
        if item.get('patch'):
            patch = str(ROOT / item['patch'])
            applied = git('apply', '--reverse', '--check', patch, cwd=repo, check=False).returncode == 0
            if not applied:
                if args.check:
                    raise RuntimeError(f'Missing compatibility patch: {item["path"]}')
                git('apply', '--check', patch, cwd=repo)
                git('apply', patch, cwd=repo)
        print(item['path'], item['revision'], 'ready')

if __name__ == '__main__':
    main()
