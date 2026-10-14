using System;
using System.Collections.Generic;
using System.Linq;

namespace HappyToy.V2
{
    // Actual sight/floor/distance evidence is supplied by the Unity boundary.
    // Multiple witnesses each advance once per entry; quiet use never erases memory.
    public sealed class CabinetTauntMemory
    {
        public const int CaptureAt = 3;
        public const float CloseDistance = 4, EscapeDistance = 12;
        [Serializable] public sealed class Visit
        {
            public string cabinet, observer;
            public int count, entry;
            public float x,y,z;
            internal Visit Copy() => (Visit)MemberwiseClone();
        }
        readonly List<Visit> visits = new List<Visit>();
        public int Count(string cabinet,string observer) => visits.FirstOrDefault(v=>v.cabinet==cabinet && v.observer==observer)?.count??0;
        public bool Remembers(string observer,float x,float y,float z) => visits.Any(v=>v.observer==observer &&
            (v.x-x)*(v.x-x)+(v.y-y)*(v.y-y)+(v.z-z)*(v.z-z)<.01f);
        public bool Observe(int entry,string cabinet,string observer,float x,float y,float z,float distance)
        {
            if(entry<=0 || string.IsNullOrEmpty(cabinet) || string.IsNullOrEmpty(observer) ||
                !Finite(x)||!Finite(y)||!Finite(z)||!Finite(distance)||distance<0)throw new ArgumentException("Invalid cabinet evidence");
            if(distance>CloseDistance)return false;
            var visit=visits.FirstOrDefault(v=>v.cabinet==cabinet && v.observer==observer);
            if(visit==null){visit=new Visit {cabinet=cabinet,observer=observer,x=x,y=y,z=z};visits.Add(visit);}
            if(entry<visit.entry)throw new ArgumentException("Cabinet entry cannot move backwards");
            if(entry!=visit.entry){visit.entry=entry;visit.count=Math.Min(CaptureAt,visit.count+1);}
            return visit.count>=CaptureAt;
        }
        public void ForgetDistant(float x,float y,float z)
        {
            if(!Finite(x)||!Finite(y)||!Finite(z))throw new ArgumentException("Invalid player position");
            visits.RemoveAll(v=>(v.x-x)*(v.x-x)+(v.y-y)*(v.y-y)+(v.z-z)*(v.z-z)>EscapeDistance*EscapeDistance);
        }
        public Visit[] Capture() => visits.Select(v=>v.Copy()).ToArray();
        public static void Validate(Visit[] snapshot,int entry)
        {
            if(snapshot==null)return; // Older saves have no taunt history.
            if(snapshot.Length>4096)throw new ArgumentException("Too many cabinet encounters");
            var keys=new HashSet<string>();
            foreach(var v in snapshot)
                if(v==null || string.IsNullOrEmpty(v.cabinet)||string.IsNullOrEmpty(v.observer) ||
                    v.count<1||v.count>CaptureAt||v.count>v.entry||v.entry<1||v.entry>entry||!Finite(v.x)||!Finite(v.y)||!Finite(v.z)||
                    !keys.Add(v.cabinet+"\n"+v.observer))throw new ArgumentException("Invalid cabinet taunt history");
        }
        public void Restore(Visit[] snapshot,int entry)
        { Validate(snapshot,entry);visits.Clear();if(snapshot!=null)visits.AddRange(snapshot.Select(v=>v.Copy())); }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
