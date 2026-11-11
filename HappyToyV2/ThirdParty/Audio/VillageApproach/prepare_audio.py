"""Rebuild Unity audio from the retained CC0 original recordings."""
import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
OUT = PROJECT / 'Assets' / 'Resources' / 'Audio' / 'VillageApproach'
ORIGINALS = {
    'dog-bark-original.ogg': '0ac194a829fd26d68c8cc47c76754576a419c5c2',
    'cicadas-original.ogg': '6b93430720d5af137884f509addff63ce6145844',
    'wind-chime-original.ogg': '1c41e7f03f9cbc886c15c60b80233964f91aaf99',
}
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--ffmpeg', default='ffmpeg', help='Path to FFmpeg 8.1')
args = parser.parse_args()
for name, expected in ORIGINALS.items():
    actual = hashlib.sha1((HERE / name).read_bytes()).hexdigest()
    if actual != expected:
        raise ValueError(f'Unexpected original checksum for {name}: {actual}')
OUT.mkdir(parents=True, exist_ok=True)
subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y',
                '-i', str(HERE / 'dog-bark-original.ogg'), '-ac', '1', '-ar', '48000',
                '-c:a', 'pcm_s16le', str(OUT / 'dog-bark.wav')], check=True)
shutil.copyfile(HERE / 'cicadas-original.ogg', OUT / 'cicadas.ogg')
subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y',
                '-i', str(HERE / 'wind-chime-original.ogg'), '-ac', '1', '-ar', '44100',
                '-c:a', 'pcm_s16le', str(OUT / 'wind-chime.wav')], check=True)
