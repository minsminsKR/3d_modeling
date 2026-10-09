using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Meshes stay immutable Resources assets. Each caller owns combined meshes,
    // material pools and wrappers; imported axis conversion remains in the child.
    public static class CorridorDetailLibrary
    {
        public static readonly string[] Modules = { "memory-seal", "entrance-panel", "passage-upright", "passage-header",
            "ceiling-joist", "lantern-hardware", "hanging-seal", "framed-plaque", "binding-stamp",
            "memory-socket", "timber-plaque", "classroom-photo", "window-recess", "exercise-leaf" };

        public static GameObject Resource(string key, int lod)
        {
            if (Array.IndexOf(Modules, key) < 0 || lod < 0 || lod > 1) throw new ArgumentException("Unknown authored corridor detail");
            var prefab = Resources.Load<GameObject>("CorridorDetails/" + key + "-lod" + lod);
            if (!prefab || prefab.GetComponentsInChildren<Collider>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Required render-only Blender corridor detail absent: " + key);
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) throw new InvalidOperationException("Empty authored corridor detail: " + key);
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
                if (!mesh || !mesh.isReadable || !renderer || mesh.uv.Length != mesh.vertexCount ||
                    mesh.normals.Length != mesh.vertexCount || renderer.sharedMaterials.Length != mesh.subMeshCount)
                    throw new InvalidOperationException("Detail requires readable mesh, normals and measured UVs: " + key);
            }
            return prefab;
        }

        public static Transform Attach(string key, Transform parent, Func<string, Material> resolver, int lod = 0)
        {
            if (!parent || resolver == null) throw new ArgumentException("Corridor detail owner and material resolver required");
            var root = new GameObject("Authored corridor detail — " + key).transform;
            root.SetParent(parent, false);
            try
            {
                UnityEngine.Object.Instantiate(Resource(key, lod), root, false);
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mapped = renderer.sharedMaterials.Select(m => m ? resolver(m.name) : null).ToArray();
                    if (mapped.Any(m => !m)) throw new InvalidOperationException("Corridor detail material unresolved: " + key);
                    renderer.sharedMaterials = mapped; renderer.receiveShadows = true;
                }
                return root;
            }
            catch { UnityEngine.Object.Destroy(root.gameObject); throw; }
        }
    }

    public sealed partial class HauntedCorridorPresentation
    {
        sealed class DetailBatch
        {
            public int cell, lod; public Material material; public bool castShadows;
            public readonly List<CombineInstance> parts = new List<CombineInstance>();
        }
        static readonly Dictionary<string, ArchitecturePart[]> detailResources = new Dictionary<string, ArchitecturePart[]>();
        readonly Dictionary<string, DetailBatch> detailBatches = new Dictionary<string, DetailBatch>();
        public int AuthoredDetailModules { get; private set; }

        static ArchitecturePart[] DetailResource(string module, int lod)
        {
            string key = module + "-lod" + lod;
            if (detailResources.TryGetValue(key, out var cached)) return cached;
            var parts = new List<ArchitecturePart>();
            foreach (var filter in CorridorDetailLibrary.Resource(module, lod).GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    parts.Add(new ArchitecturePart { mesh=filter.sharedMesh, submesh=i, slot=renderer.sharedMaterials[i].name,
                        local=filter.transform.localToWorldMatrix });
            }
            cached=parts.ToArray(); detailResources.Add(key, cached); return cached;
        }

        Material DetailMaterial(string slot, int cell, Material paperOverride = null)
        {
            switch (slot)
            {
                case "CD_timber": return timber;
                case "CD_paper": return paperOverride ? paperOverride : SectorPaper(cell);
                case "CD_binding": return SectorBinding(cell);
                case "CD_ink": return ink;
                case "CD_iron": return graphicsSurfaces.Get("metal-rust", new Color(.48f,.49f,.46f));
                default: throw new InvalidOperationException("Unknown corridor detail surface: " + slot);
            }
        }

        void AddDetail(int cell, string module, Vector3 at, Quaternion rotation, Vector3 scale,
            bool castShadows = true, Material paperOverride = null)
        {
            var placement = Matrix4x4.TRS(at-ArchitectureOrigin(cell), rotation, scale);
            for (int lod=0; lod<2; lod++) foreach (var part in DetailResource(module, lod))
            {
                var material=DetailMaterial(part.slot,cell,paperOverride);
                string key=cell+":"+lod+":"+material.GetEntityId()+":"+castShadows;
                if (!detailBatches.TryGetValue(key,out var batch))
                {
                    batch=new DetailBatch { cell=cell,lod=lod,material=material,castShadows=castShadows };
                    detailBatches.Add(key,batch);
                }
                batch.parts.Add(new CombineInstance { mesh=MeasuredArchitectureMesh(part,scale,material),subMeshIndex=part.submesh,
                    transform=placement*part.local });
            }
            AuthoredDetailModules++;
        }

        void EmitDynamicDetail(string module,Transform root,int cell,bool castShadows)
        {
            var levels=new List<Renderer>[2] {new List<Renderer>(),new List<Renderer>()};
            for (int lod=0;lod<2;lod++)
            {
                var batches=new Dictionary<Material,List<CombineInstance>>();
                foreach(var part in DetailResource(module,lod))
                {
                    var material=DetailMaterial(part.slot,cell);
                    if(!batches.TryGetValue(material,out var list)){list=new List<CombineInstance>();batches.Add(material,list);}
                    list.Add(new CombineInstance {mesh=MeasuredArchitectureMesh(part,Vector3.one,material),subMeshIndex=part.submesh,transform=part.local});
                }
                foreach(var entry in batches)
                {
                    var mesh=new Mesh {name="Authored "+module+" LOD"+lod,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(entry.Value.ToArray(),true,true);owned.Add(mesh);
                    var go=new GameObject(mesh.name);go.transform.SetParent(root,false);go.layer=8;
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=entry.Key;renderer.receiveShadows=true;
                    renderer.shadowCastingMode=castShadows?ShadowCastingMode.On:ShadowCastingMode.Off;
                    levels[lod].Add(renderer);VisualBatches++;
                }
            }
            var group=root.gameObject.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;
            group.SetLODs(new[] {new LOD(.12f,levels[0].ToArray()),new LOD(.005f,levels[1].ToArray())});group.RecalculateBounds();
            AuthoredDetailModules++;
        }

        void EmitDetails()
        {
            foreach(var cellGroup in detailBatches.Values.GroupBy(x=>x.cell))
            {
                var root=new GameObject("Blender corridor detail cell "+cellGroup.Key);root.transform.SetParent(additions,false);
                root.transform.position=ArchitectureOrigin(cellGroup.Key);root.layer=8;
                var levels=new List<Renderer>[2] {new List<Renderer>(),new List<Renderer>()};
                foreach(var batch in cellGroup)
                {
                    var mesh=new Mesh {name="Authored details cell "+batch.cell+" LOD"+batch.lod,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(batch.parts.ToArray(),true,true);mesh.RecalculateBounds();owned.Add(mesh);
                    var go=new GameObject(batch.material.name+" LOD"+batch.lod);go.transform.SetParent(root.transform,false);go.layer=8;
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.material;
                    renderer.receiveShadows=true;renderer.shadowCastingMode=batch.castShadows?ShadowCastingMode.On:ShadowCastingMode.Off;
                    levels[batch.lod].Add(renderer);VisualBatches++;
                }
                var group=root.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;
                group.SetLODs(new[] {new LOD(.38f,levels[0].ToArray()),new LOD(.005f,levels[1].ToArray())});group.RecalculateBounds();ArchitectureLodGroups++;
            }
            detailBatches.Clear();
        }
    }
}
