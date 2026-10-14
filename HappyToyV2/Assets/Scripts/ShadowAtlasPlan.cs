using System;
using System.Collections.Generic;

namespace HappyToy.V2
{
    // All local tiles have the same size, and the torch occupies a power-of-two
    // square aligned to those tiles. The area bound therefore also bounds packing.
    public sealed class ShadowAtlasPlan
    {
        public struct Candidate { public int id; public bool point; public float distanceSquared; }
        public readonly List<int> selected=new List<int>();
        public long pixels;
        public int pointCasters,spotCasters,faces;
        sealed class DistanceOrder : IComparer<Candidate>
        {
            public int Compare(Candidate a,Candidate b)
            {int distance=a.distanceSquared.CompareTo(b.distanceSquared);return distance!=0?distance:a.id.CompareTo(b.id);}
        }
        static readonly IComparer<Candidate> Order=new DistanceOrder();
        public static ShadowAtlasPlan Build(int atlas,int torchSize,int localSize,bool torchActive,IEnumerable<Candidate> candidates)
        {
            var plan=new ShadowAtlasPlan();
            plan.Rebuild(atlas,torchSize,localSize,torchActive,new List<Candidate>(candidates));return plan;
        }
        // Own and reuse the output and caller's scratch list; ordering is intentionally
        // in-place. No per-frame LINQ buffers, result object or selection list.
        public void Rebuild(int atlas,int torchSize,int localSize,bool torchActive,List<Candidate> candidates)
        {
            if(atlas<=0 || torchSize<=0 || localSize<=0 || torchSize>atlas || localSize>torchSize ||
                atlas%torchSize!=0 || torchSize%localSize!=0)
                throw new ArgumentException("Shadow tile sizes must divide the atlas and torch footprint");
            selected.Clear();pixels=0;pointCasters=spotCasters=faces=0;
            long available=(long)atlas*atlas;
            if(torchActive){pixels=(long)torchSize*torchSize;faces=1;}
            candidates.Sort(Order);
            foreach(var candidate in candidates)
            {
                if(candidate.point && pointCasters>=2)continue;
                int count=candidate.point?6:1;long cost=(long)localSize*localSize*count;
                if(pixels+cost>available)continue;
                selected.Add(candidate.id);pixels+=cost;faces+=count;
                if(candidate.point)pointCasters++;else spotCasters++;
            }
        }
    }
}
