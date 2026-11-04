using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Opt-in review route: the normal gate still uses the existing immediate
    // masked transition. Only a forward crossing in the central opening ends
    // the story after this review gate has completely opened.
    public sealed class TempleGateReviewTraversal : MonoBehaviour
    {
        TemplePrologueWorld world;
        GameSession session;
        TempleGateReviewMotion motion;
        Action crossed;
        Vector3 previous;
        bool completed;
        public bool Started => motion && motion.Started;

        public void Prepare(TemplePrologueWorld owner, GameSession game, TempleGateReviewMotion gate, Action onCrossed)
        {
            if (world || !owner || !game || !game.player || !gate || onCrossed == null)
                throw new ArgumentException("Fresh gate traversal review is required");
            world = owner; session = game; motion = gate; crossed = onCrossed;
            previous = game.player.transform.position;
        }

        public void Begin()
        {
            if (!world || Started || completed) throw new InvalidOperationException("Gate traversal already started");
            previous = session.player.transform.position;
            StartCoroutine(motion.OpenDuringGameplay());
        }

        void Update()
        {
            if (!world || !session || !session.player || completed) return;
            Vector3 current = session.player.transform.position;
            if (session.InputAllowed && motion.Progress >= 1)
            {
                float plane = world.GatePoint.z - 1;
                if (previous.z > plane && current.z <= plane)
                {
                    float t = (previous.z - plane) / (previous.z - current.z);
                    float x = Mathf.Lerp(previous.x, current.x, t) - world.GatePoint.x;
                    if (Mathf.Abs(x) <= .9f)
                    {
                        completed = true;
                        crossed();
                    }
                }
            }
            previous = current;
        }
    }
}
