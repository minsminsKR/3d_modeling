"""Apply deterministic action presets to an AI-inferred GLB skeleton.

These clips are procedural animation, not an AI motion-generation model.
Clean rigged.glb remains untouched. All axes use glTF's Y-up convention.
"""
from pathlib import Path
import io
import json
import math
import struct
import numpy as np
from PIL import Image
from rigging_artifacts import read_glb, accessor

MOTION_VERSION = 'actions-v2'


def multiply(a, b):
    av, bv = np.asarray(a[:3]), np.asarray(b[:3])
    return np.r_[a[3]*bv+b[3]*av+np.cross(av,bv), a[3]*b[3]-np.dot(av,bv)]


def rotation_matrix(q):
    x,y,z,w = np.asarray(q)/np.linalg.norm(q)
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                     [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                     [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])


def node_matrix(node):
    if 'matrix' in node: return np.array(node['matrix']).reshape(4,4).T
    m = np.eye(4)
    m[:3,:3] = rotation_matrix(node.get('rotation',[0,0,0,1])) @ np.diag(node.get('scale',[1,1,1]))
    m[:3,3] = node.get('translation',[0,0,0])
    return m


def transforms(doc, overrides=None):
    nodes = doc['nodes']
    parent = {c:i for i,n in enumerate(nodes) for c in n.get('children',[])}
    world = {}
    def visit(i):
        if i not in world:
            local = dict(nodes[i])
            local.update((overrides or {}).get(i,{}))
            world[i] = (visit(parent[i]) if i in parent else np.eye(4)) @ node_matrix(local)
        return world[i]
    for i in range(len(nodes)): visit(i)
    return world, parent


def skeleton_roles(doc, binary):
    world, parent = transforms(doc)
    joints = list(dict.fromkeys(j for s in doc['skins'] for j in s['joints']))
    roots = [j for j in joints if parent.get(j) not in joints]
    minimum, maximum = np.full(3,np.inf), np.full(3,-np.inf)
    mass = {j:0. for j in joints}
    for node in doc['nodes']:
        if 'skin' not in node: continue
        skin = doc['skins'][node['skin']]
        for primitive in doc['meshes'][node['mesh']]['primitives']:
            a = primitive['attributes']
            positions = accessor(doc,binary,a['POSITION'])
            minimum = np.minimum(minimum,positions.min(axis=0))
            maximum = np.maximum(maximum,positions.max(axis=0))
            indices, weights = accessor(doc,binary,a['JOINTS_0']), accessor(doc,binary,a['WEIGHTS_0'])
            for k,j in enumerate(skin['joints']): mass[j] += float(np.where(indices==k,weights,0).sum())
    height = max(float(maximum[1]-minimum[1]),.001)
    width = max(float(maximum[0]-minimum[0]),.001)
    center = (maximum+minimum)/2
    total = max(sum(mass.values()),1)
    arms = []
    for side in [-1,1]:
        candidates = [j for j in joints if j not in roots
            and (world[j][0,3]-center[0])*side > width*.18
            and minimum[1]+height*.2 < world[j][1,3] < minimum[1]+height*.83
            and mass[j]/total > .002]
        if candidates:
            # Pick a proximal lateral branch, not several joints in the same limb.
            candidates.sort(key=lambda j:(sum(1 for p in candidates if p in ancestors(j,parent)), -mass[j]))
            arms.append({'node':candidates[0], 'side':side})
    lateral = {a['node'] for a in arms}
    central = [j for j in joints if j not in roots and j not in lateral
        and abs(world[j][0,3]-center[0]) < width*.15
        and world[j][1,3] > minimum[1]+height*.45 and mass[j]/total > .002]
    head = max(central,key=lambda j:world[j][1,3]) if central else None
    return {'roots':roots,'arms':arms,'head':head,'height':height,
            'convention':'glTF Y-up; geometric lateral-branch classification',
            'joint_names':{str(j):doc['nodes'][j].get('name',str(j)) for j in joints}}, world, parent


