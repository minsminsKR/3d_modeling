using System;
using System.Collections.Generic;
using System.Linq;

namespace HappyToy.V2
{
    // Visual identity follows real connected routes, rather than an arbitrary colour grid.
    // This plan never changes connections, spawn points, perception or navigation.
    public sealed class CorridorSectorPlan
    {
        public readonly int[] Sectors = new int[CorridorLayout.Count];
        public readonly int[] GoalDistance = new int[CorridorLayout.Count];
        public readonly int[] BranchOrdinals = new int[CorridorLayout.Count];
        public readonly int[] GoalCells;
        public readonly int[] Connections;
        public int BranchCount { get; private set; }
        public CorridorSectorPlan(CorridorLayout layout)
        {
            if(layout==null || layout.Relics.Length!=5 || layout.Relics.Distinct().Count()!=5)
                throw new ArgumentException("Five distinct connected corridor goals required");
            GoalCells=(int[])layout.Relics.Clone(); Connections=(int[])layout.Connections.Clone();
            for(int cell=0;cell<Sectors.Length;cell++) Sectors[cell]=GoalDistance[cell]=-1;
            var queue=new Queue<int>();
            // Tie priority changes with the seed, but the same seed reconstructs identical regions.
            foreach(int sector in Enumerable.Range(0,5).OrderBy(i=>Tie(layout.Seed,i)))
            { int goal=GoalCells[sector]; Sectors[goal]=sector; GoalDistance[goal]=0; queue.Enqueue(goal); }
            while(queue.Count>0)
            {
                int cell=queue.Dequeue();
                foreach(int next in layout.Neighbors(cell))
                    if(Sectors[next]<0)
                    { Sectors[next]=Sectors[cell]; GoalDistance[next]=GoalDistance[cell]+1; queue.Enqueue(next); }
            }
            if(Sectors.Any(x=>x<0)) throw new ArgumentException("Corridor contains unreachable cells");
            var branches=Enumerable.Range(0,CorridorLayout.Count).Where(cell=>Degree(Connections[cell])>=3)
                .OrderBy(cell=>layout.Distance[cell]).ThenBy(cell=>Tie(layout.Seed,cell)).ToArray();
            for(int i=0;i<branches.Length;i++) BranchOrdinals[branches[i]]=i+1;
            BranchCount=branches.Length;
        }
        static uint Tie(int seed,int value)
        {
            uint hash=unchecked((uint)seed*747796405u+(uint)(value+1)*2891336453u);
            hash=(hash^(hash>>16))*2246822519u; return hash^(hash>>13);
        }
        public static int Degree(int mask)
        { int count=0;for(int d=0;d<4;d++) if((mask&(1<<d))!=0) count++;return count; }
    }
}
