using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace HappyToy.V2.Editor
{
    // Background-only geometry review; outputs are disposable build artifacts.
    public static class FlickerCreaturePoseReview
    {
        public static void Export()
        {
            string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            string requested=Environment.GetEnvironmentVariable("HAPPYTOY_FLICKER_CPU_REVIEW_DIR");
            string output=Path.GetFullPath(string.IsNullOrWhiteSpace(requested)?Path.Combine(builds,"FlickerLargeCpuReview"):requested);
            if(!output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Review must stay under Builds");
            if(!string.IsNullOrWhiteSpace(requested)&&Directory.Exists(output))throw new InvalidOperationException("Use a fresh CPU review folder");
            Directory.CreateDirectory(output);
            string material="newmtl skin\nKd 1 1 1\nmap_Kd "+Path.GetFullPath("Assets/Resources/SchoolFlickerCreature/creature-base.png").Replace('\\','/')+"\n";
            File.WriteAllText(Path.Combine(output,"creature.mtl"),material);
            var actor=new GameObject("CPU review actor");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SchoolFlickerCreature/creature-visual.prefab");
            var root=Object.Instantiate(prefab,actor.transform);var motion=new SchoolFlickerCreatureMotion(root.transform);
            var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh.vertexCount).First();var mesh=new Mesh();
            try
            {
                for(int inverted=0;inverted<2;inverted++)for(int tremor=0;tremor<3;tremor++)
                {
                    motion.ApplyState(0,inverted==1,tremor+1);skin.BakeMesh(mesh);var vertices=mesh.vertices;var uv=skin.sharedMesh.uv;
                    var fit=SchoolFlickerCreatureFit.Scale(motion.CurrentBounds,new Vector3(0,0,12),Quaternion.Euler(0,180,0),out var offset);
                    using(var writer=new StreamWriter(Path.Combine(output,$"pose-{inverted}-{tremor}.obj")))
                    {
                        writer.WriteLine("mtllib creature.mtl\nusemtl skin");
                        foreach(var vertex in vertices){var p=actor.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));p=Quaternion.Inverse(Quaternion.Euler(0,180,0))*offset+Vector3.Scale(p,fit);writer.WriteLine(FormattableString.Invariant($"v {p.x} {-p.z} {p.y}"));}
                        foreach(var v in uv)writer.WriteLine(FormattableString.Invariant($"vt {v.x} {v.y}"));
                        var triangles=skin.sharedMesh.triangles;
                        for(int i=0;i<triangles.Length;i+=3){int a=triangles[i]+1,b=triangles[i+1]+1,c=triangles[i+2]+1;writer.WriteLine($"f {a}/{a} {b}/{b} {c}/{c}");}
                    }
                }
                Debug.Log("FLICKER_CPU_REVIEW_EXPORTED "+output);
            }
            finally{Object.DestroyImmediate(mesh);motion.Dispose();Object.DestroyImmediate(actor);}
        }
    }
}
