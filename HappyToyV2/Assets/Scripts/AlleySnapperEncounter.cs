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
        public bool Acquired { get; private set; }
        public bool Cleared { get; private set; }
        public bool ModelAvailable { get; private set; }

        public static AlleySnapperEncounter Create(GameSession session, Transform parent, Vector3 beastPosition, Vector3 pickupPosition)
        {
            var root = new GameObject("Alley snapper encounter");
            root.transform.SetParent(parent, false);
            var result = root.AddComponent<AlleySnapperEncounter>();
            result.session = session; result.beastPosition = beastPosition;
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

            var prefab = Resources.Load<GameObject>("AlleyBeast/Beast");
            result.ModelAvailable = prefab;
            if (!prefab)
            {
                // Never silently substitute a fabricated animal or trap the
                // player behind an invisible obstacle if art is unavailable.
                result.Cleared = true;
                Debug.LogWarning("Alley beast art missing; obstruction disabled.");
                return result;
            }
            result.beast = Instantiate(prefab, root.transform);
            result.beast.transform.position = beastPosition;
            result.barrier = new GameObject("Beast passage blocker");
            result.barrier.transform.SetParent(root.transform, false);
            result.barrier.transform.position = beastPosition + Vector3.up * .8f;
            result.barrier.AddComponent<BoxCollider>().size = new Vector3(5.8f, 1.6f, .45f);
            return result;
        }

        void Update()
        {
            if (!session || !session.player || !session.InputAllowed) return;
            if (!Acquired && pickup && !pickup.activeSelf)
            {
                Acquired = true;
                session.player.Items.Select(PlayerItem.Snapper);
                session.Notify("콩알탄을 주웠다. Q 또는 왼쪽 클릭 (패드 RT)으로 짐승 가까이에 던지자.");
            }
            if (Cleared) return;
            var shot = session.player.Firecrackers.LastThrown;
            if (shot && shot != observed && shot.Exploded)
            {
                observed = shot;
                if (Vector3.Distance(shot.FirstContactPoint, beastPosition) <= 4f)
                {
                    Cleared = true; barrier.SetActive(false);
                    session.Notify("짐승이 놀라 달아났다. 골목 끝으로 가자.");
                }
            }
            if (!Cleared && Acquired && session.player.Firecrackers.Count == 0 && !shot && pickup)
                pickup.SetActive(true); // A missed throw cannot permanently block progress.
        }

        void LateUpdate()
        {
            if (!Cleared || !beast || !session || !session.InputAllowed) return;
            retreat += Time.deltaTime;
            beast.transform.position = beastPosition + Vector3.forward * Mathf.Min(4f, retreat * 4f);
            if (retreat >= 1f) beast.SetActive(false);
        }
    }
}
