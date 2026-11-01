"""Pack an inspected student review blend into a portable editable source."""
import argparse
from pathlib import Path
import sys
import bpy

HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--student',choices=('01','02','03'),required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
output=HERE/'finished'/('student'+args.student+'.blend')
output.parent.mkdir(parents=True,exist_ok=True)
if not bpy.data.objects.get('Seated student head rig') or not bpy.data.objects.get('Student seated game review'):
    raise RuntimeError('The reviewed editable rig and game mesh are required')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(output),compress=True)
print('PACKED_STUDENT_SOURCE',output,flush=True)
