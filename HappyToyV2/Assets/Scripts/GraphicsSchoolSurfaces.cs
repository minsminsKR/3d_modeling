using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // School presentation owns render replacements, never scene geometry or
    // source assets. ChapterAtmosphere calls Refresh after adding its dressing.
    [DisallowMultipleComponent]
    public sealed class GraphicsSchoolSurfaces : MonoBehaviour
    {
        sealed class State
        {
            public MeshRenderer renderer;
            public MeshFilter filter;
            public Mesh originalMesh,appliedMesh;
            public Material[] originalMaterials,appliedMaterials;
            public MaterialPropertyBlock originalBlock;
            public MaterialPropertyBlock appliedBlock;
            public bool enabled;
            public bool hiddenDoor;
        }
        readonly Dictionary<MeshRenderer,State> states=new Dictionary<MeshRenderer,State>();
        readonly List<GameObject> batches=new List<GameObject>();
        readonly List<Mesh> batchMeshes=new List<Mesh>();
        readonly List<MeshRenderer> batchedRenderers=new List<MeshRenderer>();
        readonly List<GameObject> details=new List<GameObject>();
        readonly HashSet<EntityId> dressedDoors=new HashSet<EntityId>();
        GraphicsSurfaceLibrary.Pool pool;
        GameSession session;
        public int SurfaceCount { get; private set; }
        public int BevelledBoxes { get; private set; }
        public int ImportedSurfaces { get; private set; }
        public int StaticBatches => batches.Count;
        public int MaterialCount => pool==null?0:pool.MaterialCount;
        public int DoorJoineryCount { get; private set; }
        public int ContactRegions { get; private set; }

        public void Prepare(GameSession source)
        {
            if(session&&session!=source)throw new InvalidOperationException("School surface owner changed");
            if(!source)throw new ArgumentNullException(nameof(source));
            session=source;Refresh();
        }
        static string Context(Transform item)
        {
            string context="";
            for(int depth=0;item&&depth<5;depth++,item=item.parent)context+=" "+item.name.ToLowerInvariant();
            return context.Replace('_',' ');
        }
        static bool Excluded(MeshRenderer renderer)
        {
            if(renderer.GetComponent<TextMesh>()||renderer.GetComponentInParent<StalkerBrain>(true)||
                renderer.GetComponentInParent<LanternMaskEncounter>(true)||renderer.GetComponentInParent<WeepingAngelEncounter>(true)||
                renderer.GetComponentInParent<PlayerMotor>(true)||renderer.GetComponentInParent<HauntedCorridorPresentation>(true))return true;
            // Original painted evidence, blood film, water and readable signage
            // remain their authored image/shader; these are not flat building slabs.
            string name=Context(renderer.transform);
            return name.Contains("painting")||name.Contains("portrait image")||name.Contains("blood")||name.Contains("standing water")||
                name.Contains("name slip")||name.Contains("drawing")||name.Contains("graphic upgrade")||renderer.bounds.center.x>100;
        }
        static string Key(MeshRenderer renderer,Material material)
        {
            if(!material||!material.shader)return "";
            string shader=material.shader.name;
            if(shader!="Universal Render Pipeline/Lit"&&shader!="Standard")return "";
            if(material.renderQueue>=3000||material.HasProperty("_Surface")&&material.GetFloat("_Surface")>.5f)return "";
            string name=renderer.name.ToLowerInvariant().Replace('_',' '),context=Context(renderer.transform);
            string slot=material.name.ToLowerInvariant().Replace('_',' ');
            string existing=GraphicsSurfaceLibrary.SurfaceKey(material);
            if(existing.Length>0)return existing;
            bool furniture=context.Contains("chair")||context.Contains("desk")||context.Contains("piano")||context.Contains("bench")||
                context.Contains("stand")||context.Contains("cabinet")||context.Contains("cot")||context.Contains("bed");
            if(!furniture)
            {
                if((name.Contains("floor")&&!name.Contains("wall")&&!name.Contains("side")&&!name.Contains("back"))||name.Contains("stair tread"))
                    return context.Contains("washroom")||slot.Contains("grout")||slot.Contains("ceramic")?"ceramic-tile":
                        renderer.bounds.center.y< -1?"concrete-rough":"wood-floor";
                if(name.Contains("ceiling"))return "concrete-rough";
                if(name.Contains("wall")||name.Contains("partition")||name.EndsWith("end")||name.EndsWith("side")||name.EndsWith("back")||name.Contains("door surround"))
                    return name.Contains("tile")?"ceramic-tile":renderer.bounds.center.y< -1?"concrete-rough":"plaster-damp";
            }
            if(slot.Contains("brass")||slot.Contains("copper"))return "brass-tarnished";
            if(slot.Contains("enamel")||slot.Contains("paint")||name.Contains("window rail")||name.Contains("mullion"))return "painted-metal";
            if(slot.Contains("iron")||slot.Contains("steel")||slot.Contains("metal")||slot.Contains("chrome")||name.Contains("pipe")||name.Contains("pedal"))return "metal-rust";
            if(slot.Contains("wood")||slot.Contains("oak")||slot.Contains("timber")||slot.Contains("plywood")||name.Contains("leaf")||name.Contains("skirting")||name.Contains("frame"))return "wood-aged";
            if(slot.Contains("paper")||name.Contains("score"))return "paper-aged";
            if(slot.Contains("ivory")||slot.Contains("rubber")||slot.Contains("plastic"))return "wax-tallow";
            return ""; // keep genuine imported textures/materials outside the named surface families
        }
        static Color Tint(string key,Material original)
        {
            Color source=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.color;
            if(key=="wood-floor"||key=="wood-aged"||key=="plaster-damp"||key=="concrete-rough"||key=="ceramic-tile")
                return Color.Lerp(Color.white,source,.18f); // a scan carries its own real albedo
            return key=="paper-aged"?new Color(.96f,.93f,.85f):source;
        }
        public void Refresh()
        {
            if(!session||!isActiveAndEnabled)return;
            ClearBatches();
            if(pool==null||pool.Disposed)pool=new GraphicsSurfaceLibrary.Pool();
            var renderers=gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            foreach(var renderer in renderers)
            {
                if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy||Excluded(renderer))continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                // Other upgraded prop owners retain their exact metre-authored UV
                // and cached materials; they are already physically mapped.
                bool sourcePrimitive=filter.sharedMesh.vertexCount==24&&Vector3.Distance(filter.sharedMesh.bounds.size,Vector3.one)<.002f;
                if(!states.ContainsKey(renderer)&&renderer.sharedMaterials.All(m=>GraphicsSurfaceLibrary.SurfaceKey(m).Length>0)&&
                    (!sourcePrimitive||filter.sharedMesh.name.StartsWith("Graphics ")||Context(renderer.transform).Contains("graphics prop")))continue;
                if(!states.TryGetValue(renderer,out var state))
                {
                    var originals=renderer.sharedMaterials;
                    if(!originals.Any(m=>Key(renderer,m).Length>0))continue;
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    state=new State{renderer=renderer,filter=filter,originalMesh=filter.sharedMesh,originalMaterials=originals,
                        originalBlock=block,enabled=renderer.enabled};states.Add(renderer,state);
                }
                var applied=(Material[])state.originalMaterials.Clone();
                string primary="";
                for(int i=0;i<applied.Length;i++)
                {
                    string key=Key(renderer,state.originalMaterials[i]);if(key.Length==0)continue;
                    if(primary.Length==0)primary=key;
                    applied[i]=pool.Get(key,Tint(key,state.originalMaterials[i]));
                }
                if(primary.Length==0)continue;
                renderer.sharedMaterials=applied;state.appliedMaterials=applied;
                renderer.SetPropertyBlock(null); // discard old anisotropic per-cube tiling
                if(!state.appliedMesh)
                {
                    var source=state.originalMesh;
                    bool primitive=source.vertexCount==24&&Vector3.Distance(source.bounds.size,Vector3.one)<.002f;
                    if(primitive)
                    {
                        Vector3 size=renderer.transform.lossyScale;size=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
                        float bevel=primary=="wood-floor"||renderer.name.ToLowerInvariant().Contains("floor")?0:
                            Mathf.Min(.004f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.12f);
                        var box=pool.MetreBoxMesh(size,bevel,GraphicsSurfaceLibrary.TileSpan(primary));
                        state.appliedMesh=pool.MetreMappedMesh(box,renderer.transform,GraphicsSurfaceLibrary.TileSpan(primary));BevelledBoxes++;
                    }
                    else if(source.isReadable)
                    {state.appliedMesh=pool.MetreMappedMesh(source,renderer.transform,GraphicsSurfaceLibrary.TileSpan(primary));ImportedSurfaces++;}
                    else
                    {
                        // Retain original imported geometry and its authored UV.
                        // Uniform measured scale avoids random/anisotropic UV stretching.
                        float physical=Mathf.Max(renderer.bounds.size.x,Mathf.Max(renderer.bounds.size.y,renderer.bounds.size.z));
                        float repeat=physical/GraphicsSurfaceLibrary.TileSpan(primary);
                        var block=new MaterialPropertyBlock();block.SetVector("_BaseMap_ST",new Vector4(repeat,repeat,0,0));state.appliedBlock=block;
                        state.appliedMesh=source;ImportedSurfaces++;
                    }
                }
                filter.sharedMesh=state.appliedMesh;
                if(state.appliedBlock!=null)renderer.SetPropertyBlock(state.appliedBlock);
            }
            SurfaceCount=states.Count;
            DressDoors();BuildContactWear();CombineArchitecture();
        }
        void DressDoors()
        {
            foreach(var door in gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Interactable>(true)))
            {
                if(!door.gameObject.activeInHierarchy||door.kind!=Interactable.Kind.Door||!door.movingLeaf||door.transform.position.x>100||!dressedDoors.Add(door.GetEntityId()))continue;
                foreach(var leaf in new[]{door.movingLeaf,door.secondaryLeaf})
                {
                    if(!leaf)continue;
                    var filter=leaf.GetComponent<MeshFilter>();if(!filter||filter.sharedMesh.bounds.size.x>1.01f)continue;
                    Vector3 size=leaf.lossyScale;size=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
                    var root=new GameObject("Graphics school recessed joinery");root.transform.SetParent(leaf,false);
                    root.transform.localScale=new Vector3(1/size.x,1/size.y,1/size.z);details.Add(root);
                    var geometry=new GraphicsSurfaceLibrary.Geometry(Vector3.zero,GraphicsSurfaceLibrary.TileSpan("wood-aged"));
                    // Recess the backing inside the unchanged physical leaf so
                    // its joined stiles remain visible without a protruding slab.
                    geometry.Box(Vector3.zero,new Vector3(size.x,size.y,size.z-.012f),Quaternion.identity,.001f);
                    foreach(float face in new[]{-1f,1f})
                    {
                        foreach(float x in new[]{-1f,1f})geometry.Box(new Vector3(x*(size.x*.5f-.055f),0,face*(size.z*.5f-.003f)),
                            new Vector3(.045f,size.y-.035f,.004f),Quaternion.identity,.001f);
                        foreach(float y in new[]{-.38f,.39f})geometry.Box(new Vector3(0,y*size.y,face*(size.z*.5f-.003f)),
                            new Vector3(size.x-.065f,.055f,.004f),Quaternion.identity,.001f);
                    }
                    var mesh=geometry.Mesh("Graphics school door fine joinery");batchMeshes.Add(mesh);
                    root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=pool.Get("wood-aged",new Color(.89f,.83f,.73f));
                    var sourceRenderer=leaf.GetComponent<MeshRenderer>();
                    if(sourceRenderer&&states.TryGetValue(sourceRenderer,out var state))
                    {sourceRenderer.enabled=false;state.hiddenDoor=true;}
                    DoorJoineryCount++;
                }
            }
        }
        void BuildContactWear()
        {
            // One persistent coherent base region per structural wall. Existing
            // contact/edge geometry remains within the opaque wall footprint.
            foreach(var state in states.Values)
            {
                var renderer=state.renderer;if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy||!renderer.name.ToLowerInvariant().Contains("wall"))continue;
                string name="Graphics contact wear "+renderer.GetEntityId().ToString();
                if(details.Any(item=>item&&item.name==name))continue;
                var bounds=renderer.bounds;if(bounds.size.y<1.8f)continue;
                bool alongX=bounds.size.x>bounds.size.z;float width=alongX?bounds.size.x:bounds.size.z;
                if(width<.8f)continue;
                string key=GraphicsSurfaceLibrary.SurfaceKey(renderer.sharedMaterial);if(key!="plaster-damp"&&key!="concrete-rough")continue;
                var root=new GameObject(name);root.transform.SetParent(transform,false);details.Add(root);
                var draft=new GraphicsSurfaceLibrary.Geometry(Vector3.zero,GraphicsSurfaceLibrary.TileSpan(key));
                float floor=bounds.min.y,region=renderer.transform.position.y<0?.23f:.12f;
                int segments=Mathf.Clamp(Mathf.CeilToInt(width/.65f),2,30);
                for(int face=-1;face<=1;face+=2)for(int part=0;part<segments;part++)
                {
                    float start=-width*.49f+width*.98f*part/segments,end=-width*.49f+width*.98f*(part+1)/segments;
                    float ha=region*(.72f+.22f*Mathf.Sin(part*.91f+bounds.center.x*.18f));
                    float hb=region*(.72f+.22f*Mathf.Sin((part+1)*.91f+bounds.center.x*.18f));
                    Func<float,float,Vector3> point=(x,y)=>alongX?new Vector3(bounds.center.x+x,y,bounds.center.z+face*(bounds.extents.z+.0007f)):
                        new Vector3(bounds.center.x+face*(bounds.extents.x+.0007f),y,bounds.center.z+x);
                    var a=point(start,floor+.015f);var b=point(end,floor+.015f);var c=point(end,floor+hb);var d=point(start,floor+ha);
                    draft.Quad(a,b,c,d);draft.Quad(b,a,d,c);
                }
                var mesh=draft.Mesh(name);batchMeshes.Add(mesh);root.AddComponent<MeshFilter>().sharedMesh=mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial=pool.Get(key,new Color(.86f,.86f,.82f));ContactRegions++;
            }
        }
        static bool Architecture(State state)
        {
            if(!state.renderer||!state.renderer.enabled||!state.renderer.gameObject.activeInHierarchy||state.renderer.GetComponentInParent<Interactable>())return false;
            string name=state.renderer.name.ToLowerInvariant();
            return name.Contains("floor")||name.Contains("wall")||name.Contains("ceiling")||name.Contains("partition")||
                name.EndsWith("end")||name.EndsWith("side")||name.EndsWith("back")||name.Contains("stair tread");
        }
        void CombineArchitecture()
        {
            var groups=new Dictionary<string,List<State>>();
            foreach(var state in states.Values.Where(Architecture))
            {
                // Imported GPU-only source meshes retain their original renderer.
                // A failed CPU combine must never make that source geometry vanish.
                if(state.appliedMaterials.Length!=1||!state.appliedMesh||!state.appliedMesh.isReadable)continue;
                var at=state.renderer.bounds.center;
                string id=state.appliedMaterials[0].GetEntityId().ToString()+":"+Mathf.FloorToInt(at.x/6)+":"+
                    Mathf.FloorToInt((state.renderer.bounds.min.y+.3f)/5)+":"+Mathf.FloorToInt(at.z/6);
                if(!groups.TryGetValue(id,out var group)){group=new List<State>();groups.Add(id,group);}group.Add(state);
            }
            foreach(var group in groups.Values)
            {
                var mesh=new Mesh{name="Graphics school spatial architecture",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(group.Select(state=>new CombineInstance{mesh=state.appliedMesh,
                    transform=transform.worldToLocalMatrix*state.filter.transform.localToWorldMatrix}).ToArray(),true,true);
                var root=new GameObject("Graphics school architecture batch");root.transform.SetParent(transform,false);batches.Add(root);batchMeshes.Add(mesh);
                root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=group[0].appliedMaterials[0];renderer.receiveShadows=true;
                foreach(var state in group){state.renderer.enabled=false;batchedRenderers.Add(state.renderer);}
            }
        }
        void ClearBatches()
        {
            foreach(var renderer in batchedRenderers)if(renderer&&states.TryGetValue(renderer,out var state))renderer.enabled=state.enabled;
            foreach(var batch in batches)if(batch)GraphicsSurfaceLibrary.DestroyOwned(batch);
            // Persistent contact/joinery meshes are owned until teardown, not a refresh.
            foreach(var mesh in batchMeshes.Where(mesh=>mesh&&mesh.name=="Graphics school spatial architecture"))GraphicsSurfaceLibrary.DestroyOwned(mesh);
            batchMeshes.RemoveAll(mesh=>!mesh||mesh.name=="Graphics school spatial architecture");batches.Clear();batchedRenderers.Clear();
        }
        void OnEnable(){if(session)Refresh();}
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
        void Release()
        {
            ClearBatches();
            foreach(var state in states.Values)
            {
                if(!state.renderer)continue;
                if(state.renderer.sharedMaterials.SequenceEqual(state.appliedMaterials??Array.Empty<Material>()))
                {state.renderer.sharedMaterials=state.originalMaterials;state.renderer.SetPropertyBlock(state.originalBlock.isEmpty?null:state.originalBlock);}
                if(state.filter&&state.filter.sharedMesh==state.appliedMesh)state.filter.sharedMesh=state.originalMesh;
                if(state.hiddenDoor)state.renderer.enabled=state.enabled;
            }
            foreach(var detail in details)if(detail)GraphicsSurfaceLibrary.DestroyOwned(detail);
            foreach(var mesh in batchMeshes)if(mesh)GraphicsSurfaceLibrary.DestroyOwned(mesh);
            states.Clear();details.Clear();batchMeshes.Clear();dressedDoors.Clear();pool?.Dispose();pool=null;
            SurfaceCount=BevelledBoxes=ImportedSurfaces=DoorJoineryCount=ContactRegions=0;
        }
    }
}
