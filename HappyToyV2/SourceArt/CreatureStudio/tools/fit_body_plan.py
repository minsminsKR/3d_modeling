"""Record reviewed standing source anatomy for failed generated AI skins.

This creates a compact 22-joint locomotion rig from the NEW generated mesh.
Actual lateral limb cross sections refine the measured template positions.
Full source AI rigs remain independent evidence, not reused skin weights.
"""
import argparse
import json
import hashlib
from pathlib import Path
import numpy as np
from glb_io import read, accessor, node_matrices


def make_baby(source, rig_source, mapping_path):
    """Keep reviewed Baby AI joint positions but replace its torn source skin."""
    document, binary = read(source)
    primitive = document['meshes'][0]['primitives'][0]
    vertices = accessor(document, binary, primitive['attributes']['POSITION'])
    low, high = vertices.min(0), vertices.max(0)
    mapping = json.loads(Path(mapping_path).read_text(encoding='utf8'))['map']
    rig_document, _ = read(rig_source)
    matrices, _ = node_matrices(rig_document)
    nodes = {node.get('name'): index for index, node in enumerate(rig_document['nodes'])}
    joints = {name: matrices[nodes[node]][:3, 3] for name, node in mapping.items()}
    specs = []
    def bone(name, parent, end):
        head = joints[name]
        tail = joints[end] if isinstance(end, str) else head + np.array(end)
        specs.append({'name': 'mixamorig:' + name, 'parent': 'mixamorig:' + parent if parent else None,
                      'head': head.tolist(), 'tail': tail.tolist(), 'semantic': name})
    bone('Hips', None, 'Spine'); bone('Spine', 'Hips', 'Spine1'); bone('Spine1', 'Spine', 'Spine2')
    bone('Spine2', 'Spine1', 'Neck'); bone('Neck', 'Spine2', 'Head'); bone('Head', 'Neck', (0, .10, 0))
    for side in ('Left', 'Right'):
        bone(side+'Shoulder', 'Spine2', side+'Arm'); bone(side+'Arm', side+'Shoulder', side+'ForeArm')
        bone(side+'ForeArm', side+'Arm', side+'Hand')
        bone(side+'Hand', side+'ForeArm', (0, -.085, .012))
        bone(side+'UpLeg', 'Hips', side+'Leg'); bone(side+'Leg', side+'UpLeg', side+'Foot')
        bone(side+'Foot', side+'Leg', side+'ToeBase'); bone(side+'ToeBase', side+'Foot', (0, 0, .03))
    return {'version': 1, 'creature': 'Baby', 'coordinateSystem': 'source glTF Y-up / face +Z; Blender (x,-z,y)',
            'source': Path(source).name, 'sourceSHA256': hashlib.sha256(Path(source).read_bytes()).hexdigest(),
            'sourceBounds': [low.tolist(), high.tolist()], 'target_height_m': 1.48, 'bones': specs,
            'reviewedAIJointSource': Path(rig_source).name,
            'reviewedAIJointSHA256': hashlib.sha256(Path(rig_source).read_bytes()).hexdigest(),
            'sourceAIUsedForSkin': False, 'skinMethod': 'new source surface geodesic binding',
            'headLock': {'aboveY': .158, 'frontZ': .080, 'frontMinY': .145, 'halfWidth': .135},
            'animation_profile_overrides': {},
            'manualReview': 'Actual Baby source face/jaw/neck Y .15-.18 boundary; rigid head above .158. Upright AI joints fitted actual limbs; original AI skin rejected by normal-disabled crawling pose audit.'}


