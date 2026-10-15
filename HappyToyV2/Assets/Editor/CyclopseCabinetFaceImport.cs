using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Facial finishing of the existing webapp model, not a replacement creature.
    public static class CyclopseCabinetFaceImport
    {
        public const string Folder = "Assets/Resources/CyclopseExpression/Cabinet";
        public const string Source = "SourceArt/CyclopseCabinetFace";
        [Serializable] public sealed class Profile
        {
            public Vector3 mouth, eye;
            public Vector2 radius;
            public float drop, stretch, widthStretch, frontStart, frontEnd, eyeWiden, openingSeconds, closingSeconds;
            public int teethPerRow, seed;
            public float upperHeightScale, jawTaper, jawSideShift, jawCant, jawRecede, lowerJawAngle, cornerDroop;
            public Tooth[] upperTeeth, lowerTeeth;
        }
        [Serializable] public sealed class Tooth
        {
            public float position, length, width, lean, twist, depth;
            public bool pointed, broken;
        }
        public static string InputHash()
        {
            using (var sha = SHA256.Create())
            using (var bytes = new MemoryStream())
            {
                foreach (string file in new[] { Source + "/gape-profile.json", Source + "/closed-lips-generated-original.png", "SourceArt/CyclopseExpression/grin-profile.json",
                    "Assets/Resources/EnemyStudio/Cyclopse/Cyclopse.fbx", "Assets/Editor/CyclopseCabinetFaceImport.cs" })
                { var b = File.ReadAllBytes(file); bytes.Write(b, 0, b.Length); }
                return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(low, high, value));
        public static Vector3 Sculpt(Vector3 grin, Profile p)
        {
            var d = grin - p.mouth;
            float front = Smooth(p.frontStart, p.frontEnd, grin.z);
            float lateral = 1-Smooth(.28f,.45f,Mathf.Abs(d.x));
            float neck = 1-Smooth(.44f,.64f,-d.y);
            float upper = 1-Smooth(.11f,.17f,d.y);
            float mask = front*lateral*(d.y<0?neck:upper);
            // Hinge the lower face downward. Keep the upper arch shallow;
            // lower teeth and chin travel much farther than the upper jaw.
            float jaw = Smooth(0,.82f,-d.y/p.radius.y);
            float side = d.x/p.radius.x;
            float corner = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(side)),2) * (1-Smooth(.7f,1.4f,Mathf.Abs(d.y/p.radius.y)));
            var delta = new Vector3(d.x*p.widthStretch-d.x*p.jawTaper*jaw+p.jawSideShift*jaw,
                -p.drop*jaw + Mathf.Max(0,d.y)*(p.upperHeightScale-1) + p.jawCant*side*jaw - p.cornerDroop*corner*(side<0?1:.25f),
                -p.jawRecede*jaw);
            var e = grin-p.eye;
            float lid = Mathf.Exp(-e.x*e.x/.018f-e.y*e.y/.004f)*front;
            return grin+delta*mask+new Vector3(e.x*.1f,Mathf.Sign(e.y)*p.eyeWiden,.008f)*lid;
        }

        public static CabinetFaceData Bake(SkinnedMeshRenderer skin, Vector3[] posed, CyclopseExpressionPreview.Profile grinProfile)
        {
            Directory.CreateDirectory(Folder);
            string texture=Folder+"/closed-lips.png";
            var sourceBytes=File.ReadAllBytes(Source+"/closed-lips-generated-original.png");
            if(!File.Exists(texture)||!File.ReadAllBytes(texture).SequenceEqual(sourceBytes))
            {AssetDatabase.ReleaseCachedFileHandles();File.WriteAllBytes(texture,sourceBytes);}
            AssetDatabase.Refresh();
            var importer=(TextureImporter)AssetImporter.GetAtPath(texture);
            importer.sRGBTexture=true;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var p = JsonUtility.FromJson<Profile>(File.ReadAllText(Source + "/gape-profile.json"));
            var data = AssetDatabase.LoadAssetAtPath<CabinetFaceData>(Folder + "/Face.asset");
            if (!data) { data = ScriptableObject.CreateInstance<CabinetFaceData>(); AssetDatabase.CreateAsset(data, Folder + "/Face.asset"); }
            data.bindDeltas = new Vector3[posed.Length]; data.mouthUV = new Vector3[posed.Length];
            var matrices = skin.bones.Select((bone, i) => bone.localToWorldMatrix * skin.sharedMesh.bindposes[i]).ToArray();
            var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights(); int cursor = 0;
            var opened = new Vector3[posed.Length];
            for (int i = 0; i < posed.Length; i++)
            {
                Matrix4x4 matrix = Matrix4x4.zero;
                for (int j = 0; j < counts[i]; j++)
                {
                    var influence = weights[cursor++]; var source = matrices[influence.boneIndex];
                    for (int element = 0; element < 16; element++) matrix[element] += source[element] * influence.weight;
                }
                var grin = CyclopseExpressionPreview.Sculpt(posed[i], grinProfile, 1);
                opened[i] = Sculpt(grin, p);
                data.bindDeltas[i] = matrix.inverse.MultiplyVector(opened[i] - grin);
                data.mouthUV[i] = new Vector3((grin.x - p.mouth.x) / .33f, (grin.y - p.mouth.y) / (grin.y > p.mouth.y ? .13f : .33f),
                    -Smooth(.25f, .40f, grin.z));
            }
            var head = skin.bones.Single(t => t.name == "mixamorig:Head");
            var centre = p.mouth;
            data.headBone = head.name; data.localPosition = head.InverseTransformPoint(centre);
            data.localRotation = Quaternion.Inverse(head.rotation);
            data.localScale = new Vector3(1 / head.lossyScale.x, 1 / head.lossyScale.y, 1 / head.lossyScale.z);
            data.openingSeconds = p.openingSeconds; data.closingSeconds = p.closingSeconds; data.jawDrop=p.drop; data.jawStretch=p.stretch;
            data.materials = new[] {
                Material("Enamel", new Color(.65f,.51f,.29f), .52f),
                Material("StainedEnamel", new Color(.42f,.29f,.13f), .4f),
                Material("Gums", new Color(.24f,.055f,.045f), .58f),
                Material("Cavity", new Color(.009f,.0018f,.003f), .12f),
                Material("Tongue", new Color(.19f,.045f,.065f), .72f) };
            var built = BuildMouth(p, out var mouthClosed);
            data.mouthClosed = mouthClosed;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Mouth.asset");
            if (!mesh) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, Folder + "/Mouth.asset"); }
            EditorUtility.CopySerialized(built, mesh); UnityEngine.Object.DestroyImmediate(built);
            data.lipTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            data.mouth = mesh;
            BakePatch(skin, posed, grinProfile, p, centre, data);
            data.sourceHash = InputHash();
            EditorUtility.SetDirty(mesh); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
            ExportAuthoring(skin, posed, opened, mesh, centre, data.mouthUV, mouthClosed);
            Debug.Log("CYCLOPSE_CABINET_FACE_IMPORT_PASS teeth=" + (p.upperTeeth.Length+p.lowerTeeth.Length) + " vertices=" + posed.Length);
            return data;
        }

        static void BakePatch(SkinnedMeshRenderer skin, Vector3[] posed, CyclopseExpressionPreview.Profile grinProfile, Profile p, Vector3 centre, CabinetFaceData data)
        {
            var grin=posed.Select(v=>CyclopseExpressionPreview.Sculpt(v,grinProfile,1)).ToArray();
            var samples=Enumerable.Range(0,posed.Length).Where(i=>grin[i].z>.5f && grin[i].y>1.05f && grin[i].y<1.72f && Mathf.Abs(grin[i].x)<.43f).ToArray();
            var originalUV=skin.sharedMesh.uv;
            var vertices=new List<Vector3>();var deltas=new List<Vector3>();var uv=new List<Vector2>();var faceUV=new List<Vector2>();
            var mouthUV=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            const int rings=32,segments=96;
            // Retopologize only the lower facial surface, using the actual
            // creature's sampled front depth/UV. The original eye stays owned
            // by MonsterRedEyes. Dense polar loops support the huge open jaw.
            for(int r=0;r<=rings;r++)for(int j=0;j<segments;j++)
            {
                float f=Mathf.Max(.001f,r/(float)rings),a=j*Mathf.PI*2/segments;
                float x=Mathf.Cos(a)*.375f*f,y=p.mouth.y+Mathf.Sin(a)*(Mathf.Sin(a)>0?.186f:.365f)*f;
                var near=samples.OrderBy(i=>(new Vector2(grin[i].x-x,grin[i].y-y)).sqrMagnitude).Take(8).ToArray();
                float z=near.Average(i=>grin[i].z)+.012f;
                var v=new Vector3(x,y,z);vertices.Add(v-centre);deltas.Add(Sculpt(v,p)-v);
                uv.Add(originalUV[near[0]]);faceUV.Add(new Vector2(.5f-x/.86f,.5f+(y-1.6716551f)/.86f));
                mouthUV.Add(new Vector3(x/p.radius.x,(y-p.mouth.y)/p.radius.y,1));
                colors.Add(Color.white);
            }
            for(int r=0;r<rings;r++)for(int j=0;j<segments;j++)
            {
                int a=r*segments+j,b=r*segments+(j+1)%segments,c=a+segments,d=b+segments;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            string path=Folder+"/LowerFace.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name="Cyclopse retopologized lower face from original surface";
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetUVs(1,faceUV);mesh.SetUVs(2,mouthUV);mesh.SetColors(colors);
            mesh.RecalculateNormals();mesh.RecalculateBounds();data.facePatch=mesh;data.patchDeltas=deltas.ToArray();EditorUtility.SetDirty(mesh);
            string output="Builds/CyclopseCabinetFace/authoring";Directory.CreateDirectory(output);
            File.WriteAllText(output+"/patch.json",JsonUtility.ToJson(new Authoring{neutral=vertices.ToArray(),opened=vertices.Select((v,i)=>v+deltas[i]).ToArray(),uv=faceUV.ToArray(),triangles=triangles.ToArray(),mouthUV=mouthUV.ToArray(),mouthCentre=centre},true));
        }
        static Material Material(string name, Color colour, float smoothness)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            if(name.Contains("Enamel"))
            {m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/CyclopseExpression/chase-face.png"));colour=name=="Enamel"?new Color(.95f,.9f,.8f):new Color(.7f,.58f,.38f);}
            m.SetColor("_BaseColor", colour); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(m); return m;
        }
        public static Mesh BuildMouth(Profile p, out Vector3[] closed)
        {
            var v = new List<Vector3>(); var toothOpened = new Dictionary<int,Vector3>(); var toothUV = new Dictionary<int,Vector2>(); var indices = Enumerable.Range(0, 5).Select(_ => new List<int>()).ToArray();
            float width = p.radius.x, height = p.radius.y;
            const int segments = 64;
            // Concave bowl, recessed 24cm, with a closed back. Its front edge
            // sits beneath the lip ring; the skin's opening is clipped in every
            // rendering pass, so teeth have real perspective and occlusion.
            for (int ring = 0; ring < 8; ring++)
            {
                float f = 1 - ring / 7f * .96f;
                for (int s = 0; s < segments; s++)
                {
                    float a = s * Mathf.PI * 2 / segments;
                    v.Add(new Vector3(Mathf.Cos(a) * width * f, Mathf.Sin(a) * height * f, -.018f - (1-f) * .24f));
                }
            }
            for (int r = 0; r < 7; r++) for (int s = 0; s < segments; s++)
            {
                int a = r * segments + s, b = r * segments + (s+1)%segments;
                indices[3].AddRange(new[] { a,b,a+segments,b,b+segments,a+segments });
            }
            int back = v.Count; v.Add(new Vector3(0,0,-.258f));
            for (int s = 0; s < segments; s++) indices[3].AddRange(new[] { 7*segments+s,7*segments+(s+1)%segments,back });
            // A continuous, irregular gum rim bridges the teeth into the face.
            int rim = v.Count; const int sides = 10;
            for (int s = 0; s < segments; s++) for (int j = 0; j < sides; j++)
            {
                float a = s * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
                float radius = .018f + .004f * Mathf.Sin(a*9);
                v.Add(new Vector3(Mathf.Cos(a)*(width+Mathf.Cos(b)*radius), Mathf.Sin(a)*(height+Mathf.Cos(b)*radius), .006f+Mathf.Sin(b)*radius));
            }
            for (int s = 0; s < segments; s++) for (int j = 0; j < sides; j++)
            {
                int a = rim+s*sides+j, b = rim+s*sides+(j+1)%sides;
                int c = rim+(s+1)%segments*sides+j, d = rim+(s+1)%segments*sides+(j+1)%sides;
                indices[2].AddRange(new[] {a,c,b,b,c,d});
            }
            var random = new System.Random(p.seed);
            for (int row=0;row<2;row++)
            {
                var teeth=row==0?p.upperTeeth:p.lowerTeeth;
                for(int tooth=0;tooth<teeth.Length;tooth++)
                {
                    var spec=teeth[tooth];float sign=row==0?1:-1;
                    var start=new Vector3(spec.position*width*.94f,sign*Mathf.Sqrt(1-spec.position*spec.position)*height*.94f,.004f+spec.depth);
                    var orientation=Quaternion.Euler(0,spec.twist,spec.lean);
                    var targetRoot=Sculpt(p.mouth+start,p)-p.mouth;
                    var jawRotation=row==0?Quaternion.identity:Quaternion.Euler(p.lowerJawAngle,0,-5);
                    int first=v.Count;const int rings=7,radial=10;
                    float bend=(float)(random.NextDouble()-.5)*.022f;
                    for(int r=0;r<rings;r++)for(int j=0;j<radial;j++)
                    {
                        float t=r/(rings-1f),b=j*Mathf.PI*2/radial;
                        float bulge=(spec.pointed?Mathf.Pow(1-t,.7f):Mathf.Lerp(1,spec.broken?.64f:.4f,t))*(1+.14f*Mathf.Sin(t*Mathf.PI));
                        float chip=spec.broken?Mathf.Sin(b*3+tooth)*.008f*t*t:0;
                        var local=new Vector3(Mathf.Cos(b)*spec.width*bulge+bend*t*t,-sign*(spec.length*t+chip),Mathf.Sin(b)*spec.width*.62f*bulge+Mathf.Sin(t*Mathf.PI)*.01f);
                        var offset=orientation*local;
                        toothOpened[v.Count]=targetRoot+jawRotation*offset;
                        toothUV[v.Count]=new Vector2(.49f+Mathf.Cos(b)*.018f+(tooth%3-1)*.008f,.34f-t*.12f);
                        v.Add(start+offset);
                    }
                    int material=(tooth+row*2)%4==1?1:0;
                    for(int r=0;r<rings-1;r++)for(int j=0;j<radial;j++)
                    {
                        int a=first+r*radial+j,b=first+r*radial+(j+1)%radial;
                        if(row==0)indices[material].AddRange(new[]{a,b,a+radial,b,b+radial,a+radial});
                        else indices[material].AddRange(new[]{a,a+radial,b,b,a+radial,b+radial});
                    }
                }
            }
            // Small raised tongue, visible only behind the lower teeth.
            int tongue = v.Count; const int lon = 24, lat = 12;
            for(int y=0;y<=lat;y++) for(int x=0;x<=lon;x++)
            {
                float a=x*Mathf.PI*2/lon,b=y*Mathf.PI/lat;
                v.Add(new Vector3(Mathf.Cos(a)*Mathf.Sin(b)*.084f,-height*.76f+Mathf.Cos(b)*.022f,-.08f+Mathf.Sin(a)*Mathf.Sin(b)*.07f));
            }
            for(int y=0;y<lat;y++)for(int x=0;x<lon;x++)
            {
                int a=tongue+y*(lon+1)+x,b=a+1,c=a+lon+1;
                indices[4].AddRange(new[]{a,b,c,b,c+1,c});
            }
            closed=v.ToArray();
            for(int i=0;i<v.Count;i++)v[i]=toothOpened.TryGetValue(i,out var target)?target:Sculpt(p.mouth+v[i],p)-p.mouth;
            var mesh = new Mesh { name = "Cyclopse hinged cabinet jaw and 22 irregular teeth", subMeshCount=5 };
            mesh.SetVertices(v); mesh.uv=Enumerable.Range(0,v.Count).Select(i=>toothUV.TryGetValue(i,out var uv)?uv:Vector2.zero).ToArray(); for(int i=0;i<5;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static void ExportAuthoring(SkinnedMeshRenderer skin, Vector3[] neutral, Vector3[] opened, Mesh mouth, Vector3 centre, Vector3[] mouthUV, Vector3[] closed)
        {
            string folder="Builds/CyclopseCabinetFace/authoring"; Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/face.json",JsonUtility.ToJson(new Authoring { neutral=neutral,opened=opened,uv=skin.sharedMesh.uv,
                triangles=skin.sharedMesh.triangles,mouthUV=mouthUV,mouthCentre=centre },true));
            File.WriteAllText(folder+"/mouth.json",JsonUtility.ToJson(new MouthAuthoring { vertices=mouth.vertices, closed=closed, uv=mouth.uv,
                parts=Enumerable.Range(0,mouth.subMeshCount).Select(i=>new Part { triangles=mouth.GetTriangles(i) }).ToArray() },true));
        }
        [Serializable] sealed class Authoring { public Vector3[] neutral,opened,mouthUV; public Vector2[] uv; public int[] triangles; public Vector3 mouthCentre; }
        [Serializable] sealed class Part { public int[] triangles; }
        [Serializable] sealed class MouthAuthoring { public Vector3[] vertices,closed; public Vector2[] uv; public Part[] parts; }
    }
}
