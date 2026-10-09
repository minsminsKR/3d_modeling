using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    public sealed partial class HauntedCorridorPresentation
    {
        sealed class ArchitecturePart
        {
            public Mesh mesh; public int submesh; public string slot; public Matrix4x4 local;
        }
        sealed class ArchitectureBatch
        {
            public int cell, lod; public Material material; public bool floor;
            public readonly List<CombineInstance> parts = new List<CombineInstance>();
        }
        static readonly Dictionary<string, ArchitecturePart[]> architectureResources = new Dictionary<string, ArchitecturePart[]>();
        readonly Dictionary<string, ArchitectureBatch> architectureBatches = new Dictionary<string, ArchitectureBatch>();
        readonly Dictionary<string,Mesh> authoredUvMeshes = new Dictionary<string,Mesh>();
        readonly Dictionary<int,Vector3> extraArchitectureOrigins = new Dictionary<int,Vector3>();
        public int AuthoredArchitectureModules { get; private set; }
        public int ArchitectureLodGroups { get; private set; }

        static ArchitecturePart[] ArchitectureResource(string key)
        {
            if (architectureResources.TryGetValue(key, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>("CorridorArchitecture/" + key);
            if (!prefab || prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Required render-only Blender architecture absent: " + key);
            var parts = new List<ArchitecturePart>();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
                if (!mesh || !mesh.isReadable || !renderer || mesh.uv.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Architecture must retain imported normals and measured UVs: " + key);
                for (int i = 0; i < mesh.subMeshCount; i++)
                    parts.Add(new ArchitecturePart {mesh=mesh,submesh=i,slot=renderer.sharedMaterials[i].name,
                        local=filter.transform.localToWorldMatrix});
            }
            if (parts.Count == 0) throw new InvalidOperationException("Empty authored architecture: " + key);
            cached=parts.ToArray(); architectureResources.Add(key,cached); return cached;
        }

        Material ArchitectureMaterial(string slot, int cell)
        {
            switch(slot)
            {
                case "CA_timber": return timber;
                case "CA_paper": return SectorPaper(cell);
                case "CA_floor": return graphicsSurfaces.Get("wood-aged",new Color(.83f,.79f,.72f));
                case "CA_iron": return graphicsSurfaces.Get("metal-rust",new Color(.48f,.49f,.46f));
                case "CA_plaster": return graphicsSurfaces.Get("plaster-damp",new Color(.86f,.83f,.77f));
                default: throw new InvalidOperationException("Unknown architecture surface: " + slot);
            }
        }

        Mesh MeasuredArchitectureMesh(ArchitecturePart part,Vector3 scale,Material material)
        {
            float span=GraphicsSurfaceLibrary.TileSpan(material);
            string key=part.mesh.GetEntityId()+":"+scale.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+
                scale.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+
                scale.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+span;
            if(authoredUvMeshes.TryGetValue(key,out var cached))return cached;
            var mapped=UnityEngine.Object.Instantiate(part.mesh);mapped.name=part.mesh.name+" measured architecture UV";
            var matrix=Matrix4x4.Scale(scale)*part.local;var normalMatrix=matrix.inverse.transpose;
            var vertices=mapped.vertices;var normals=mapped.normals;var uv=new Vector2[vertices.Length];
            Bounds bounds=default;bool found=false;
            foreach(var vertex in vertices)
            {var point=matrix.MultiplyPoint3x4(vertex);if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);}
            bool timberGrain=GraphicsSurfaceLibrary.SurfaceKey(material).StartsWith("wood",StringComparison.Ordinal);
            for(int i=0;i<vertices.Length;i++)
            {
                var point=matrix.MultiplyPoint3x4(vertices[i]);var normal=normalMatrix.MultiplyVector(normals[i]).normalized;
                if(timberGrain)
                {
                    var n=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                    int omit=n.y>=n.x&&n.y>=n.z?1:n.x>=n.z?0:2;
                    int a=(omit+1)%3,b=(omit+2)%3;if(bounds.size[b]>bounds.size[a]){int swap=a;a=b;b=swap;}
                    uv[i]=new Vector2(point[a],point[b])/span;
                }
                else uv[i]=GraphicsSurfaceLibrary.Project(point,normal)/span;
            }
            mapped.uv=uv;mapped.RecalculateTangents();owned.Add(mapped);authoredUvMeshes.Add(key,mapped);return mapped;
        }

        void AddArchitecture(int cell, string module, Vector3 at, Quaternion rotation, Vector3 scale)
        {
            var origin=ArchitectureOrigin(cell);
            var placement=Matrix4x4.TRS(at-origin,rotation,scale);
            for(int lod=0;lod<2;lod++)
                foreach(var part in ArchitectureResource(module+"-lod"+lod))
                {
                    var material=ArchitectureMaterial(part.slot,cell);
                    string key=cell+":"+lod+":"+material.GetEntityId();
                    if(!architectureBatches.TryGetValue(key,out var batch))
                    {batch=new ArchitectureBatch {cell=cell,lod=lod,material=material,floor=part.slot=="CA_floor"};architectureBatches.Add(key,batch);}
                    batch.parts.Add(new CombineInstance {mesh=MeasuredArchitectureMesh(part,scale,material),subMeshIndex=part.submesh,transform=placement*part.local});
                }
            AuthoredArchitectureModules++;
        }

        void Panel(int cell,Vector3 edge,Vector3 across,Vector3 inward,Quaternion basis,float offset,float width,System.Random random)
        {
            int bays=Mathf.Max(1,Mathf.RoundToInt(width/SectorBayWidth(cell)));float bay=width/bays;
            for(int index=0;index<bays;index++)
            {
                float x=offset-width*.5f+(index+.5f)*bay;
                AddArchitecture(cell,"wall-"+random.Next(3),edge+across*x+inward*.121f,basis,new Vector3(bay,1,1));
            }
            CladWallFaces++;
        }

        void DressAuthoredFloorsAndCeilings()
        {
            // Imported board courses replace only visual faces; all capsule support,
            // sight-blocking walls, doors and baked navigation remain authoritative.
            foreach(var state in physical)
                if(state.collider.name=="Floor") Hide(state.collider.GetComponent<MeshRenderer>());
            for(int cell=0;cell<CorridorLayout.Count;cell++)
            {
                var center=run.CellPosition(cell);center.y=0;
                for(int x=0;x<6;x++)for(int z=0;z<2;z++)
                {
                    var at=center+new Vector3(x-2.5f,0,z*3-1.5f);
                    AddArchitecture(cell,"floor-course",at,Quaternion.identity,Vector3.one);
                    AddArchitecture(cell,"ceiling-course",at+Vector3.up*2.95f,Quaternion.identity,Vector3.one);
                }
            }
        }

        void DressAuthoredStructuralTimber()
        {
            foreach(var state in physical)
            {
                string module=state.collider.name=="Door post"?"door-post":state.collider.name=="Door lintel"?"door-lintel":null;
                if(module==null)continue;
                Hide(state.collider.GetComponent<MeshRenderer>());
                int cell=NearestCell(state.center);var size=state.size;var rotation=Quaternion.identity;Vector3 scale;
                if(module=="door-post")scale=new Vector3(size.x/.14f,size.y/2.4f,size.z/.14f);
                else
                {
                    if(size.z>size.x)rotation=Quaternion.Euler(0,90,0);
                    scale=new Vector3(Mathf.Max(size.x,size.z)/2.8f,size.y/.56f,Mathf.Min(size.x,size.z)/.24f);
                }
                AddArchitecture(cell,module,state.center,rotation,scale);
            }
        }

        Vector3 ArchitectureOrigin(int cell)
        {
            if(extraArchitectureOrigins.TryGetValue(cell,out var origin))return origin;
            origin=run.CellPosition(cell);origin.y=0;return origin;
        }

        int ChamberChunk(Vector3 world)
        {
            var room=run.AltarRoomRoot;var local=room.InverseTransformPoint(world);
            int x=Mathf.Clamp(Mathf.FloorToInt((local.x+6)/3),0,3),z=Mathf.Clamp(Mathf.FloorToInt((local.z+9)/3),0,4);
            int key=CorridorLayout.Count+z*4+x;
            extraArchitectureOrigins[key]=room.TransformPoint(new Vector3(x*3-4.5f,0,z*3-7.5f));return key;
        }

        void DressAuthoredClassroomShell()
        {
            if(!run.AltarRoomRoot)return;var room=run.AltarRoomRoot;
            foreach(var state in physical)
            {
                if(!state.collider.name.StartsWith("Classroom ",StringComparison.Ordinal))continue;
                string name=state.collider.name;
                if(name.EndsWith("floor",StringComparison.Ordinal)||name.EndsWith("ceiling",StringComparison.Ordinal))
                {
                    bool floor=name.EndsWith("floor",StringComparison.Ordinal);
                    if(floor)Hide(state.collider.GetComponent<MeshRenderer>());
                    var center=room.InverseTransformPoint(state.center);var size=state.collider.transform.lossyScale;
                    int nx=Mathf.CeilToInt(size.x),nz=Mathf.CeilToInt(size.z/3);float width=size.x/nx,length=size.z/nz;
                    float y=floor?0:3.38f;
                    for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
                    {
                        var at=room.TransformPoint(new Vector3(center.x-size.x*.5f+(x+.5f)*width,y,center.z-size.z*.5f+(z+.5f)*length));
                        AddArchitecture(ChamberChunk(at),floor?"floor-course":"ceiling-course",at,room.rotation,new Vector3(width,1,length/3));
                    }
                }
                else if(name.Contains("wall"))
                {
                    var center=room.InverseTransformPoint(state.center);center.y=0;var size=state.collider.transform.lossyScale;
                    Vector3 inward;float width;
                    if(size.x<size.z){inward=new Vector3(-Mathf.Sign(center.x),0,0);width=size.z;}
                    else {inward=new Vector3(0,0,-Mathf.Sign(center.z));width=size.x;}
                    var across=new Vector3(inward.z,0,-inward.x);int bays=Mathf.CeilToInt(width);float bay=width/bays;
                    for(int i=0;i<bays;i++)
                    {
                        var at=room.TransformPoint(center+inward*.112f+across*(-width*.5f+(i+.5f)*bay));
                        AddArchitecture(ChamberChunk(at),"plaster-wall",at,room.rotation*Quaternion.LookRotation(inward),new Vector3(bay,1,1));
                    }
                }
            }
        }

        void EmitDynamicArchitecture(string module,Transform root,int cell)
        {
            var levels=new List<Renderer>[2] {new List<Renderer>(),new List<Renderer>()};
            for(int lod=0;lod<2;lod++)
            {
                var batches=new Dictionary<Material,List<CombineInstance>>();
                foreach(var part in ArchitectureResource(module+"-lod"+lod))
                {
                    var material=ArchitectureMaterial(part.slot,cell);
                    if(!batches.TryGetValue(material,out var list)){list=new List<CombineInstance>();batches.Add(material,list);}
                    list.Add(new CombineInstance {mesh=MeasuredArchitectureMesh(part,Vector3.one,material),subMeshIndex=part.submesh,transform=part.local});
                }
                foreach(var entry in batches)
                {
                    var mesh=new Mesh {name="Blender moving "+module+" LOD"+lod,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(entry.Value.ToArray(),true,true);owned.Add(mesh);
                    var go=new GameObject(mesh.name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=entry.Key;renderer.receiveShadows=true;
                    levels[lod].Add(renderer);VisualBatches++;
                }
            }
            var group=root.gameObject.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;
            group.SetLODs(new[] {new LOD(.32f,levels[0].ToArray()),new LOD(.012f,levels[1].ToArray())});group.RecalculateBounds();
            AuthoredArchitectureModules++;
        }

        void EmitArchitecture()
        {
            foreach(var cellGroup in architectureBatches.Values.GroupBy(x=>x.cell))
            {
                var root=new GameObject("Blender architecture cell "+cellGroup.Key);root.transform.SetParent(additions,false);
                root.transform.position=ArchitectureOrigin(cellGroup.Key);
                var renderers=new List<Renderer>[2] {new List<Renderer>(),new List<Renderer>()};
                foreach(var batch in cellGroup)
                {
                    var mesh=new Mesh {name="Authored corridor cell "+batch.cell+" LOD"+batch.lod,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(batch.parts.ToArray(),true,true);mesh.RecalculateBounds();owned.Add(mesh);
                    if(batch.floor)
                    {
                        // Continuous measured grain across neighbouring modules.
                        // Classroom soak overlays use this same local projection.
                        var vertices=mesh.vertices;var uv=new Vector2[vertices.Length];
                        float span=GraphicsSurfaceLibrary.TileSpan(batch.material);
                        for(int i=0;i<vertices.Length;i++)
                        {
                            var world=vertices[i]+root.transform.position;
                            var point=batch.cell>=CorridorLayout.Count&&run.AltarRoomRoot?
                                run.AltarRoomRoot.InverseTransformPoint(world):world-run.CellPosition(0);
                            uv[i]=new Vector2(point.z,point.x)/span;
                        }
                        mesh.uv=uv;mesh.RecalculateTangents();
                    }
                    var go=new GameObject(batch.material.name+" LOD"+batch.lod);go.transform.SetParent(root.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=batch.material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                    renderers[batch.lod].Add(renderer);VisualBatches++;
                }
                var group=root.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;
                group.SetLODs(new[] {new LOD(.38f,renderers[0].ToArray()),new LOD(.012f,renderers[1].ToArray())});
                group.RecalculateBounds();ArchitectureLodGroups++;
            }
            architectureBatches.Clear();
        }
    }
}
