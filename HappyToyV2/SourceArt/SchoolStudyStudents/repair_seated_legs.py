"""Correct source-derived leg positions before the seated surface is baked.

The source rig and texture are retained. Geometry outside the leg-weighted
region and the shoulder/head rig remains untouched; this is not a base mesh.
"""
import math
from mathutils import Matrix


def repair(rig, source, posed, upper_names, lower_names, hip_angle, lower_angle):
    if len(source.data.vertices)!=len(posed.data.vertices):
        raise RuntimeError('The evaluated skin must preserve source vertex order')
    if len(upper_names)!=2 or len(lower_names)!=2:
        raise RuntimeError('Both source leg chains are required')
    rotation=Matrix.Rotation(math.radians(-hip_angle),4,'X').to_3x3()
    lower_rotation=Matrix.Rotation(math.radians(lower_angle-hip_angle),4,'X').to_3x3()
    hips=[rig.matrix_world@rig.data.bones[name].head_local for name in upper_names]
    knees=[rig.matrix_world@rig.data.bones[name].head_local for name in lower_names]
    groups=[];ankles=[]
    for upper,lower in zip(upper_names,lower_names):
        knee=rig.data.bones[lower]
        foot=next((child for child in knee.children if child.use_deform),None)
        if foot is None:raise RuntimeError('Both lower legs must have a deforming foot bone')
        ankles.append(rig.matrix_world@foot.head_local)
        toe=next((child for child in foot.children if child.use_deform),None) if foot else None
        names=[upper,lower]+([foot.name] if foot else [])+([toe.name] if toe else [])
        groups.append([source.vertex_groups[name].index for name in names if source.vertex_groups.get(name)])
    inverse=posed.matrix_world.inverted();counts=[0,0]
    for vertex,target in zip(source.data.vertices,posed.data.vertices):
        point=source.matrix_world@vertex.co
        if point.z>max(hips[0].z,hips[1].z)+.03:continue
        weights={entry.group:entry.weight for entry in vertex.groups}
        totals=[sum(weights.get(group,0) for group in chain) for chain in groups]
        side=0 if totals[0]>=totals[1] else 1;strength=totals[side]
        if strength<.35:continue
        hip,knee=hips[side],knees[side];moved_knee=hip+rotation@(knee-hip)
        blend=max(0,min(1,(point.z-(knee.z-.11))/.22));blend=blend*blend*(3-2*blend)
        thigh=hip+rotation@(point-hip)
        ankle=ankles[side];moved_ankle=moved_knee+lower_rotation@(ankle-knee)
        shin=moved_knee+lower_rotation@(point-knee)
        flat_foot=moved_ankle+(point-ankle)
        ankle_blend=max(0,min(1,(point.z-(ankle.z-.06))/.12));ankle_blend=ankle_blend*ankle_blend*(3-2*ankle_blend)
        shin=flat_foot.lerp(shin,ankle_blend)
        desired=shin.lerp(thigh,blend)
        hip_fade=max(0,min(1,(hip.z+.02-point.z)/.16));hip_fade=hip_fade*hip_fade*(3-2*hip_fade)
        influence=max(0,min(1,(strength-.35)/.5));influence=influence*influence*(3-2*influence)*hip_fade
        original=posed.matrix_world@target.co;target.co=inverse@original.lerp(desired,influence)
        counts[side]+=1
    if min(counts)==0:raise RuntimeError('Both legs must contain source-weighted vertices')
    posed.data.update()
    return counts
