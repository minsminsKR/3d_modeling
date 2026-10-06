using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // A managed evidence clock. Travel time never consumes inspection dwell;
    // only actual arrival can mark the observed sound position as investigated.
    public sealed class NoiseInvestigationClock
    {
        public const float ArrivalDistance = .65f, MaximumTravel = 90f;
        public bool Active { get; private set; }
        public bool Arrived { get; private set; }
        public Vector3 Point { get; private set; }
        public float TravelRemaining { get; private set; }
        public float DwellRemaining { get; private set; }
        public float DwellDuration { get; private set; }
        public enum Step { Travelling, Inspecting, Expired }

        [Serializable] public sealed class Progress
        {
            public int version = 1;
            public bool active, arrived;
            public Vector3 point;
            public float travelRemaining, dwellRemaining, dwellDuration;
            public bool LegacyEmpty => !active && !arrived && point == Vector3.zero &&
                travelRemaining == 0 && dwellRemaining == 0 && dwellDuration == 0;
            public void Validate()
            {
                if (version < 0 || version > 1 || version == 0 && !LegacyEmpty ||
                    !CorridorCheckpoint.Vector(point) ||
                    !CorridorCheckpoint.Number(travelRemaining,0,MaximumTravel) ||
                    !CorridorCheckpoint.Number(dwellDuration,0,3600) ||
                    !CorridorCheckpoint.Number(dwellRemaining,0,dwellDuration) ||
                    active && dwellDuration <= 0 || arrived && travelRemaining != 0)
                    throw new ArgumentException("Invalid noise investigation clock");
            }
        }

        public void Begin(Vector3 point,float dwellSeconds,float routeLength,float authoredSpeed,int closedDoors)
        {
            if (!CorridorCheckpoint.Vector(point) || !CorridorCheckpoint.Number(dwellSeconds,0,3600) || dwellSeconds<=0 ||
                !StealthRules.Finite(routeLength) || routeLength < 0 || !StealthRules.Finite(authoredSpeed) || authoredSpeed < 0 ||
                closedDoors < 0 || closedDoors > 128) throw new ArgumentException("Invalid observed investigation route");
            Point=point; Active=true; Arrived=false; DwellDuration=DwellRemaining=dwellSeconds;
            // The minimum speed bounds fixture/disabled locomotion waits; it does
            // not change any actual actor speed. Four seconds covers a push+slide.
            double budget=routeLength/Math.Max(.25,authoredSpeed)*1.25+2+closedDoors*4;
            TravelRemaining=(float)Math.Max(3,Math.Min(MaximumTravel,budget));
        }
        public Step Tick(float seconds,float distance,bool playing)
        {
            if (!StealthRules.Finite(seconds) || seconds < 0 || !StealthRules.Finite(distance) || distance < 0)
                throw new ArgumentException("Invalid investigation clock step");
            if (!Active) return Step.Expired;
            if (!playing) return Arrived ? Step.Inspecting : Step.Travelling;
            if (!Arrived)
            {
                if (distance <= ArrivalDistance)
                {
                    Arrived=true; TravelRemaining=0;
                    return Step.Inspecting; // Arrival frame belongs to travel, not dwell.
                }
                TravelRemaining=Math.Max(0,TravelRemaining-Math.Min(seconds,TravelRemaining));
                if (TravelRemaining > 0) return Step.Travelling;
                Active=false; return Step.Expired;
            }
            DwellRemaining=Math.Max(0,DwellRemaining-Math.Min(seconds,DwellRemaining));
            if (DwellRemaining > 0) return Step.Inspecting;
            Active=false; return Step.Expired;
        }
        public void Cancel() { Active=false; }
        public void Reset()
        { Active=Arrived=false; Point=Vector3.zero; TravelRemaining=DwellRemaining=DwellDuration=0; }
        public Progress Capture() => new Progress { active=Active,arrived=Arrived,point=Point,
            travelRemaining=TravelRemaining,dwellRemaining=DwellRemaining,dwellDuration=DwellDuration };
        public void Restore(Progress saved)
        {
            if (saved == null) { Reset(); return; }
            saved.Validate();
            if (saved.LegacyEmpty) { Reset(); return; }
            Active=saved.active; Arrived=saved.arrived; Point=saved.point;
            TravelRemaining=saved.travelRemaining; DwellRemaining=saved.dwellRemaining; DwellDuration=saved.dwellDuration;
        }
    }

    // Called only when an actual noise is accepted or a legacy investigation is
    // resumed. Its path and real door leaves bound travel; it reads no player pose.
    public static class NoiseInvestigationRoute
    {
        static readonly RaycastHit[] hits=new RaycastHit[64];
        static readonly HashSet<Interactable> doors=new HashSet<Interactable>();
        public static float Length(Vector3 origin,NavMeshPath route)
        {
            float length=0; var previous=origin;
            foreach (var corner in route.corners) { length+=Vector3.Distance(previous,corner); previous=corner; }
            return length;
        }
        public static int ClosedDoors(Vector3 origin,NavMeshPath route)
        {
            doors.Clear(); int saturated=0; var previous=origin;
            foreach (var corner in route.corners)
            {
                var delta=corner-previous;
                if (delta.sqrMagnitude>.0001f)
                {
                    int count=Physics.RaycastNonAlloc(previous+Vector3.up,delta.normalized,hits,delta.magnitude,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    if (count==hits.Length) saturated=8;
                    for (int i=0;i<count;i++)
                    {
                        var door=hits[i].collider.GetComponentInParent<Interactable>();
                        if (door && door.kind==Interactable.Kind.Door && door.movingLeaf && !door.IsOpen) doors.Add(door);
                    }
                }
                previous=corner;
            }
            return Math.Max(saturated,doors.Count);
        }
        public static void Begin(NoiseInvestigationClock clock,Vector3 origin,Vector3 point,NavMeshPath route,float speed,float dwell)
        { clock.Begin(point,dwell,Length(origin,route),speed,ClosedDoors(origin,route)); }
    }
}
