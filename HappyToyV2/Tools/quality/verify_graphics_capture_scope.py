"""Second native-art gate for real subject/floor/probe scopes discovered by the first20-view review."""
from pathlib import Path
import argparse, datetime, importlib.util, json, math, sys

def verify(directory):
    previous = Path(__file__).with_name('verify_graphics_capture.py')
    spec = importlib.util.spec_from_file_location('graphics_artifact_verifier', previous)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    base = module.verify(directory)
    report = json.loads((directory / 'graphics-review.json').read_text(encoding='utf-8-sig'))
    errors = list(base.get('errors', [])); frames = {f['file']: f for f in report.get('frames', [])}
    def check(condition, message):
        if not condition: errors.append(message)
    subjects = ('corridor-candle-close', 'corridor-battery-close', 'corridor-door-joinery',
                'corridor-lantern', 'corridor-cabinet', 'corridor-seal-room')
    for view in subjects:
        frame = frames.get(view + '.png', {})
        check(frame.get('subjectInsideCorridor') is True and bool(frame.get('subjectPath')),
              'Subject is not proven under the actual corridor world: ' + view)
    for name, frame in frames.items():
        center, size, eye = frame.get('reflectionCenter', {}), frame.get('reflectionSize', {}), frame.get('eye', {})
        check(all(isinstance(center.get(k), (int, float)) and isinstance(size.get(k), (int, float)) and
                  isinstance(eye.get(k), (int, float)) and abs(eye[k] - center[k]) <= size[k] * .5 + .02 for k in ('x', 'y', 'z')),
              'Actual reflection box does not contain the observer, including its real floor: ' + name)
        if name.startswith('corridor-'):
            check(eye.get('x', 0) > 190 and eye.get('z', 0) > 190,
                  'Corridor observer left the actual generated world for the retained school: ' + name)
        if name.startswith('school-'):
            check(bool(frame.get('supportFloor')) and frame.get('supportFloorLayer') in (0, 8, 9),
                  'Actual authored support collider not recorded: ' + name)
            floor_y = frame.get('supportFloorY')
            check(isinstance(floor_y, (int, float)) and math.isfinite(floor_y) and
                  abs(center.get('y', 1e9) - floor_y - 1.5) < .02,
                  'Actual school probe height differs from observed supporting floor: ' + name)
    wash = frames.get('school-washroom.png', {})
    check('washroom' in str(wash.get('supportFloor', '')).lower(), 'Washroom camera is outside the actual tiled washroom floor')
    many = frames.get('corridor-many-candles.png', {})
    check(many.get('visibleLitCandles', 0) >= 2, 'Many-candle image lacks two real frustum/ray-visible lit flames')
    off, lit = frames.get('corridor-candle-unlit.png', {}), frames.get('corridor-candle-lit.png', {})
    check(lit.get('reflectionCaptures', 0) > off.get('reflectionCaptures', 0),
          'Actual same-cell candle/torch A-B did not refresh the reflection through production cadence')
    actor_observations = []
    for i in range(1, 5):
        name = f'corridor-enemy-{i}.png'; actor = frames.get(name, {}).get('actor')
        check(isinstance(actor, dict) and actor.get('floorObserved') is True, 'Missing real actor/floor diagnostics: ' + name)
        if isinstance(actor, dict):
            for field in ('clipTime', 'motionGroundGap', 'rootY', 'floorY', 'bakedWithoutScaleWorldMinY', 'bakedWithScaleWorldMinY'):
                check(isinstance(actor.get(field), (int, float)) and math.isfinite(actor[field]), 'Nonfinite/missing actor observation: ' + name + ' ' + field)
            actor_observations.append({'file': name, **actor})
    return {'status': 'CAPTURE_SCOPES_PASS' if not errors else 'FAIL', 'errors': errors,
            'scope': 'Actual full20 native artifacts plus actual world subject/floor/observer reflection bounds, visible multi-candle frustum/ray scope and same-cell light-revision probe refresh. Actor skin minima/animation phase are observations, not all-feet-grounded certification. Actual visual acceptance remains separate.',
            'readUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'base': base,
            'actors': actor_observations, 'visualAcceptance': 'PENDING_REAL_IMAGE_INSPECTION'}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', required=True, type=Path); parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    try: result = verify(args.directory.resolve())
    except Exception as error: result = {'status': 'FAIL', 'scope': 'Actual artifact/parser failure; no capture pass claimed', 'errors': [str(error)]}
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'status': result['status'], 'errors': result.get('errors', []), 'output': str(args.output)}, ensure_ascii=False))
    sys.exit(0 if result['status'] == 'CAPTURE_SCOPES_PASS' else 1)
