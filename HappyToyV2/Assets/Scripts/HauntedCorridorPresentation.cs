using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    /// <summary>
    /// Original timber, paper and seal-lantern dressing for the seeded corridor.
    /// All opaque dressing follows existing solid geometry. No physics or navigation
    /// components are added, so a visually concealed opening never fools enemy sight.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class HauntedCorridorPresentation : MonoBehaviour
    {
        sealed class PhysicalState
        {
            public Collider collider;
            public Vector3 position, scale, center, size;
            public Quaternion rotation;
            public bool enabled;
            public int layer;
        }
        sealed class ObstacleState
        {
            public NavMeshObstacle obstacle;
            public Vector3 center, size;
            public bool enabled, carving;
            public NavMeshObstacleShape shape;
        }
        sealed class LampState
        {
            public Light light;
            public Color color;
            public float intensity, range;
        }
        sealed class HiddenState { public MeshRenderer renderer; public bool enabled; }
        sealed class MaterialState { public MeshRenderer renderer; public Material[] original, applied; }

        // A separate mesh per cell/material keeps URP light selection local and
        // avoids one draw per lattice batten. No temporary primitive colliders exist.
        sealed class Draft
        {
            public readonly Vector3 origin;
            public readonly Material material;
            public readonly bool castShadows;
            readonly GraphicsSurfaceLibrary.Geometry geometry;
            public Draft(Vector3 origin,Material material,bool castShadows=true)
            {this.origin=origin;this.material=material;this.castShadows=castShadows;
                geometry=new GraphicsSurfaceLibrary.Geometry(origin,GraphicsSurfaceLibrary.TileSpan(material));}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)=>geometry.Quad(a,b,c,d);
            public void Box(Vector3 center,Vector3 size,Quaternion rotation)=>geometry.Box(center,size,rotation,
                Mathf.Min(.003f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.12f));
            public Mesh Mesh(string name)=>geometry.Mesh(name);
            public int VertexCount=>geometry.VertexCount;
        }

        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly GraphicsSurfaceLibrary.Pool graphicsSurfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<PhysicalState> physical = new List<PhysicalState>();
        readonly List<ObstacleState> obstacles = new List<ObstacleState>();
        readonly List<HiddenState> hidden = new List<HiddenState>();
        readonly List<MaterialState> cabinetMaterials = new List<MaterialState>();
        readonly Dictionary<Mesh, Mesh> cabinetPeekMeshes = new Dictionary<Mesh, Mesh>();
        readonly List<LampState> lamps = new List<LampState>();
        readonly List<Light> eligibleLanterns = new List<Light>();
        readonly List<Transform> externalVisualRoots = new List<Transform>();
        readonly Dictionary<string,Draft> drafts = new Dictionary<string,Draft>();
        CorridorRun run;
        Material timber, paper, ink, cloth, warmShade, redShade;
        Transform additions;
        TextMesh returnClue;
        bool originalFog, ownsFog;
        FogMode originalFogMode;
        float originalFogDensity;
        Color originalFogColor;
        int lastRecovered=-1;
        public bool Prepared { get; private set; }
        public int CladWallFaces { get; private set; }
        public int LayeredPassages { get; private set; }
        public int SealShrines { get; private set; }
        public int VisualBatches { get; private set; }
        public int TimberCabinets { get; private set; }
        public int PreservedColliders => physical.Count;
        public int PreservedObstacles => obstacles.Count;
        public Transform AdditionRoot => additions;
        public TextMesh ReturnClue => returnClue;
        // Eligibility only: LocalShadowBudget owns all Light.shadows decisions.
        public IReadOnlyList<Light> Lanterns => eligibleLanterns;

        public bool PhysicalStateIntact => physical.All(state => state.collider &&
            state.collider.enabled==state.enabled && state.collider.gameObject.layer==state.layer &&
            state.collider.transform.position==state.position && state.collider.transform.rotation==state.rotation &&
            state.collider.transform.lossyScale==state.scale && state.collider.bounds.center==state.center && state.collider.bounds.size==state.size) &&
            obstacles.All(state => state.obstacle && state.obstacle.enabled==state.enabled &&
                state.obstacle.center==state.center && state.obstacle.size==state.size &&
                state.obstacle.shape==state.shape && state.obstacle.carving==state.carving);

        public void Prepare(CorridorRun source)
        {
            if (Prepared || run) throw new InvalidOperationException("Corridor presentation already prepared");
            if (!source || source.Layout==null) throw new ArgumentException("Built corridor required",nameof(source));
            run=source;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponentInParent<StalkerBrain>()) continue;
                physical.Add(new PhysicalState {collider=collider,position=collider.transform.position,rotation=collider.transform.rotation,
                    scale=collider.transform.lossyScale,center=collider.bounds.center,size=collider.bounds.size,enabled=collider.enabled,layer=collider.gameObject.layer});
            }
            foreach (var obstacle in GetComponentsInChildren<NavMeshObstacle>(true))
                obstacles.Add(new ObstacleState {obstacle=obstacle,center=obstacle.center,size=obstacle.size,
                    enabled=obstacle.enabled,carving=obstacle.carving,shape=obstacle.shape});
            additions=new GameObject("Original seal-and-timber corridor dressing").transform;
            additions.SetParent(transform,false);
            timber=Surface("Weathered corridor lattice",new Color(.64f,.49f,.32f),Resources.Load<Texture2D>("Corridor/aged-floor-v2"));
            var fibers=PaperTexture();
            paper=Surface("Smoke-stained handmade paper",new Color(.77f,.71f,.56f),fibers);
            cloth=Surface("Oxide-red seal cloth",new Color(.38f,.12f,.075f),fibers);
            ink=Surface("Faded seal ink",new Color(.12f,.075f,.035f),fibers);
            warmShade=Surface("Translucent amber paper lantern",new Color(.79f,.54f,.26f),fibers,new Color(.70f,.31f,.085f)*.75f);
            redShade=Surface("Translucent vermilion paper lantern",new Color(.69f,.20f,.09f),fibers,new Color(.52f,.085f,.025f)*.72f);
            PrepareSectors();
            for (int cell=0;cell<CorridorLayout.Count;cell++)
            {
                Vector3 center=run.CellPosition(cell);center.y=0;
                var wood=CellDraft(cell,timber);
                // Real ceiling joists sit above head height and never disguise a passage.
                foreach(float z in new[]{-1.7f,1.7f})
                    wood.Box(center+new Vector3(0,2.935f,z),new Vector3(5.78f,.055f,.10f),Quaternion.identity);
                wood.Box(center+Vector3.up*2.935f,new Vector3(.10f,.055f,5.78f),Quaternion.identity);
                for (int direction=0;direction<4;direction++)
                {
                    int neighbor=CorridorLayout.Neighbor(cell,direction);
                    if ((direction==2 || direction==3) && neighbor>=0) continue;
                    Vector3 normal=new Vector3(CorridorLayout.DX[direction],0,CorridorLayout.DZ[direction]);
                    Vector3 edge=center+normal*3;
                    bool passage=(run.Layout.Connections[cell]&(1<<direction))!=0;
                    DressEdge(cell,edge,normal,passage,unchecked(run.Seed*17+cell*113+direction*43));
                    if(neighbor>=0) DressEdge(neighbor,edge,-normal,passage,unchecked(run.Seed*19+neighbor*113+direction*43));
                    if(passage) LayeredPassages++;
                }
            }
            DressSectorClues();
            DressLanterns();
            DressSlidingLeaves();
            DressShrines();
            DressCabinets();
            DressEntrance();
            foreach(var entry in drafts)
                Emit(entry.Value,additions,"Timber-paper cell batch "+entry.Key,false);
            drafts.Clear();
            ApplyFog();
            Prepared=true;
            if(!PhysicalStateIntact) throw new InvalidOperationException("Visual dressing changed corridor physics");
            RefreshReturnClue();
        }

        Draft CellDraft(int cell,Material material,bool castShadows=true)
        {
            string key=cell+":"+material.name+":"+castShadows;
            if (!drafts.TryGetValue(key,out var draft))
            {
                Vector3 origin=run.CellPosition(cell);origin.y=0;
                draft=new Draft(origin,material,castShadows);drafts.Add(key,draft);
            }
            return draft;
        }

        void DressEdge(int cell,Vector3 edge,Vector3 outward,bool passage,int seed)
        {
            Vector3 inward=-outward, across=new Vector3(outward.z,0,-outward.x);
            Quaternion basis=Quaternion.LookRotation(inward);
            var random=new System.Random(seed);
            if(!passage) Panel(cell,edge,across,inward,basis,0,5.74f,random);
            else
            {
                // Existing solid side walls start at +/-1.36m. Decorative profiles
                // stay outside the original post's +/-1.29m clear opening.
                Panel(cell,edge,across,inward,basis,-2.18f,1.57f,random);
                Panel(cell,edge,across,inward,basis,2.18f,1.57f,random);
                var wood=CellDraft(cell,timber);
                foreach(float side in new[]{-1f,1f})
                {
                    wood.Box(edge+across*side*1.42f+inward*.145f+Vector3.up*1.26f,new Vector3(.12f,2.45f,.035f),basis);
                    wood.Box(edge+across*side*1.50f+inward*.128f+Vector3.up*1.33f,new Vector3(.035f,2.27f,.022f),basis);
                }
                wood.Box(edge+inward*.145f+Vector3.up*2.72f,new Vector3(2.95f,.13f,.038f),basis);
                // Three unequal short paper strips above all standing eye/capsule heights.
                if((seed&3)!=0)
                    for(int strip=0;strip<3;strip++)
                    {
                        float width=.11f+(float)random.NextDouble()*.055f;
                        float bottom=2.37f+(float)random.NextDouble()*.075f;
                        HangingSeal(CellDraft(cell,(seed&7)==1?SectorBinding(cell):SectorPaper(cell),false),edge+across*(strip-1)*.34f+inward*.14f,
                            across,inward,width,bottom,2.70f,random);
                    }
            }
        }

        void Panel(int cell,Vector3 edge,Vector3 across,Vector3 inward,Quaternion basis,float offset,float width,System.Random random)
        {
            var wood=CellDraft(cell,timber);var sheet=CellDraft(cell,SectorPaper(cell));
            Vector3 at=edge+across*offset+inward*.12f;
            // Shallow cladding follows the original wall: continuous opaque backing,
            // lower timber boards, then uneven inset paper with a structural lattice.
            wood.Box(at+Vector3.up*.44f,new Vector3(width,.72f,.018f),basis);
            foreach(float y in new[]{.10f,.80f,2.56f})
                wood.Box(at+inward*.014f+Vector3.up*y,new Vector3(width,.065f,.025f),basis);
            int bays=Mathf.Max(1,Mathf.RoundToInt(width/SectorBayWidth(cell)));float bay=width/bays;
            for(int index=0;index<bays;index++)
            {
                float x=offset-width*.5f+(index+.5f)*bay;
                float top=2.50f-(float)random.NextDouble()*.045f;
                float bottom=.86f+(float)random.NextDouble()*.04f;
                Vector3 face=edge+across*x+inward*.137f;
                // Slightly bent lower corners and unequal upper edges make old paper
                // readable as a material, rather than a pristine rectangular paint strip.
                sheet.Quad(face-across*(bay*.5f-.025f)+Vector3.up*bottom,
                    face+across*(bay*.5f-.025f)+Vector3.up*(bottom+.012f)+inward*.003f,
                    face+across*(bay*.5f-.025f)+Vector3.up*top,
                    face-across*(bay*.5f-.025f)+Vector3.up*(top-.012f));
                // Both wall sides have their own inward-facing sheet. Winding differs
                // from the across axis on opposite sides, so choose the visible order.
                sheet.Quad(face+across*(bay*.5f-.025f)+Vector3.up*(bottom+.012f)+inward*.003f,
                    face-across*(bay*.5f-.025f)+Vector3.up*bottom,
                    face-across*(bay*.5f-.025f)+Vector3.up*(top-.012f),
                    face+across*(bay*.5f-.025f)+Vector3.up*top);
                for(int line=0;line<2;line++)
                    wood.Box(face+across*(line==0?-bay*.24f:bay*.24f)+inward*.015f+Vector3.up*1.68f,
                        new Vector3(.018f,1.64f,.014f),basis);
                wood.Box(edge+across*(offset-width*.5f+index*bay)+inward*.157f+Vector3.up*1.7f,
                    new Vector3(.04f,1.77f,.027f),basis);
                if(random.NextDouble()<.23)
                {
                    float patchY=1.02f+(float)random.NextDouble()*.5f;
                    var patch=CellDraft(cell,(index&1)==0?SectorBinding(cell):SectorPaper(cell),false);
                    HangingSeal(patch,face+inward*.038f,across,inward,.12f,patchY,patchY+.28f,random);
                    var marks=CellDraft(cell,ink,false);
                    for(int mark=0;mark<3;mark++)
                        marks.Box(face+inward*.046f+Vector3.up*(patchY+.06f+mark*.06f),new Vector3(.054f,.012f,.003f),basis);
                }
            }
            wood.Box(edge+across*(offset+width*.5f)+inward*.157f+Vector3.up*1.7f,new Vector3(.04f,1.77f,.027f),basis);
            foreach(float y in SectorCrossbars(cell))
                wood.Box(at+inward*.05f+Vector3.up*y,new Vector3(width,.022f,.02f),basis);
            // Slightly separated bottom planks break the repeating wall panel rhythm.
            for(float x=-width*.5f+.28f;x<width*.5f;x+=.46f)
                wood.Box(at+across*x+inward*.022f+Vector3.up*.43f,new Vector3(.018f,.62f,.012f),basis);
            CladWallFaces++;
        }

        static void HangingSeal(Draft draft,Vector3 plane,Vector3 across,Vector3 normal,float width,float bottom,float top,System.Random random)
        {
            float notch=.025f+(float)random.NextDouble()*.035f;
            Vector3 a=plane-across*width*.5f+Vector3.up*bottom;
            Vector3 b=plane+across*width*.5f+Vector3.up*(bottom+notch)+normal*.009f;
            Vector3 c=plane+across*width*.5f+Vector3.up*top;
            Vector3 d=plane-across*width*.5f+Vector3.up*top;
            draft.Quad(a,b,c,d);draft.Quad(b,a,d,c);
        }

        void DressLanterns()
        {
            foreach(var light in GetComponentsInChildren<Light>(true).Where(light=>light.name=="Corridor lamp"))
            {
                lamps.Add(new LampState {light=light,color=light.color,intensity=light.intensity,range=light.range});
                eligibleLanterns.Add(light);
                int cell=NearestCell(light.transform.position);
                bool red=cell!=0 && Array.IndexOf(run.Layout.Relics,cell)<0 && (cell%12==0 || cell%12==8);
                light.color=red?new Color(.94f,.33f,.15f):new Color(1f,.69f,.37f);
                light.intensity=cell==0?2.65f:red?2.15f:2.45f;
                // Keep the authored light positions/range. Paper transmits this point
                // light, so its visual shade casts no opaque self-shadow around the bulb.
                var wood=CellDraft(cell,timber);Vector3 center=light.transform.position;
                var lanternRoot=new GameObject("Graphics paper lantern composition").transform;
                lanternRoot.SetParent(additions,false);lanternRoot.position=center;
                var lantern=GraphicsPropLibrary.Attach("paper-lantern",lanternRoot,
                    slot=>slot=="GU_washi"?(red?redShade:warmShade):graphicsSurfaces.Resolve(slot),owned);
                foreach(var renderer in lantern.GetComponentsInChildren<MeshRenderer>())
                {renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;}
                var threatFlicker=light.gameObject.AddComponent<ThreatFixtureFlicker>();
                threatFlicker.Configure(run.Lighting,light,lantern.GetComponentsInChildren<Renderer>(),cell*.73f);
                wood.Box(center+Vector3.up*.30f,new Vector3(.027f,.20f,.027f),Quaternion.identity);
                if(red)
                {
                    var tassel=CellDraft(cell,cloth,false);
                    tassel.Box(center+Vector3.down*.245f,new Vector3(.035f,.09f,.022f),Quaternion.identity);
                }
                var housing=GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(renderer=>renderer.name=="Lamp housing" &&
                    Vector3.Distance(renderer.transform.position,center)<.01f);
                Hide(housing);
            }
        }

        static void Beam(Draft draft,Vector3 a,Vector3 b,float width)
        {
            Vector3 delta=b-a;
            draft.Box((a+b)*.5f,new Vector3(width,width,delta.magnitude),Quaternion.LookRotation(delta));
        }

        void DressSlidingLeaves()
        {
            foreach(var door in GetComponentsInChildren<Interactable>(true).Where(item=>item.kind==Interactable.Kind.Door))
            {
                if(!door.movingLeaf) continue;
                var root=new GameObject("Sliding timber-paper leaf dressing").transform;
                root.SetParent(door.movingLeaf,false);externalVisualRoots.Add(root);
                root.localScale=new Vector3(1/door.movingLeaf.localScale.x,1/door.movingLeaf.localScale.y,1/door.movingLeaf.localScale.z);
                var wood=new Draft(Vector3.zero,timber);var sheets=new Draft(Vector3.zero,SectorPaper(NearestCell(door.transform.position)));
                Hide(door.movingLeaf.GetComponent<MeshRenderer>());
                wood.Box(Vector3.zero,new Vector3(2.59f,2.35f,.10f),Quaternion.identity);
                foreach(float side in new[]{-1f,1f})
                {
                    wood.Box(new Vector3(0,-.76f,side*.048f),new Vector3(2.57f,.79f,.018f),Quaternion.identity);
                    for(int panel=0;panel<4;panel++)
                    {
                        float x=(panel-1.5f)*.62f;
                        sheets.Box(new Vector3(x,.34f,side*.050f),new Vector3(.59f,1.35f,.009f),Quaternion.identity);
                        wood.Box(new Vector3(x-.31f,.35f,side*.052f),new Vector3(.035f,1.46f,.014f),Quaternion.identity);
                        for(int slat=0;slat<2;slat++)
                            wood.Box(new Vector3(x+(slat==0?-.16f:.16f),.35f,side*.052f),new Vector3(.016f,1.33f,.014f),Quaternion.identity);
                    }
                    foreach(float y in new[]{-.35f,.25f,.90f,1.10f})
                        wood.Box(new Vector3(0,y,side*.048f),new Vector3(2.57f,.037f,.022f),Quaternion.identity);
                    var mount=new GameObject("Graphics leaf handle mount").transform;mount.SetParent(root,false);
                    mount.localPosition=new Vector3(1.23f,-.05f,side*.044f);
                    mount.localRotation=Quaternion.Euler(0,side>0?180:0,0);
                    var hardware=GraphicsPropLibrary.Attach("door-hardware",mount,graphicsSurfaces.Resolve,owned);
                    // Fit owns the identity imported-model wrapper. The mount
                    // turns its centered pull outward on each real leaf face.
                    GraphicsPropLibrary.Fit(hardware,new Bounds(Vector3.zero,new Vector3(.06f,.21f,.022f)));
                }
                Emit(wood,root,"Moving timber leaf lattice",true);Emit(sheets,root,"Moving leaf aged paper",true);
            }
        }

        void DressShrines()
        {
            var memories=GetComponentsInChildren<Interactable>(true).Where(item=>item.kind==Interactable.Kind.CorridorMemory)
                .OrderBy(item=>item.stableId,StringComparer.Ordinal).ToArray();
            foreach(var item in memories)
            {
                int cell=NearestCell(item.transform.position);Vector3 center=run.CellPosition(cell);center.y=.03f;
                var wood=CellDraft(cell,timber);var band=CellDraft(cell,SectorBinding(cell),false);var marks=CellDraft(cell,ink,false);
                // The new skin sits inside the preserved physical box. Its old opaque
                // renderer must not hide it or be baked into an architecture chunk.
                Hide(GetComponentsInChildren<MeshRenderer>(true).Single(renderer=>renderer.name=="Memory altar" && NearestCell(renderer.transform.position)==cell));
                // Original bevelled joinery model, authored as a separate version.
                // Geometry remains inside the preserved physical altar body.
                var altar=new GameObject("Graphics joined seal altar");
                altar.transform.SetParent(additions,false);altar.transform.position=center;
                var model=GraphicsPropLibrary.Attach("seal-altar",altar.transform,graphicsSurfaces.Resolve,owned);
                // Only the visual model meets the real slab top (Y=0). The
                // existing cell/item/collider offsets remain unchanged.
                altar.transform.position=new Vector3(center.x,-GraphicsPropLibrary.LocalBounds(model).min.y,center.z);
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {renderer.receiveShadows=true;VisualBatches++;}
                Hide(item.GetComponent<MeshRenderer>());
                var root=new GameObject("Folded original memory seal").transform;
                root.SetParent(item.transform,false);
                // Parent item is an original scaled cube. Normalize this child's scale
                // so its metre-authored folded paper remains inside the item's collider.
                root.localScale=new Vector3(1/item.transform.localScale.x,1/item.transform.localScale.y,1/item.transform.localScale.z);
                externalVisualRoots.Add(root);
                var seal=new Draft(Vector3.zero,SectorPaper(cell),false);var sealInk=new Draft(Vector3.zero,ink,false);var tie=new Draft(Vector3.zero,SectorBinding(cell),false);
                seal.Box(Vector3.zero,new Vector3(.265f,.278f,.112f),Quaternion.identity);
                seal.Quad(new Vector3(-.132f,.139f,-.058f),new Vector3(.132f,.139f,-.058f),
                    new Vector3(.10f,.052f,-.067f),new Vector3(-.06f,.068f,-.067f));
                foreach(float face in new[]{-1f,1f})
                {
                    tie.Box(new Vector3(0,-.048f,face*.060f),new Vector3(.266f,.017f,.004f),Quaternion.identity);
                    for(int stroke=0;stroke<3;stroke++)
                        sealInk.Box(new Vector3((stroke==1?.018f:-.012f),.052f-stroke*.029f,face*.063f),
                            new Vector3(stroke==1?.066f:.084f,.007f,.003f),Quaternion.Euler(0,0,stroke==1?-8:5));
                    sealInk.Box(new Vector3(-.022f,.036f,face*.064f),new Vector3(.007f,.095f,.003f),Quaternion.identity);
                }
                Emit(seal,root,"Folded seal paper",true);Emit(sealInk,root,"Original memory ink strokes",true);Emit(tie,root,"Memory binding cord",true);
                SealShrines++;
            }
        }

        void DressCabinets()
        {
            var grain=Resources.Load<Texture2D>("GraphicsPbr/wood-aged/albedo");
            var body=Surface("Corridor cabinet aged timber",new Color(.90f,.75f,.55f),grain);
            var panel=Surface("Corridor cabinet recessed timber",new Color(.72f,.54f,.36f),grain);
            var hardware=Surface("Corridor cabinet tarnished iron",new Color(.28f,.25f,.20f),null);
            foreach(var cabinet in GetComponentsInChildren<Interactable>(true).Where(item=>item.kind==Interactable.Kind.HidingPlace))
            {
                bool dressed=false;
                foreach(var renderer in cabinet.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(!renderer.enabled) continue;
                    var original=renderer.sharedMaterials;var applied=(Material[])original.Clone();bool changed=false;
                    for(int slot=0;slot<original.Length;slot++)
                    {
                        if(!original[slot]) continue;
                        string name=original[slot].name.ToLowerInvariant();
                        // Keep rubber feet and vent recesses. Replace only the native
                        // cabinet's existing body/panel/handle material slots, not mesh,
                        // transforms, inside/outside markers or collision geometry.
                        if(name.Contains("cabinet handles")) applied[slot]=hardware;
                        else if(name.Contains("recessed cabinet panels")) applied[slot]=panel;
                        else if(name.Contains("cabinet enamel") || name.Contains("plywood edge")) applied[slot]=body;
                        else continue;
                        changed=true;
                    }
                    if(!changed) continue;
                    cabinetMaterials.Add(new MaterialState {renderer=renderer,original=original,applied=applied});
                    renderer.sharedMaterials=applied;dressed=true;
                }
                if(dressed || cabinet.GetComponentsInChildren<MeshRenderer>(true).Any(renderer=>renderer.enabled))
                {
                    var originals=cabinet.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer.enabled).ToArray();
                    var target=GraphicsPropLibrary.LocalBounds(cabinet.transform);
                    var root=new GameObject("Graphics corridor timber cabinet").transform;root.SetParent(cabinet.transform,false);
                    externalVisualRoots.Add(root);
                    var model=GraphicsPropLibrary.Attach("cabinet-timber",root,graphicsSurfaces.Resolve,owned);
                    GraphicsPropLibrary.Fit(model,target);
                    CabinetPeekWindow.Prepare(cabinet,model,cabinetPeekMeshes,owned);
                    foreach(var renderer in originals)Hide(renderer);
                    TimberCabinets++;
                }
            }
        }

        void DressEntrance()
        {
            var exit=GetComponentsInChildren<Interactable>(true).Single(item=>item.name=="Sealed entrance" && item.kind==Interactable.Kind.Exit);
            Vector3 center=exit.transform.position;
            var wood=CellDraft(0,timber);var sheet=CellDraft(0,paper);var binding=CellDraft(0,cloth,false);
            Quaternion basis=Quaternion.Euler(0,90,0);
            wood.Box(center+Vector3.right*.062f,new Vector3(1.55f,2.17f,.014f),basis);
            foreach(float side in new[]{-1f,1f})
                wood.Box(center+Vector3.right*.072f+Vector3.forward*side*.73f,new Vector3(.055f,2.14f,.016f),basis);
            sheet.Box(center+Vector3.right*.072f+Vector3.up*.20f,new Vector3(1.12f,.69f,.013f),basis);
            for(int mark=0;mark<5;mark++)
                binding.Box(center+Vector3.right*.08f+new Vector3(0,-.33f,(mark-2)*.18f),new Vector3(.085f,.35f,.008f),basis);
            returnClue=new GameObject("Memory-return door clue").AddComponent<TextMesh>();
            returnClue.transform.SetParent(additions,false);
            returnClue.transform.SetPositionAndRotation(center+Vector3.right*.085f+Vector3.up*.20f,Quaternion.Euler(0,-90,0));
            returnClue.anchor=TextAnchor.MiddleCenter;returnClue.alignment=TextAlignment.Center;
            returnClue.fontSize=72;returnClue.characterSize=.016f;returnClue.color=new Color(.18f,.105f,.06f);
            returnClue.gameObject.AddComponent<AnnexSignFont>().Apply();
        }

        void RefreshReturnClue()
        {
            if(!returnClue || !run || lastRecovered==run.Recovered) return;
            lastRecovered=run.Recovered;
            returnClue.text=lastRecovered<CorridorRun.Required?"기억을 돌려놓는 문\n봉인 "+lastRecovered+" / 5":"다섯 봉인이 풀렸다\n돌아갈 수 있다";
        }
        void LateUpdate()
        {
            if(!Prepared) return;
            RefreshReturnClue();
            FitSectorClues();
            if(!returnClue) return;
            // This physical plaque faces +X, so its world Z/Y bounds are its
            // printed width/height. Fit live glyph bounds after native font updates.
            var bounds=returnClue.GetComponent<MeshRenderer>().bounds;
            float fit=Mathf.Max(bounds.size.z/1.05f,bounds.size.y/.60f);
            if(fit>1.001f) returnClue.transform.localScale/=fit;
        }

        int NearestCell(Vector3 position)
        {
            Vector3 relative=position-run.CellPosition(0);
            return Mathf.Clamp(Mathf.RoundToInt(relative.x/6),0,CorridorLayout.Width-1)+
                Mathf.Clamp(Mathf.RoundToInt(relative.z/6),0,CorridorLayout.Width-1)*CorridorLayout.Width;
        }
        void Hide(MeshRenderer renderer)
        {
            if(!renderer) return;
            hidden.Add(new HiddenState {renderer=renderer,enabled=renderer.enabled});renderer.enabled=false;
        }
        void Emit(Draft draft,Transform parent,string name,bool local)
        {
            if(draft.VertexCount==0)return;
            var mesh=draft.Mesh(name);owned.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            if(!local) go.transform.position=draft.origin;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=draft.material;
            renderer.shadowCastingMode=draft.castShadows?ShadowCastingMode.On:ShadowCastingMode.Off;
            renderer.receiveShadows=true;VisualBatches++;
        }
        Material Surface(string name,Color color,Texture2D texture,Color emission=default)
        {
            string kind=name.ToLowerInvariant();
            string key=kind.Contains("iron")?"metal-rust":kind.Contains("timber")||kind.Contains("lattice")?"wood-aged":"paper-aged";
            var material=graphicsSurfaces.Get(key,color,emission);
            material.name=name+" — measured PBR";
            return material;
        }
        Texture2D PaperTexture()
        {
            var paper=Resources.Load<Texture2D>("GraphicsPbr/paper-aged/albedo");
            if(!paper)throw new InvalidOperationException("Missing physical paper scan/fibre surface");
            return paper; // imported resource is not presentation-owned
        }
        void ApplyFog()
        {
            originalFog=RenderSettings.fog;originalFogMode=RenderSettings.fogMode;
            originalFogDensity=RenderSettings.fogDensity;originalFogColor=RenderSettings.fogColor;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=.018f;RenderSettings.fogColor=new Color(.025f,.019f,.014f);
            ownsFog=true;
            // Exp2 retains ~90% un-fogged contrast at 18m. A corridor corner or the
            // actual torch, rather than a dense fog curtain, controls enemy visibility.
        }
        void OnDestroy()
        {
            foreach(var state in cabinetMaterials) if(state.renderer && state.renderer.sharedMaterials.SequenceEqual(state.applied))
                state.renderer.sharedMaterials=state.original;
            foreach(var state in hidden) if(state.renderer) state.renderer.enabled=state.enabled;
            foreach(var state in lamps) if(state.light)
            {state.light.color=state.color;state.light.intensity=state.intensity;state.light.range=state.range;}
            foreach(var root in externalVisualRoots) if(root) Destroy(root.gameObject);
            if(additions) Destroy(additions.gameObject);
            foreach(var resource in owned) if(resource) Destroy(resource);
            graphicsSurfaces.Dispose();
            if(ownsFog && SceneManager.GetActiveScene()==gameObject.scene &&
                RenderSettings.fogMode==FogMode.ExponentialSquared && Mathf.Approximately(RenderSettings.fogDensity,.018f))
            {
                RenderSettings.fog=originalFog;RenderSettings.fogMode=originalFogMode;
                RenderSettings.fogDensity=originalFogDensity;RenderSettings.fogColor=originalFogColor;
            }
        }
    }
}
