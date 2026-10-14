using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // The previous single-triangle UV clipping left only six texels in one iris.
    // Clip the full visible 3D iris against every real game-atlas triangle instead.
    public static class BabyEyeMaskImport
    {
        const string Source="SourceArt/CreatureStudio/Baby/", Assets="Assets/Resources/EnemyStudio/Baby/";
        const int Size=2048;
        [Serializable] sealed class Eye {public Vector2 uv;public int materialSlot;public string sourceMesh;public Vector2[] uvBoundarySamples;public bool aperture;}
        [Serializable] sealed class Profile {public int version=2;public string key="Baby";public Eye[] eyes;}
        [Serializable] sealed class Recipe
        {
            public string inputHash,maskHash,profileHash;
            public string method="Visible posed game triangles clipped against anatomical iris ellipses, separately across UV seams; original pupil remains black";
            public float irisRadiusOfEyeSpacing=.145f,pupilFraction=.32f;
            public Vector3[] centres;public int[] visibleTriangles,whiteTexels;
        }
        static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static string Inputs()=>string.Join(":",new[]{Assets+"Baby.fbx",Assets+"BabyRuntime.prefab",Assets+"patrol.anim",Source+"eyes-authoring.json","Assets/Editor/BabyEyeMaskImport.cs"}.Select(p=>Hash(File.ReadAllBytes(p))));
        public static void Ensure()
        {
            string file=Source+"eyes-surface-mask.json";
            var recipe=File.Exists(file)?JsonUtility.FromJson<Recipe>(File.ReadAllText(file)):null;
            if(recipe==null || recipe.inputHash!=Inputs() || !File.Exists(Source+"eyes-surface-emission.png") ||
                recipe.maskHash!=Hash(File.ReadAllBytes(Assets+"eyes-emission.png")) ||
                recipe.profileHash!=Hash(File.ReadAllBytes(Assets+"eyes-profile.json")))Apply();
        }
        public static void Apply()
        {
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("EnemyStudio/Baby/BabyRuntime"));
            GameObject collision=null;Mesh mesh=null;Texture2D texture=null;
            try
            {
                Resources.Load<AnimationClip>("EnemyStudio/Baby/patrol").SampleAnimation(root,0);
                var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single();var source=skin.sharedMesh;
                var vertices=EnemyStudioRig.PosedVertices(skin);var uv=source.uv;var triangles=source.triangles;
                var profile=JsonUtility.FromJson<Profile>(File.ReadAllText(Source+"eyes-authoring.json"));
                if(profile.eyes.Length!=2)throw new InvalidDataException("Baby requires two original eye landmarks");
                var head=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Head");
                var centres=profile.eyes.Select(eye=>Resolve(eye.uv,vertices,uv,source.GetTriangles(eye.materialSlot),head.position)).ToArray();
                var right=(centres[0]-centres[1]).normalized;var forward=Vector3.Cross(right,Vector3.up).normalized;
                if(forward.z<0)forward=-forward;
                var up=Vector3.Cross(forward,right).normalized;
                float radius=Vector3.Distance(centres[0],centres[1])*.145f;
                mesh=new Mesh {indexFormat=source.indexFormat,vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateBounds();
                collision=new GameObject("Owned anatomical eye-mask projection");var collider=collision.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
                var mask=new Color32[Size*Size];var recipe=new Recipe {centres=centres,visibleTriangles=new int[2],whiteTexels=new int[2]};
                for(int eye=0;eye<2;eye++)
                {
                    var layer=new Color32[Size*Size];var centre=centres[eye];
                    var boundary=new List<KeyValuePair<Vector2,Vector2>>();
                    var eyeTriangles=source.GetTriangles(profile.eyes[eye].materialSlot);
                    // White iris then black pupil, both clipped per actual triangle.
                    for(int pass=0;pass<2;pass++)
                        for(int i=0;i<eyeTriangles.Length;i+=3)
                        {
                            int a=eyeTriangles[i],b=eyeTriangles[i+1],c=eyeTriangles[i+2];
                            Vector2 Project(Vector3 v) {var d=v-centre;return new Vector2(Vector3.Dot(d,right),Vector3.Dot(d,up))/radius;}
                            var p=new[]{Project(vertices[a]),Project(vertices[b]),Project(vertices[c])};
                            if(p.Min(v=>v.x)>1 || p.Max(v=>v.x)<-1 || p.Min(v=>v.y)>1 || p.Max(v=>v.y)<-1)continue;
                            if(Mathf.Abs(Cross(p[1]-p[0],p[2]-p[0]))<1e-8f)continue;
                            var polygon=Clip(p,pass==0?1:.32f);if(polygon.Count<3)continue;
                            var middle=polygon.Aggregate(Vector2.zero,(sum,v)=>sum+v)/polygon.Count;
                            var expected=Weights(middle,p[0],p[1],p[2]);
                            var expectedWorld=vertices[a]*expected.x+vertices[b]*expected.y+vertices[c]*expected.z;
                            var ray=new Ray(centre+right*(middle.x*radius)+up*(middle.y*radius)+forward*.3f,-forward);
                            if(!collider.Raycast(ray,out var hit,.6f) || Vector3.Distance(hit.point,expectedWorld)>.002f)continue;
                            if(pass==0)recipe.visibleTriangles[eye]++;
                            var mapped=polygon.Select(v=>{var w=Weights(v,p[0],p[1],p[2]);return uv[a]*w.x+uv[b]*w.y+uv[c]*w.z;}).ToArray();
                            // Stay inside the source triangle after JSON float
                            // rounding, including tiny charts at atlas seams.
                            if(pass==0)for(int vertex=0;vertex<mapped.Length;vertex++)boundary.Add(new KeyValuePair<Vector2,Vector2>(polygon[vertex],Vector2.Lerp(mapped[vertex],(uv[a]+uv[b]+uv[c])/3,.02f)));
                            for(int fan=1;fan<mapped.Length-1;fan++)Raster(layer,mapped[0],mapped[fan],mapped[fan+1],pass==0);
                        }
                    recipe.whiteTexels[eye]=layer.Count(pixel=>pixel.r>0);
                    if(recipe.visibleTriangles[eye]<4 || recipe.whiteTexels[eye]<32)throw new InvalidDataException("An anatomical Baby iris has insufficient game-atlas coverage");
                    for(int pixel=0;pixel<mask.Length;pixel++)if(layer[pixel].r>0)mask[pixel]=layer[pixel];
                    // Keep existing anchor UVs; boundary samples now span the full
                    // actual visible iris, instead of one clipped source triangle.
                    profile.eyes[eye].uvBoundarySamples=Enumerable.Range(0,32).Select(index=>
                    {
                        float angle=index*Mathf.PI*2/32;
                        var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                        // The original mesh has gaps. Use extrema of its actual
                        // visible clipped patches, never invent a boundary in air.
                        return boundary.OrderByDescending(v=>Vector2.Dot(v.Key,direction)).First().Value;
                    }).ToArray();
                }
                texture=new Texture2D(Size,Size,TextureFormat.RGB24,false,true);texture.SetPixels32(mask);texture.Apply();var png=texture.EncodeToPNG();
                var json=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(profile,true));
                File.WriteAllBytes(Source+"eyes-surface-emission.png",png);File.WriteAllBytes(Source+"eyes-surface-profile.json",json);
                WriteChanged(Assets+"eyes-emission.png",png);WriteChanged(Assets+"eyes-profile.json",json);
                AssetDatabase.Refresh();var importer=(TextureImporter)AssetImporter.GetAtPath(Assets+"eyes-emission.png");
                importer.sRGBTexture=false;importer.maxTextureSize=Size;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.SaveAndReimport();
                recipe.inputHash=Inputs();recipe.maskHash=Hash(png);recipe.profileHash=Hash(json);
                File.WriteAllText(Source+"eyes-surface-mask.json",JsonUtility.ToJson(recipe,true));
                Debug.Log("BABY_ANATOMICAL_EYE_MASK_PASS triangles="+string.Join(",",recipe.visibleTriangles)+" texels="+string.Join(",",recipe.whiteTexels));
            }
            finally
            {
                if(collision)UnityEngine.Object.DestroyImmediate(collision);if(mesh)UnityEngine.Object.DestroyImmediate(mesh);
                if(texture)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(root);
            }
        }
        static void WriteChanged(string path,byte[] bytes)
        {if(File.ReadAllBytes(path).SequenceEqual(bytes))return;AssetDatabase.ReleaseCachedFileHandles();File.WriteAllBytes(path,bytes);}
        static Vector3 Resolve(Vector2 point,Vector3[] positions,Vector2[] uv,int[] triangles,Vector3 head)
        {
            float best=float.PositiveInfinity;var result=Vector3.zero;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                if(Mathf.Abs(Cross(uv[b]-uv[a],uv[c]-uv[a]))<1e-10f)continue;
                var w=Weights(point,uv[a],uv[b],uv[c]);if(w.x<-.00001f||w.y<-.00001f||w.z<-.00001f)continue;
                var p=positions[a]*w.x+positions[b]*w.y+positions[c]*w.z;float distance=(p-head).sqrMagnitude;
                if(distance<best){best=distance;result=p;}
            }
            if(float.IsInfinity(best))throw new InvalidDataException("Original Baby eye UV has no real surface");return result;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static Vector3 Weights(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {float determinant=Cross(b-a,c-a),v=Cross(p-a,c-a)/determinant,w=Cross(b-a,p-a)/determinant;return new Vector3(1-v-w,v,w);}
        static List<Vector2> Clip(Vector2[] triangle,float radius)
        {
            var polygon=triangle.ToList();
            for(int edge=0;edge<48 && polygon.Count>0;edge++)
            {
                float angle=edge*Mathf.PI*2/48,next=(edge+1)*Mathf.PI*2/48;
                var a=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;var b=new Vector2(Mathf.Cos(next),Mathf.Sin(next))*radius;
                var output=new List<Vector2>();var previous=polygon[polygon.Count-1];float prevSide=Cross(b-a,previous-a);
                foreach(var point in polygon)
                {
                    float side=Cross(b-a,point-a);
                    if((side>=0)!=(prevSide>=0))output.Add(Vector2.LerpUnclamped(previous,point,prevSide/(prevSide-side)));
                    if(side>=0)output.Add(point);previous=point;prevSide=side;
                }
                polygon=output;
            }
            return polygon;
        }
        static void Raster(Color32[] pixels,Vector2 a,Vector2 b,Vector2 c,bool white)
        {
            if(Mathf.Abs(Cross(b-a,c-a))<1e-14f)return;
            int left=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))*Size)-1,0,Size-1),right=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))*Size)+1,0,Size-1);
            int bottom=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))*Size)-1,0,Size-1),top=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))*Size)+1,0,Size-1);
            for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++)
            {
                bool inside=false;
                foreach(var offset in white?new[]{new Vector2(.25f,.25f),new Vector2(.75f,.25f),new Vector2(.25f,.75f),new Vector2(.75f,.75f)}:new[]{new Vector2(.5f,.5f)})
                {var w=Weights(new Vector2((x+offset.x)/Size,(y+offset.y)/Size),a,b,c);if(w.x>=0&&w.y>=0&&w.z>=0){inside=true;break;}}
                if(inside)pixels[y*Size+x]=white?new Color32(255,255,255,255):new Color32(0,0,0,255);
            }
        }
    }
}
