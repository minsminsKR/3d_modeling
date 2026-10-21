"""Acquire the selected original CC0 scans through Poly Haven's public API.

Downloaded PNG bytes are preserved unchanged, verified against the API's MD5
and size, and recorded with SHA256. Runtime derivation is a separate offline
step in build_surfaces.py. --verify-only performs no network or mutations.
"""
from pathlib import Path
import argparse
import concurrent.futures
import datetime
import hashlib
import json
import urllib.request

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
OUT = HERE / 'Acquired'
SELECTION = ('rough_plaster_broken', 'worn_mossy_plasterwall', 'peeling_painted_wall', 'dirty_tiles')
CHANNELS = ('Diffuse', 'nor_gl', 'arm', 'Displacement')
AGENT = 'HappyToyV2-SchoolAuthoring/1.0 (https://github.com/minsminsKR/3d_modeling)'


def digest(path, algorithm='sha256'):
    h = hashlib.new(algorithm)
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def read(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': AGENT}), timeout=90) as response:
        return response.read()


def verify(manifest):
    errors = []
    for asset in manifest['assets']:
        for item in asset['sources'].values():
            p = PROJECT / item['path']
            if not p.is_file() or p.stat().st_size != item['bytes'] or digest(p) != item['sha256']:
                errors.append(item['path'])
            elif digest(p, 'md5') != item['officialMd5']:
                errors.append(item['path'] + ' (official MD5)')
    print(json.dumps({'status': 'ACQUIRED_SCAN_HASHES_PASS' if not errors else 'FAIL',
                      'assets': len(manifest['assets']), 'originalMaps': sum(len(a['sources']) for a in manifest['assets']),
                      'bytes': sum(i['bytes'] for a in manifest['assets'] for i in a['sources'].values()), 'errors': errors}))
    return not errors


def acquire():
    OUT.mkdir(parents=True, exist_ok=True)
    assets, jobs = [], []
    for slug in SELECTION:
        folder = OUT / slug
        folder.mkdir(parents=True, exist_ok=True)
        info_bytes, files_bytes = read('https://api.polyhaven.com/info/' + slug), read('https://api.polyhaven.com/files/' + slug)
        info, files = json.loads(info_bytes), json.loads(files_bytes)
        (folder / 'info.json').write_bytes(info_bytes)
        (folder / 'files.json').write_bytes(files_bytes)
        record = {'slug': slug, 'sourcePage': 'https://polyhaven.com/a/' + slug,
                  'license': 'CC0-1.0', 'licensePage': 'https://polyhaven.com/license',
                  'authors': info.get('authors', {}), 'physicalMetres': [n / 1000 for n in info['dimensions']],
                  'apiInfoSha256': digest(folder / 'info.json'), 'apiFilesSha256': digest(folder / 'files.json'), 'sources': {}}
        for channel in CHANNELS:
            entry = files[channel]['2k']['png']
            target = folder / entry['url'].rsplit('/', 1)[-1]
            record['sources'][channel] = {'url': entry['url'], 'path': target.relative_to(PROJECT).as_posix(),
                                         'officialMd5': entry['md5'], 'officialBytes': entry['size']}
            jobs.append((target, entry, record['sources'][channel]))
        assets.append(record)

    def fetch(job):
        path, expected, record = job
        if not path.is_file() or path.stat().st_size != expected['size'] or digest(path, 'md5') != expected['md5']:
            data = read(expected['url'])
            if len(data) != expected['size'] or hashlib.md5(data).hexdigest() != expected['md5']:
                raise ValueError('Official source checksum/size mismatch: ' + str(path))
            path.write_bytes(data)
        record['bytes'], record['sha256'] = path.stat().st_size, digest(path)
        return path.name, path.stat().st_size

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
        for name, size in executor.map(fetch, jobs):
            print(json.dumps({'downloaded': name, 'bytes': size}), flush=True)
    license_path = OUT / 'CC0-1.0.txt'
    license_path.write_bytes(read('https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt'))
    manifest = {'acquiredUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'provider': 'Poly Haven',
                'api': 'https://api.polyhaven.com', 'apiCredit': 'Powered by Poly Haven',
                'resolution': [2048, 2048], 'generator': 'SourceArt/SchoolHorror/acquire_photographed_sources.py',
                'generatorSha256': digest(Path(__file__)),
                'licenseFile': license_path.relative_to(PROJECT).as_posix(), 'licenseSha256': digest(license_path),
                'licenseSource': 'https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt',
                'assets': assets}
    (OUT / 'acquisition-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args()
    manifest = json.loads((OUT / 'acquisition-manifest.json').read_text()) if args.verify_only else acquire()
    raise SystemExit(0 if verify(manifest) else 1)