def make(source, creature):
    document, binary = read(source)
    primitive = document['meshes'][0]['primitives'][0]
    vertices = accessor(document, binary, primitive['attributes']['POSITION'])
    _, unique = np.unique(np.round(vertices*1e5).astype(np.int32), axis=0, return_index=True)
    points = vertices[unique]
    low, high = points.min(0), points.max(0)
    height = high[1]-low[1]
    mid = (low[1]+high[1])/2
    # Coordinates below use source height=1 standing A-pose; Y is adjusted
    # against the measured generator bounds, never against the failed AI rig.
    def point(x, y, z):
        return np.array([x*height, mid+y*height, z*height], dtype=float)
    joints = {name: point(*value) for name, value in {
        'Hips': (0,-.16,.035), 'Spine': (0,-.025,.035), 'Spine1': (0,.105,.035),
        'Spine2': (0,.205,.04), 'Neck': (0,.265,.045), 'Head': (0,.325,.075)}.items()}
    fitting = []

    def station(name, side, target_y, x_range, z_range):
        selected = (abs(points[:,1]-(mid+target_y*height))<.008*height)
        selected &= (points[:,0]*side>x_range[0]*height)&(points[:,0]*side<x_range[1]*height)
        selected &= (points[:,2]>z_range[0]*height)&(points[:,2]<z_range[1]*height)
        if selected.sum()<16:
            raise ValueError('No reliable source limb cross section: '+name)
        value = points[selected].mean(0)
        fitting.append({'joint':name, 'surfaceVertices':int(selected.sum()), 'sourcePosition':value.tolist(),
                        'method':'mean of welded actual limb surface horizontal station',
                        'yStationNormalized':target_y,'xRangeNormalized':x_range,'zRangeNormalized':z_range})
        return value

    for label, side in [('Left',1),('Right',-1)]:
        joints[label+'Shoulder'] = point(side*.08,.205,.04)
        joints[label+'Arm'] = point(side*.165,.205,.045)
        if creature == 'Uncat':
            joints[label+'ForeArm'] = station(label+'ForeArm',side,.055,(.235,.29),(-.03,.17))
            joints[label+'Hand'] = station(label+'Hand',side,-.035,(.265,.32),(.04,.21))
        else:
            joints[label+'ForeArm'] = station(label+'ForeArm',side,.015,(.235,.31),(-.06,.15))
            joints[label+'Hand'] = station(label+'Hand',side,-.10,(.27,.34),(-.04,.15))
        joints[label+'UpLeg'] = point(side*.12,-.175,.015)
        joints[label+'Leg'] = station(label+'Leg',side,-.31,(.085,.19),(-.08,.13))
        joints[label+'Foot'] = station(label+'Foot',side,-.432,(.085,.215),(-.055,.13))
        toe_mask=(points[:,0]*side>.085*height)&(points[:,0]*side<.24*height)
        toe_mask &= (points[:,1]<mid-.455*height)&(points[:,2]>.075*height)
        toe = points[toe_mask]
        if len(toe)<16:
            raise ValueError('No measured source toe volume: '+label)
        joints[label+'ToeBase'] = toe.mean(0)
    specs = []
    def bone(name,parent,tail):
        head = joints[name]
        ending = joints[tail] if isinstance(tail,str) else head+np.array(tail)*height
        specs.append({'name':'mixamorig:'+name,'parent':'mixamorig:'+parent if parent else None,
                      'head':head.tolist(),'tail':ending.tolist(),'semantic':name})
    bone('Hips',None,'Spine');bone('Spine','Hips','Spine1');bone('Spine1','Spine','Spine2')
    bone('Spine2','Spine1','Neck');bone('Neck','Spine2','Head');bone('Head','Neck',(0,.09,0))
    for label in ('Left','Right'):
        bone(label+'Shoulder','Spine2',label+'Arm');bone(label+'Arm',label+'Shoulder',label+'ForeArm')
        bone(label+'ForeArm',label+'Arm',label+'Hand');bone(label+'Hand',label+'ForeArm',(0,-.085,.012))
        bone(label+'UpLeg','Hips',label+'Leg');bone(label+'Leg',label+'UpLeg',label+'Foot')
        bone(label+'Foot',label+'Leg',label+'ToeBase');bone(label+'ToeBase',label+'Foot',(0,0,.04))
    return {'version':1,'creature':creature,'coordinateSystem':'source glTF Y-up / face +Z; Blender (x,-z,y)',
        'source':Path(source).name,'sourceSHA256':hashlib.sha256(Path(source).read_bytes()).hexdigest(),
        'sourceBounds':[low.tolist(),high.tolist()],'target_height_m':2.2 if creature=='Uncat' else 2.38,
        'bones':specs,'fittedStations':fitting,'sourceAIUsedForSkin':False,'skinMethod':'new source surface geodesic binding',
        'headLock':{'aboveY':float(mid+.255*height),'frontZ':float(.205*height),'frontMinY':float(mid+.15*height),'halfWidth':float(.16*height)},
        'animation_profile_overrides':{},'manualReview':'Actual original front/side source previews; torso joints inside body and lateral limb stations measured. Verify large-motion skin and floor before delivery.'}


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('source',type=Path)
    parser.add_argument('--creature',choices=['Uncat','Cyclopse','Baby'],required=True)
    parser.add_argument('--rig-source',type=Path)
    parser.add_argument('--map',type=Path)
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    report=make_baby(args.source,args.rig_source,args.map) if args.creature=='Baby' else make(args.source,args.creature)
    args.output.write_text(json.dumps(report,indent=2),encoding='utf8')
    print('FITTED_SOURCE_BODY_PLAN',args.creature,len(report['bones']))
