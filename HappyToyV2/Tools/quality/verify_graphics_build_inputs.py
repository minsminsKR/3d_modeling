"""Read-only full-graph build verifier plus explicit graphics asset retention checks."""
from pathlib import Path
import argparse, datetime, hashlib, importlib.util, json, re, struct, sys

PBR_KEYS = ('brass-tarnished', 'ceramic-tile', 'cloth-charred', 'concrete-rough',
            'metal-rust', 'painted-metal', 'paper-aged', 'plaster-damp', 'wax-pool',
            'wax-tallow', 'wood-aged', 'wood-floor')
CHANNELS = ('albedo.png', 'ao.png', 'metallic-smoothness.png', 'normal.png')
PROP_KEYS = ('battery-supply', 'cabinet-shell', 'cabinet-timber', 'candle-flame',
             'candle-waymark', 'door-hardware', 'paper-lantern', 'seal-altar')
PROTECTED = {
    'Assets/Annex/SchoolAnnex.unity': '0f2d25f211c76c7aa5299702ad15cf52d042f5a316ef32f5c4f43d69994aaede',
    'Assets/Generated/SchoolPipeline 17.asset': 'b48380a6a0890c6e64b62383885a9176418754572fbd273ea0687ffd362f6daa',
    'Assets/Generated/SchoolRenderer 17.asset': '61b3c1d274b8e9ca7f9dd1eeae7a0b8f430b9465c465d6844d087424871086dc',
}
SCOPE = ('Full current Assets/Packages/ProjectSettings inventory and hashes through the existing verifier, '
         'explicit new graphics Resources/settings input inclusion, original hash protection (LF-canonical YAML assets; raw scene bytes), and copied credit equality. Build inputs retain strict raw byte hashes. '
         'Input inclusion does not by itself certify actual shipped shader appearance, GPU duration, survival or audio.')

def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024*1024), b''): digest.update(block)
    return digest.hexdigest()

def png_header(path):
    with path.open('rb') as stream: header = stream.read(33)
    if header[:8] != b'\x89PNG\r\n\x1a\n' or header[12:16] != b'IHDR':
        raise ValueError('Invalid PNG header: ' + str(path))
    width, height, bits, colour, _, _, _ = struct.unpack('>IIBBBBB', header[16:29])
    return {'width': width, 'height': height, 'bits': bits, 'colourType': colour}

def utc(value):
    parsed = datetime.datetime.fromisoformat(value.replace('Z', '+00:00'))
    if parsed.tzinfo is None: raise ValueError('Fresh-build threshold must include a time zone')
    return parsed.astimezone(datetime.timezone.utc)

def base_verifier(path):
    spec = importlib.util.spec_from_file_location('previous_full_graph_verifier', path)
    if not spec or not spec.loader: raise ValueError('Existing full-graph verifier unavailable')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module

