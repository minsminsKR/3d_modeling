"""Deterministic opt-in forest-earth quilting candidate; not production art.

The preserved imagegen original is the sole source.  The 2048px candidate
covers 2x2 source tiles, using unblended crops and minimum-error overlap cuts.
See README-candidate.md for dependencies, limitations and Unity workflow.
"""
from pathlib import Path
import argparse
import hashlib
import json
import tempfile
import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parents[3]
SOURCE = PROJECT / 'SourceArt/TempleForest/forest-earth/imagegen-original.png'
ASSETS = PROJECT / 'Assets/Resources/TempleForest/forest-earth-candidate'
EXPECTED = '27ca10c3e6fab171c0feba68eab3afbaea5ab7351fb814f34c8e1cc2e6cb28bf'
SEED, SOURCE_SIZE, PATCH, OVERLAP, STEP, OUTPUT = 211107, 1024, 640, 128, 512, 2048
NAMES = ('forest-earth-candidate-albedo.png', 'forest-earth-candidate-height.png')

def vertical_cut(cost):
    h, w = cost.shape
    last = cost[0].astype(np.float32)
    back = np.zeros((h, w), np.int8)
    for y in range(1, h):
        candidates = np.stack((np.r_[np.inf, last[:-1]], last, np.r_[last[1:], np.inf]))
        choice = np.argmin(candidates, axis=0)
        back[y] = choice.astype(np.int8) - 1
        last = cost[y] + candidates[choice, np.arange(w)]
    seam = np.empty(h, dtype=np.int32)
    seam[-1] = np.argmin(last)
    for y in range(h-1, 0, -1):
        seam[y-1] = seam[y] + back[y, seam[y]]
    return seam

def derive():
    source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    if source_hash != EXPECTED:
        raise ValueError('Original source hash changed: ' + source_hash)
    original = np.asarray(Image.open(SOURCE).convert('RGB').resize((SOURCE_SIZE, SOURCE_SIZE), Image.Resampling.LANCZOS), dtype=np.uint8)
    canvas = np.zeros((OUTPUT, OUTPUT, 3), dtype=np.uint8)
    used = np.zeros((OUTPUT, OUTPUT), dtype=bool)
    labels = np.full((OUTPUT, OUTPUT), -1, dtype=np.int8)
    rng = np.random.default_rng(SEED)
    for row in range(4):
        for col in range(4):
            ys = (row*STEP + np.arange(PATCH)) % OUTPUT
            xs = (col*STEP + np.arange(PATCH)) % OUTPUT
            index = np.ix_(ys, xs)
            prior, occupied = canvas[index], used[index]
            probe = occupied[::4, ::4]
            best, best_score = None, float('inf')
            for _ in range(32):
                x = int(rng.integers(0, SOURCE_SIZE-PATCH+1))
                y = int(rng.integers(0, SOURCE_SIZE-PATCH+1))
                rotation = int(rng.integers(0, 4))
                candidate = np.rot90(original[y:y+PATCH, x:x+PATCH], rotation)
                score = 0 if not np.any(probe) else np.abs(
                    candidate[::4, ::4].astype(np.int16) - prior[::4, ::4].astype(np.int16))[probe].mean()
                if score < best_score:
                    best_score, best = score, candidate.copy()
            error = np.abs(best.astype(np.int16) - prior.astype(np.int16)).mean(axis=2)
            use_new = np.ones((PATCH, PATCH), dtype=bool)
            if col > 0:
                seam = vertical_cut(error[:, :OVERLAP]); use_new[:, :OVERLAP] &= np.arange(OVERLAP)[None, :] >= seam[:, None]
            if col == 3:
                seam = vertical_cut(error[:, -OVERLAP:]); use_new[:, -OVERLAP:] &= np.arange(OVERLAP)[None, :] <= seam[:, None]
            if row > 0:
                seam = vertical_cut(error[:OVERLAP, :].T); use_new[:OVERLAP, :] &= np.arange(OVERLAP)[:, None] >= seam[None, :]
            if row == 3:
                seam = vertical_cut(error[-OVERLAP:, :].T); use_new[-OVERLAP:, :] &= np.arange(OVERLAP)[:, None] <= seam[None, :]
            take = ~occupied | use_new
            merged = prior.copy(); merged[take] = best[take]
            canvas[index] = merged; used[index] = True
            region_labels = labels[index]; region_labels[take] = row*4+col; labels[index] = region_labels
    if not used.all():
        raise ValueError('Candidate has uncovered pixels')
    # Move the periodic cut to a location crossing no quilt seam at all.
    # Each pair of pixels across the final outer edge comes from one source
    # crop at adjacent coordinates; this is not a mirrored image or a blur.
    cuts = []
    for axis in (0, 1):
        count = (labels != np.roll(labels, 1, axis=axis)).sum(axis=1-axis)
        difference = np.abs(canvas.astype(np.int16) - np.roll(canvas.astype(np.int16), 1, axis=axis)).mean(axis=(1-axis, 2))
        choices = np.flatnonzero(count == count.min())
        cut = int(choices[np.argmin(difference[choices])])
        if count[cut] != 0:
            raise ValueError('No uninterrupted source pixels at the periodic cut')
        cuts.append(cut)
    canvas = np.roll(canvas, shift=(-cuts[0], -cuts[1]), axis=(0, 1))
    labels = np.roll(labels, shift=(-cuts[0], -cuts[1]), axis=(0, 1))
    return canvas, labels

