using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Snapped rig poses and a per-pose floor anchor, using the actual skin.
    public sealed class SchoolFlickerCreatureMotion
    {
        readonly Transform visual,owner;
        readonly Transform[] joints;
        readonly Quaternion[] rest;
        readonly Vector3[] restScale;
        readonly SkinnedMeshRenderer skin;
        readonly BoneWeight[] weights;
        readonly bool[] feet,head,leftFoot,rightFoot,torso;
        readonly Transform leftJoint,rightJoint;
        readonly Vector3 leftRest,rightRest;
        const int TremorVariants=4,CalibrationCount=32*TremorVariants;
        readonly Vector3?[] leftPositions=new Vector3?[CalibrationCount],rightPositions=new Vector3?[CalibrationCount];
        readonly Vector3?[] adjustments=new Vector3?[CalibrationCount];
        readonly Bounds?[] poseBounds=new Bounds?[CalibrationCount];
        readonly float?[] widthScales=new float?[CalibrationCount],heightScales=new float?[CalibrationCount];
        readonly Mesh baked=new Mesh();
        static readonly float[] ranges={0,14,18,48,65,24,34,85,110,24,34,85,110,0,0,0,0};
        static readonly int[] fittedSeeds={3,16,36,47,55,59,66,70,72,74,75,83,86,102,105,106};
        // Chosen from measured floor-safe poses for a fuller body silhouette.
        static readonly int[] imposingUpright={6,13};
        static readonly int[] imposingInverted={6,4,10,13};
        uint seed;
        [Serializable] public sealed class Calibration
        {
            public int version=3;public string prefabSha256,motionSha256;public float[] scales,heights;public Vector3[] offsets,leftFeet,rightFeet,boundsMin,boundsMax;
        }
        public bool Inverted {get;private set;}
        public int TremorStep {get;private set;}
        public Bounds CurrentBounds {get;private set;}
        public void SetSeed(uint value){seed=value;}
        public SchoolFlickerCreatureMotion(Transform model,bool useStoredCalibration=true)
        {
            visual=model;owner=model.parent;
            var all=visual.GetComponentsInChildren<Transform>(true);
            string[] names={"bone_0","bone_1","bone_3","bone_4","bone_5","bone_6","bone_7","bone_8","bone_9","bone_25","bone_26","bone_27","bone_28","bone_44","bone_45","bone_48","bone_49"};
            joints=names.Select(name=>all.SingleOrDefault(t=>t.name==name)??throw new InvalidOperationException("Creature joint missing: "+name)).ToArray();
            rest=joints.Select(t=>t.localRotation).ToArray();
            restScale=joints.Select(t=>t.localScale).ToArray();
            skin=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh?s.sharedMesh.triangles.Length:0).First();
            weights=skin.sharedMesh.boneWeights;
            feet=skin.bones.Select(b=>b&&(b.name=="bone_46"||b.name=="bone_47"||b.name=="bone_50"||b.name=="bone_51")).ToArray();
            head=skin.bones.Select(b=>b&&(b.name=="bone_4"||b.name=="bone_5")).ToArray();
            torso=skin.bones.Select(b=>b&&(b.name=="bone_1"||b.name=="bone_2"||b.name=="bone_3")).ToArray();
            leftFoot=skin.bones.Select(b=>b&&(b.name=="bone_46"||b.name=="bone_47")).ToArray();rightFoot=skin.bones.Select(b=>b&&(b.name=="bone_50"||b.name=="bone_51")).ToArray();
            leftJoint=all.Single(t=>t.name=="bone_46");rightJoint=all.Single(t=>t.name=="bone_50");leftRest=leftJoint.localPosition;rightRest=rightJoint.localPosition;
            if(weights.Length==0||!feet.Any(x=>x)||!head.Any(x=>x)||!torso.Any(x=>x))throw new InvalidOperationException("Creature needs weighted feet, head and torso");
            if(useStoredCalibration)
            {
                var data=Resources.Load<TextAsset>("SchoolFlickerCreature/pose-calibration");
                if(!data)throw new InvalidOperationException("Creature pose calibration is missing");
                var c=JsonUtility.FromJson<Calibration>(data.text);
                if(c==null||c.version!=3||c.scales==null||c.scales.Length!=CalibrationCount||c.heights==null||c.heights.Length!=CalibrationCount||c.offsets==null||c.offsets.Length!=CalibrationCount||c.leftFeet==null||c.leftFeet.Length!=CalibrationCount||c.rightFeet==null||c.rightFeet.Length!=CalibrationCount||c.boundsMin==null||c.boundsMin.Length!=CalibrationCount||c.boundsMax==null||c.boundsMax.Length!=CalibrationCount)throw new InvalidOperationException("Creature pose calibration is invalid");
                for(int i=0;i<CalibrationCount;i++){if(!(c.scales[i]>.1f&&c.scales[i]<10f&&c.heights[i]>.1f&&c.heights[i]<10f))throw new InvalidOperationException("Creature pose scale is invalid");widthScales[i]=c.scales[i];heightScales[i]=c.heights[i];adjustments[i]=c.offsets[i];leftPositions[i]=c.leftFeet[i];rightPositions[i]=c.rightFeet[i];var box=new Bounds();box.SetMinMax(c.boundsMin[i],c.boundsMax[i]);poseBounds[i]=box;}
            }
        }
        static float Signed(long pose,int joint,int axis)
        {
            unchecked{uint x=(uint)pose*747796405u+(uint)joint*2891336453u+(uint)axis*277803737u+1013904223u;
                x^=x>>16;x*=2246822519u;x^=x>>13;x*=3266489917u;x^=x>>16;return (x%20001)/10000f-1f;}
        }
        static bool Influenced(BoneWeight w,bool[] mask)
        {
            return w.weight0>=.5f&&mask[w.boneIndex0]||w.weight1>=.5f&&mask[w.boneIndex1]||
                w.weight2>=.5f&&mask[w.boneIndex2]||w.weight3>=.5f&&mask[w.boneIndex3];
        }
        public void Apply(double seconds)
        {
            long flash=SchoolWalkerFlicker.FlashIndex(seconds);
            int tremor=SchoolWalkerFlicker.Dark(seconds)?0:1+(int)(Math.Floor(seconds*24)%3);
            unchecked{uint x=seed^(uint)flash*0x9e3779b9u;x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;ApplyState(flash,flash>0&&(x&1)!=0,tremor);}
        }
        public void ApplyState(long flash,bool inverted,int tremor=0)
        {
            if(tremor<0||tremor>=TremorVariants)throw new ArgumentOutOfRangeException(nameof(tremor));
            int slot=(int)(flash%fittedSeeds.Length);Inverted=inverted;TremorStep=tremor;
            int poseSlot=Inverted?imposingInverted[slot%imposingInverted.Length]:
                imposingUpright[tremor==0?(slot/2)%imposingUpright.Length:0];
            visual.localPosition=Vector3.zero;visual.localRotation=Inverted?Quaternion.Euler(0,0,180):Quaternion.identity;
            ApplyPoseIndex(fittedSeeds[poseSlot]);
            if(tremor!=0)
            {
                float side=tremor==2?-1:1,twist=tremor==3?-1:1;
                // The expanded upright shoulder cannot swing down on the
                // reverse beat without piercing the floor. Keep that beat as
                // a distinct neutral twist while the inverted pose swings both ways.
                if(!Inverted&&tremor==2){side=1;twist=0;}
                float neck=Inverted?.20f:1f;
                joints[2].localRotation*=Quaternion.Euler(7*twist*neck,10*side*neck,13*side*neck);
                joints[3].localRotation*=Quaternion.Euler(-10*twist*neck,8*side*neck,-11*side*neck);
                joints[4].localRotation*=Quaternion.Euler(12*twist*neck,-14*side*neck,16*side*neck);
                joints[5].localRotation*=Quaternion.Euler(12*side,0,10*twist);
                joints[9].localRotation*=Quaternion.Euler(-12*side,0,-10*twist);
            }
            leftJoint.localPosition=leftRest;rightJoint.localPosition=rightRest;
            int key=(slot*2+(Inverted?1:0))*TremorVariants+tremor;
            if(!widthScales[key].HasValue)
            {
                float widthScale=1,heightScale=1;
                // Fit the actual posed silhouette, instead of the neutral
                // bounding box, so each visible pose fills the corridor.
                for(int iteration=0;iteration<9;iteration++)
                {
                    visual.localScale=new Vector3(widthScale,1,1);skin.BakeMesh(baked);var sample=baked.vertices;
                    float minX=float.PositiveInfinity,maxX=float.NegativeInfinity;
                    foreach(var vertex in sample){float x=owner.InverseTransformPoint(skin.transform.TransformPoint(vertex)).x;minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);}
                    float width=maxX-minX;if(width<.1f)throw new InvalidOperationException("Creature silhouette is invalid");
                    widthScale*=Mathf.Sqrt(4.20f/width);
                }
                // Preserve the enlarged body while fitting its posed skin below
                // the ceiling; each discrete pose gets its own vertical fit.
                for(int iteration=0;iteration<9;iteration++)
                {
                    visual.localScale=new Vector3(widthScale,heightScale,1);skin.BakeMesh(baked);var sample=baked.vertices;
                    float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
                    foreach(var vertex in sample){float y=owner.InverseTransformPoint(skin.transform.TransformPoint(vertex)).y;minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                    float height=maxY-minY;if(height<.1f)throw new InvalidOperationException("Creature height is invalid");
                    heightScale*=Mathf.Sqrt(3.18f/height);
                }
                visual.localScale=new Vector3(widthScale,heightScale,1);
                if(!Inverted)
                {
                    // The imported soles differ by roughly 2 cm. Adjust only
                    // the foot joint with the higher sole, without interpolating.
                    for(int pass=0;pass<5;pass++)
                    {
                        skin.BakeMesh(baked);var sample=baked.vertices;float left=float.PositiveInfinity,right=float.PositiveInfinity;
                        for(int i=0;i<sample.Length;i++)
                        {
                            float y=owner.InverseTransformPoint(skin.transform.TransformPoint(sample[i])).y;
                            if(Influenced(weights[i],leftFoot))left=Mathf.Min(left,y);
                            if(Influenced(weights[i],rightFoot))right=Mathf.Min(right,y);
                        }
                        if(float.IsInfinity(left)||float.IsInfinity(right))throw new InvalidOperationException("Creature sole weights missing");
                        float difference=left-right;if(Mathf.Abs(difference)<.002f)break;
                        var joint=difference>0?leftJoint:rightJoint;
                        joint.position+=owner.TransformVector(Vector3.up*(-Mathf.Abs(difference)));
                    }
                }
                leftPositions[key]=leftJoint.localPosition;rightPositions[key]=rightJoint.localPosition;
                skin.BakeMesh(baked);var vertices=baked.vertices;
                if(vertices.Length!=weights.Length)throw new InvalidOperationException("Creature skin/weight geometry mismatch");
                var target=Inverted?head:feet;float min=float.PositiveInfinity;double torsoX=0;int torsoCount=0;
                var bounds=new Bounds(owner.InverseTransformPoint(skin.transform.TransformPoint(vertices[0])),Vector3.zero);
                for(int i=0;i<vertices.Length;i++)
                {
                    var point=owner.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                    bounds.Encapsulate(point);
                    if(Influenced(weights[i],torso)){torsoX+=point.x;torsoCount++;}
                    if(Influenced(weights[i],target))min=Mathf.Min(min,point.y);
                }
                if(float.IsInfinity(min)||torsoCount==0)throw new InvalidOperationException("Creature ground or torso anchor has no strongly weighted vertices");
                widthScales[key]=widthScale;heightScales[key]=heightScale;adjustments[key]=new Vector3(-(float)(torsoX/torsoCount),-min,0);
                bounds.center+=adjustments[key].Value;poseBounds[key]=bounds;
            }
            leftJoint.localPosition=leftPositions[key].Value;rightJoint.localPosition=rightPositions[key].Value;
            visual.localScale=new Vector3(widthScales[key].Value,heightScales[key].Value,1);visual.localPosition=adjustments[key].Value;
            CurrentBounds=poseBounds[key].Value;
        }
        public void ApplyPoseIndex(long pose)
        {
            for(int i=0;i<joints.Length;i++)
            {
                joints[i].localScale=i==2?Vector3.Scale(restScale[i],new Vector3(10f,4f,4f)):
                    restScale[i]*((i==5||i==9)?.42f:1f);
                joints[i].localRotation=rest[i]*Quaternion.Euler(Signed(pose,i,0)*ranges[i],Signed(pose,i,1)*ranges[i],Signed(pose,i,2)*ranges[i]);
            }
        }
        public Calibration CaptureCalibration()
        {
            if(widthScales.Any(x=>!x.HasValue)||heightScales.Any(x=>!x.HasValue)||adjustments.Any(x=>!x.HasValue)||leftPositions.Any(x=>!x.HasValue)||rightPositions.Any(x=>!x.HasValue))throw new InvalidOperationException("Calibrate every pose and orientation first");
            return new Calibration{scales=widthScales.Select(x=>x.Value).ToArray(),heights=heightScales.Select(x=>x.Value).ToArray(),offsets=adjustments.Select(x=>x.Value).ToArray(),leftFeet=leftPositions.Select(x=>x.Value).ToArray(),rightFeet=rightPositions.Select(x=>x.Value).ToArray(),boundsMin=poseBounds.Select(x=>x.Value.min).ToArray(),boundsMax=poseBounds.Select(x=>x.Value.max).ToArray()};
        }
        public void Dispose(){if(baked){if(Application.isPlaying)UnityEngine.Object.Destroy(baked);else UnityEngine.Object.DestroyImmediate(baked);}}
    }
}
