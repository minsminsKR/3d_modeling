using System;
using UnityEngine;

namespace HappyToy.V2
{
    [Serializable] public struct SchoolBabyProgress
    {
        public bool present;
        public Vector3 position;public float yaw;
        public void Validate()
        {
            if(!StealthRules.Finite(position.x)||!StealthRules.Finite(position.y)||!StealthRules.Finite(position.z)||!StealthRules.Finite(yaw)||
                !SchoolLoopPlan.Walkable(position.x,position.z)||Mathf.Abs(position.y)>.25f||yaw<0||yaw>=360)
                throw new ArgumentException("Invalid school Baby pose");
        }
    }
    [Serializable] public sealed class SchoolAnomalyProgress
    {
        public int version=1;
        public SchoolAnomalyDeck.State deck;
        public SchoolAnomalyEventClock.State clock;
        public SchoolBabyProgress baby;
        public void Validate(int traversal)
        {
            if(version!=1||deck.Passage!=traversal)throw new ArgumentException("School anomaly and loop traversal disagree");
            SchoolAnomalyDeck.Validate(deck);SchoolAnomalyEventClock.Validate(clock,deck.Kind);
            if(baby.present!=(deck.Kind==SchoolAnomalyKind.Baby&&clock.Triggered))throw new ArgumentException("School Baby state is missing or unexpected");
            if(baby.present)baby.Validate();
        }
    }
}
