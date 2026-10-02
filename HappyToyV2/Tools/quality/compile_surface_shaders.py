#!/usr/bin/env python3
"""Offline DXC checks against installed URP 17.6 headers; never a Unity import/render test."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess

VARIANTS = {
    'main-only': [], 'forward': ['_ADDITIONAL_LIGHTS'],
    'vertex-lights': ['_ADDITIONAL_LIGHTS_VERTEX'], 'cluster': ['_CLUSTER_LIGHT_LOOP']}
for mode, light in [('forward', '_ADDITIONAL_LIGHTS'), ('cluster', '_CLUSTER_LIGHT_LOOP')]:
    VARIANTS[mode + '-main-shadow'] = [light, '_MAIN_LIGHT_SHADOWS']
    for shadow, name in [('_MAIN_LIGHT_SHADOWS_CASCADE', 'cascade'), ('_MAIN_LIGHT_SHADOWS_SCREEN', 'screen-transparent')]:
        VARIANTS[mode + '-shadow-' + name] = [light, shadow, '_ADDITIONAL_LIGHT_SHADOWS', '_SHADOWS_SOFT']
    for fog in ['LINEAR', 'EXP', 'EXP2']:
        VARIANTS[mode + '-cookies-fog-' + fog.lower()] = [light, '_LIGHT_COOKIES', 'FOG_' + fog]
    VARIANTS[mode + '-instancing'] = [light, 'INSTANCING_ON']
    for quality in ['LOW', 'MEDIUM', 'HIGH']:
        VARIANTS[mode + '-soft-' + quality.lower()] = [light, '_MAIN_LIGHT_SHADOWS_CASCADE',
            '_ADDITIONAL_LIGHT_SHADOWS', '_SHADOWS_SOFT_' + quality, '_LIGHT_COOKIES']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--dxc', type=Path, required=True)
    parser.add_argument('--include-root', type=Path, required=True,
                        help='Directory containing Packages mapped to the installed editor BuiltInPackages.')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    project, dxc, includes, out = (p.resolve() for p in (args.project, args.dxc, args.include_root, args.output))
    if out == project or project in out.parents:
        parser.error('Keep compiler outputs outside the Unity project.')
    for package in ['core', 'universal']:
        manifest = includes / ('Packages/com.unity.render-pipelines.' + package + '/package.json')
        if not manifest.is_file() or json.loads(manifest.read_text()).get('version') != '17.6.0':
            parser.error('Both installed URP/Core package manifests must be version 17.6.0.')
    out.mkdir(parents=True, exist_ok=True)
    compiler = subprocess.check_output([str(dxc), '--version'], text=True).strip()
    results, shaders = [], {}
    for shader in ['ShallowWater', 'BloodFilm']:
        source = project / 'Assets/Annex' / (shader + '.shader')
        text = source.read_text()
        if text.count('HLSLPROGRAM') != 1 or text.count('ENDHLSL') != 1:
            parser.error('Expected exactly one HLSLPROGRAM in ' + shader)
        hlsl = text.split('HLSLPROGRAM', 1)[1].split('ENDHLSL', 1)[0]
        extracted = out / (shader + '.hlsl')
        extracted.write_text(hlsl)
        shaders[shader] = {'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                           'hlslSha256': hashlib.sha256(hlsl.encode()).hexdigest()}
        cases = [('dxil', name, keys) for name, keys in VARIANTS.items()]
        for mode, light in [('forward', '_ADDITIONAL_LIGHTS'), ('cluster', '_CLUSTER_LIGHT_LOOP')]:
            cases.append(('spirv', mode, [light, '_MAIN_LIGHT_SHADOWS_CASCADE', '_ADDITIONAL_LIGHT_SHADOWS',
                '_SHADOWS_SOFT_HIGH', '_LIGHT_COOKIES', 'FOG_EXP2', 'INSTANCING_ON']))
        for backend, name, keywords in cases:
            for entry, profile, stage in [('vert', 'vs_6_0', 'VERTEX'), ('frag', 'ps_6_0', 'FRAGMENT')]:
                key = shader + '-' + backend + '-' + name + '-' + entry
                api = 'VULKAN' if backend == 'spirv' else 'D3D11'
                defines = ['SHADER_API_' + api + '=1', 'UNITY_COMPILER_HLSL=1', 'UNITY_COMPILER_DXC=1',
                    'UNITY_VERSION=600060', 'SHADER_TARGET=' + ('45' if '_CLUSTER_LIGHT_LOOP' in keywords else '35'),
                    'SHADER_STAGE_' + stage + '=1'] + [value + '=1' for value in keywords]
                command = [str(dxc), '-HV', '2018', '-flegacy-macro-expansion', '-T', profile, '-E', entry,
                           '-I', str(includes)]
                if backend == 'spirv': command += ['-spirv', '-fspv-target-env=vulkan1.1']
                for define in defines: command += ['-D', define]
                command += ['-Fo', str(out / (key + ('.spv' if backend == 'spirv' else '.dxil'))), str(extracted)]
                process = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)
                log = process.stdout.replace(str(out), '$OUTPUT').replace(str(includes), '$INCLUDES')
                (out / (key + '.log')).write_text(log)
                result = {'shader': shader, 'backend': backend, 'variant': name, 'entry': entry,
                          'exitCode': process.returncode, 'diagnosticsEmpty': not log.strip(), 'keywords': keywords}
                results.append(result)
                if process.returncode: print(key, log)
    report = {'scope': 'Offline HLSL compilation with exact shipped URP/Core 17.6 headers. DXIL SM6 and Vulkan SPIR-V targets, not Unity ShaderLab import, variant stripping, render or performance validation.',
              'compiler': compiler, 'unityVersion': '6000.6.0f1', 'urp': '17.6.0', 'shaders': shaders,
              'dxilCount': sum(r['backend'] == 'dxil' for r in results),
              'spirvCount': sum(r['backend'] == 'spirv' for r in results), 'results': results}
    report['status'] = 'PASS' if all(r['exitCode'] == 0 for r in results) else 'FAIL'
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({k: v for k, v in report.items() if k not in ('results', 'shaders')}))
    return 0 if report['status'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())
