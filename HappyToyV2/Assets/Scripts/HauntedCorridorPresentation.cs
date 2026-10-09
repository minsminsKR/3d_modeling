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
                if (!run.Layout.IsRoom(cell)) DressNarrowHall(cell);
                // Real ceiling joists sit above head height and never disguise a passage.
                foreach(float z in new[]{-1.7f,1.7f})
                    AddDetail(cell,"ceiling-joist",center+new Vector3(0,2.935f,z),Quaternion.identity,new Vector3(5.78f,1,1));
                AddDetail(cell,"ceiling-joist",center+Vector3.up*2.935f,Quaternion.Euler(0,90,0),new Vector3(5.78f,1,1));
                for (int direction=0;direction<4;direction++)
                {
                    int neighbor=CorridorLayout.Neighbor(cell,direction);
                    if ((direction==2 || direction==3) && neighbor>=0) continue;
                    Vector3 normal=new Vector3(CorridorLayout.DX[direction],0,CorridorLayout.DZ[direction]);
                    Vector3 edge=center+normal*3;
                    bool passage=(run.Layout.Connections[cell]&(1<<direction))!=0 || run.Layout.IsAltarPortal(cell,direction);
                    if (passage && !run.Layout.FramedPassage(cell,direction)) continue;
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
            DressAuthoredFloorsAndCeilings();
            DressAuthoredStructuralTimber();
            DressAuthoredClassroomShell();
            EmitDetails();
            EmitArchitecture();
            ApplyFog();
            Prepared=true;
            if(!PhysicalStateIntact) throw new InvalidOperationException("Visual dressing changed corridor physics");
            RefreshReturnClue();
        }

        void DressNarrowHall(int cell)
        {
            var center=run.CellPosition(cell); center.y=0;
            int mask=run.Layout.Connections[cell]; var edges=CorridorRun.HallTileEdges;
            for(int x=0;x<3;x++) for(int z=0;z<3;z++)
            {
                if(CorridorRun.HallTileOpen(mask,x,z)) continue;
                for(int d=0;d<4;d++)
                {
                    int nx=x+CorridorLayout.DX[d], nz=z+CorridorLayout.DZ[d];
                    if(nx<0 || nx>2 || nz<0 || nz>2 || !CorridorRun.HallTileOpen(mask,nx,nz)) continue;
                    var inward=new Vector3(CorridorLayout.DX[d],0,CorridorLayout.DZ[d]);
                    var across=new Vector3(inward.z,0,-inward.x);
                    var at=center+new Vector3((edges[x]+edges[x+1])*.5f,0,(edges[z]+edges[z+1])*.5f);
                    at+=inward*((d%2==0 ? edges[z+1]-edges[z] : edges[x+1]-edges[x])*.5f);
                    float width=d%2==0 ? edges[x+1]-edges[x] : edges[z+1]-edges[z];
                    Panel(cell,at,across,inward,Quaternion.LookRotation(inward),0,width-.02f,new System.Random(unchecked(run.Seed+cell*43+x*7+z*17+d)));
                }
            }
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
                foreach(float side in new[]{-1f,1f})
                {
                    AddDetail(cell,"passage-upright",edge+across*side*1.42f+inward*.145f+Vector3.up*1.26f,basis,Vector3.one);
                    AddDetail(cell,"passage-upright",edge+across*side*1.50f+inward*.128f+Vector3.up*1.33f,basis,
                        new Vector3(.035f/.12f,2.27f/2.45f,.022f/.035f));
                }
                AddDetail(cell,"passage-header",edge+inward*.145f+Vector3.up*2.72f,basis,Vector3.one);
                // Three unequal short paper strips above all standing eye/capsule heights.
                if((seed&3)!=0)
                    for(int strip=0;strip<3;strip++)
                    {
                        float width=.11f+(float)random.NextDouble()*.055f;
                        float bottom=2.37f+(float)random.NextDouble()*.075f;
                        AddDetail(cell,"hanging-seal",edge+across*(strip-1)*.34f+inward*.14f+Vector3.up*2.70f,
                            basis,new Vector3(width,2.70f-bottom,1),false,(seed&7)==1?SectorBinding(cell):SectorPaper(cell));
                    }
            }
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
                Vector3 center=light.transform.position;
                var lanternRoot=new GameObject("Graphics paper lantern composition").transform;
                lanternRoot.SetParent(additions,false);lanternRoot.position=center;
                var lantern=GraphicsPropLibrary.Attach("paper-lantern",lanternRoot,
                    slot=>slot=="GU_washi"?(red?redShade:warmShade):graphicsSurfaces.Resolve(slot),owned);
                foreach(var renderer in lantern.GetComponentsInChildren<MeshRenderer>())
                {renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;}
                var threatFlicker=light.gameObject.AddComponent<ThreatFixtureFlicker>();
                threatFlicker.Configure(run.Lighting,light,lantern.GetComponentsInChildren<Renderer>(),cell*.73f);
                AddDetail(cell,"lantern-hardware",center,Quaternion.identity,Vector3.one,false);
                var housing=GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(renderer=>renderer.name=="Lamp housing" &&
                    Vector3.Distance(renderer.transform.position,center)<.01f);
                Hide(housing);
            }
        }

        void DressSlidingLeaves()
        {
            foreach(var door in GetComponentsInChildren<Interactable>(true).Where(item=>item.kind==Interactable.Kind.Door))
            {
                if(!door.movingLeaf) continue;
                var root=new GameObject("Sliding timber-paper leaf dressing").transform;
                root.SetParent(door.movingLeaf,false);externalVisualRoots.Add(root);
                root.localScale=new Vector3(1/door.movingLeaf.localScale.x,1/door.movingLeaf.localScale.y,1/door.movingLeaf.localScale.z);
                Hide(door.movingLeaf.GetComponent<MeshRenderer>());
                EmitDynamicArchitecture("door-leaf",root,NearestCell(door.transform.position));
                var hardware=CorridorDoorHardware.Attach(door,door.movingLeaf,2.6f,2.36f,.12f);
                externalVisualRoots.Add(hardware.transform);
            }
        }

        void DressShrines()
        {
            var memories=GetComponentsInChildren<Interactable>(true).Where(item=>item.kind==Interactable.Kind.CorridorMemory)
                .OrderBy(item=>item.stableId,StringComparer.Ordinal).ToArray();
            foreach(var item in memories)
            {
                int cell=NearestCell(item.transform.position);Vector3 center=run.CellPosition(cell);center.y=.03f;
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
                EmitDynamicDetail("memory-seal",root,cell,false);
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
            var exit=GetComponentsInChildren<Interactable>(true).Single(item=>item.name=="Sealed entrance");
            Vector3 center=exit.transform.position;
            Quaternion basis=Quaternion.Euler(0,90,0);
            AddDetail(0,"entrance-panel",center,basis,Vector3.one);
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
            if(run.Layout.Version>=3) { returnClue.text="돌아갈 수 없는 입구\n기억은 붉은 교실로"; return; }
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
