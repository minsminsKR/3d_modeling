using System;
using System.Collections.Generic;
using System.Linq;

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
        public static ShadowAtlasPlan Build(int atlas,int torchSize,int localSize,bool torchActive,IEnumerable<Candidate> candidates)
        {
            if(atlas<=0 || torchSize<=0 || localSize<=0 || torchSize>atlas || localSize>torchSize ||
                atlas%torchSize!=0 || torchSize%localSize!=0)
                throw new ArgumentException("Shadow tile sizes must divide the atlas and torch footprint");
            var plan=new ShadowAtlasPlan();long available=(long)atlas*atlas;
            if(torchActive){plan.pixels=(long)torchSize*torchSize;plan.faces=1;}
            foreach(var candidate in candidates.OrderBy(x=>x.distanceSquared).ThenBy(x=>x.id))
            {
                if(candidate.point && plan.pointCasters>=2)continue;
                int faces=candidate.point?6:1;long cost=(long)localSize*localSize*faces;
                if(plan.pixels+cost>available)continue;
                plan.selected.Add(candidate.id);plan.pixels+=cost;plan.faces+=faces;
                if(candidate.point)plan.pointCasters++;else plan.spotCasters++;
            }
            return plan;
        }
    }
}
