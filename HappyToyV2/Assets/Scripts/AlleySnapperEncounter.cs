using UnityEngine;

namespace HappyToy.V2
{
    // Owns only the introductory pickup and a noise-driven path obstruction.
    // It observes the existing projectile's real first impact, never a keypress.
    public sealed class AlleySnapperEncounter : MonoBehaviour
    {
        GameSession session;
        GameObject pickup, barrier, beast;
        Vector3 beastPosition;
        FirecrackerProjectile observed;
        float retreat;
        bool retreatFinished;
        Vector3 escapePoint;
        Material markerMaterial, keeperMaterial;
        AudioSource dogVoice;
        float nextBark;
        AlleyBeastRetreat animatedRetreat;
        public bool Acquired { get; private set; }
        public bool Cleared { get; private set; }
        public bool Reacting { get; private set; }
        public bool ModelAvailable { get; private set; }
        public bool Bypassed { get; private set; }
        public bool CanSuspend => session && session.player && !Reacting && !session.player.Firecrackers.LastThrown;
        public Vector3 GroundTarget => beastPosition + Vector3.back * 1.6f;
        public const float TargetRadius = 1.05f;
        public static bool ValidGroundImpact(Vector3 point, Vector3 normal, Vector3 target)
        {
            var horizontal = point - target; horizontal.y = 0;
            return normal.y >= .7f && Mathf.Abs(point.y - target.y) <= .18f &&
                horizontal.sqrMagnitude <= TargetRadius * TargetRadius;
        }
        public void Restore(bool acquired, bool cleared, int restoredStock, bool bypassed)
        {
            Acquired = acquired; Bypassed = bypassed || !ModelAvailable; Reacting = false;
            if (pickup) pickup.SetActive(!acquired || (!cleared && restoredStock == 0));
            if (!cleared || !ModelAvailable) return;
            Cleared = true; retreatFinished = true;
            if (barrier) barrier.SetActive(false);
            if (beast) beast.transform.position = escapePoint;
        }

        public static AlleySnapperEncounter Create(GameSession session, Transform parent, Vector3 beastPosition, Vector3 pickupPosition, Vector3 shopkeeperPosition)
        {
            var root = new GameObject("Alley snapper encounter");
            root.transform.SetParent(parent, false);
            var result = root.AddComponent<AlleySnapperEncounter>();
            result.session = session; result.beastPosition = beastPosition;
            result.escapePoint = beastPosition + new Vector3(2.15f, 0, .65f);
            result.CreateShopkeeper(shopkeeperPosition);
            result.CreateGroundMarker();
            session.player.Firecrackers.RestoreStock(0, 0);
            result.pickup = new GameObject("Alley snapper supply");
            result.pickup.transform.SetParent(root.transform, false);
            result.pickup.transform.position = pickupPosition + Vector3.up * .15f;
            result.pickup.layer = 9;
            var collider = result.pickup.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .056f, .0067f); collider.size = new Vector3(.28f, .20f, .25f);
            var interact = result.pickup.AddComponent<Interactable>();
            interact.kind = Interactable.Kind.FirecrackerSupply; interact.stableId = "alley-snapper";
            interact.label = "콩알탄 한 묶음 줍기 (6발)";
            var visual = SnapperVisuals.AttachPackage(result.pickup.transform);
            foreach (var child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 9;
            result.pickup.AddComponent<AlleySupplyStateHook>().Prepare(result);

            var animatedPrefab = Resources.Load<GameObject>("AlleyBeast/Animated/DogMorphReview");
            var prefab = SelectBeastPrefab(animatedPrefab, Resources.Load<GameObject>("AlleyBeast/Beast"));
            result.ModelAvailable = prefab;
            if (!prefab)
            {
                // Never silently substitute a fabricated animal or trap the
                // player behind an invisible obstacle if art is unavailable.
                result.Cleared = true; result.Bypassed = true;
                Debug.LogWarning("Alley beast art missing; obstruction disabled.");
                return result;
            }
            result.beast = Instantiate(prefab, root.transform);
            result.beast.transform.position = beastPosition;
            var bark = Resources.Load<AudioClip>("Audio/VillageApproach/dog-bark");
            if (bark)
            {
                result.dogVoice = result.beast.AddComponent<AudioSource>();
                result.dogVoice.clip = bark; result.dogVoice.playOnAwake = false;
                result.dogVoice.spatialBlend = 1; result.dogVoice.minDistance = 2;
                result.dogVoice.maxDistance = 15; result.dogVoice.volume = .45f;
                result.dogVoice.dopplerLevel = 0;
            }
            if (prefab == animatedPrefab)
                result.animatedRetreat = new AlleyBeastRetreat(result.beast.transform,
                    result.beast.GetComponentInChildren<Animation>(true), result.escapePoint);
            result.barrier = new GameObject("Beast passage blocker");
            result.barrier.transform.SetParent(root.transform, false);
            result.barrier.transform.position = beastPosition + Vector3.up * .8f;
            result.barrier.AddComponent<BoxCollider>().size = new Vector3(5.8f, 1.6f, .45f);
            return result;
        }

