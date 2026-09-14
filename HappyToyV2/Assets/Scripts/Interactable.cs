using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed class Interactable : MonoBehaviour
    {
        public enum Kind { Door, NameSlip, HidingPlace, Exit }
        public Kind kind;
        public string label;
        public string stableId;
        public Transform movingLeaf;
        public Transform secondaryLeaf;
        public Vector3 openOffset = new Vector3(1.45f, 0, 0);
        public Transform inside, outside;
        public NavMeshObstacle obstacle;
        bool open;
        Vector3 closedPosition;
        Vector3 secondaryClosed;
        void Awake() { if (movingLeaf) closedPosition = movingLeaf.localPosition; if(secondaryLeaf)secondaryClosed=secondaryLeaf.localPosition; }
        void Update()
        {
            if (kind != Kind.Door || !movingLeaf) return;
            movingLeaf.localPosition = Vector3.MoveTowards(movingLeaf.localPosition,
                closedPosition + (open ? openOffset : Vector3.zero), Time.deltaTime * 1.8f);
            if(secondaryLeaf)secondaryLeaf.localPosition=Vector3.MoveTowards(secondaryLeaf.localPosition,secondaryClosed-(open?openOffset:Vector3.zero),Time.deltaTime*1.8f);
            if (obstacle) obstacle.enabled = !open;
        }
        public void Use(PlayerMotor player)
        {
            switch (kind)
            {
                case Kind.Door:
                    // Do not close a door on a player standing in its opening.
                    var local=transform.InverseTransformPoint(player.transform.position);
                    float halfWidth=obstacle?obstacle.size.x*.5f:1.2f;
                    if (open && Mathf.Abs(local.x)<halfWidth+.35f && Mathf.Abs(local.z)<.5f && local.y>-.5f && local.y<2.5f) return;
                    open = !open; break;
                case Kind.NameSlip:
                    if (GameSession.Current.Collect(stableId)) gameObject.SetActive(false);
                    break;
                case Kind.HidingPlace:
                    if (player.Hidden) player.LeaveHiding();
                    else if (inside && outside) player.Hide(this, inside.position, outside.position);
                    break;
                case Kind.Exit: GameSession.Current.TryEscape(); break;
            }
        }
    }
}
