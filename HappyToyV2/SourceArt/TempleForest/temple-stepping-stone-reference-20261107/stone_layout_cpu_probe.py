import math, random, hashlib, json, time, os, sys
from collections import defaultdict
from pathlib import Path
if os.name == 'nt':
    import ctypes
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x00004000)

WIDTH,LENGTH,HALF_W,HALF_L = 3.4, 31.0, 1.7, 15.5
SEED = 43093
AXES = [(math.cos(math.pi*i/32), math.sin(math.pi*i/32)) for i in range(32)]
CELL=0.8

def x_edge_margin(z):
    return .03 + .11*(.5+.5*math.sin(z*.83+1.2)) + .06*(.5+.5*math.sin(z*1.71-.7))

def extent(stone):
    x,z,a,b,c,s = stone
    return math.hypot(a*c,b*s), math.hypot(a*s,b*c)

def support(a,b,c,s,ux,uz):
    return math.hypot(a*(ux*c+uz*s), b*(-ux*s+uz*c))

def separate(p,q,gap=.008):
    dx,dz=p[0]-q[0],p[1]-q[1]
    for ux,uz in AXES:
        if abs(dx*ux+dz*uz) >= support(*p[2:],ux,uz)+support(*q[2:],ux,uz)+gap:
            return True
    return False

def make_layout(densify,fill_small=False):
    rng=random.Random(SEED); stones=[]; buckets=defaultdict(list)
    def try_add(x,z,small=False):
        a,b=(rng.uniform(.12,.18),rng.uniform(.105,.155)) if small else (rng.uniform(.27,.325),rng.uniform(.225,.285))
        yaw=rng.random()*2*math.pi
        p=(x,z,a,b,math.cos(yaw),math.sin(yaw)); rx,rz=extent(p)
        if abs(x)+rx > HALF_W-x_edge_margin(z) or abs(z)+rz>HALF_L-.04:return False
        bx,bz=int(math.floor(x/CELL)),int(math.floor(z/CELL))
        for i in range(bx-1,bx+2):
            for j in range(bz-1,bz+2):
                for k in buckets[(i,j)]:
                    if not separate(p,stones[k]):return False
        buckets[(bx,bz)].append(len(stones));stones.append(p);return True
    cols,rows=6,55
    for row in range(rows):
        z=-HALF_L+(row+.5)*LENGTH/rows
        for col in range(cols):
            x=-HALF_W+(col+.5+(.35 if row%2 else -.35))*WIDTH/cols
            for attempt in range(5):
                if try_add(x+rng.uniform(-.11,.11),z+rng.uniform(-.105,.105)):break
    if densify:
        for i in range(12000):
            try_add(rng.uniform(-HALF_W,HALF_W),rng.uniform(-HALF_L,HALF_L))
    if fill_small:
        for i in range(12000):
            try_add(rng.uniform(-HALF_W,HALF_W),rng.uniform(-HALF_L,HALF_L),True)
    return stones

def summarize(stones):
    step=.05;nx,nz=68,620;counts=bytearray(nx*nz)
    for x,z,a,b,c,s in stones:
        rx,rz=extent((x,z,a,b,c,s))
        xmin=max(0,int((x-rx+HALF_W)/step)-1);xmax=min(nx-1,int((x+rx+HALF_W)/step)+1)
        zmin=max(0,int((z-rz+HALF_L)/step)-1);zmax=min(nz-1,int((z+rz+HALF_L)/step)+1)
        for ix in range(xmin,xmax+1):
            dx=-HALF_W+(ix+.5)*step-x
            for iz in range(zmin,zmax+1):
                dz=-HALF_L+(iz+.5)*step-z
                if ((dx*c+dz*s)/a)**2+((-dx*s+dz*c)/b)**2<=1:
                    counts[ix*nz+iz]+=1
    def coverage(condition):
        indexes=[ix for ix in range(nx) if condition(-HALF_W+(ix+.5)*step)]
        return round(100*sum(counts[ix*nz+iz]>0 for ix in indexes for iz in range(nz))/(len(indexes)*nz),3)
    samples=[x for x in counts]
    edges=[]; empty_span=run=0; center_ix=[i for i in range(nx) if abs(-HALF_W+(i+.5)*step)<.4]
    for iz in range(nz):
        positions=[abs(-HALF_W+(ix+.5)*step) for ix in range(nx) if counts[ix*nz+iz]]
        if positions:edges.append(max(positions))
        if any(counts[ix*nz+iz] for ix in center_ix):run=0
        else:run+=1;empty_span=max(empty_span,run)
    edges.sort();
    data=json.dumps(stones,separators=(',',':')).encode()
    return {'count':len(stones),'small_count':sum(1 for p in stones if p[2]<.20),'coverage_percent':round(100*sum(v>0 for v in samples)/len(samples),3),
      'center_abs_x_below_0_6_coverage_percent':coverage(lambda x:abs(x)<.6),
      'outer_abs_x_above_1_1_coverage_percent':coverage(lambda x:abs(x)>1.1),
      'sampled_overlapping_percent':round(100*sum(v>1 for v in samples)/len(samples),5),
      'max_center_band_empty_z_span_m':round(empty_span*step,3),
      'outermost_occupied_abs_x_p05_m':round(edges[int((len(edges)-1)*.05)],3) if edges else None,
      'outermost_occupied_abs_x_p95_m':round(edges[int((len(edges)-1)*.95)],3) if edges else None,
      'theoretical_triangles_all_3000_1200_400':[len(stones)*i for i in (3000,1200,400)],
      'layout_sha256':hashlib.sha256(data).hexdigest()}

def main():
    start=time.time(); out={}
    for label,dense,small in [('staggered',False,False),('densified',True,False),('densified_small_gaps',True,True)]:
        layout=make_layout(dense,small);result=summarize(layout);repeat=make_layout(dense,small)
        result['deterministic_repeat']=layout==repeat
        out[label]=result
    out['limitations']='Hypothetical conservative ellipses, not the generated GLB silhouette, stone mesh contact, a render, GPU/draw calls or gameplay approval. 5 cm cell-center raster can miss smaller holes or intersections. No fixed production thresholds are implied.'
    out['elapsed_seconds']=round(time.time()-start,3)
    print(json.dumps(out,indent=2,ensure_ascii=False))
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
if __name__=='__main__':main()