def ancestors(j,parent):
    result = []
    while j in parent:
        j = parent[j]; result.append(j)
    return result


def write_glb(doc, binary, output):
    doc['buffers'][0]['byteLength'] = len(binary)
    encoded = json.dumps(doc,separators=(',',':'),ensure_ascii=False).encode()
    encoded += b' ' * (-len(encoded)%4)
    binary = bytes(binary) + b'\0' * (-len(binary)%4)
    total = 12+8+len(encoded)+8+len(binary)
    Path(output).write_bytes(struct.pack('<4sII',b'glTF',2,total)+struct.pack('<II',len(encoded),0x4E4F534A)+encoded+struct.pack('<II',len(binary),0x004E4942)+binary)


def write_texture_preview(source, output, texture_size=2048):
    """Keep exact geometry, UVs and weights; resize only embedded image pixels.

    Generated UV atlases contain many tiny islands. Mesh decimation without a
    texture bake destroys them, so the web proxy deliberately keeps all faces.
    """
    doc, raw = read_glb(source)
    images = {image['bufferView']:image for image in doc.get('images',[]) if 'bufferView' in image}
    binary = bytearray()
    for n,view in enumerate(doc.get('bufferViews',[])):
        offset = view.get('byteOffset',0)
        data = raw[offset:offset+view['byteLength']]
        if n in images:
            with Image.open(io.BytesIO(data)) as image:
                if max(image.size)>texture_size:
                    image.thumbnail((texture_size,texture_size),Image.Resampling.LANCZOS)
                    packed = io.BytesIO(); image.save(packed,format='PNG')
                    data = packed.getvalue(); images[n]['mimeType']='image/png'
        binary.extend(b'\0'*(-len(binary)%4))
        view['byteOffset']=len(binary);view['byteLength']=len(data)
        binary.extend(data)
    write_glb(doc,binary,output)


