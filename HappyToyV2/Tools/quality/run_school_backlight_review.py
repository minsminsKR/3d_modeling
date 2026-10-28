#!/usr/bin/env python3
"""Optional isolated URP school backlight review, never starts a native player."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir', type=Path, required=True, help='Fresh folder under project Builds')
    parser.add_argument('--timeout', type=int, default=1200)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[2]
    builds = (project / 'Builds').resolve()
    output = args.output_dir.resolve()
    if not output.is_relative_to(builds) or output == builds or output.exists():
        parser.error('output-dir must be a fresh directory below Builds')
    if args.timeout <= 0:
        parser.error('timeout must be positive')
    unity = shutil.which('unity')
    if not unity:
        parser.error('Unity CLI is unavailable')
    output.mkdir(parents=True)
    env = os.environ.copy()
    env['HAPPYTOY_SCHOOL_BACKLIGHT_REVIEW_DIR'] = str(output / 'frames')
    command = [unity, 'test', str(project), '--editor-version', '6000.6.0f1', '--mode', 'PlayMode',
        '--output', str(output / 'results.xml'), '--timeout', str(args.timeout), '--filter',
        'HappyToy.V2.CloudTests.CloudPlayModeTests.SchoolEmergencyBacklightActualCameraReview', '--',
        '-logFile', str(output / 'editor.log'), '-noaudio', '-v2-audit-no-desktop-interaction', '-job-worker-count', '2']
    options = {}
    if sys.platform == 'win32':
        info = subprocess.STARTUPINFO()
        info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        info.wShowWindow = subprocess.SW_HIDE
        options = {'startupinfo': info, 'creationflags': subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS}
    result = subprocess.run(command, cwd=project, env=env, capture_output=True, text=True,
        encoding='utf-8', errors='replace', **options)
    (output / 'cli.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    verified = False
    reason = ''
    report = output / 'results.xml'
    if result.returncode == 0 and report.is_file():
        try:
            root = ET.parse(report).getroot()
            matching = [case for case in root.iter('test-case')
                if case.get('fullname', '').endswith('.SchoolEmergencyBacklightActualCameraReview')]
            verified = (root.get('result') == 'Passed' and len(matching) == 1 and
                matching[0].get('result') == 'Passed' and (output / 'frames' / 'measurement.json').is_file())
            if not verified:
                reason = 'Review case or measurement missing, failed or skipped'
        except (ET.ParseError, OSError) as exc:
            reason = str(exc)
    else:
        reason = 'Unity did not produce a successful current test report'
    print(json.dumps({'exitCode': result.returncode, 'verified': verified, 'reason': reason,
        'output': str(output), 'graphicsBackendRequested': True, 'desktopIsolation': True,
        'noNativePlayer': True}, indent=2))
    return 0 if verified else 1


if __name__ == '__main__':
    raise SystemExit(main())
