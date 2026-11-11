using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns the new-run-only route before the existing resumable temple sequence.
    public sealed class TempleApproachFlow : MonoBehaviour
    {
        GameSession session;
        AlleySnapperEncounter encounter;
        Action mountainEnd;
        GameObject route;
        bool reachedMountain, requested;
        public bool Active { get; private set; }
        public bool SnapperAcquired => encounter && encounter.Acquired;
        public string Objective => !encounter || !encounter.Acquired ? "골목에서 콩알탄을 찾아보세요." :
            !encounter.Cleared ? "콩알탄을 던져 길을 막은 짐승을 쫓으세요." :
            !reachedMountain ? "골목 끝의 산길로 가세요." : "산길을 따라 절로 가세요.";

        public void Prepare(GameSession owner, Action onMountainEnd)
        {
            if (!owner || !owner.player || onMountainEnd == null) throw new ArgumentException("도입 경로를 준비할 수 없습니다.");
            session = owner; mountainEnd = onMountainEnd;
            route = new GameObject("Alley and mountain approach"); route.transform.SetParent(transform, false);
            AlleyApproachWorld.Create(route.transform);
            MountainApproachWorld.Create(route.transform);
            encounter = AlleySnapperEncounter.Create(session, route.transform,
                new Vector3(500, 0, 405), new Vector3(498.4f, .8f, 395));
            session.player.RelocateWorld(new Vector3(500, .04f, 386), Quaternion.identity);
            Active = true;
            session.Notify(PlayerControls.UsingGamepad ? "왼쪽 스틱으로 이동하고 오른쪽 스틱으로 둘러보세요." :
                "WASD로 이동하고 마우스로 주변을 둘러보세요.");
        }
        void Update()
        {
            if (!Active || requested || !session || !session.InputAllowed) return;
            float z = session.player.transform.position.z;
            if (!reachedMountain && encounter && encounter.Acquired && encounter.Cleared && z >= 414)
            { reachedMountain = true; session.Notify("산길을 따라 절로 가자."); }
            if (reachedMountain && z >= 446)
            { requested = true; mountainEnd(); }
        }
        public void Finish()
        {
            Active = false;
            if (route) Destroy(route);
        }
    }
}
