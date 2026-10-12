using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    public sealed partial class SchoolCampusLayout
    {
        const float WallHeight = 3.2f;
        Material Plaster(int floor) => surfaces.Get(floor < 0 ? "concrete-rough" : "plaster-damp", floor < 0 ? new Color(.65f,.72f,.67f) : new Color(.83f,.85f,.78f));
        Material Timber => surfaces.Get("wood-aged", new Color(.60f,.56f,.46f));
        Material Iron => surfaces.Get("metal-rust", new Color(.65f,.69f,.66f));
        GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool physical = false, bool walkable = false, Transform parent = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent ? parent : Root, false); go.transform.position = position; go.transform.localScale = size;
            var mesh = surfaces.MetreBoxMesh(size, walkable ? 0 : Mathf.Min(.006f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .12f), GraphicsSurfaceLibrary.TileSpan(material));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            if (physical)
            {
                go.AddComponent<BoxCollider>();
                if (!walkable) { var modifier = go.AddComponent<NavMeshModifier>(); modifier.overrideArea = true; modifier.area = 1; }
            }
            return go;
        }
        void BuildArchitecture()
        {
            foreach (var space in spaces.Where(s=>s.room))
            {
                var r = space.rect; var c = space.Centre;
                var floorKey = space.floor < 0 ? "concrete-rough" : space.room ? "wood-floor" : "ceramic-tile";
                Slab(space.id + " school floor", c + Vector3.down * .12f, new Vector3(r.width,.24f,r.height),
                    surfaces.Get(floorKey, space.floor < 0 ? new Color(.68f,.75f,.70f) : Color.white), true, true);
                Slab(space.id + " stained ceiling", c + Vector3.up * 3.3f, new Vector3(r.width,.2f,r.height), Plaster(space.floor), true);
            }
            // Tessellate the hallway union exactly once. Intersections have no
            // overlapping/coplanar floor or ceiling and share world-space UVs.
            foreach(int floor in new[]{-1,0,1})
            {
                var halls=spaces.Where(s=>s.floor==floor && !s.room).ToArray();
                var xs=halls.SelectMany(s=>new[]{s.rect.xMin,s.rect.xMax}).Distinct().OrderBy(x=>x).ToArray();
                var zs=halls.SelectMany(s=>new[]{s.rect.yMin,s.rect.yMax}).Distinct().OrderBy(z=>z).ToArray();
                for(int x=0;x<xs.Length-1;x++)for(int z=0;z<zs.Length-1;z++)
                {
                    var centre=new Vector2((xs[x]+xs[x+1])*.5f,(zs[z]+zs[z+1])*.5f);
                    if(!halls.Any(s=>s.rect.Contains(centre)))continue;
                    var c=new Vector3(centre.x,floor*5,centre.y);var size=new Vector3(xs[x+1]-xs[x],.24f,zs[z+1]-zs[z]);
                    Slab("Campus continuous corridor floor",c+Vector3.down*.12f,size,
                        surfaces.Get(floor<0?"concrete-rough":"ceramic-tile",floor<0?new Color(.68f,.75f,.70f):Color.white),true,true);
                    size.y=.2f;Slab("Campus continuous corridor ceiling",c+Vector3.up*3.3f,size,Plaster(floor),true);
                }
            }
            var keys = new HashSet<string>();
            foreach (var s in spaces)
            {
                Edge(s, false, s.rect.xMin, s.rect.yMin, s.rect.yMax, -1, keys);
                Edge(s, false, s.rect.xMax, s.rect.yMin, s.rect.yMax, 1, keys);
                Edge(s, true, s.rect.yMin, s.rect.xMin, s.rect.xMax, -1, keys);
                Edge(s, true, s.rect.yMax, s.rect.xMin, s.rect.xMax, 1, keys);
            }
            foreach (var opening in openings.Where(o => o.door)) MakeDoor(opening);
            Stair(29.8f, 11.2f, 1, 1); Stair(13.8f, -11.2f, -1, -1);
            foreach (var s in spaces.Where(s => !s.room && s.rect.width > 5))
                for (float x = s.rect.xMin + 2.7f; x < s.rect.xMax - 1; x += 6.4f)
                {
                    // Beams and suspended tubes mark repeating bays; lamps leave dark intervals.
                    Box("School corridor concrete beam", new Vector3(x,s.Height + 3.08f,s.rect.center.y), new Vector3(.24f,.24f,s.rect.height), Plaster(s.floor));
                    Fixture(new Vector3(x,s.Height + 2.96f,s.rect.center.y), s.floor);
                }
        }
        GameObject Slab(string name,Vector3 position,Vector3 size,Material material,bool physical,bool walkable=false)
        {
            var geometry=new GraphicsSurfaceLibrary.Geometry(position,GraphicsSurfaceLibrary.TileSpan(material));
            geometry.Box(position,size,Quaternion.identity,0);var mesh=geometry.Mesh("Campus world-mapped structural slab");owned.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(Root,false);go.transform.position=position;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(physical)
            {go.AddComponent<BoxCollider>().size=size;if(!walkable){var modifier=go.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;}}
            return go;
        }
        void Edge(Space space, bool alongX, float constant, float start, float end, int outward, HashSet<string> keys)
        {
            var cuts = new List<float> { start, end };
            foreach (var other in spaces.Where(s => s.floor == space.floor))
            { cuts.Add(alongX ? other.rect.xMin : other.rect.yMin); cuts.Add(alongX ? other.rect.xMax : other.rect.yMax); }
            var holes = openings.Where(o => o.floor == space.floor && (alongX ? o.normal == Vector2.up : o.normal == Vector2.right) &&
                Mathf.Abs((alongX ? o.centre.y : o.centre.x) - constant) < .015f).ToArray();
            foreach (var hole in holes) { float centre = alongX ? hole.centre.x : hole.centre.y; cuts.Add(centre - hole.width * .5f); cuts.Add(centre + hole.width * .5f); }
            cuts = cuts.Where(c => c >= start - .001f && c <= end + .001f).Select(c => Mathf.Clamp(c,start,end)).Distinct().OrderBy(c => c).ToList();
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float a = cuts[i], b = cuts[i + 1], mid = (a + b) * .5f; if (b - a < .01f) continue;
                var outside = alongX ? new Vector2(mid,constant + outward * .03f) : new Vector2(constant + outward * .03f,mid);
                var adjacent = spaces.Where(s => s != space && s.floor == space.floor && s.rect.Contains(outside)).ToArray();
                // No partitions between overlapping/corner-connected hallway rectangles.
                if (!space.room && adjacent.Any(s => !s.room)) continue;
                var key = space.floor + ":" + alongX + ":" + Mathf.RoundToInt(constant * 1000) + ":" + Mathf.RoundToInt(a * 1000) + ":" + Mathf.RoundToInt(b * 1000);
                if (!keys.Add(key)) continue;
                var p = alongX ? new Vector3(mid,space.Height,constant) : new Vector3(constant,space.Height,mid);
                bool opening = holes.Any(o => Mathf.Abs(mid - (alongX ? o.centre.x : o.centre.y)) < o.width * .5f - .005f);
                if (opening)
                {
                    Box("School doorway lintel wall",p + Vector3.up * 2.88f,alongX ? new Vector3(b-a,.64f,.20f) : new Vector3(.20f,.64f,b-a),Plaster(space.floor),true);
                    continue;
                }
                Box("School structural wall",p + Vector3.up * 1.6f,alongX ? new Vector3(b-a,WallHeight,.20f) : new Vector3(.20f,WallHeight,b-a),Plaster(space.floor),true);
                Box("School timber skirting",p + Vector3.up * .16f,alongX ? new Vector3(b-a,.23f,.225f) : new Vector3(.225f,.23f,b-a),Timber);
                Box("School chair rail",p + Vector3.up * 1.03f,alongX ? new Vector3(b-a,.055f,.223f) : new Vector3(.223f,.055f,b-a),Timber);
                // Irregular moisture footline belongs to the actual wall, not floating old room coordinates.
                if (b-a > .6f)
                    Box("School damp footline",p + Vector3.up * .34f,alongX ? new Vector3(b-a,.06f,.204f) : new Vector3(.204f,.06f,b-a),surfaces.Get("plaster-damp",new Color(.27f,.34f,.29f)));
            }
        }
        void MakeDoor(Opening opening)
        {
            var root = new GameObject("School sliding classroom door " + opening.id); root.layer = 8;
            root.transform.SetParent(Root,false); root.transform.SetPositionAndRotation(opening.Position,
                Quaternion.LookRotation(new Vector3(opening.normal.x,0,opening.normal.y)));
            float width = opening.width - .10f, height = 2.47f;
            var leaf = Box("School framed wooden door leaf",opening.Position + Vector3.up * (height * .5f),new Vector3(width,height,.10f),Timber,true);
            leaf.layer = 9; leaf.transform.SetParent(root.transform,true); leaf.transform.localRotation = Quaternion.identity;
            var ignored = leaf.GetComponent<NavMeshModifier>(); ignored.ignoreFromBuild = true;
            var blocker = root.AddComponent<NavMeshObstacle>(); blocker.shape = NavMeshObstacleShape.Box;
            blocker.center = Vector3.up * (height * .5f); blocker.size = new Vector3(width,height,.28f); blocker.carving = false;
            var interaction = root.AddComponent<Interactable>(); interaction.stableId = "campus-" + opening.id;
            interaction.ConfigureDoor(leaf.transform,blocker,Vector3.right * (opening.width + .10f)); doors.Add(interaction);
            foreach (int side in new[] {-1,1})
            {
                var post = Box("School doorway timber jamb",opening.Position,new Vector3(.09f,2.56f,.25f),Timber,false, false,root.transform);
                post.transform.localPosition = new Vector3(side * opening.width * .5f,1.28f,0); post.transform.localRotation = Quaternion.identity;
                // The glazed inset and its stiles make this a recognisable school door.
                var inset = Box("School door frosted upper pane",opening.Position,new Vector3(width-.32f,.72f,.012f),surfaces.Get("painted-metal",new Color(.19f,.25f,.24f)),false,false,leaf.transform);
                inset.transform.localScale = new Vector3((width-.32f)/width,.72f/height,.12f);
                inset.transform.localPosition = new Vector3(0,.23f,side*.56f); inset.transform.localRotation = Quaternion.identity;
            }
            var track = Box("School sliding door overhead rail",opening.Position,new Vector3(opening.width*2.05f,.075f,.17f),Iron,false,false,root.transform);
            track.transform.localPosition = new Vector3(opening.width*.5f,2.56f,0); track.transform.localRotation = Quaternion.identity;
            CorridorDoorHardware.Attach(interaction,leaf.transform,width,height,.10f);
            string room = spaces.Where(s => s.floor == opening.floor && s.room && s.rect.Contains(opening.centre - opening.normal*.2f)).Select(s=>s.label).FirstOrDefault() ??
                spaces.Where(s => s.floor == opening.floor && s.room && s.rect.Contains(opening.centre + opening.normal*.2f)).Select(s=>s.label).FirstOrDefault();
            foreach (var side in new[] {-1f,1f})
                Sign(room ?? "교실",opening.Position + Vector3.up * 2.79f + new Vector3(opening.normal.x,0,opening.normal.y)*side*.13f,
                    Quaternion.LookRotation(new Vector3(opening.normal.x,0,opening.normal.y)*-side),new Vector2(1.7f,.28f));
        }
        void Stair(float x,float start,int direction,int heightDirection)
        {
            for(int i=0;i<25;i++)
            {
                float y=heightDirection*(i+1)*.2f,z=start+direction*(i+.5f)*.4f;
                Box("Campus stair tread",new Vector3(x,y-.15f,z),new Vector3(3.2f,.3f,.405f),surfaces.Get("concrete-rough",new Color(.86f,.86f,.80f)),true,true);
                foreach(int side in new[]{-1,1})
                {
                    Box("Campus stairwell wall",new Vector3(x+side*1.6f,y+1.55f,z),new Vector3(.15f,3.1f,.405f),Plaster(heightDirection),true);
                    Box("Campus stair handrail",new Vector3(x+side*1.46f,y+.9f,z),new Vector3(.045f,.045f,.405f),Iron);
                }
                Box("Campus stairwell ceiling",new Vector3(x,y+3.2f,z),new Vector3(3.2f,.15f,.405f),Plaster(heightDirection),true);
                if(i%8==4) Fixture(new Vector3(x,y+2.85f,z),heightDirection);
            }
        }
        void Fixture(Vector3 p,int floor)
        {
            Box("School fluorescent fixture housing",p,new Vector3(.19f,.06f,1.15f),Iron);
            Box("School dim fluorescent diffuser",p-Vector3.up*.035f,new Vector3(.12f,.022f,1.02f),surfaces.Get("painted-metal",new Color(.6f,.65f,.59f),new Color(.45f,.59f,.49f)));
            var light = new GameObject("Campus local fluorescent pool").AddComponent<Light>(); light.transform.SetParent(Root,false); light.transform.position=p-Vector3.up*.20f;
            light.type=LightType.Point; light.color=floor<0?new Color(.28f,.56f,.45f):floor>0?new Color(.53f,.64f,.71f):new Color(.62f,.69f,.52f);
            light.intensity=floor<0?1.15f:1.45f; light.range=5.3f; light.shadows=LightShadows.None;
        }
        void Sign(string text,Vector3 p,Quaternion rotation,Vector2 size)
        {
            var board=Box("Campus mounted Korean room sign",p,new Vector3(size.x,size.y,.025f),surfaces.Get("painted-metal",new Color(.28f,.39f,.34f)));
            board.transform.rotation=rotation;
            var label=new GameObject("Campus depth-tested Korean lettering").AddComponent<TextMesh>(); label.transform.SetParent(Root,false);
            label.transform.SetPositionAndRotation(p+rotation*Vector3.back*.017f,rotation); label.text=text; label.fontSize=64; label.characterSize=.040f;
            label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.color=new Color(.76f,.79f,.68f); label.gameObject.AddComponent<AnnexSignFont>().Apply();
            fittedSigns.Add((label,size-new Vector2(.1f,.055f)));
        }
        readonly List<(TextMesh text,Vector2 limit)> fittedSigns = new List<(TextMesh,Vector2)>();
        void CombineStaticVisuals()
        {
            // Spatial batches keep the new multi-room campus affordable without
            // merging doors, hiding places, movable actors or depth-tested text.
            var renderers=Root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && !r.GetComponent<TextMesh>() &&
                !r.GetComponentInParent<Interactable>() && !r.GetComponentInParent<StalkerBrain>() &&
                r.sharedMaterials.Length==1 && GraphicsSurfaceLibrary.SurfaceKey(r.sharedMaterial).Length>0 &&
                r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh &&
                r.GetComponent<MeshFilter>().sharedMesh.isReadable && r.sharedMaterial.shader.name!="HappyToy/ShallowWater").ToArray();
            var groups=renderers.GroupBy(r=>r.sharedMaterial.GetEntityId()+":"+Mathf.FloorToInt(r.bounds.center.x/9)+":"+
                Mathf.FloorToInt((r.bounds.min.y+.3f)/5)+":"+Mathf.FloorToInt(r.bounds.center.z/9));
            foreach(var group in groups)
            {
                var members=group.ToArray();if(members.Length<2)continue;
                var mesh=new Mesh{name="Campus spatial static mesh",indexFormat=IndexFormat.UInt32};owned.Add(mesh);
                mesh.CombineMeshes(members.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,
                    transform=Root.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray(),true,true);
                var batch=new GameObject("Campus architectural spatial batch",typeof(MeshFilter),typeof(MeshRenderer));batch.transform.SetParent(Root,false);
                batch.GetComponent<MeshFilter>().sharedMesh=mesh;batch.GetComponent<MeshRenderer>().sharedMaterial=members[0].sharedMaterial;
                foreach(var renderer in members)renderer.enabled=false;
            }
        }
        void LateUpdate()
        {
            foreach(var fit in fittedSigns)
            {
                if(!fit.text)continue; var bounds=fit.text.GetComponent<Renderer>().localBounds;
                if(bounds.size.x<.001f||bounds.size.y<.001f)continue;
                fit.text.transform.localScale=Vector3.one*Mathf.Min(1,fit.limit.x/bounds.size.x,fit.limit.y/bounds.size.y);
            }
        }
    }
}
