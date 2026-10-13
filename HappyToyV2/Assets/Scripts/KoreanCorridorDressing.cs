using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

namespace HappyToy.V2
{
    // Seeded placement of generated props. Keep every central path and the
    // complete sliding-door travel volume clear of decorative collisions.
    public sealed class KoreanCorridorDressing : MonoBehaviour
    {
        public readonly struct Zone
        {
            public readonly int Cell, Connections;
            public readonly Vector3 Position;
            public readonly bool Wide;
            public Zone(int cell, int connections, Vector3 position, bool wide)
            { Cell=cell; Connections=connections; Position=position; Wide=wide; }
        }
        readonly Collider[] hits=new Collider[48];
        readonly List<KoreanCorridorProp> props=new List<KoreanCorridorProp>();
        public IReadOnlyList<KoreanCorridorProp> Props=>props;
        Interactable[] doors;
        static readonly Vector3[] Directions={Vector3.forward,Vector3.right,Vector3.back,Vector3.left};
        bool Clear(Vector3 bottom, Vector3 size, Quaternion rotation)
        {
            var centre=bottom+Vector3.up*size.y*.5f;
            var reservation=new Bounds(centre,rotation*size);
            reservation.size=new Vector3(Mathf.Abs(reservation.size.x),size.y,Mathf.Abs(reservation.size.z));
            reservation.Expand(.16f);
            foreach(var door in doors)
            {
                if(!door.movingLeaf) continue;
                var shift=door.movingLeaf.parent.TransformVector(door.openOffset);
                foreach(var collider in door.movingLeaf.GetComponentsInChildren<Collider>())
                {
                    if(collider.isTrigger) continue;
                    var sweep=collider.bounds; var moved=sweep; moved.center+=shift; sweep.Encapsulate(moved);
                    if(sweep.Intersects(reservation)) return false;
                }
            }
            int count=Physics.OverlapBoxNonAlloc(centre,size*.5f+Vector3.one*.06f,hits,rotation,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length) return false;
            for(int i=0;i<count;i++) if(hits[i] && hits[i].bounds.max.y>.10f) return false;
            return true;
        }
        public void Prepare(Zone[] zones,int seed)
        {
            doors=transform.parent.GetComponentsInChildren<Interactable>(true);
            var originals=new Dictionary<string,GameObject>();
            foreach(string key in new[]{"jangseung","onggi","dokkaebi-tile"})
            {
                var original=Resources.Load<GameObject>("KoreanCorridorProps/"+key);
                if(!original) throw new InvalidOperationException("Generated Korean prop missing: "+key);
                originals.Add(key,original);
            }
            foreach(var zone in zones)
            {
                if(!zone.Wide) continue;
                // Sparse landmarks, paired household jars and occasional wall
                // guardians support the horror setting without filling the route.
                var keys=new List<string>{"onggi"};
                if((zone.Cell+seed)%3==0) keys.Add("jangseung");
                if((zone.Cell+seed)%2==0) keys.Add("dokkaebi-tile");
                foreach(string key in keys)
                {
                    Vector3 size=key=="jangseung"?new Vector3(.36f,1.4f,.30f):key=="onggi"?new Vector3(.53f,.65f,.53f):new Vector3(.50f,.48f,.16f);
                    bool placed=false;
                    for(int candidate=0;candidate<8 && !placed;candidate++)
                    {
                        int direction=(candidate/2+zone.Cell+seed)&3;
                        if((zone.Connections&(1<<direction))!=0) continue;
                        var normal=Directions[direction]; var along=Vector3.Cross(Vector3.up,normal);
                        float offset=candidate%2==0?-2.05f:2.05f;
                        var at=zone.Position+normal*(key=="dokkaebi-tile"?2.73f:2.53f)+along*offset;
                        at.y=key=="dokkaebi-tile"?1.68f:0;
                        var rotation=Quaternion.LookRotation(-normal);
                        if(!Clear(at,size,rotation)) continue;
                        var root=Instantiate(originals[key],at,rotation,transform); root.name="Korean corridor "+key+" — cell "+zone.Cell;
                        var marker=root.AddComponent<KoreanCorridorProp>(); marker.Configure(key,zone.Cell); props.Add(marker);
                        if(key!="dokkaebi-tile")
                        {
                            var collision=root.AddComponent<BoxCollider>(); collision.center=Vector3.up*size.y*.5f; collision.size=size;
                            var navigation=root.AddComponent<NavMeshModifier>(); navigation.overrideArea=true; navigation.area=1;
                        }
                        placed=true; Physics.SyncTransforms();
                    }
                }
            }
            if(props.Count<6) throw new InvalidOperationException("No clear Korean dressing placements in the generated corridor");
        }
    }
}
