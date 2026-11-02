using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // The body is a source-derived seated pose; only the head pivot tracks.
    public sealed class SchoolStudyStudents : MonoBehaviour
    {
        sealed class Student
        {
            public Transform root,head;
            public Quaternion rest;
        }
        readonly List<Student> students=new List<Student>();
        readonly List<GameObject> roomLights=new List<GameObject>();
        readonly List<Material> studentMaterials=new List<Material>();
        public int Count=>students.Count;
        public void Prepare(SchoolLoopWorld world)
        {
            if(!world)throw new ArgumentNullException(nameof(world));
            transform.SetParent(world.Root,false);
            var sources=new GameObject[3];
            for(int i=0;i<sources.Length;i++)
            {
                sources[i]=Resources.Load<GameObject>($"SchoolStudyStudents/student0{i+1}/student-visual");
                if(!sources[i])throw new InvalidOperationException("Reviewed seated student prefab is missing: "+(i+1));
                var sourceMaterial=sources[i].GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial;
                var readable=new Material(sourceMaterial);readable.SetTexture("_EmissionMap",sourceMaterial.GetTexture("_BaseMap"));
                readable.SetColor("_EmissionColor",new Color(.8f,.85f,.9f));readable.EnableKeyword("_EMISSION");studentMaterials.Add(readable);
            }
            foreach(Transform section in world.Root)
            {
                if(!section.name.StartsWith("School loop section ",StringComparison.Ordinal))continue;
                var illumination=new GameObject("Study ghost room visibility light");illumination.transform.SetParent(section,false);
                illumination.transform.localPosition=new Vector3(-5.8f,2.65f,18.2f);
                var light=illumination.AddComponent<Light>();light.type=LightType.Point;light.range=7;light.intensity=11;
                light.color=new Color(.72f,.77f,.79f);light.shadows=LightShadows.None;roomLights.Add(illumination);
                for(int row=0;row<3;row++)for(int col=0;col<3;col++)
                {
                    float x=-8.05f+col*1.80f,z=16.15f+row*1.86f-.66f;
                    float yaw=row==1&&col==2?11:row==2&&col==0?-16:0;
                    if(col==2&&row==1)x+=.13f;
                    var instance=Instantiate(sources[(row*3+col)%sources.Length],section,false);
                    instance.name="Study room ghost student "+row+"-"+col;
                    foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.sharedMaterial=studentMaterials[(row*3+col)%sources.Length];
                    instance.transform.localPosition=new Vector3(x,0,z);
                    instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
                    Transform head=null;
                    foreach(var child in instance.GetComponentsInChildren<Transform>(true))
                        if(child.name=="HeadPivot"){head=child;break;}
                    if(!head)throw new InvalidOperationException("Seated student head pivot is missing");
                    students.Add(new Student{root=instance.transform,head=head,rest=head.localRotation});
                }
            }
        }
        public void Tick(Vector3 playerEyes,bool advancing,float delta)
        {
            if(!advancing)return;
            float blend=1-Mathf.Exp(-4*Mathf.Max(0,delta));
            foreach(var student in students)
            {
                if(!student.head||!student.root)continue;
                var direction=playerEyes-student.head.position;direction.y=0;
                if(direction.sqrMagnitude<.0001f)continue;
                float targetYaw=Mathf.Clamp(Vector3.SignedAngle(student.root.forward,direction,Vector3.up),-105,105);
                Quaternion resting=student.head.parent.rotation*student.rest;
                student.head.rotation=Quaternion.Slerp(student.head.rotation,Quaternion.AngleAxis(targetYaw,student.root.up)*resting,blend);
            }
        }
        void OnDestroy()
        {
            foreach(var student in students)if(student.root)Destroy(student.root.gameObject);
            foreach(var light in roomLights)if(light)Destroy(light);
            foreach(var material in studentMaterials)if(material)Destroy(material);
        }
    }
}
