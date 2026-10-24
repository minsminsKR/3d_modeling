#!/usr/bin/env python3
"""Recheck exact non-passing NUnit cases, or inspect existing case durations.

This is a selected rerun. Its result never certifies a fresh complete suite.
It does not change time scale, game rules, test assertions or full-suite scope.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

_spec=importlib.util.spec_from_file_location('happytoy_feature_checks',Path(__file__).with_name('run_feature_tests.py'))
runner=importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(runner)


def retry_selectors(report: Path) -> tuple[dict, tuple[str,...]]:
    result=runner.inspect_results(report,())
    if not result['total']:
        raise ValueError('Source report contains zero test cases')
    selectors=tuple(dict.fromkeys(result['incomplete']))
    if any(not name or name=='unknown' or ';' in name or '\n' in name for name in selectors):
        raise ValueError('Source report has an unsafe or absent exact test identity')
    if not selectors and result['errors']:
        raise ValueError('The suite failed outside individual cases; run its original profile')
    return result,selectors


def main() -> int:
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--results',type=Path,required=True)
    parser.add_argument('--mode',choices=('EditMode','PlayMode'),help='Required for reruns; never infer the mode from a filename')
    parser.add_argument('--include-profile',action='append',choices=runner.PROFILES,default=[],help='Also recheck the chosen mode of relevant feature profiles')
    parser.add_argument('--timings-only',action='store_true',help='Read an existing report without starting Unity')
    parser.add_argument('--dry-run',action='store_true')
    parser.add_argument('--project',type=Path,default=runner.PROJECT)
    parser.add_argument('--output-dir',type=Path)
    parser.add_argument('--graphics-api',choices=('d3d11','d3d12'))
    parser.add_argument('--no-desktop-interaction',action='store_true',default=True,
        help='Enabled by default: isolate batch input/cursor writes and hide low-priority Windows children')
    parser.add_argument('--timeout',type=int,default=7200)
    args=parser.parse_args()
    report=args.results.resolve()
    try:
        source,selectors=retry_selectors(report)
    except (OSError,ValueError,ET.ParseError) as error:
        parser.error(str(error))
    if args.timings_only:
        print(json.dumps({'scope':'Existing report analysis; no tests executed',**source},indent=2))
        return 0
    if not args.mode:parser.error('--mode is required for a selected rerun')
    if args.timeout<=0:parser.error('--timeout must be positive')
    selected=set(selectors)
    for profile in args.include_profile:
        if profile=='full':parser.error('Use run_feature_tests.py --profile full for a complete run')
        for mode,names in runner.stages(profile):
            if mode==args.mode:selected.update(names)
    if not selected:
        print(json.dumps({'status':'NO_RETRY_NEEDED','testsExecuted':0,'sourceReport':str(report),
                          'scope':'No non-passing source cases; current sources were not tested'},indent=2))
        return 0
    selected=tuple(sorted(selected))
    project=args.project.resolve()
    if f'm_EditorVersion: {runner.VERSION}' not in (project/'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8'):
        parser.error('The project must use its pinned Unity version')
    output=(args.output_dir or project/'Builds/Verification/feature-tests'/
            (datetime.now().strftime('%Y%m%d-%H%M%S')+'-retry-'+uuid.uuid4().hex[:8])).resolve()
    if output.exists():parser.error('Use a new output directory; never reuse previous results')
    unity=shutil.which('unity')
    if not unity and not args.dry_run:parser.error('Unity CLI is unavailable')
    plan=runner.commands('retry-failed',project,output,unity or 'unity',timeout=args.timeout,
        graphics_api=args.graphics_api,no_desktop_interaction=args.no_desktop_interaction,
        selected_stages=[(args.mode,selected)])
    metadata={'scope':'Selected non-passing cases plus explicit feature checks; no new complete-suite verdict',
              'sourceReport':str(report),'sourceSha256':hashlib.sha256(report.read_bytes()).hexdigest(),
              'sourceCounts':{key:source[key] for key in ('total','passed','failed','skipped')},
              'includedProfiles':args.include_profile,'selectors':selected,'stages':plan,
              'noDesktopInteractionRequested':args.no_desktop_interaction}
    if args.dry_run:
        print(json.dumps(metadata,indent=2))
        return 0
    output.mkdir(parents=True)
    (output/'plan.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
    startup=runner.desktop_process_options()
    result={'status':'FAIL','scope':metadata['scope'],'sourceSha256':metadata['sourceSha256'],
            'noDesktopInteractionRequested':args.no_desktop_interaction,
            'windowsHiddenBelowNormalRequested':bool(startup)}
    try:
        print('Selected recheck cases: '+str(len(selected)),flush=True)
        completed=subprocess.run(plan[0]['command'],cwd=project,check=False,**startup)
        result.update(runner.inspect_results(Path(plan[0]['report']),selected))
        result['exitCode']=completed.returncode
        if not completed.returncode and not result['errors']:result['status']='PASS'
    except (OSError,ValueError,ET.ParseError) as error:result['reason']=str(error)
    finally:
        (output/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))
    return 0 if result['status']=='PASS' else 1


if __name__=='__main__':
    raise SystemExit(main())
