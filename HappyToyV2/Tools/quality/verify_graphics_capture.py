"""Verify 27 native PNGs, including final classroom and rendering controls; visual acceptance is separate."""
from pathlib import Path
import argparse, datetime, hashlib, json, math, sys
from PIL import Image, ImageStat

VIEWS = (
    'corridor-candle-unlit', 'corridor-candle-lit', 'corridor-candle-close',
    'corridor-battery-close', 'corridor-door-joinery', 'corridor-lantern',
    'corridor-floor-ceiling', 'corridor-cabinet', 'corridor-seal-room', 'corridor-many-candles',
    'corridor-floor-ceiling-ao-control', 'corridor-floor-ceiling-shadow-control', 'corridor-final-classroom-entry',
    'corridor-floor-ceiling-lod-control',
    'corridor-floor-ceiling-material-control',
    'corridor-final-classroom-joinery', 'corridor-final-classroom-altar',
    'corridor-enemy-1', 'corridor-enemy-2', 'corridor-enemy-3', 'corridor-enemy-4',
    'school-ground-hall', 'school-washroom', 'school-music', 'school-upper', 'school-basement', 'school-candle-close',
)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def verify(directory):
    directory = directory.resolve(); path = directory / 'graphics-review.json'
    report = json.loads(path.read_text(encoding='utf-8-sig')); errors, images = [], []
    def check(condition, message):
        if not condition: errors.append(message)
    check(report.get('status') == 'PASS', 'Actual art audit reported ' + str(report.get('status')) + ': ' + str(report.get('failure')))
    check(not report.get('errors'), 'Actual art audit retained runtime errors: ' + repr(report.get('errors')))
    check(report.get('unity') == '6000.6.0f1', 'Native review editor version differs')
    check(bool(report.get('device')), 'Actual native graphics device missing')
    frames = report.get('frames', [])
    expected = {view + '.png' for view in VIEWS}; names = [frame.get('file') for frame in frames]
    check(len(frames) == len(expected) and len(set(names)) == len(expected) and set(names) == expected,
          'Actual view inventory differs: missing=' + repr(sorted(expected - set(names))) + ' extra=' + repr(sorted(set(names) - expected)))
    for frame in frames:
        name = frame.get('file', '')
        check(name in expected and Path(name).name == name, 'Unexpected camera image path: ' + str(name))
        if name not in expected: continue
        image_path = directory / name
        check(image_path.is_file(), 'Actual image missing: ' + name)
        if image_path.is_file():
            with Image.open(image_path) as image:
                image.load(); check(image.format == 'PNG' and image.size == (1600, 900), 'Wrong actual image format/resolution: ' + name)
                pixels = image.convert('RGB'); means = ImageStat.Stat(pixels).mean
                for field, measured in zip(('meanRed', 'meanGreen', 'meanBlue'), means):
                    recorded = frame.get(field)
                    check(isinstance(recorded, (int, float)) and math.isfinite(recorded) and abs(recorded - measured) < .002,
                          'Actual PNG channel mean differs from capture JSON: ' + name + ' ' + field)
                images.append({'file': name, 'bytes': image_path.stat().st_size, 'sha256': sha(image_path),
                               'dimensions': list(image.size), 'decodedChannelMeans': means})
        check(frame.get('mode') == ('school' if name.startswith('school-') else 'corridor'), 'Wrong actual mode: ' + name)
        check(frame.get('graphicsDevice') == report.get('device'), 'Per-view device differs: ' + name)
        check(frame.get('pipeline') == 'GraphicsPipeline', 'Owned current pipeline missing: ' + name)
        check(frame.get('projectColorSpace') == 'Linear' and frame.get('captureSrgb') is False,
              'Capture HDR target must be explicit Linear before CPU sRGB encoding: ' + name)
        check('SFloat' in str(frame.get('captureFormat')), 'Actual capture target lost floating-point precision: ' + name)
        check(frame.get('hdrCamera') is True and frame.get('hdrPipeline') is True and frame.get('postProcessing') is True,
              'Actual HDR/post configuration absent: ' + name)
        check(frame.get('sourceCameraType') == 'Base' and frame.get('captureCameraType') == 'Base' and frame.get('sourceCameraStack') == 0,
              'Single-camera capture omitted a source camera stack: ' + name)
        check(frame.get('antialiasing') == 'SubpixelMorphologicalAntiAliasing', 'Actual SMAA configuration differs: ' + name)
        check(frame.get('toneMapping') == 'Neutral' and frame.get('bloomActive') is True and abs(frame.get('bloomIntensity', -1) - .16) < .001,
              'Actual volume-stack tone/bloom configuration differs: ' + name)
        check(frame.get('shadowAtlas') == 2048 and frame.get('torchTile') == 1024 and frame.get('localTile') == 512,
              'Actual punctual shadow atlas/tier contract differs: ' + name)
        planned, observed = frame.get('selectedShadowPixels'), frame.get('observedShadowPixels')
        check(isinstance(planned, int) and planned == observed and 0 <= planned <= 2048**2,
              'Actual punctual shadow tile area differs/exceeds atlas: ' + name)
        check(isinstance(frame.get('shadowFaces'), int) and 0 <= frame['shadowFaces'] <= 16,
              'Actual shadow face count exceeds tile plan: ' + name)
        check(frame.get('reflectionReady') is True and frame.get('reflectionTextureWidth') == 128 and frame.get('reflectionCaptures', 0) >= 1,
              'Actual room reflection texture unavailable/unsettled: ' + name)
        wait = frame.get('reflectionSettleSeconds')
        check(isinstance(wait, (int, float)) and math.isfinite(wait) and wait >= 3.1,
              'Actual pose did not wait through natural reflection cadence: ' + name)
        for field in ('eye', 'look', 'reflectionCenter', 'reflectionSize'):
            vector = frame.get(field, {})
            check(all(isinstance(vector.get(axis), (int, float)) and math.isfinite(vector[axis]) for axis in ('x', 'y', 'z')),
                  'Missing/nonfinite observed spatial data: ' + name + ' ' + field)
    return {'status': 'CAPTURE_ARTIFACTS_PASS' if not errors else 'FAIL',
            'scope': 'Actual native JSON status, exact20 real PNGs/checksums/decoded statistics and recorded HDR/post/probe/shadow configuration. Requires separate ViewImage inspection for visible material/lighting acceptance; no survival, audio, GPU/FPS or HDR-display certification.',
            'readUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'directory': str(directory),
            'actualAuditStatus': report.get('status'), 'actualAuditFailure': report.get('failure'), 'errors': errors,
            'jsonSha256': sha(path), 'device': report.get('device'), 'images': images,
            'gpuTimingFeatureObserved': sorted({bool(frame.get('gpuTimingFeatureEnabled')) for frame in frames}),
            'visualAcceptance': 'PENDING_REAL_IMAGE_INSPECTION'}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', required=True, type=Path); parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    try: result = verify(args.directory)
    except Exception as error: result = {'status': 'FAIL', 'scope': 'Actual artifact/parser failure; no render pass claimed', 'errors': [str(error)]}
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'status': result['status'], 'errors': result.get('errors', []), 'output': str(args.output)}, ensure_ascii=False))
    sys.exit(0 if result['status'] == 'CAPTURE_ARTIFACTS_PASS' else 1)
