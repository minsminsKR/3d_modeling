using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Only the explicit native input audit creates this owner. Restore the two
    // original speed fields before interactions, danger and measured patrols.
    public sealed class NativeAuditMovement : MonoBehaviour
    {
        [Serializable] public sealed class Evidence
        {
            public float requestedMultiplier=1,originalWalkSpeed,originalRunSpeed,boostedGameSeconds,boostedPhysicalMeters;
            public int boostedSegments;public bool restored;
            public string scope="Diagnostic clear-straight travel acceleration only. Corners, doors, item input, danger and the optional four-enemy patrol use original speeds. AI, time scale, input, collisions and finite stocks are unchanged. Accelerated/transition render intervals are excluded from normal-speed performance evidence.";
        }
        PlayerMotor player;
        public Evidence Data {get;private set;}
        public bool Active {get;private set;}
        public float AppliedMultiplier=>Active?Data.requestedMultiplier:1;
        public static NativeAuditMovement Attach(PlayerMotor player,float multiplier)
        {
            if(float.IsNaN(multiplier)||float.IsInfinity(multiplier)||multiplier<1||multiplier>12)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            var owner=player.gameObject.AddComponent<NativeAuditMovement>();owner.player=player;
            owner.Data=new Evidence{requestedMultiplier=multiplier,originalWalkSpeed=player.walkSpeed,originalRunSpeed=player.runSpeed,restored=true};
            return owner;
        }
        public void Apply(bool eligible)
        {
            bool next=eligible&&Data.requestedMultiplier>1;
            if(next==Active)return;
            if(next)Data.boostedSegments++;
            Active=next;player.walkSpeed=Data.originalWalkSpeed*AppliedMultiplier;player.runSpeed=Data.originalRunSpeed*AppliedMultiplier;
            Data.restored=!Active&&player.walkSpeed==Data.originalWalkSpeed&&player.runSpeed==Data.originalRunSpeed;
        }
        public void ObserveStep(float metres,float gameSeconds)
        {
            if(!Active)return;
            Data.boostedPhysicalMeters+=metres;Data.boostedGameSeconds+=Mathf.Max(0,gameSeconds);
        }
        public void Restore()
        {
            if(!player||Data==null)return;
            Active=false;player.walkSpeed=Data.originalWalkSpeed;player.runSpeed=Data.originalRunSpeed;
            Data.restored=player.walkSpeed==Data.originalWalkSpeed&&player.runSpeed==Data.originalRunSpeed;
        }
        void OnDisable()=>Restore();
        void OnDestroy()=>Restore();
    }
}