def graphics_inventory(project, expected=None):
    errors, records, guids, maps = [], [], {}, []
    def check(condition, message):
        if not condition: errors.append(message)
    def include(name):
        path = project / name
        check(path.is_file() and path.stat().st_size > 0, 'Missing/empty graphics input: ' + name)
        if not path.is_file(): return None
        digest = sha(path)
        if expected is not None:
            check(name in expected, 'Graphics input omitted from actual build manifest: ' + name)
            check(expected.get(name) == digest, 'Graphics input hash differs from actual build: ' + name)
        records.append({'path': name, 'bytes': path.stat().st_size, 'sha256': digest})
        return path
    def asset(name):
        path, meta = include(name), include(name + '.meta')
        if meta:
            match = re.search(r'^guid: ([0-9a-f]{32})\s*$', meta.read_text(encoding='utf-8-sig'), re.M)
            check(match is not None, 'Missing valid metadata GUID: ' + name)
            if match:
                guid = match.group(1)
                check(guid not in guids.values(), 'Duplicate required graphics GUID: ' + name)
                guids[name] = guid
        return path, meta
    pbr_root = project / 'Assets/Resources/GraphicsPbr'
    check(pbr_root.is_dir(), 'PBR Resources root missing')
    actual_sets = sorted(p.name for p in pbr_root.iterdir() if p.is_dir()) if pbr_root.is_dir() else []
    check(actual_sets == sorted(PBR_KEYS), 'Expected exactly the twelve named PBR sets: ' + repr(actual_sets))
    for key in PBR_KEYS:
        folder = 'Assets/Resources/GraphicsPbr/' + key
        include(folder + '.meta')
        for channel in CHANNELS:
            path, meta = asset(folder + '/' + channel)
            if path:
                info = png_header(path); maps.append({'path': folder + '/' + channel, **info})
                check(info['width'] == 2048 and info['height'] == 2048 and info['bits'] == 8,
                      'PBR source PNG dimensions/bit depth changed: ' + str(path))
            if meta:
                text = meta.read_text(encoding='utf-8-sig')
                for name, value in [('textureShape', 1), ('textureType', 1 if channel == 'normal.png' else 0),
                                    ('sRGBTexture', 1 if channel == 'albedo.png' else 0), ('enableMipMap', 1), ('isReadable', 0)]:
                    check(re.search(r'^\s*' + name + r': ' + str(value) + r'\s*$', text, re.M) is not None,
                          'Actual importer setting changed: ' + folder + '/' + channel + ' ' + name)
        names = ['material.mat'] + (['material-emissive.mat'] if key in ('paper-aged','painted-metal') else [])
        for name in names:
            path, _ = asset(folder + '/' + name)
            if path:
                text = path.read_text(encoding='utf-8-sig')
                for keyword in ('_NORMALMAP', '_METALLICSPECGLOSSMAP', '_OCCLUSIONMAP'):
                    check(re.search(r'^\s*- ' + re.escape(keyword) + r'\s*$', text, re.M) is not None,
                          'Retained PBR material keyword omitted: ' + folder + '/' + name + ' ' + keyword)
                if name == 'material-emissive.mat':
                    check(re.search(r'^\s*- _EMISSION\s*$', text, re.M) is not None, 'Paper emission variant missing')
                for prop, channel in (('_BaseMap', 'albedo.png'), ('_BumpMap', 'normal.png'),
                                      ('_OcclusionMap', 'ao.png'), ('_MetallicGlossMap', 'metallic-smoothness.png')):
                    match = re.search(r'- ' + re.escape(prop) + r':\s*\n\s*m_Texture: \{[^\n]*guid: ([0-9a-f]{32})', text)
                    check(match is not None and match.group(1) == guids.get(folder + '/' + channel),
                          'Retained material map GUID mismatch: ' + folder + '/' + name + ' ' + prop)
    check(len(list(pbr_root.rglob('*.png'))) == 48 if pbr_root.is_dir() else False, 'Expected exactly48 PBR PNGs')
    check(len(list(pbr_root.rglob('*.mat'))) == 14 if pbr_root.is_dir() else False, 'Expected exactly14 retained PBR materials')
    props_root = project / 'Assets/Resources/GraphicsUpgrade/Props'
    actual_props = sorted(p.stem for p in props_root.glob('*.fbx'))
    check(actual_props == sorted(PROP_KEYS), 'Expected exactly the eight named prop FBXs: ' + repr(actual_props))
    for key in PROP_KEYS:
        path, meta = asset('Assets/Resources/GraphicsUpgrade/Props/' + key + '.fbx')
        if path:
            with path.open('rb') as stream: check(stream.read(18) == b'Kaydara FBX Binary', 'Unexpected binary FBX header: ' + key)
        if meta:
            check(re.search(r'^\s*addColliders: 0\s*$', meta.read_text(encoding='utf-8-sig'), re.M) is not None,
                  'Imported prop unexpectedly generates physics: ' + key)
    flame, flame_meta = asset('Assets/Resources/GraphicsUpgrade/Textures/candle-flame-v3.png')
    if flame:
        info = png_header(flame)
        check(info['width'] == 1024 and info['height'] == 1536 and info['colourType'] == 6,
              'Photographic flame must retain the actual1024x1536 RGBA source (import max size1024 is separate)')
    asset('Assets/Resources/GraphicsUpgrade/Shaders/CandleFlame.shader')
    pipeline, _ = asset('Assets/GraphicsUpgrade/Settings/GraphicsPipeline.asset')
    renderer, _ = asset('Assets/GraphicsUpgrade/Settings/GraphicsRenderer.asset')
    for name in ('Assets/Resources/GraphicsPbr.meta', 'Assets/Resources/GraphicsUpgrade.meta',
                 'Assets/Resources/GraphicsUpgrade/Props.meta', 'Assets/Resources/GraphicsUpgrade/Textures.meta',
                 'Assets/Resources/GraphicsUpgrade/Shaders.meta', 'Assets/GraphicsUpgrade.meta', 'Assets/GraphicsUpgrade/Settings.meta'):
        include(name)
    for name, digest in PROTECTED.items():
        path = project / name
        # Git's Windows autocrlf changes YAML line endings without changing the
        # authored pipeline. The preserved scene remains a byte-exact LFS asset.
        protected = hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest() if path.is_file() and name.endswith('.asset') else sha(path) if path.is_file() else None
        check(path.is_file() and (sha(path) == digest or protected == digest), 'Protected original changed: ' + name)
        if expected is not None: check(path.is_file() and expected.get(name) == sha(path), 'Protected original hash absent/wrong in build: ' + name)
    pipeline_guid = guids.get('Assets/GraphicsUpgrade/Settings/GraphicsPipeline.asset')
    renderer_guid = guids.get('Assets/GraphicsUpgrade/Settings/GraphicsRenderer.asset')
    if pipeline:
        text = pipeline.read_text(encoding='utf-8-sig')
        check(renderer_guid is not None and renderer_guid in text, 'Owned pipeline renderer reference missing')
        for field, value in (('m_SupportsHDR', 1), ('m_RequireDepthTexture', 1), ('m_ReflectionProbeBlending', 1),
                             ('m_ReflectionProbeBoxProjection', 1), ('m_AdditionalLightsShadowmapResolution', 2048),
                             ('m_AdditionalLightsShadowResolutionTierMedium', 512), ('m_AdditionalLightsShadowResolutionTierHigh', 1024)):
            check(re.search(r'^\s*' + field + ': ' + str(value) + r'\s*$', text, re.M) is not None,
                  'Owned pipeline serialized rendering contract changed: ' + field)
    if renderer:
        text = renderer.read_text(encoding='utf-8-sig')
        check('Universal.ScreenSpaceAmbientOcclusion' in text and 'm_Active: 1' in text, 'Owned actual SSAO feature missing/inactive')
        check(re.search(r'postProcessData: \{fileID: 11400000, guid: [0-9a-f]{32}', text) is not None,
              'Owned renderer actual post-process resources missing')
    for name in ('ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset'):
        text = (project / name).read_text(encoding='utf-8-sig')
        check(pipeline_guid is not None and pipeline_guid in text, 'Owned pipeline missing from current settings: ' + name)
    return {'errors': errors, 'requiredInputs': records, 'metadataGuids': guids,
            'pbrSets': list(PBR_KEYS), 'pbrPngCount': len(maps), 'pbrMaterialCount': 14,
            'propModels': list(PROP_KEYS), 'protectedOriginals': PROTECTED}

