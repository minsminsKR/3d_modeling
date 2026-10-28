using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Keep the weighted visible contact skin on the school floor after tremor.
    // Sample contact vertices from Unity's baked skin. Recalculate only when
    // the discrete pose/tremor step changes; never bake on unchanged frames.
    public sealed class SchoolFlickerCreatureGrounding : IDisposable
    {
        readonly Transform visual,section,leftJoint,rightJoint;
        readonly SkinnedMeshRenderer skin;
        readonly Mesh baked=new Mesh();
        readonly List<Vector3> samples=new List<Vector3>();
        readonly BoneWeight[] weights;
        readonly Transform[] bones;
        readonly int[] left,right,head;
        long cachedFlash=long.MinValue,cachedStep=long.MinValue;
        bool cachedInverted,cachedDark;
        Vector3 leftDelta,rightDelta;
        float visualDelta;
        const float SurfaceHeight=.002f;
        public SchoolFlickerCreatureGrounding(Transform visual,Transform section)
        {
            if(!visual||!section)throw new ArgumentException("Creature and school floor required");
            this.visual=visual;this.section=section;
            skin=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh?s.sharedMesh.triangles.Length:0).First();
            var mesh=skin.sharedMesh;weights=mesh.boneWeights;bones=skin.bones;
            var l=new List<int>();var r=new List<int>();var h=new List<int>();
            for(int i=0;i<weights.Length;i++)
            {
                if(Has(weights[i],"bone_46","bone_47"))l.Add(i);
                if(Has(weights[i],"bone_50","bone_51"))r.Add(i);
                if(Has(weights[i],"bone_4","bone_5"))h.Add(i);
            }
            left=l.ToArray();right=r.ToArray();head=h.ToArray();
            if(left.Length==0||right.Length==0||head.Length==0)throw new InvalidOperationException("Creature floor-contact weights missing");
            var transforms=visual.GetComponentsInChildren<Transform>(true);
            leftJoint=transforms.Single(t=>t.name=="bone_46");rightJoint=transforms.Single(t=>t.name=="bone_50");
        }
        bool Has(BoneWeight w,string a,string b)
        {
            bool Match(int i,float weight)=>weight>=.5f&&bones[i]&&(bones[i].name==a||bones[i].name==b);
            return Match(w.boneIndex0,w.weight0)||Match(w.boneIndex1,w.weight1)||Match(w.boneIndex2,w.weight2)||Match(w.boneIndex3,w.weight3);
        }
        void RefreshMatrices()
        {
            // Use Unity's own skinning and scale convention, matching the
            // rendered mesh and the independent runtime contact check.
            skin.BakeMesh(baked);baked.GetVertices(samples);
        }
        float Minimum(int[] indices)
        {
            float min=float.PositiveInfinity;
            foreach(int i in indices)min=Mathf.Min(min,skin.transform.TransformPoint(samples[i]).y);
            return min;
        }
        public void Dispose()
        {
            if(!baked)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(baked);
            else UnityEngine.Object.DestroyImmediate(baked);
        }
        public void Apply(double seconds,bool inverted)
        {
            long flash=SchoolWalkerFlicker.FlashIndex(seconds);
            bool dark=SchoolWalkerFlicker.Dark(seconds);
            long step=dark?0:(long)Math.Floor(Math.Max(0,seconds)*14);
            if(flash!=cachedFlash||step!=cachedStep||dark!=cachedDark||inverted!=cachedInverted)
            {
                cachedFlash=flash;cachedStep=step;cachedDark=dark;cachedInverted=inverted;
                var l0=leftJoint.localPosition;var r0=rightJoint.localPosition;
                if(!inverted)
                {
                    // Equalize both sole contacts; this changes only the higher
                    // ankle and is repeated for blended-weight seam vertices.
                    for(int n=0;n<6;n++)
                    {
                        RefreshMatrices();float a=Minimum(left),b=Minimum(right),difference=a-b;
                        if(Mathf.Abs(difference)<.001f)break;
                        var joint=difference>0?leftJoint:rightJoint;
                        joint.position+=Vector3.down*Mathf.Abs(difference);
                    }
                }
                leftDelta=leftJoint.localPosition-l0;rightDelta=rightJoint.localPosition-r0;
                RefreshMatrices();float contact=inverted?Minimum(head):Mathf.Min(Minimum(left),Minimum(right));
                float floorY=section.TransformPoint(new Vector3(0,SurfaceHeight,0)).y;
                visualDelta=floorY-contact;
                visual.position+=Vector3.up*visualDelta;
            }
            else
            {
                leftJoint.localPosition+=leftDelta;rightJoint.localPosition+=rightDelta;
                visual.position+=Vector3.up*visualDelta;
            }
        }
    }
}
