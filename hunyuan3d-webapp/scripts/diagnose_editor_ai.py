from pathlib import Path
import sys,json
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from rigging_editor import read_skeleton
from rigging_artifacts import read_glb
from rigging_motion import transforms
for path in sys.argv[1:]:
    doc,_=read_glb(path);world,_=transforms(doc)
    print(json.dumps({'path':path,'skeleton':read_skeleton(path),'meshes':[{ 'name':n.get('name'),'matrix':world[i].tolist()} for i,n in enumerate(doc['nodes']) if 'mesh' in n]},indent=2))