def verify(project, build, not_before, base_path):
    project, build = project.resolve(), build.resolve()
    base = base_verifier(base_path).verify(project, build)
    manifest = json.loads((build / 'quality-build.json').read_text(encoding='utf-8-sig'))
    expected = {entry['path']: entry['sha256'] for entry in manifest.get('inputs', [])}
    graphics = graphics_inventory(project, expected)
    errors = list(base.get('errors', [])) + graphics['errors']
    if base.get('status') != 'BUILD_INPUTS_PASS' and not base.get('errors'): errors.append('Existing full-graph verifier did not pass')
    created = utc(manifest['createdUtc']); threshold = utc(not_before)
    now = datetime.datetime.now(datetime.timezone.utc)
    if created < threshold or created > now + datetime.timedelta(minutes=5):
        errors.append('Actual build manifest is outside the explicit fresh-build UTC boundary')
    for name in ('Graphics-Credits.txt', 'HappyToyV2_Data/Managed/Assembly-CSharp.dll'):
        path = build / name
        if not path.is_file() or path.stat().st_size == 0: errors.append('Native graphics artifact missing/empty: ' + name)
    copied, original = build / 'Graphics-Credits.txt', project / 'ThirdParty/Graphics/Graphics-Credits.txt'
    if not copied.is_file() or not original.is_file() or sha(copied) != sha(original): errors.append('Copied graphics credits differ from actual current source')
    credits = {'source': str(original), 'copied': str(copied), 'sha256': sha(copied) if copied.is_file() else None}
    return {'status': 'GRAPHICS_BUILD_INPUTS_PASS' if not errors else 'FAIL', 'scope': SCOPE,
            'readUtc': now.isoformat(), 'project': str(project), 'build': str(build), 'errors': errors,
            'notBeforeUtc': threshold.isoformat(), 'baseVerifierSha256': sha(base_path),
            'base': base, 'graphics': graphics, 'graphicsCredits': credits}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    parser.add_argument('--build', type=Path)
    parser.add_argument('--not-before', help='UTC ISO timestamp recorded before starting the fresh build')
    parser.add_argument('--source-only', action='store_true', help='Read actual asset inventory only; never returns a build pass')
    parser.add_argument('--base-verifier', type=Path, default=Path(__file__).resolve().parent / 'verify_build_inputs.py')
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    if not args.source_only and (args.build is None or args.not_before is None): parser.error('--build and --not-before are required without --source-only')
    try:
        if args.source_only:
            inventory = graphics_inventory(args.project.resolve())
            result = {'status': 'SOURCE_GRAPHICS_INVENTORY_PASS' if not inventory['errors'] else 'FAIL',
                      'scope': 'Read-only actual source inventory/settings; no native build/pass claimed',
                      'readUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), **inventory}
        else: result = verify(args.project, args.build, args.not_before, args.base_verifier)
    except Exception as error: result = {'status': 'FAIL', 'scope': 'Parser/artifact failure; no native pass claimed', 'errors': [str(error)]}
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'status': result['status'], 'errors': result.get('errors', []), 'output': str(args.output)}, ensure_ascii=False))
    sys.exit(0 if result['status'].endswith('_PASS') else 1)
