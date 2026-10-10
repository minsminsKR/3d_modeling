"""Round remeshed limb planes while preserving topology, UVs and human hands.
Uses Blender's volume-preserving Laplacian relaxation, followed by explicit
anatomical weighting/pins because the modifier's global volume compensation
would otherwise move zero-weight fingers. Existing high base coordinates are
retargeted to the same low surface before actual tangent normal rebaking.
"""
import bpy
VERSION=1
def relax_skin(meshes,highs=None):
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    bottom=min(p.z for p in points);height=max(p.z for p in points)-bottom
    changes={}
    high_by_name={o.name[len('SCULPT HIGH | '):]:o for o in highs or []}
    for obj in meshes:
        if obj.data.materials[0].name!='MW_skin':continue
        if obj.get('anatomicalCurvatureVersion')==VERSION:continue
        vertices=len(obj.data.vertices);polygons=len(obj.data.polygons)
        before=[v.co.copy() for v in obj.data.vertices]
        uv_before=[tuple(p.uv) for p in obj.data.uv_layers.active.data] if obj.data.uv_layers.active else None
        group=obj.vertex_groups.new(name='Authored anatomical upper-limb curvature')
        weights=[]
        for v in obj.data.vertices:
            physical=((obj.matrix_world@v.co).z-bottom)*1.64/height
            weight=max(0,min(1,(physical-.23)/.13));weights.append(weight)
            group.add([v.index],weight,'REPLACE')
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        modifier=obj.modifiers.new('Volume-preserving continuous muscle surface','LAPLACIANSMOOTH')
        modifier.iterations=18;modifier.lambda_factor=.65;modifier.lambda_border=.001
        modifier.use_volume_preserve=True;modifier.use_normalized=True;modifier.vertex_group=group.name
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        # Re-pin the complete hands and blend the wrist boundary back smoothly;
        # no global scale compensation may displace the finger/contact geometry.
        for v in obj.data.vertices:v.co=before[v.index]+(v.co-before[v.index])*weights[v.index]
        assert len(obj.data.vertices)==vertices and len(obj.data.polygons)==polygons
        assert uv_before==([tuple(p.uv) for p in obj.data.uv_layers.active.data] if obj.data.uv_layers.active else None)
        assert all((v.co-before[v.index]).length<.000001 for v in obj.data.vertices if weights[v.index]==0)
        for poly in obj.data.polygons:poly.use_smooth=True
        obj.data.update();obj['anatomicalCurvatureVersion']=VERSION
        if obj.name in high_by_name:
            high=high_by_name[obj.name];assert len(high.data.vertices)==vertices
            for high_v,low_v in zip(high.data.vertices,obj.data.vertices):high_v.co=low_v.co
            high.data.update();high['anatomicalCurvatureVersion']=VERSION
        distance=max((v.co-before[v.index]).length for v in obj.data.vertices)
        changes[obj.name]=distance
        print('ANATOMICAL_CURVATURE_PASS',obj.name,'maxMove='+str(distance),'handMove=0','UV/topology preserved',flush=True)
    return changes
