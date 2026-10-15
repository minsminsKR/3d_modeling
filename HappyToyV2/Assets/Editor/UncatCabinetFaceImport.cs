using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Finishes the existing Uncat head in its authored face frame. Shared mouth
    // topology is reused; muzzle depth, jaw recipe and both eye surfaces are Uncat's.
    public static class UncatCabinetFaceImport
    {
        const string Folder="Assets/Resources/ChaseFaces/Uncat/Cabinet";
        const string Source="SourceArt/UncatCabinetFace";
        [Serializable] public sealed class Profile
        {
            public Vector3 mouth;
            public float scale,shellRecession,patchWidth,patchTop,patchBottom,coverWidth,coverTop,coverBottom,clipStart,clipEnd,depthStart,depthEnd,openingSeconds,closingSeconds;
            public CyclopseCabinetFaceImport.Profile jaw;
        }
        public static CreatureFaceAuthoring.Profile FaceProfile()=>JsonUtility.FromJson<CreatureFaceAuthoring.Profile>(File.ReadAllText("SourceArt/ChaseFaces/Uncat/projection.json"));
        static string InputHash()
        {
            using(var sha=SHA256.Create())using(var bytes=new MemoryStream())
            {
                foreach(string path in new[]{Source+"/gape-profile.json",Source+"/closed-lips-generated-original.png","SourceArt/ChaseFaces/Uncat/projection.json",
                    "Assets/Resources/EnemyStudio/Uncat/Uncat.fbx","Assets/Resources/EnemyStudio/Uncat/patrol.anim",
                    "Assets/Editor/UncatCabinetFaceImport.cs","Assets/Editor/CyclopseCabinetFaceImport.cs"})
                {var b=File.ReadAllBytes(path);bytes.Write(b,0,b.Length);}
                return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static void Ensure()
        {
            CreatureFaceImport.Ensure();
            var face=AssetDatabase.LoadAssetAtPath<CyclopseExpressionData>("Assets/Resources/ChaseFaces/Uncat/Expression.asset");
            if(!face||!face.cabinetFace||face.cabinetFace.sourceHash!=InputHash())Apply();
        }
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            string texture=Folder+"/closed-lips.png";var bytes=File.ReadAllBytes(Source+"/closed-lips-generated-original.png");
            if(!File.Exists(texture)||!File.ReadAllBytes(texture).SequenceEqual(bytes)){AssetDatabase.ReleaseCachedFileHandles();File.WriteAllBytes(texture,bytes);}
            AssetDatabase.Refresh();var importer=(TextureImporter)AssetImporter.GetAtPath(texture);
            importer.sRGBTexture=true;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var p=JsonUtility.FromJson<Profile>(File.ReadAllText(Source+"/gape-profile.json"));var projection=FaceProfile();
            var forward=projection.forward.normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;
            var rotation=Quaternion.LookRotation(forward,Vector3.up);
            Vector3 Local(Vector3 world){var d=world-projection.centre;return new Vector3(Vector3.Dot(d,right),d.y,Vector3.Dot(d,forward));}
            Vector3 World(Vector3 local)=>projection.centre+rotation*local;
            Vector3 Sculpt(Vector3 local)
            {
                var canonical=p.jaw.mouth+(local-p.mouth)/p.scale;
                return p.mouth+(CyclopseCabinetFaceImport.Sculpt(canonical,p.jaw)-p.jaw.mouth)*p.scale;
            }
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("EnemyStudio/Uncat/UncatRuntime"));
            try
            {
                Resources.Load<AnimationClip>("EnemyStudio/Uncat/patrol").SampleAnimation(root,0);
                var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single();var posed=EnemyStudioRig.PosedVertices(skin);
                var local=posed.Select(Local).ToArray();var opened=local.Select(v=>World(Sculpt(v))).ToArray();
                var data=AssetDatabase.LoadAssetAtPath<CabinetFaceData>(Folder+"/Face.asset");
                if(!data){data=ScriptableObject.CreateInstance<CabinetFaceData>();AssetDatabase.CreateAsset(data,Folder+"/Face.asset");}
                data.bindDeltas=new Vector3[posed.Length];data.mouthUV=new Vector3[posed.Length];
                var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*skin.sharedMesh.bindposes[i]).ToArray();
                var counts=skin.sharedMesh.GetBonesPerVertex();var weights=skin.sharedMesh.GetAllBoneWeights();int cursor=0;
                for(int i=0;i<posed.Length;i++)
                {
                    var matrix=Matrix4x4.zero;
                    for(int j=0;j<counts[i];j++)
                    {var influence=weights[cursor++];var m=matrices[influence.boneIndex];for(int e=0;e<16;e++)matrix[e]+=m[e]*influence.weight;}
                    data.bindDeltas[i]=matrix.inverse.MultiplyVector(opened[i]-posed[i]);var d=local[i]-p.mouth;
                    data.mouthUV[i]=new Vector3(d.x/p.coverWidth,d.y/(d.y>0?p.coverTop:p.coverBottom),
                        -Mathf.SmoothStep(0,1,Mathf.InverseLerp(p.clipStart,p.clipEnd,local[i].z)));
                }
                var head=skin.bones.Single(t=>t.name=="mixamorig:Head");var centre=World(p.mouth);
                data.headBone=head.name;data.localPosition=head.InverseTransformPoint(centre);data.localRotation=Quaternion.Inverse(head.rotation)*rotation;
                data.localScale=new Vector3(1/head.lossyScale.x,1/head.lossyScale.y,1/head.lossyScale.z);
                data.openingSeconds=p.openingSeconds;data.closingSeconds=p.closingSeconds;
                data.lipTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
                data.materials=new[]{Material("Enamel",new Color(.94f,.89f,.75f),.46f),Material("StainedEnamel",new Color(.7f,.56f,.36f),.36f),
                    Material("Gums",new Color(.24f,.045f,.045f),.58f),Material("Cavity",new Color(.008f,.002f,.003f),.1f),Material("Tongue",new Color(.24f,.055f,.075f),.65f)};
                var mouth=CyclopseCabinetFaceImport.BuildMouth(p.jaw,out var closed);mouth.vertices=mouth.vertices.Select(v=>v*p.scale).ToArray();
                mouth.name="Uncat long hinged jaw with 22 irregular teeth";mouth.RecalculateBounds();mouth.RecalculateNormals();
                data.mouthClosed=closed.Select(v=>v*p.scale).ToArray();data.mouth=StoreMesh("Mouth",mouth);
                BakePatch();data.sourceHash=InputHash();EditorUtility.SetDirty(data);
                var expression=AssetDatabase.LoadAssetAtPath<CyclopseExpressionData>("Assets/Resources/ChaseFaces/Uncat/Expression.asset");
                expression.cabinetFace=data;EditorUtility.SetDirty(expression);AssetDatabase.SaveAssets();
                const string output="Builds/UncatCabinetFace/authoring";Directory.CreateDirectory(output);
                File.WriteAllText(output+"/face.json",JsonUtility.ToJson(new Surface{neutral=posed,opened=opened,uv=skin.sharedMesh.uv,triangles=skin.sharedMesh.triangles,mouthCentre=centre},true));
                File.WriteAllText(output+"/mouth.json",JsonUtility.ToJson(new Mouth{vertices=data.mouth.vertices.Select(v=>rotation*v).ToArray(),closed=data.mouthClosed.Select(v=>rotation*v).ToArray(),uv=data.mouth.uv,
                    parts=Enumerable.Range(0,data.mouth.subMeshCount).Select(i=>new Part{triangles=data.mouth.GetTriangles(i)}).ToArray()},true));
                var near=local.Where(v=>Mathf.Abs(v.x)<.12f&&Mathf.Abs(v.y-p.mouth.y)<.06f).Select(v=>v.z).OrderBy(v=>v).ToArray();
                Debug.Log("UNCAT_CABINET_FACE_IMPORT_PASS vertices="+posed.Length+" teeth="+(p.jaw.upperTeeth.Length+p.jaw.lowerTeeth.Length)+" mouthDepthSamples="+string.Join(",",near.Where((v,i)=>i%Math.Max(1,near.Length/8)==0).Select(v=>v.ToString("F3"))));

                void BakePatch()
                {
                    var samples=Enumerable.Range(0,local.Length).Where(i=>Mathf.Abs(local[i].x)<p.patchWidth+.05f&&local[i].y>p.mouth.y-p.patchBottom-.05f&&local[i].y<p.mouth.y+p.patchTop+.025f&&local[i].z>p.depthStart&&local[i].z<p.depthEnd).ToArray();
                    var vertices=new List<Vector3>();var target=new List<Vector3>();var uv=new List<Vector2>();var faceUV=new List<Vector2>();var mouthUV=new List<Vector3>();var triangles=new List<int>();
                    const int rings=32,segments=96;
                    for(int r=0;r<=rings;r++)for(int j=0;j<segments;j++)
                    {
                        float f=Mathf.Max(.001f,r/(float)rings),a=j*Mathf.PI*2/segments;
                        float x=p.mouth.x+Mathf.Cos(a)*p.patchWidth*f,y=p.mouth.y+Mathf.Sin(a)*(Mathf.Sin(a)>0?p.patchTop:p.patchBottom)*f;
                        var near=samples.OrderBy(i=>new Vector2(local[i].x-x,local[i].y-y).sqrMagnitude).Take(8).ToArray();
                        // Smooth the overlapping generated muzzle sheets into one
                        // continuous shell at the measured original lip depth.
                        float radius=new Vector2((x-p.mouth.x)/(p.jaw.radius.x*p.scale),(y-p.mouth.y)/(p.jaw.radius.y*p.scale)).magnitude;
                        float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.15f,2.5f,radius));
                        var v=new Vector3(x,y,p.mouth.z+.012f-p.shellRecession*edge);
                        vertices.Add(v-p.mouth);target.Add(Sculpt(v)-p.mouth);uv.Add(skin.sharedMesh.uv[near[0]]);
                        faceUV.Add(new Vector2(.5f-x/projection.size,.5f+y/projection.size));
                        mouthUV.Add(new Vector3((x-p.mouth.x)/(p.jaw.radius.x*p.scale),(y-p.mouth.y)/(p.jaw.radius.y*p.scale),1));
                    }
                    for(int r=0;r<rings;r++)for(int j=0;j<segments;j++)
                    {int a=r*segments+j,b=r*segments+(j+1)%segments,c=a+segments,d=b+segments;triangles.AddRange(new[]{a,c,b,b,c,d});}
                    var mesh=new Mesh{name="Uncat lower muzzle facial finishing"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
                    mesh.SetUVs(0,uv);mesh.SetUVs(1,faceUV);mesh.SetUVs(2,mouthUV);mesh.colors=Enumerable.Repeat(Color.white,vertices.Count).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                    data.facePatch=StoreMesh("LowerFace",mesh);data.patchDeltas=target.Select((v,i)=>v-vertices[i]).ToArray();
                    const string output="Builds/UncatCabinetFace/authoring";Directory.CreateDirectory(output);
                    File.WriteAllText(output+"/patch.json",JsonUtility.ToJson(new Surface{neutral=vertices.Select(v=>rotation*v).ToArray(),opened=target.Select(v=>rotation*v).ToArray(),uv=faceUV.ToArray(),mouthUV=mouthUV.ToArray(),triangles=triangles.ToArray(),mouthCentre=centre},true));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static Mesh StoreMesh(string name,Mesh built)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+name+".asset");
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");}
            EditorUtility.CopySerialized(built,mesh);UnityEngine.Object.DestroyImmediate(built);EditorUtility.SetDirty(mesh);return mesh;
        }
        static Material Material(string name,Color color,float smoothness)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Cull",0);
            if(name.Contains("Enamel"))m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/ChaseFaces/Uncat/chase-face.png"));
            EditorUtility.SetDirty(m);return m;
        }
        [Serializable] sealed class Surface{public Vector3[] neutral,opened,mouthUV;public Vector2[] uv;public int[] triangles;public Vector3 mouthCentre;}
        [Serializable] sealed class Part{public int[] triangles;}
        [Serializable] sealed class Mouth{public Vector3[] vertices,closed;public Vector2[] uv;public Part[] parts;}
    }
}
