using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Event-owned marks affect only the school's actual pooled floor material.
    // A fixed ring limits shader cost and is rebuilt deterministically on load.
    public sealed class SchoolBloodPuddles : IDisposable
    {
        const int Capacity=16;
        static readonly int CountId=Shader.PropertyToID("_BloodMarkCount"),MarksId=Shader.PropertyToID("_BloodMarks"),SpreadId=Shader.PropertyToID("_BloodSpread");
        readonly Vector4[] marks=new Vector4[Capacity];
        readonly List<Material> materials;
        int count,next;
        float progress=-1;
        public SchoolBloodPuddles(SchoolHorrorSurfaces surfaces)
        {
            if(surfaces==null)throw new ArgumentNullException(nameof(surfaces));
            materials=new List<Material>{surfaces.Floor,surfaces.RenewedFloor,surfaces.Puddle};
            materials.AddRange(surfaces.AdditionalWater);Apply();SetElapsed(0);
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
        public void SetElapsed(float elapsed)
        {
            float value=SchoolBloodSpread.Progress(elapsed);
            if(value==progress)return;progress=value;
            foreach(var material in materials)if(material)material.SetFloat(SpreadId,value);
        }
        public void Dispose(){count=0;Apply();progress=-1;SetElapsed(0);}
    }
}
