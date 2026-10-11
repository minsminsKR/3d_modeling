"""Prepare edited skeleton or finish a CPU-only saved version."""
from pathlib import Path
import json
import shutil
import subprocess
import sys
from rigging_process_guard import install_process_guard
install_process_guard()
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from rigging_editor import apply_edit
from rigging_artifacts import inspect_glb,write_joint_preview
request=json.loads(Path(sys.argv[1]).read_text());folder=Path(request['output_dir'])
source=Path(request['source']);edited=folder/'edited-input.glb'
state=apply_edit(source,edited,request['edit'])
state.update(base_version=request['base_version'])
(folder/'editor-state.json').write_text(json.dumps(state,ensure_ascii=False,indent=2))
if request['edit']['weight_mode']=='keep':
    result=folder/'rigged.glb';shutil.copy2(edited,result)
    info=inspect_glb(result);preview=folder/'rig-preview.glb'
    info['preview_joint_count']=write_joint_preview(result,preview)
    subprocess.run([sys.executable,str(root/'scripts/rigging_motion_prepare.py'),str(result)],check=True,timeout=600)
    info.update(model_path=str(result),preview_path=str(preview),source=inspect_glb(source),
        preserve_original=True,gpu_id=None,model='SkinTokens / TokenRig · 수동 수정',edited=True,weight_mode='keep')
    (folder/'rigging-result.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
