"""CPU-only texture preview and action clips; never overwrite the AI rig."""
import json
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from rigging_process_guard import install_process_guard
install_process_guard()
from rigging_artifacts import inspect_glb
from rigging_motion import write_action_clips, write_texture_preview, MOTION_VERSION

source = Path(sys.argv[1]).resolve()
folder = source.parent
full = folder/f'rig-{MOTION_VERSION}.glb'
preview = folder/f'preview-{MOTION_VERSION}.glb'
manifest = folder/f'motion-{MOTION_VERSION}.json'
editor_state=folder/'editor-state.json'
groups=json.loads(editor_state.read_text()).get('groups',[]) if editor_state.is_file() else []
info = write_action_clips(source,full,groups=groups)
write_texture_preview(full,preview)
full_info, proxy_info = inspect_glb(full), inspect_glb(preview)
if full_info['animations'] != proxy_info['animations']: raise ValueError('Preview action set differs from full model')
info.update({'model_file':full.name,'preview_file':preview.name,'source_file':source.name,
             'full_model':full_info,'preview_model':proxy_info,
             'preview_roles':info['roles'], 'geometry_preserved':True,'preview_texture_size':2048})
temporary = manifest.with_suffix('.tmp')
temporary.write_text(json.dumps(info,ensure_ascii=False,indent=2))
temporary.replace(manifest)
print(json.dumps(info,ensure_ascii=False),flush=True)
