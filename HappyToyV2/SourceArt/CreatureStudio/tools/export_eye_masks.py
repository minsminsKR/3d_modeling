"""Rasterize reviewed iris UV polygons on a newly generated atlas.

Input UVs are Blender/Unity bottom-left. The PNG stores top-left image rows.
Pupil polygons and eyelid cutouts stay black; the runtime supplies red emission.
No old creature eye atlas coordinates are accepted or substituted.
"""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageChops


def export(authoring_path, destination, size=2048):
    plan = json.loads(Path(authoring_path).read_text(encoding='utf8'))
    key = plan['key']
    expected = 1 if key == 'Cyclopse' else 2
    if len(plan['eyes']) != expected:
        raise ValueError(f'{key} needs {expected} reviewed iris regions')
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    mask = Image.new('L', (size, size), 0)
    painter = ImageDraw.Draw(mask)
    profile = {'version': 2, 'key': key, 'eyes': []}
    coverage = []

    def polygon(points):
        if len(points) < 4:
            raise ValueError('An actual iris boundary needs at least four reviewed points')
        result = []
        for point in points:
            x, y = float(point['x']), float(point['y'])
            if not 0 <= x <= 1 or not 0 <= y <= 1:
                raise ValueError('UV polygon lies outside the new atlas')
            result.append((round(x*(size-1)), round((1-y)*(size-1))))
        return result

    def triangle(points):
        if len(points) != 3:
            raise ValueError('A raster triangle needs exactly three source UV corners')
        return polygon([points[0], points[1], points[2], points[0]])[:3]

    for index, eye in enumerate(plan['eyes']):
        layer = Image.new('L', (size, size), 0)
        layer_draw = ImageDraw.Draw(layer)
        if eye.get('rasterTriangles'):
            # Rasterize real selected surface triangles independently. A seam
            # never joins two distant UV islands with a giant filled polygon.
            for corners in eye['rasterTriangles']:
                layer_draw.polygon(triangle(corners), fill=255)
            for corners in eye.get('blackTriangles', []):
                layer_draw.polygon(triangle(corners), fill=0)
            method = 'source eye surface triangles, independent UV seam corners'
        elif eye.get('uvIslands'):
            for island in eye['uvIslands']:
                layer_draw.polygon(polygon(island['boundary']), fill=255)
                for cutout in island.get('blackCutouts', []):
                    layer_draw.polygon(polygon(cutout), fill=0)
            method = 'reviewed separate UV islands'
        else:
            if not eye.get('uvIslandContinuityVerified', False):
                raise ValueError('A single filled iris polygon requires explicit UV-island continuity review')
            layer_draw.polygon(polygon(eye['uvBoundarySamples']), fill=255)
            method = 'single continuous UV island, explicitly reviewed'
        for cutout in eye.get('blackCutouts', []):
            layer_draw.polygon(polygon(cutout), fill=0)
        white = sum(value > 0 for value in layer.getdata())
        if white == 0:
            raise ValueError('Reviewed eye mask has no visible iris pixels')
        mask = ImageChops.lighter(mask, layer)
        coverage.append({'eye': index, 'whitePixels': white, 'method': method,
                         'sourceCenterGLTF': eye.get('sourceCenterGLTF'),
                         'sourceRadius': eye.get('sourceRadius'),
                         'selectedSourceTriangles': len(eye.get('rasterTriangles', [])),
                         'uvIslands': len(eye.get('uvIslands', []))})
        entry = {name: eye[name] for name in ('uv', 'materialSlot', 'uvBoundarySamples')}
        entry.update(aperture=False, sourceMesh=eye.get('sourceMesh', key+'_LOD0'))
        profile['eyes'].append(entry)
    mask.save(destination/'eyes-emission.png')
    (destination/'eyes-profile.json').write_text(json.dumps(profile, indent=2), encoding='utf8')
    report = {'key': key, 'eyes': expected, 'whitePixels': sum(value > 0 for value in mask.getdata()),
              'uvOrigin': 'bottom-left Blender/Unity', 'sourceAuthoringSHA256': hashlib.sha256(Path(authoring_path).read_bytes()).hexdigest(),
              'sourceGLBSHA256': plan.get('sourceGLBSHA256'), 'coverage': coverage}
    (Path(authoring_path).parent/'eyes-export-report.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('authoring', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--size', type=int, choices=[2048, 4096], default=2048)
    args = parser.parse_args()
    print(json.dumps(export(args.authoring, args.destination, args.size), indent=2))