def write_action_clips(source, output, roles=None, groups=None):
    doc, raw = read_glb(source)
    inferred, world, parent = skeleton_roles(doc,raw)
    roles = roles or inferred
    configured=[]
    for group in groups or []:
        members={int(j) for j in group['joints']}
        anchors=[j for j in members if parent.get(j) not in members]
        configured.append(dict(group,anchors=anchors))
    if configured:
        custom_arms=[{'node':j,'side':-1 if world[j][0,3]<0 else 1} for g in configured if g['type']=='arm' for j in g['anchors']]
        roles['arms']=custom_arms
    roles['groups']=configured
    binary = bytearray(raw)
    def add(values, kind):
        values = np.asarray(values,dtype='<f4')
        binary.extend(b'\0'*(-len(binary)%4))
        start = len(binary); packed = values.tobytes(); binary.extend(packed)
        views = doc.setdefault('bufferViews',[])
        views.append({'buffer':0,'byteOffset':start,'byteLength':len(packed)})
        item = {'bufferView':len(views)-1,'componentType':5126,'count':len(values),'type':kind}
        if kind=='SCALAR': item.update(min=[float(values.min())],max=[float(values.max())])
        doc.setdefault('accessors',[]).append(item)
        return len(doc['accessors'])-1
    def world_rotation(j,axis,angles):
        if 'matrix' in doc['nodes'][j]: raise ValueError('Animation target must use TRS')
        basis = world[parent[j]][:3,:3] if j in parent else np.eye(3)
        local_axis = np.linalg.solve(basis,np.asarray(axis,dtype=float))
        local_axis /= np.linalg.norm(local_axis)
        base = doc['nodes'][j].get('rotation',[0,0,0,1])
        quats = [multiply(np.r_[local_axis*math.sin(a/2),math.cos(a/2)],base) for a in angles]
        return [q/np.linalg.norm(q) for q in quats]
    specs = [('Idle','대기',3.2),('Bow','인사',2.8),('Hop','점프',2.4)]
    if roles['arms']: specs.insert(1,('Wave','팔 흔들기',2.4))
    if configured:
        specs.append(('GroupAction','전체 팔다리 그룹',3.2))
        specs.extend((f"Limb_{g['id']}",g['name'],3.2) for g in configured)
    clips = []
    doc['animations'] = []
    for name,label,duration in specs:
        t = np.linspace(0,duration,49); p = t/duration
        cycle = np.sin(2*np.pi*p); pulse = np.sin(np.pi*p)**2
        # Exact neutral endpoints make loops, clip changes and reset stable.
        cycle[[0,-1]]=0; pulse[[0,-1]]=0
        animation = {'name':name,'samplers':[],'channels':[],
                     'extras':{'label_ko':label,'source':'procedural preset on AI rig','version':MOTION_VERSION}}
        time_id = add(t,'SCALAR')
        def channel(j,path,values):
            animation['samplers'].append({'input':time_id,'output':add(values,'VEC4' if path=='rotation' else 'VEC3'),'interpolation':'LINEAR'})
            animation['channels'].append({'sampler':len(animation['samplers'])-1,'target':{'node':j,'path':path}})
        grouped = name=='GroupAction' or name.startswith('Limb_')
        for root in ([] if grouped else roles['roots']):
            tilt = .025*cycle if name=='Idle' else .18*pulse if name=='Bow' else -.10*cycle if name=='Hop' else .035*cycle
            axis = [0,0,1] if name in ['Idle','Wave'] else [1,0,0]
            channel(root,'rotation',world_rotation(root,axis,tilt))
            if name=='Hop':
                # Smooth takeoff/landing with no horizontal accumulation.
                lift = roles['height']*.18*np.sin(np.pi*p)**4
                lift[[0,-1]]=0
                basis = world[parent[root]][:3,:3] if root in parent else np.eye(3)
                delta = np.linalg.solve(basis,np.vstack([np.zeros(len(t)),lift,np.zeros(len(t))])).T
                channel(root,'translation',np.asarray(doc['nodes'][root].get('translation',[0,0,0]))+delta)
        for arm in ([] if grouped else roles['arms']):
            j,side = arm['node'],arm['side']
            if name=='Wave': angle = -side*(.32*pulse+.22*cycle)*pulse
            elif name=='Hop': angle = -side*.22*pulse
            elif name=='Bow': angle = side*.08*pulse
            else: angle = side*.035*cycle
            channel(j,'rotation',world_rotation(j,[0,0,1],angle))
        head = roles.get('head')
        if head is not None and not grouped:
            angle = .09*pulse if name=='Bow' else .04*cycle
            channel(head,'rotation',world_rotation(head,[1,0,0],angle))
        if grouped:
            for group in configured:
                if name!='GroupAction' and name!=f"Limb_{group['id']}": continue
                axis={'arm':[0,0,1],'leg':[1,0,0],'head':[1,0,0],'other':[0,1,0]}[group['type']]
                angle=math.radians(group['amplitude'])*np.sin(2*np.pi*(p+group['phase']))*pulse
                angle[[0,-1]]=0
                for joint in group['anchors']:
                    sign=-1 if group['type']=='arm' and world[joint][0,3]>0 else 1
                    channel(joint,'rotation',world_rotation(joint,axis,angle*sign))
        doc['animations'].append(animation)
        clips.append({'name':name,'label':label,'duration':duration,'channels':len(animation['channels'])})
    doc.setdefault('asset',{}).setdefault('extras',{})['a100_motion'] = {'version':MOTION_VERSION,'source':'procedural presets on SkinTokens AI rig'}
    write_glb(doc,binary,output)
    return {'version':MOTION_VERSION,'clips':clips,'roles':roles,'method':'procedural presets on AI-inferred skeleton'}
