using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns the new-run-only route before the existing resumable temple sequence.
    public sealed class TempleApproachFlow : MonoBehaviour
    {
        GameSession session;
        AlleySnapperEncounter encounter;
        ApproachSceneStream streaming;
        AlleyDuskEnvironment alleyEnvironment;
        BoxCollider mountainGate, villageReturnGate;
        Action mountainEnd;
        GameObject route;
        bool reachedMountain, requested;
        bool villageLoading, villageUnloading, villageStreamed;
        public bool Active { get; private set; }
        public bool VillageReady { get; private set; }
        public string VillageFailure { get; private set; }
        public bool MountainReady { get; private set; }
        public string MountainFailure { get; private set; }
        public bool CanSuspend => Active && encounter;
        public AlleySnapperEncounter Encounter => encounter;
        public bool SnapperAcquired => encounter && encounter.Acquired;
        public string Objective => encounter && encounter.Bypassed ? (!reachedMountain ? "골목 끝의 산길로 가세요." : "산길을 따라 절로 가세요.") :
            !encounter || !encounter.Acquired ? "골목에서 콩알탄을 찾아보세요." :
            encounter.Reacting ? "개가 길을 비킬 때까지 기다리세요." :
            !encounter.Cleared ? "개 앞 바닥의 표시된 곳에 콩알탄을 던지세요." :
            !reachedMountain ? "골목 끝의 산길로 가세요." : "산길을 따라 절로 가세요.";

        public void Prepare(GameSession owner, Action onMountainEnd)
        {
            if (!owner || !owner.player || onMountainEnd == null) throw new ArgumentException("도입 경로를 준비할 수 없습니다.");
            session = owner; mountainEnd = onMountainEnd;
            route = new GameObject("Alley and mountain approach"); route.transform.SetParent(transform, false);
            var skyline = new GameObject("Village shared skyline"); skyline.transform.SetParent(route.transform, false);
            skyline.transform.position = new Vector3(500, 0, 400);
            skyline.AddComponent<AlleyVillageHorizon>().Prepare();
            var village = CreateVillageContent();
            bool indoorShop = Resources.Load<GameObject>("AlleyStorefrontInterior/StorefrontDoorCandidate");
            encounter = AlleySnapperEncounter.Create(session, route.transform, new Vector3(500, 0, 405),
                indoorShop ? new Vector3(494.9f, .8f, 393.6f) : new Vector3(498.4f, .8f, 395),
                indoorShop ? new Vector3(494.65f, 1.2f, 392.55f) : new Vector3(497.75f, 1.2f, 393.8f));
            var gate = new GameObject("Mountain loading safety gate"); gate.transform.SetParent(route.transform, false);
            gate.layer = 8; gate.transform.position = new Vector3(500, 1.2f, 413.7f);
            mountainGate = gate.AddComponent<BoxCollider>(); mountainGate.size = new Vector3(8, 2.4f, .2f);
            var returnGate = new GameObject("Village loading safety gate"); returnGate.transform.SetParent(route.transform, false);
            returnGate.layer = 8; returnGate.transform.position = new Vector3(500, 1.74f, 426);
            villageReturnGate = returnGate.AddComponent<BoxCollider>(); villageReturnGate.size = new Vector3(24, 2.4f, .2f);
            villageReturnGate.enabled = false;
            streaming = gameObject.AddComponent<ApproachSceneStream>();
            session.player.RelocateWorld(new Vector3(500, .04f, 386), Quaternion.identity);
            Active = true;
            LoadVillage(village, true);
            // Start loading before the first playable village frame: the
            // mountain is already visible from the entrance to the alley.
            PreloadMountain();
            session.Notify(PlayerControls.UsingGamepad ? "왼쪽 스틱으로 이동하고 오른쪽 스틱으로 둘러보세요." :
                "WASD로 이동하고 마우스로 주변을 둘러보세요.");
        }
        void Update()
        {
            if (!session || !session.player) return;
            float z = session.player.transform.position.z;
            MaintainVillage(z);
            if (!Active || requested || !session.InputAllowed) return;
            // Duplicate requests are ignored while the first async load is pending.
            if (!MountainReady && string.IsNullOrEmpty(MountainFailure)) PreloadMountain();
            if (!reachedMountain && encounter && encounter.Cleared && (encounter.Acquired || encounter.Bypassed) && z >= 414)
            { reachedMountain = true; session.Notify("산길을 따라 절로 가자."); }
            if (reachedMountain && z >= 474)
            { requested = true; mountainEnd(); }
        }
        GameObject CreateVillageContent()
        {
            var content = new GameObject("Village generated content"); content.transform.SetParent(route.transform, false);
            try
            {
                var alley = AlleyApproachWorld.Create(content.transform);
                alleyEnvironment = alley.GetComponent<AlleyDuskEnvironment>();
                VillageMountainTransitionDressing.Create(content.transform);
                return content;
            }
            catch { Destroy(content); throw; }
        }
        void LoadVillage(GameObject content, bool initial)
        {
            villageLoading = true;
            streaming.LoadVillage(content.transform, error =>
            {
                villageLoading = false;
                villageStreamed = string.IsNullOrEmpty(error);
                // A failed return load still has the generated village in the
                // base scene; allow travel rather than stranding the player.
                VillageFailure = initial ? error : null;
                VillageReady = villageStreamed || !initial;
                if (villageReturnGate) villageReturnGate.enabled = false;
                if (!villageStreamed) Debug.LogWarning("Village additive loading failed: " + error);
            });
        }
        void MaintainVillage(float z)
        {
            if (!streaming || !route) return;
            // If the player reverses while unload is still in progress, close
            // the return gate immediately rather than waiting for that operation.
            if (z <= 445 && !VillageReady && villageReturnGate) villageReturnGate.enabled = true;
            // Keep the village until the far end of its dressing is at least
            // 45m behind; a returning player preloads it 19m before the gate.
            if (z >= 468 && VillageReady && villageStreamed && !villageLoading && !villageUnloading)
            {
                villageUnloading = true; VillageReady = false;
                ReleaseAlleyEnvironment();
                streaming.UnloadVillage(error =>
                {
                    villageUnloading = false;
                    if (!string.IsNullOrEmpty(error))
                    {
                        VillageReady = true;
                        if (villageReturnGate) villageReturnGate.enabled = false;
                        if (alleyEnvironment) alleyEnvironment.enabled = true;
                        Debug.LogWarning("Village unload failed: " + error);
                    }
                    else { villageStreamed = false; alleyEnvironment = null; }
                });
            }
            else if (z <= 445 && !VillageReady && !villageLoading && !villageUnloading && string.IsNullOrEmpty(VillageFailure))
            {
                if (villageReturnGate) villageReturnGate.enabled = true;
                try { LoadVillage(CreateVillageContent(), false); }
                catch (Exception error)
                {
                    VillageFailure = error.Message;
                    Debug.LogError("Returning village preparation failed: " + error);
                    if (session) session.Notify("마을을 다시 불러오지 못했습니다. 산길에 머물러 주세요.");
                }
            }
        }
        public void PreloadMountain()
        {
            if (!Active || MountainReady || !string.IsNullOrEmpty(MountainFailure) || !streaming) return;
            streaming.LoadMountain(scene =>
            {
                var content = CreateMountainContent();
                try { ApproachSceneStream.MoveToScene(content.transform, scene); }
                catch { Destroy(content); throw; }
                MountainReady = true;
                if (mountainGate) mountainGate.enabled = false;
            }, error =>
            {
                // Keep the route playable if the additive container is missing.
                // This is a documented fallback, not a successful stream load.
                Debug.LogWarning("Mountain additive loading failed, trying the base-scene fallback: " + error);
                try
                {
                    CreateMountainContent();
                    MountainReady = true;
                    if (mountainGate) mountainGate.enabled = false;
                }
                catch (Exception fallback)
                {
                    MountainFailure = error + "; fallback: " + fallback.Message;
                    Debug.LogError("Mountain preparation failed: " + MountainFailure);
                    if (session) session.Notify("산길을 불러오지 못했습니다. 이전 구역은 계속 이동할 수 있습니다.");
                }
            });
        }
        GameObject CreateMountainContent()
        {
            var content = new GameObject("Mountain generated content");
            content.transform.SetParent(route.transform, false);
            try
            {
                var world = MountainApproachWorld.Create(content.transform);
                MountainForkDressing.Create(world.transform);
                TempleApproachConnector.Create(world.transform);
                return content;
            }
            catch { Destroy(content); throw; }
        }
        public void Finish()
        {
            Active = false;
            // Keep both zones until the temple world ends: the player can turn
            // around without exposing an unloaded slope or a missing village.
        }
        public void ReleaseAlleyEnvironment()
        {
            if (alleyEnvironment) alleyEnvironment.ReleaseEnvironment();
        }
        void OnDestroy() { ReleaseAlleyEnvironment(); if (streaming) streaming.Retire(); }
    }
}