        // Broken or absent animation art keeps the previously verified static dog.
        static GameObject SelectBeastPrefab(GameObject animated, GameObject fallback)
        {
            var animation = animated ? animated.GetComponentInChildren<Animation>(true) : null;
            var skin = animated ? animated.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            return animation && animation.clip && animation.clip.length > 0 && skin && skin.sharedMesh &&
                skin.sharedMesh.blendShapeCount > 1 ? animated : fallback;
        }

        void Update()
        {
            if (!session || !session.player || !session.InputAllowed) return;
            if (Cleared || Reacting) return;
            if (dogVoice && Vector3.Distance(session.player.transform.position, beastPosition) < 7f)
            {
                nextBark -= Time.deltaTime;
                if (nextBark <= 0) { dogVoice.Play(); nextBark = 6f; }
            }
            var shot = session.player.Firecrackers.LastThrown;
            if (shot && shot != observed && shot.Exploded)
            {
                observed = shot;
                if (Acquired && shot.HasFirstContact && ValidGroundImpact(shot.FirstContactPoint, shot.FirstContactNormal, GroundTarget))
                {
                    Reacting = true; if (dogVoice) dogVoice.Stop();
                    session.Notify("개가 소리에 놀라 길옆으로 물러나고 있다.");
                }
                else session.Notify("바닥의 표시된 곳에 던져 소리로 개를 유도하자.");
            }
            if (!Cleared && Acquired && session.player.Firecrackers.Count == 0 && !shot && pickup)
                pickup.SetActive(true); // A missed throw cannot permanently block progress.
        }

