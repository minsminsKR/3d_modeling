using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // The visible door and its physical leaf rotate around the same hinge.
    public sealed class SchoolMovingDoors : MonoBehaviour
    {
        sealed class Door { public Transform hinge;public Collider leaf; }
        readonly List<Door> doors=new List<Door>();
        GameSession session;bool active;float elapsed;
        public int Count=>doors.Count;
        public float Elapsed=>elapsed;
        public void RestoreElapsed(float value){elapsed=value;Tick(0,true);}
        public void Prepare(GameSession owner){session=owner;}
        public void Register(Transform hinge,Collider leaf){doors.Add(new Door{hinge=hinge,leaf=leaf});}
        public void SetActive(bool value)
        {
            active=value;elapsed=0;
            if(!active)foreach(var door in doors)if(door.hinge)door.hinge.localRotation=Quaternion.identity;
        }
        public void Tick(float delta,bool advancing)
        {
            if(!active||!advancing)return;
            elapsed=Mathf.Min(1000000,elapsed+Mathf.Max(0,delta));
            var player=session&&session.player?session.player.GetComponent<CharacterController>():null;
            for(int i=0;i<doors.Count;i++)
            {
                var door=doors[i];if(!door.hinge)continue;
                float amount=SchoolFrenzyMotion.Amount(elapsed,i,0);
                float from=door.hinge.localEulerAngles.y,to=-85*amount;
                float arc=Mathf.DeltaAngle(from,to);
                int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(arc)/5));
                var safe=door.hinge.localRotation;
                for(int step=1;step<=steps;step++)
                {
                    door.hinge.localRotation=Quaternion.Euler(0,from+arc*step/steps,0);
                    if(player&&door.leaf&&door.leaf.enabled&&Physics.ComputePenetration(door.leaf,door.leaf.transform.position,door.leaf.transform.rotation,
                        player,player.transform.position,player.transform.rotation,out _,out _)){door.hinge.localRotation=safe;break;}
                    safe=door.hinge.localRotation;
                }
            }
        }
    }
}
