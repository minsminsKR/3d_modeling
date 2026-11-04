using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Physics for the opt-in gate prototype. The generated stone threshold is
    // slightly too high for the player's normal step offset, so its walking
    // surface is represented by a short invisible ramp once the doors open.
    public sealed class TempleGateReviewPassage : MonoBehaviour
    {
        BoxCollider blocker, ramp;

        public void Prepare(BoxCollider closedBlocker, Vector3 gatePoint)
        {
            if (blocker || !closedBlocker) throw new ArgumentException("Fresh gate blocker is required");
            blocker = closedBlocker;
            var surface = new GameObject("Review gate stair collision");
            surface.layer = 8;
            surface.transform.SetParent(transform, false);
            surface.transform.SetPositionAndRotation(gatePoint + new Vector3(0, .15f, -.25f), Quaternion.Euler(17.5f, 0, 0));
            ramp = surface.AddComponent<BoxCollider>();
            ramp.size = new Vector3(2.4f, .10f, 1.18f);
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            if (!blocker || !ramp) throw new InvalidOperationException("Review passage is not prepared");
            blocker.enabled = !open;
            ramp.enabled = open;
        }

        void OnDestroy()
        {
            if (blocker) blocker.enabled = true;
            if (ramp) Destroy(ramp.gameObject);
        }
    }
}