        internal void SupplyCollected()
        {
            if (Acquired || !session || !session.player) return;
            Acquired = true;
            session.player.Items.Select(PlayerItem.Snapper);
            session.Notify("콩알탄을 주웠다. 개 앞 바닥의 표시된 곳에 던지자.");
        }
        void CreateGroundMarker()
        {
            // A Resources material preserves the exact shader variant in player builds.
            var template = Resources.Load<Material>("GraphicsPbr/paper-aged/material-emissive");
            if (!template) { Debug.LogWarning("Ground marker template is missing"); return; }
            markerMaterial = new Material(template) { name = "Faint chalk ground target" };
            markerMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            markerMaterial.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            markerMaterial.SetColor("_BaseColor", new Color(.40f, .37f, .32f, 1));
            markerMaterial.SetColor("_EmissionColor", new Color(.13f, .12f, .10f));
            var marker = new GameObject("Chalk ground target"); marker.transform.SetParent(transform, false);
            marker.transform.position = GroundTarget + Vector3.up * .012f;
            var line = marker.AddComponent<LineRenderer>(); line.useWorldSpace = false;
            line.material = markerMaterial; line.loop = true; line.positionCount = 32;
            line.startWidth = line.endWidth = .025f; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * .48f, 0, Mathf.Sin(a) * .48f));
            }
        }
        void CreateShopkeeper(Vector3 shopkeeperPosition)
        {
            // Placeholder counter conversation; final human art, voice and shop
            // interior are intentionally separate authoring tasks.
            var keeper = new GameObject("Shopkeeper conversation placeholder");
            keeper.transform.SetParent(transform, false);
            keeper.transform.position = shopkeeperPosition;
            keeper.layer = 9;
            var template = Resources.Load<Material>("GraphicsPbr/cloth-charred/material");
            if (template)
            {
                keeperMaterial = new Material(template) { name = "Temporary shopkeeper silhouette" };
                keeperMaterial.SetColor("_BaseColor", new Color(.16f, .12f, .10f));
                var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                torso.name = "Temporary shopkeeper torso"; torso.transform.SetParent(keeper.transform, false);
                torso.transform.localPosition = new Vector3(-.38f, -.18f, 0);
                torso.transform.localScale = new Vector3(.42f, .48f, .27f);
                torso.GetComponent<Renderer>().sharedMaterial = keeperMaterial; Destroy(torso.GetComponent<Collider>());
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Temporary shopkeeper head"; head.transform.SetParent(keeper.transform, false);
                head.transform.localPosition = new Vector3(-.38f, .46f, 0);
                head.transform.localScale = new Vector3(.29f, .34f, .27f);
                head.GetComponent<Renderer>().sharedMaterial = keeperMaterial; Destroy(head.GetComponent<Collider>());
            }
            var box = keeper.AddComponent<BoxCollider>();
            box.center = new Vector3(-.18f, .08f, 0); box.size = new Vector3(.78f, 1.16f, .55f);
            var interact = keeper.AddComponent<Interactable>();
            interact.kind = Interactable.Kind.TemplePrologue; interact.label = "우암상회 주인과 대화";
            keeper.AddComponent<TemplePrologueTarget>().Prepare(() => session && session.InputAllowed, player => {
                if (!Acquired && pickup && player.Firecrackers.AddSupply()) pickup.SetActive(false);
                session.Notify(Acquired ? "주인: 저 아이를 다치게 하지 말고, 바닥에서 소리만 내 보게." :
                    "주인: 콩알탄이야. 저 앞 바닥에서 터뜨리면 개가 길옆으로 갈 게야.");
            });
        }
        void OnDestroy() { if (markerMaterial) Destroy(markerMaterial); if (keeperMaterial) Destroy(keeperMaterial); }

        void LateUpdate()
        {
            if (!Reacting || retreatFinished || !session || !session.InputAllowed) return;
            if (!beast)
            {
                // If the active art disappears, never leave a permanent blocker.
                Reacting = false; Cleared = true; Bypassed = true;
                if (barrier) barrier.SetActive(false);
                return;
            }
            if (animatedRetreat != null)
                retreatFinished = animatedRetreat.Advance(Time.deltaTime);
            else
            {
                retreat += Time.deltaTime;
                beast.transform.position = Vector3.MoveTowards(beastPosition, escapePoint, retreat * 1.2f);
                retreatFinished = Vector3.Distance(beast.transform.position, escapePoint) < .001f;
            }
            if (!retreatFinished) return;
            Reacting = false; Cleared = true;
            if (barrier) barrier.SetActive(false);
            session.Notify("개가 길옆으로 물러났다. 골목 끝으로 가자.");
        }
    }
    // FirecrackerSupply deactivates itself after the inventory transaction.
    // Capture the matching event state in the same frame, even if pause follows.
    internal sealed class AlleySupplyStateHook : MonoBehaviour
    {
        AlleySnapperEncounter owner;
        public void Prepare(AlleySnapperEncounter encounter) { owner = encounter; }
        void OnDisable() { if (owner && !gameObject.activeSelf) owner.SupplyCollected(); }
    }
}
