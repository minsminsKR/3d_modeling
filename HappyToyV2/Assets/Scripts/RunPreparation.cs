using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // The same preparation steps serve real menu loading and immediate audit fixtures.
    public static class RunPreparation
    {
        public static IEnumerator Flatten(IEnumerator steps,System.Action<string,float> timing=null)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(steps);
            try
            {
                while(stack.Count > 0)
                {
                    var current = stack.Peek();
                    long start=timing!=null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    bool more=current.MoveNext();
                    if(timing!=null)timing(current.GetType().FullName,(float)((System.Diagnostics.Stopwatch.GetTimestamp()-start)/(double)System.Diagnostics.Stopwatch.Frequency));
                    if(!more) { (stack.Pop() as System.IDisposable)?.Dispose(); continue; }
                    if(current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
            }
            finally { while(stack.Count > 0) (stack.Pop() as System.IDisposable)?.Dispose(); }
        }
        public static void Immediate(IEnumerator steps)
        {
            using(var flat = Flatten(steps) as System.IDisposable)
                while(((IEnumerator)flat).MoveNext()) { }
        }
        public static IEnumerator Navigation(NavMeshSurface surface)
        {
            var data = new NavMeshData(surface.agentTypeID);
            data.position = surface.transform.position; data.rotation = surface.transform.rotation;
            surface.navMeshData = data;
            yield return surface.UpdateNavMesh(data);
            surface.AddData();
        }
    }
}
