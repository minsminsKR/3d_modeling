using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Red light belongs to the original eye surface. No foreground discs,
    // substitute eyeballs, additional renderers, colliders or lights are made.
    [DisallowMultipleComponent]
    public sealed class MonsterRedEyes : MonoBehaviour
    {
        [Serializable] sealed class Profile
        { public int version; public string key; public EyeProfile[] eyes; }
        [Serializable] sealed class EyeProfile
        {
            public Vector2 uv;
            public int materialSlot;
            public string sourceMesh;
            public bool aperture;
            public Vector2[] uvBoundarySamples;
        }
        sealed class SlotState
        { public int index; public Material original,applied; public MaterialPropertyBlock originalBlock; }
        sealed class RendererState
        { public Renderer renderer; public readonly List<SlotState> slots=new List<SlotState>(); }
        struct Surface
        { public Renderer renderer; public Vector3 point,normal; public int slot; public float distance; }
        sealed class PosedSurface
        {
            public Renderer renderer;
            public Vector3[] vertices,normals;
            public Vector2[] uv;
            public int[][] triangles;
        }
        sealed class EyeShape
        { public Transform anchor; public Vector3 horizontal,vertical; }
        readonly List<RendererState> states=new List<RendererState>();
        readonly List<Transform> anchors=new List<Transform>();
        readonly List<EyeShape> shapes=new List<EyeShape>();
        readonly List<Renderer> emissionRenderers=new List<Renderer>();
        readonly List<Material> owned=new List<Material>();
        MaterialPropertyBlock block;
        static readonly int Emission=Shader.PropertyToID("_EmissionColor");
        // Strong enough to register at distance, bounded so original sclera,
        // iris shading and the un-emissive pupil survive a close torch view.
        static readonly Color Glow=new Color(1.1f,.012f,.008f);
        float phase;
        bool emissionEnabled=true;
        // A reference-authored mask can keep its actual black eye slits while
        // sharing the same material/anchor lifetime owner as the other faces.
        [SerializeField] bool authoredEmissionAllowed=true;
        public bool Prepared {get;private set;}
        public Transform Head {get;private set;}
        public string ProfileKey {get;private set;}
        public bool EmissionEnabled=>emissionEnabled&&authoredEmissionAllowed;
        public IReadOnlyList<Transform> EyeAnchors=>anchors;
        public IReadOnlyList<Renderer> EmissionRenderers=>emissionRenderers;
        public IReadOnlyList<float> EyeRadii=>shapes.Select(s=>Mathf.Max(
            Head.TransformVector(s.horizontal).magnitude,Head.TransformVector(s.vertical).magnitude)).ToArray();
        void Awake()=>block=new MaterialPropertyBlock();

        public static MonsterRedEyes Attach(Transform model,string key)=>Prepare(model,key,true);
        public static MonsterRedEyes AttachStatic(Transform model,string key)=>Prepare(model,key,false);
        // An authored replacement mask supplies its real recessed eye surfaces.
        // Keep the same inspection/emission owner, restoring the old face slots
        // before binding these meshes; other actors retain their original profiles.
        public void BindAuthoredStaticEyes(Renderer[] eyeSurfaces,bool allowRedEmission=true)
        {
            if(eyeSurfaces==null||eyeSurfaces.Length!=2||eyeSurfaces.Any(r=>!r)||eyeSurfaces.Distinct().Count()!=2)
                throw new ArgumentException("The authored mask must supply two real eye surfaces");
            var nextHead=Head?Head:transform.parent;
            if(!nextHead)throw new InvalidOperationException("Authored mask head pivot missing");
            foreach(var renderer in eyeSurfaces)
            {
                var filter=renderer.GetComponent<MeshFilter>();var materials=renderer.sharedMaterials;
                float radius=Mathf.Max(renderer.bounds.extents.x,renderer.bounds.extents.y,renderer.bounds.extents.z);
                if(!(renderer is MeshRenderer)||!filter||!filter.sharedMesh||!renderer.transform.IsChildOf(nextHead)||
                    materials.Length!=1||!materials[0]||!materials[0].HasProperty("_EmissionMap")||
                    !materials[0].HasProperty("_EmissionColor")||radius<.00001f||!StealthRules.Finite(radius))
                    throw new ArgumentException("Recessed eyes require valid static surfaces beneath the mask pivot");
            }
            RestoreOriginalSlots();
            foreach(var material in owned)if(material)GraphicsSurfaceLibrary.DestroyOwned(material);
            owned.Clear();states.Clear();emissionRenderers.Clear();shapes.Clear();
            Head=nextHead;
            authoredEmissionAllowed=allowRedEmission;
            if(block==null)block=new MaterialPropertyBlock();
            for(int i=0;i<2;i++)
            {
                var renderer=eyeSurfaces[i];var materials=renderer.sharedMaterials;
                if(materials.Length!=1)throw new InvalidOperationException("A recessed eye must have its own single material surface");
                Mount(renderer,0,allowRedEmission?Texture2D.whiteTexture:Texture2D.blackTexture);
                Transform anchor;
                if(i<anchors.Count&&anchors[i])anchor=anchors[i];
                else
                {
                    var name=i==0?"Left original eye":"Right original eye";
                    anchor=transform.Find(name);
                    if(!anchor){anchor=new GameObject(name).transform;anchor.SetParent(transform,false);}
                    if(i<anchors.Count)anchors[i]=anchor;else anchors.Add(anchor);
                }
                anchor.position=renderer.bounds.center;anchor.rotation=renderer.transform.rotation;
                float radius=Mathf.Max(renderer.bounds.extents.x,renderer.bounds.extents.y,renderer.bounds.extents.z);
                if(radius<.00001f||!StealthRules.Finite(radius))throw new InvalidOperationException("Empty recessed eye surface");
                shapes.Add(new EyeShape{anchor=anchor,horizontal=Head.InverseTransformVector(anchor.right*radius),
                    vertical=Head.InverseTransformVector(anchor.up*radius)});
            }
            ProfileKey="LanternMask";Prepared=true;ApplyEmission();
        }
        static MonsterRedEyes Prepare(Transform model,string key,bool skinned)
        {
            if(!model)throw new ArgumentNullException(nameof(model));
            var prior=model.GetComponentInChildren<MonsterRedEyes>(true);if(prior)return prior;
            var data=Resources.Load<TextAsset>("ThreatEyes/"+key+"-profile");
            var mask=Resources.Load<Texture2D>("ThreatEyes/"+key+"-emission");
            if(!data||!mask)throw new InvalidOperationException("Missing original-eye emission authoring: "+key+" profile="+(bool)data+" mask="+(bool)mask);
            var profile=JsonUtility.FromJson<Profile>(data.text);
            int expected=key=="Cyclopse"?1:2;
            if(profile==null||profile.version!=2||profile.key!=key||profile.eyes==null||profile.eyes.Length!=expected)
                throw new InvalidOperationException("Eye anatomy profile mismatch: "+key);
            var head=skinned?model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="mixamorig:Head"):model;
            if(!head)throw new InvalidOperationException("Original animated head missing: "+key);
            var posed=CaptureSurfaces(model,skinned);
            var surfaces=new Surface[expected];
            var boundaries=new Vector3[expected][];
            for(int i=0;i<expected;i++)
            {
                var eye=profile.eyes[i];
                if(eye.materialSlot<0||eye.uvBoundarySamples==null||eye.uvBoundarySamples.Length<4)
                    throw new InvalidOperationException("Eye surface boundary missing: "+key);
                surfaces[i]=Resolve(posed,head,eye,eye.aperture?eye.uvBoundarySamples[0]:eye.uv);
                var lips=eye.uvBoundarySamples.Select(uv=>Resolve(posed,head,eye,uv)).ToArray();
                boundaries[i]=lips.Select(surface=>surface.point).ToArray();
                if(eye.aperture)
                {
                    // The mask centre is empty space. Only its real porcelain
                    // lips carry emission; this empty anchor is for inspection.
                    var bounds=new Bounds(boundaries[i][0],Vector3.zero);
                    foreach(var p in boundaries[i])bounds.Encapsulate(p);
                    surfaces[i].point=bounds.center;
                    surfaces[i].normal=lips.Aggregate(Vector3.zero,(sum,surface)=>sum+surface.normal).normalized;
                }
            }
            var root=new GameObject("Original eye surface glow");root.transform.SetParent(head,false);
            var art=root.AddComponent<MonsterRedEyes>();art.Head=head;art.ProfileKey=key;
            art.phase=key=="Baby"?2.1f:key=="Uncat"?1.3f:key=="Cyclopse"?.4f:3.6f;
            try
            {
                for(int i=0;i<expected;i++)
                {
                    var surface=surfaces[i];
                    art.Mount(surface.renderer,surface.slot,mask);
                    var anchor=new GameObject(expected==1?"Central original eye":i==0?"Left original eye":"Right original eye").transform;
                    anchor.SetParent(root.transform,false);anchor.position=surface.point;
                    var normal=surface.normal.normalized;
                    var up=Vector3.ProjectOnPlane(head.up,normal);
                    if(up.sqrMagnitude<.001f)up=Vector3.ProjectOnPlane(Vector3.up,normal);
                    anchor.rotation=Quaternion.LookRotation(normal,up.normalized);
                    float radius=boundaries[i].Max(p=>Vector3.Distance(p,surface.point));
                    if(radius<=.00001f||float.IsNaN(radius)||float.IsInfinity(radius))throw new InvalidOperationException("Degenerate eye extent: "+key);
                    art.anchors.Add(anchor);art.shapes.Add(new EyeShape {anchor=anchor,
                        horizontal=head.InverseTransformVector(anchor.right*radius),vertical=head.InverseTransformVector(anchor.up*radius)});
                }
                art.Prepared=true;art.SetEmissionEnabled(true);return art;
            }
            catch {GraphicsSurfaceLibrary.DestroyOwned(root);throw;}
        }

        static List<PosedSurface> CaptureSurfaces(Transform model,bool skinned)
        {
            var posed=new List<PosedSurface>();
            var scratch=skinned?new Mesh():null;
            try
            {
                if(skinned)
                {
                    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if(!skin.enabled||!skin.sharedMesh)continue;
                        skin.BakeMesh(scratch,true);posed.Add(Capture(skin,scratch));scratch.Clear();
                    }
                }
                else foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(!renderer||!renderer.enabled||!filter.sharedMesh)continue;
                    if(!filter.sharedMesh.isReadable)throw new InvalidOperationException("Static eye mesh requires the authored readable importer");
                    posed.Add(Capture(renderer,filter.sharedMesh));
                }
            }
            finally {if(scratch)GraphicsSurfaceLibrary.DestroyOwned(scratch);}
            return posed;
        }
        static PosedSurface Capture(Renderer renderer,Mesh mesh)
        {
            var snapshot=new PosedSurface {renderer=renderer,vertices=mesh.vertices,normals=mesh.normals,uv=mesh.uv,
                triangles=Enumerable.Range(0,mesh.subMeshCount).Select(mesh.GetTriangles).ToArray()};
            if(snapshot.uv.Length!=snapshot.vertices.Length||snapshot.normals.Length!=snapshot.vertices.Length)
                throw new InvalidOperationException("Incomplete original eye UVs/normals");
            return snapshot;
        }
        static Surface Resolve(List<PosedSurface> posed,Transform head,EyeProfile eye,Vector2 uv)
        {
            var result=new Surface {distance=float.PositiveInfinity};
            foreach(var surface in posed)
                if(eye.materialSlot<surface.triangles.Length)FindSurface(surface,eye.materialSlot,uv,head.position,ref result);
            if(!result.renderer||float.IsInfinity(result.distance))throw new InvalidOperationException("No original eye surface at "+uv);
            return result;
        }
        static void FindSurface(PosedSurface surface,int slot,Vector2 at,Vector3 head,ref Surface result)
        {
            var renderer=surface.renderer;var vertices=surface.vertices;var uv=surface.uv;var normals=surface.normals;var triangles=surface.triangles[slot];
            var transform=renderer.transform;var normalMatrix=transform.worldToLocalMatrix.transpose;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                var ab=uv[b]-uv[a];var ac=uv[c]-uv[a];var ap=at-uv[a];
                float determinant=ab.x*ac.y-ac.x*ab.y;if(Mathf.Abs(determinant)<1e-10f)continue;
                float v=(ap.x*ac.y-ac.x*ap.y)/determinant,w=(ab.x*ap.y-ap.x*ab.y)/determinant,u=1-v-w;
                if(u<-.00001f||v<-.00001f||w<-.00001f)continue;
                var point=transform.TransformPoint(vertices[a]*u+vertices[b]*v+vertices[c]*w);
                float distance=(point-head).sqrMagnitude;if(distance>=result.distance)continue;
                var normal=normalMatrix.MultiplyVector(normals[a]*u+normals[b]*v+normals[c]*w).normalized;
                result=new Surface {renderer=renderer,point=point,normal=normal,slot=slot,distance=distance};
            }
        }
        void Mount(Renderer renderer,int slot,Texture2D mask)
        {
            var state=states.FirstOrDefault(s=>s.renderer==renderer);
            if(state==null){state=new RendererState {renderer=renderer};states.Add(state);emissionRenderers.Add(renderer);}
            if(state.slots.Any(s=>s.index==slot))return;
            var assigned=renderer.sharedMaterials;
            if(slot>=assigned.Length||!assigned[slot])throw new InvalidOperationException("Original eye material slot is absent");
            var original=assigned[slot];
            if(!original.HasProperty("_EmissionMap")||!original.HasProperty("_EmissionColor"))throw new InvalidOperationException("Original eye shader cannot carry surface emission: "+original.shader.name);
            var originalBlock=new MaterialPropertyBlock();renderer.GetPropertyBlock(originalBlock,slot);
            var material=new Material(original){name=original.name+" — original eye surface emission"};
            material.SetTexture("_EmissionMap",mask);material.SetColor("_EmissionColor",authoredEmissionAllowed?Glow:Color.black);
            if(authoredEmissionAllowed)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags=(original.globalIlluminationFlags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack) | MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            owned.Add(material);assigned[slot]=material;renderer.sharedMaterials=assigned;
            state.slots.Add(new SlotState {index=slot,original=original,applied=material,originalBlock=originalBlock});
        }
        public int[] GetAffectedSlots(Renderer renderer)=>states.FirstOrDefault(s=>s.renderer==renderer)?.slots.Select(s=>s.index).ToArray()??Array.Empty<int>();
        public Material GetOriginalMaterial(Renderer renderer,int slot)=>states.FirstOrDefault(s=>s.renderer==renderer)?.slots.FirstOrDefault(s=>s.index==slot)?.original;
        public Color GetEmissionColor(Renderer renderer,int slot)=>authoredEmissionAllowed&&GetAffectedSlots(renderer).Contains(slot)?Glow:Color.black;
        public void SetEmissionEnabled(bool value)
        {emissionEnabled=value;if(Prepared)ApplyEmission();}
        void ApplyEmission()
        {
            float breath=.97f+.03f*Mathf.Sin(Time.time*2.15f+phase);
            foreach(var state in states)
            {
                if(!state.renderer)continue;
                var current=state.renderer.sharedMaterials;
                foreach(var slot in state.slots)
                {
                    if(slot.index>=current.Length||current[slot.index]!=slot.applied)continue;
                    state.renderer.GetPropertyBlock(block,slot.index);
                    block.SetColor(Emission,EmissionEnabled?Glow*breath:Color.black);
                    state.renderer.SetPropertyBlock(block,slot.index);
                }
            }
        }
        void LateUpdate(){if(Prepared)ApplyEmission();}
        void OnEnable()
        {
            if(!Prepared)return;
            foreach(var state in states)
            {
                if(!state.renderer)continue;var current=state.renderer.sharedMaterials;
                foreach(var slot in state.slots)
                    if(slot.index<current.Length&&current[slot.index]==slot.original)current[slot.index]=slot.applied;
                state.renderer.sharedMaterials=current;
            }
            ApplyEmission();
        }
        void OnDisable()=>RestoreOriginalSlots();
        void RestoreOriginalSlots()
        {
            foreach(var state in states)
            {
                if(!state.renderer)continue;var current=state.renderer.sharedMaterials;
                foreach(var slot in state.slots)
                    if(slot.index<current.Length&&current[slot.index]==slot.applied)
                    {current[slot.index]=slot.original;state.renderer.SetPropertyBlock(slot.originalBlock,slot.index);}
                state.renderer.sharedMaterials=current;
            }
        }
        void OnDestroy()
        {
            RestoreOriginalSlots();
            foreach(var material in owned)if(material)GraphicsSurfaceLibrary.DestroyOwned(material);
        }
    }
}
