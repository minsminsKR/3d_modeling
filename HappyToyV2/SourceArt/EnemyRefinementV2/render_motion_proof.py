import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent / 'work'
for item in json.loads((ROOT/'refinement-report.json').read_text()):
    key=item['key'];bpy.ops.wm.open_mainfile(filepath=str(ROOT/'proposed/SourceArt/EnemyRefinementV2'/(key+'-refinement-v2.blend')))
    scene=bpy.context.scene;model=next(o for o in scene.objects if o.type=='MESH' and o.name!='Proof floor')
    scene.render.engine='BLENDER_EEVEE';end=max(a.frame_range.y for a in bpy.data.actions)
    for index,phase in enumerate([0,.2,.4,.6,.8,1]):
        frame=1+phase*(end-1);scene.frame_set(int(frame),subframe=frame%1)
        ev=model.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
        points=[ev.matrix_world@v.co for v in mesh.vertices];ev.to_mesh_clear()
        low=Vector(tuple(min(v[a] for v in points) for a in range(3)));high=Vector(tuple(max(v[a] for v in points) for a in range(3)))
        center=(low+high)/2;height=high.z-low.z
        scene.camera.location=center+Vector((height*.95,-height*2.3,height*.28))
        scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.camera.data.ortho_scale=max(height*1.32,max(high-low)*1.60)
        scene.render.filepath=str(ROOT/(key+'-motion-'+str(index)+'.png'));bpy.ops.render.render(write_still=True)
print('ENEMY_MOTION_FRAMING_COMPLETE',flush=True)
