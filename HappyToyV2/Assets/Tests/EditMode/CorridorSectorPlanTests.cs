using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class CorridorSectorPlanTests
    {
        object Layout(int seed)=>Activator.CreateInstance(RequireType("CorridorLayout"),seed);
        object Plan(object layout)=>Activator.CreateInstance(RequireType("CorridorSectorPlan"),layout);
        static IEnumerable<int> Neighbours(int[] connections,int cell)
        {
            for(int d=0;d<4;d++) if((connections[cell]&(1<<d))!=0)
            {int next=cell+(d==0?9:d==1?1:d==2?-9:-1);if(next>=0 && next<81)yield return next;}
        }
        [TestCase(73)] [TestCase(211)] [TestCase(42661)]
        public void AllFiveVisualRegionsAreDeterministicConnectedAndNearestByRealTopology(int seed)
        {
            var layout=Layout(seed);var before=(int[])Get<int[]>(layout,"Connections").Clone();
            var plan=Plan(layout);var sectors=Get<int[]>(plan,"Sectors");var goals=Get<int[]>(plan,"GoalCells");
            var distance=Get<int[]>(plan,"GoalDistance");var connections=Get<int[]>(plan,"Connections");
            Assert.That(sectors.Distinct().OrderBy(x=>x),Is.EqualTo(new[]{0,1,2,3,4}));
            Assert.That(Get<int[]>(Plan(Layout(seed)),"Sectors"),Is.EqualTo(sectors));
            Assert.That(Get<int[]>(layout,"Connections"),Is.EqualTo(before),"Visual plan changed physical topology");
            var nearest=Enumerable.Repeat(int.MaxValue,81).ToArray();
            for(int sector=0;sector<5;sector++)
            {
                Assert.That(sectors[goals[sector]],Is.EqualTo(sector));
                var visited=new HashSet<int>{goals[sector]};var queue=new Queue<int>();queue.Enqueue(goals[sector]);
                while(queue.Count>0) foreach(int next in Neighbours(connections,queue.Dequeue()))
                    if(sectors[next]==sector && visited.Add(next)) queue.Enqueue(next);
                Assert.That(visited.Count,Is.EqualTo(sectors.Count(x=>x==sector)),"Disconnected arbitrary colour islands");
                var fromGoal=Enumerable.Repeat(-1,81).ToArray();fromGoal[goals[sector]]=0;queue.Enqueue(goals[sector]);
                while(queue.Count>0)
                {int cell=queue.Dequeue();foreach(int next in Neighbours(connections,cell))if(fromGoal[next]<0){fromGoal[next]=fromGoal[cell]+1;queue.Enqueue(next);}}
                for(int cell=0;cell<81;cell++)nearest[cell]=Math.Min(nearest[cell],fromGoal[cell]);
            }
            Assert.That(distance,Is.EqualTo(nearest),"Regions do not follow actual shortest route to a goal");
        }
        [TestCase(73)] [TestCase(211)]
        public void NumberedBranchMarksExistOnlyAtRealThreeOrFourWayJunctions(int seed)
        {
            var layout=Layout(seed);var plan=Plan(layout);var connections=Get<int[]>(plan,"Connections");
            var branches=Get<int[]>(plan,"BranchOrdinals");int counted=0;
            for(int cell=0;cell<81;cell++)
            {
                int degree=(int)Call(RequireType("CorridorSectorPlan"),"Degree",connections[cell]);
                Assert.That(branches[cell]>0,Is.EqualTo(degree>=3));if(degree>=3)counted++;
            }
            Assert.That(Get<int>(plan,"BranchCount"),Is.EqualTo(counted));
            Assert.That(branches.Where(x=>x>0).OrderBy(x=>x),Is.EqualTo(Enumerable.Range(1,counted)));
            Assert.That(Get<int[]>(Plan(Layout(seed)),"BranchOrdinals"),Is.EqualTo(branches));
        }
    }
}
