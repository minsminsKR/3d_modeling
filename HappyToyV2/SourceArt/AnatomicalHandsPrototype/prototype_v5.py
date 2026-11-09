import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'hand-rig-prototype-v4.blend'))
h=bpy.data.objects['Blender anatomical hand candidate'];rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
# Measured from the neutral source outline; draft landmarks in metres.
chains={
 'thumb':[(.038,-.004,.065),(.061,-.012,.09),(.079,-.020,.112),(.088,-.022,.136)],
 'index':[(.033,-.002,.125),(.039,-.006,.157),(.046,-.010,.188),(.052,-.014,.218)],
 'middle':[(.012, .001,.132),(.014,-.003,.166),(.016,-.007,.198),(.018,-.011,.228)],
 'ring':[(-.012,.001,.13),(-.012,-.003,.160),(-.012,-.007,.192),(-.012,-.011,.220)],
 'little':[(-.035,-.002,.115),(-.041,-.006,.140),(-.047,-.012,.166),(-.050,-.018,.188)]}
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for digit,points in chains.items():
 for i in range(1,4):
  bone=rig.data.edit_bones[f'{digit}_{i}'];bone.use_connect=False;bone.head=points[i-1];bone.tail=points[i];bone.align_roll(Vector((0,-1,0)))
 tip=rig.data.edit_bones[digit+'_tip'];tip.use_connect=False;tip.head=points[3];tip.tail=Vector(points[3])+Vector((0,0,.003))
bpy.ops.object.mode_set(mode='OBJECT')
# Approximate digit groups from neutral surface; blend the MCP into palm and
# adjacent phalanges longitudinally. This remains an unreviewed test candidate.
allgroups=list(h.vertex_groups)
for v in h.data.vertices:
 x,y,z=v.co
 if z<.096: continue
 if x>.057 and z<.165: digit='thumb'
 else:
  # The visible four fingers are separated along x; preserve their ordering.
  digit=min(('little','ring','middle','index'),key=lambda d:abs(x-chains[d][2][0]))
 p=chains[digit]; z0=p[0][2]; z3=p[3][2]
 if z < z0-.015: continue
 for g in allgroups:
  try:g.remove([v.index])
  except RuntimeError:pass
 palm=max(0.,min(1.,(z0+.006-z)/.022))
 if palm>0: h.vertex_groups['palm'].add([v.index],palm,'REPLACE')
 t=max(0.,min(2.999,(z-z0)/(z3-z0)*3))
 # Smooth between centers of adjacent phalanges.
 s=max(0.,min(2.,t-.5));i=min(2,int(s));f=s-i
 h.vertex_groups[digit+'_'+str(i+1)].add([v.index],max(1e-6,(1-palm)*(1-f)),'REPLACE')
 if i<2 and f>1e-6:h.vertex_groups[digit+'_'+str(i+2)].add([v.index],(1-palm)*f,'REPLACE')
bpy.context.view_layer.objects.active=h
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
counts={g.name:sum(any(w.group==g.index and w.weight>.6 for w in v.groups) for v in h.data.vertices) for g in h.vertex_groups if g.name.endswith('_3')}
sums=[sum(g.weight for g in v.groups) for v in h.data.vertices]
report={'strongDistalVertices':counts,'unweighted':sum(w<.999 for w in sums),'maxInfluences':max(sum(g.weight>1e-6 for g in v.groups) for v in h.data.vertices),'status':'draft anatomical landmarks, new axes; action retargeting and texture not complete'}
(root/'prototype-v5-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'hand-rig-prototype-v5.blend'))
print('REPORT',json.dumps(report))
