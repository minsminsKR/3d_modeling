using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only prayer furnishings. The world retains the existing table
    // collision, interaction volume and incense placement coordinates.
    [DisallowMultipleComponent]
    public sealed class TemplePrayerDressing : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        bool prepared;

        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Prayer dressing already prepared");
            prepared = true;
            var source = Resources.Load<GameObject>("CorridorAltarChamber/offering-table");
            var pottery = Resources.Load<GameObject>("KoreanCorridorProps/onggi");
            var incenseVessel = Resources.Load<GameObject>("TempleForest/incense-vessel-visual");
            if (!source || !pottery || !incenseVessel || !incenseVessel.GetComponent<LODGroup>())
                throw new InvalidOperationException("Authored prayer table, pottery and incense vessel LODs are required");

            var table = Group("Authored wooden prayer table");
            var instance = Instantiate(source, table, false);
            RequireVisualOnly(instance);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i]) throw new InvalidOperationException("Prayer table material slot is empty");
                    slots[i] = Resolve(slots[i].name);
                }
                renderer.sharedMaterials = slots;
            }
            // The existing interaction and supporting collider are unchanged.
            // Fit the whole imported furnishing, including its original draped
            // textile, within the old table's footprint.
            GraphicsPropLibrary.Fit(table, new Bounds(new Vector3(0, .4225f, 7.35f), new Vector3(1.8f, .845f, .75f)));
            Batch(table);

            var jar = Group("Authored ceramic offering jar");
            var jarVisual = Instantiate(pottery, jar, false);
            RequireVisualOnly(jarVisual);
            // This model has a closed lid, so it is a side offering rather
            // than the open vessel that receives the incense sticks.
            GraphicsPropLibrary.Fit(jar, new Bounds(new Vector3(1.25f, .32f, 8.1f), new Vector3(.48f, .64f, .48f)));
            var bowl = Group("Incense vessel");
            var bowlVisual = Instantiate(incenseVessel, bowl, false);
            RequireVisualOnly(bowlVisual);
            // Existing altar top is y=.845 and the placed incense starts at
            // y=1.02. The source's central ash is .16726m above its foot, so
            // the existing incense begins ~8mm above the visible ash; its
            // release marker, position and all interaction physics stay intact.
            CorridorEntranceModels.GroundAtHeight(bowl, new Vector3(0, .845f, 7.2f), .2214165f, Quaternion.identity);

            foreach (float side in new[] { -1f, 1f })
            {
                var holder = Group("Prayer offering candle");
                // The source table has an old ledger on its right rear corner.
                holder.localPosition = new Vector3(side * .68f, .80f, 7.05f);
                var candle = GraphicsPropLibrary.Attach("candle-waymark", holder, surfaces.Resolve);
                GraphicsPropLibrary.Fit(candle, new Bounds(new Vector3(0, .09f, 0), new Vector3(.075f, .18f, .075f)));
                var flame = GraphicsPropLibrary.CreateFlame(holder, owned);
                GraphicsPropLibrary.Fit(flame, new Bounds(new Vector3(0, .22f, 0), new Vector3(.035f, .09f, .035f)));
            }
        }

        Transform Group(string name)
        {
            var result = new GameObject(name).transform; result.gameObject.layer = 8;
            result.SetParent(transform, false); return result;
        }
        static void RequireVisualOnly(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>(true) || root.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Prayer art must not add physics");
            foreach (var item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 8;
        }
        Material Resolve(string name)
        {
            switch (name)
            {
                case "CA_BloodCloth": return surfaces.Get("paper-aged", new Color(.40f, .15f, .11f));
                case "CA_Dust": return surfaces.Get("paper-aged", new Color(.45f, .43f, .37f));
                default: return surfaces.Resolve(name);
            }
        }
        void Batch(Transform table)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in table.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !filter.sharedMesh) continue;
                var mesh = filter.sharedMesh; var slots = renderer.sharedMaterials;
                if (!mesh.isReadable || slots.Length != mesh.subMeshCount)
                    throw new InvalidOperationException("Prayer table requires readable meshes and matching materials");
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!groups.TryGetValue(slots[i], out var list))
                    { list = new List<CombineInstance>(); groups.Add(slots[i], list); }
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i,
                        transform = table.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                renderer.enabled = false;
            }
            if (groups.Count == 0) throw new InvalidOperationException("Prayer table has no geometry");
            foreach (var group in groups)
            {
                var mesh = new Mesh { name = "Batched authored prayer table", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds(); owned.Add(mesh);
                var part = new GameObject("Batched prayer table surface"); part.layer = 8; part.transform.SetParent(table, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
                renderer.receiveShadows = true; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        void OnDestroy()
        {
            surfaces.Dispose();
            foreach (var resource in owned) GraphicsSurfaceLibrary.DestroyOwned(resource);
            owned.Clear();
        }
    }
}
