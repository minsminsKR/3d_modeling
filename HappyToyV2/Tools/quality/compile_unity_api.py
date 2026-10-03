#!/usr/bin/env python3
"""Compile C# against an already-installed Unity 6000.6 editor's real APIs.

Does not install/activate Unity, import or save a project, compile shaders, or build a player.
Requires matching URP 17.6.0/Input System 1.19.0/AI Navigation 2.0.12 template assemblies.
When test asmdefs exist, also requires genuine UTF 1.8.0/NUnit 2.1.0 assemblies.
Test assemblies are compiled separately and never executed by this tool.
"""
from pathlib import Path
import argparse
import collections
import datetime
import hashlib
import json
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--editor-data', type=Path, required=True)
    parser.add_argument('--template-assemblies', type=Path, required=True)
    parser.add_argument('--test-framework-assemblies', type=Path,
                        help='Directory containing genuine nunit.framework, UnityEngine.TestRunner and UnityEditor.TestRunner DLLs; defaults to template assemblies.')
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--sdk-version', default='8.0.425')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    project, editor, cache, sdk, out = (p.resolve() for p in
        (args.project, args.editor_data, args.template_assemblies, args.dotnet_root, args.output))
    if out == project or project in out.parents:
        parser.error('Keep compiler outputs outside the Unity project.')
    expected = {'com.unity.render-pipelines.universal': '17.6.0', 'com.unity.inputsystem': '1.19.0',
                'com.unity.ai.navigation': '2.0.12'}
    dependencies = json.loads((project / 'Packages/manifest.json').read_text())['dependencies']
    if any(dependencies.get(name) != version for name, version in expected.items()):
        parser.error('Project package versions changed; validate matching reference assemblies before using this check.')
    if 'activeInputHandler: 1' not in (project / 'ProjectSettings/ProjectSettings.asset').read_text():
        parser.error('This check models Input System-only player defines (activeInputHandler 1).')
    host = sdk / ('dotnet.exe' if (sdk / 'dotnet.exe').is_file() else 'dotnet')
    csc = sdk / 'sdk' / args.sdk_version / 'Roslyn/bincore/csc.dll'
    required = [host, csc, cache / 'Unity.InputSystem.dll', cache / 'Unity.AI.Navigation.dll',
                cache / 'Unity.RenderPipelines.Universal.Runtime.dll', editor / 'Managed/UnityEngine/UnityEngine.CoreModule.dll']
    for path in required:
        if not path.is_file():
            parser.error('Required installed reference/tool is missing: ' + str(path))
    out.mkdir(parents=True, exist_ok=True)
    test_names = {'HappyToy.V2.TestSupport': 'test-support',
                  'HappyToy.V2.EditModeTests': 'test-editor',
                  'HappyToy.V2.PlayModeTests': 'test-playmode'}
    expected_test_references = {'HappyToy.V2.TestSupport': set(),
                                'HappyToy.V2.EditModeTests': {'HappyToy.V2.TestSupport'},
                                'HappyToy.V2.PlayModeTests': {'HappyToy.V2.TestSupport', 'Unity.InputSystem'}}
    assembly_roots, assembly_records, assembly_definitions = {}, [], {}
    for path in sorted((project / 'Assets').rglob('*.asmdef')):
        data = path.read_bytes()
        definition = json.loads(data)
        name = definition.get('name')
        if name not in test_names:
            parser.error('Unsupported asset asmdef; update this compiler model before validating: ' + str(path))
        if 'TestAssemblies' not in definition.get('optionalUnityReferences', []):
            parser.error('Expected an isolated test assembly: ' + str(path))
        if definition.get('autoReferenced', True):
            parser.error('Test assemblies must not be auto-referenced: ' + str(path))
        expected_platforms = ['Editor'] if name == 'HappyToy.V2.EditModeTests' else []
        if (set(definition.get('references', [])) != expected_test_references[name]
                or definition.get('includePlatforms', []) != expected_platforms
                or definition.get('excludePlatforms', [])
                or definition.get('overrideReferences', False)
                or definition.get('noEngineReferences', False)
                or definition.get('defineConstraints', [])
                or definition.get('versionDefines', [])):
            parser.error('Test asmdef compilation contract changed; update this compiler model: ' + str(path))
        if name in assembly_roots.values():
            parser.error('Duplicate test assembly: ' + name)
        assembly_roots[path.parent] = name
        assembly_definitions[name] = definition
        assembly_records.append({'file': path.relative_to(project).as_posix(), 'name': name,
                                 'sha256': hashlib.sha256(data).hexdigest()})
    if assembly_roots and set(assembly_roots.values()) != set(test_names):
        parser.error('Expected all three isolated HappyToy test asmdefs.')
    test_refs = []
    if assembly_roots:
        locked = json.loads((project / 'Packages/packages-lock.json').read_text())['dependencies']
        for package, version in {'com.unity.test-framework': '1.8.0', 'com.unity.ext.nunit': '2.1.0'}.items():
            if locked.get(package, {}).get('version') != version:
                parser.error('Test package version changed; obtain matching genuine assemblies: ' + package)
        test_cache = (args.test_framework_assemblies or cache).resolve()
        test_refs = [test_cache / name for name in
                     ('nunit.framework.dll', 'UnityEngine.TestRunner.dll', 'UnityEditor.TestRunner.dll')]
        for path in test_refs:
            if not path.is_file():
                parser.error('Required genuine test framework reference is missing: ' + str(path))
    runtime, editor_sources, records = [], [], []
    test_sources = {name: [] for name in test_names}
    for path in sorted((project / 'Assets').rglob('*.cs')):
        relative = path.relative_to(project)
        data = path.read_bytes()
        target = out / 'src' / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        records.append({'file': relative.as_posix(), 'sha256': hashlib.sha256(data).hexdigest()})
        assembly = next((assembly_roots[parent] for parent in path.parents if parent in assembly_roots), None)
        if assembly:
            test_sources[assembly].append(target)
        elif 'Tests' in relative.parts:
            parser.error('Test source has no supported asmdef: ' + str(path))
        else:
            (editor_sources if 'Editor' in relative.parts else runtime).append(target)
    if assembly_roots and any(not sources for sources in test_sources.values()):
        parser.error('Every isolated test assembly must contain source files.')
    refs = sorted((editor / 'NetStandard/ref/2.1.0').glob('*.dll'))
    refs += sorted((editor / 'NetStandard/compat/2.1.0/shims/netfx').glob('*.dll'))
    refs += [p for p in sorted((editor / 'Managed').glob('*.dll')) if p.name not in ('UnityEngine.dll', 'UnityEditor.dll')]
    refs += sorted((editor / 'Managed/UnityEngine').glob('*.dll'))
    test_dll_names = {'nunit.framework.dll', 'UnityEngine.TestRunner.dll', 'UnityEditor.TestRunner.dll'}
    engine_refs = [p for p in refs if p.name not in test_dll_names]
    refs += [p for p in sorted(cache.glob('*.dll')) if not p.name.startswith('Assembly-CSharp')]
    refs = [p for p in refs if p.name not in test_dll_names]
    seen = set()
    refs = [p for p in refs if not (p.name in seen or seen.add(p.name))]
    if not list((editor / 'NetStandard/ref/2.1.0').glob('*.dll')):
        parser.error('Unity netstandard reference assemblies were not found.')
    defines = ['UNITY_6000_6', 'UNITY_6000_6_OR_NEWER', 'UNITY_6000_0_OR_NEWER', 'UNITY_2023_1_OR_NEWER',
               'UNITY_2022_3_OR_NEWER', 'UNITY_2022_2_OR_NEWER', 'UNITY_2021_3_OR_NEWER', 'UNITY_2020_3_OR_NEWER',
               'UNITY_2019_4_OR_NEWER', 'UNITY_5_3_OR_NEWER', 'UNITY_STANDALONE', 'ENABLE_INPUT_SYSTEM',
               'ENABLE_MONO', 'NET_STANDARD_2_1', 'NET_STANDARD', 'NETSTANDARD2_1', 'ENABLE_UNITY_COLLECTIONS_CHECKS']
    results = []
    stages = [
        ('editor-runtime', 'Assembly-CSharp', runtime, ['UNITY_EDITOR', 'UNITY_EDITOR_LINUX', 'UNITY_STANDALONE_LINUX'], []),
        ('editor-scripts', 'Assembly-CSharp-Editor', editor_sources, ['UNITY_EDITOR', 'UNITY_EDITOR_LINUX', 'UNITY_STANDALONE_LINUX'],
         [out / 'editor-runtime/Assembly-CSharp.dll']),
        ('linux-player-runtime', 'Assembly-CSharp', runtime, ['UNITY_STANDALONE_LINUX'], []),
        ('windows-player-runtime', 'Assembly-CSharp', runtime, ['UNITY_STANDALONE_WIN'], [])]
    if assembly_roots:
        for assembly, stage in test_names.items():
            declared_refs = []
            for reference in assembly_definitions[assembly].get('references', []):
                path = (out / test_names[reference] / (reference + '.dll')) if reference in test_names else cache / (reference + '.dll')
                if reference not in test_names and not path.is_file():
                    parser.error('Missing declared test assembly reference: ' + str(path))
                declared_refs.append(path)
            stages.append((stage, assembly, test_sources[assembly],
                           ['UNITY_EDITOR', 'UNITY_EDITOR_LINUX', 'UNITY_STANDALONE_LINUX', 'UNITY_INCLUDE_TESTS'],
                           test_refs + declared_refs))
    for name, assembly, sources, extra_defines, extra_refs in stages:
        folder = out / name
        folder.mkdir(exist_ok=True)
        dll = folder / (assembly + '.dll')
        stage_refs = engine_refs if name.startswith('test-') else refs if name.startswith('editor-') else [p for p in refs if not
            (p.name.startswith('UnityEditor') or '.Editor.' in p.name or p.name.endswith('.Editor.dll'))]
        response = ['-nologo', '-target:library', '-nostdlib+', '-langversion:9', '-unsafe+', '-deterministic+',
                    '-out:"' + str(dll) + '"', '-define:' + ';'.join(defines + extra_defines)]
        response += ['-reference:"' + str(p) + '"' for p in stage_refs + extra_refs]
        response += ['"' + str(p) + '"' for p in sources]
        rsp = folder / 'compile.rsp'
        rsp.write_text('\n'.join(response) + '\n')
        process = subprocess.run([str(host), str(csc), '@' + str(rsp)], text=True,
                                 stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
        log = process.stdout
        for path, label in ((out, '$OUTPUT'), (editor, '$UNITY_DATA'), (cache, '$TEMPLATE_ASSEMBLIES'), (sdk, '$DOTNET')):
            log = log.replace(str(path), label)
        (folder / 'compile.log').write_text(log)
        result = {'stage': name, 'assembly': assembly, 'exitCode': process.returncode, 'sourceCount': len(sources),
                  'referenceCount': len(stage_refs + extra_refs), 'log': name + '/compile.log',
                  'warningCounts': dict(collections.Counter(re.findall(r'warning (CS\d+)', log)))}
        results.append(result)
        print(json.dumps(result))
        for line in log.splitlines():
            if ': error ' in line:
                print(line)
    report = {'scope': 'External C# compilation against genuine Unity 6000.6 and matching package APIs. Test asmdefs compile separately without a reference to Assembly-CSharp. Not Unity import, serialization, ShaderLab compilation, player build, UTF execution or runtime testing.',
              'unityVersion': '6000.6.0f1', 'expectedPackageVersions': expected,
              'runtimeSources': len(runtime), 'editorSources': len(editor_sources), 'sourceManifest': records,
              'testSources': {name: len(sources) for name, sources in test_sources.items()} if assembly_roots else {},
              'assemblyDefinitions': assembly_records,
              'testFrameworkReferences': [{'name': p.name, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in test_refs],
              'results': results, 'generatedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'sourceStillMatches': all(hashlib.sha256((project / row['file']).read_bytes()).hexdigest() == row['sha256'] for row in records + assembly_records)}
    (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    return 0 if report['sourceStillMatches'] and all(row['exitCode'] == 0 for row in results) else 1


if __name__ == '__main__':
    raise SystemExit(main())