def report(canvas, labels):
    pixel = canvas.astype(np.int16)
    summary = {'source_sha256': EXPECTED, 'seed': SEED, 'output': OUTPUT,
               'input_tile_equivalents': 2, 'unity_wrap': 'Repeat',
               'note': 'Candidate uses source-derived luminance normal, not a captured PBR height map.'}
    for axis in (0, 1):
        difference = np.abs(np.diff(pixel, axis=axis)).mean(axis=2)
        seam = np.diff(labels.astype(np.int16), axis=axis) != 0
        all_difference = np.abs(pixel - np.roll(pixel, 1, axis=axis))
        edge = np.take(all_difference, 0, axis=axis)
        interior = np.take(all_difference, np.arange(1, OUTPUT), axis=axis)
        crossing = np.count_nonzero(np.take(labels, 0, axis=axis) != np.take(labels, -1, axis=axis))
        if crossing:
            raise ValueError('Periodic edge crosses an internal source patch seam')
        if edge.mean() > interior.mean() * 1.2 or np.percentile(edge, 95) > np.percentile(interior, 95) * 1.2:
            raise ValueError('Periodic edge color gradient is unusually strong')
        summary['axis_'+str(axis)] = {'seam_mean': round(float(difference[seam].mean()), 4),
            'other_mean': round(float(difference[~seam].mean()), 4),
            'seam_p95': round(float(np.percentile(difference[seam],95)), 4),
            'other_p95': round(float(np.percentile(difference[~seam],95)), 4),
            'edge_patch_discontinuities': int(crossing), 'repeat_edge_mean': round(float(edge.mean()), 4),
            'repeat_interior_mean': round(float(interior.mean()), 4),
            'repeat_edge_p95': round(float(np.percentile(edge,95)), 4),
            'repeat_interior_p95': round(float(np.percentile(interior,95)), 4)}
    # CPU luminance central-difference normals are a guard, not a replacement
    # for checking Unity's actual normal conversion/compression and GPU mips.
    rgb = canvas.astype(np.float32) / 255
    luminance = rgb @ np.array([.2126, .7152, .0722], dtype=np.float32)
    dx = (np.roll(luminance,-1,axis=1)-np.roll(luminance,1,axis=1))*.5
    dy = (np.roll(luminance,-1,axis=0)-np.roll(luminance,1,axis=0))*.5
    normal = np.stack((-dx,-dy,np.full_like(dx,.2)),axis=-1)
    normal /= np.linalg.norm(normal,axis=2,keepdims=True)
    for axis in (0, 1):
        delta = np.linalg.norm(normal - np.roll(normal,1,axis=axis),axis=2)
        edge = np.take(delta,0,axis=axis); interior = np.take(delta,np.arange(1,OUTPUT),axis=axis)
        if edge.mean() > interior.mean()*1.2 or np.percentile(edge,95) > np.percentile(interior,95)*1.2:
            raise ValueError('Periodic edge normal-proxy gradient is unusually strong')
        summary['axis_'+str(axis)]['normal_proxy_edge_mean'] = round(float(edge.mean()), 4)
        summary['axis_'+str(axis)]['normal_proxy_interior_mean'] = round(float(interior.mean()), 4)
    return summary

def main():
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--preview', action='store_true', help='Write a temporary preview outside the project')
    group.add_argument('--write', action='store_true', help='Create new candidate Unity inputs; refuses overwrite')
    group.add_argument('--verify', action='store_true', help='Recompute and compare candidate Unity pixels')
    args = parser.parse_args()
    canvas, labels = derive()
    if args.preview:
        path = Path(tempfile.gettempdir())/'happytoy-forest-earth-candidate-review'/'candidate-repro.png'
        path.parent.mkdir(parents=True, exist_ok=True)
        Image.fromarray(canvas, 'RGB').save(path)
    elif args.write:
        ASSETS.mkdir(parents=True, exist_ok=True)
        paths = [ASSETS/name for name in NAMES]
        if any(path.exists() for path in paths):
            raise FileExistsError('Candidate Unity assets already exist; use --verify rather than overwrite')
        for path in paths: Image.fromarray(canvas, 'RGB').save(path)
    else:
        for name in NAMES:
            path = ASSETS/name
            if not path.is_file() or not np.array_equal(np.asarray(Image.open(path).convert('RGB')), canvas):
                raise ValueError('Candidate differs from deterministic output: ' + str(path))
    print(json.dumps({'status': 'PASS', **report(canvas, labels)}, indent=2))

if __name__ == '__main__':
    main()
