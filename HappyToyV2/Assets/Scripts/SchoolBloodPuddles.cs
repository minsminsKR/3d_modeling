using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Event-owned marks affect only the school's actual pooled floor material.
    // A fixed ring limits shader cost and is rebuilt deterministically on load.
    public sealed class SchoolBloodPuddles : IDisposable
    {
        const int Capacity=16;
        static readonly int CountId=Shader.PropertyToID("_BloodMarkCount"),MarksId=Shader.PropertyToID("_BloodMarks");
        readonly Vector4[] marks=new Vector4[Capacity];
        readonly Material[] materials;
        int count,next;
        public SchoolBloodPuddles(SchoolHorrorSurfaces surfaces)
        {
            if(surfaces==null)throw new ArgumentNullException(nameof(surfaces));
            materials=new[]{surfaces.Floor,surfaces.RenewedFloor};Apply();
        }
        public void Add(Vector3 worldPosition)
        {
            marks[next]=new Vector4(worldPosition.x,worldPosition.z,.68f,1);
            next=(next+1)%Capacity;count=Mathf.Min(Capacity,count+1);Apply();
        }
        void Apply()
        {
            foreach(var material in materials)if(material){material.SetFloat(CountId,count);material.SetVectorArray(MarksId,marks);}
        }
        public void Dispose(){count=0;Apply();}
    }
}
