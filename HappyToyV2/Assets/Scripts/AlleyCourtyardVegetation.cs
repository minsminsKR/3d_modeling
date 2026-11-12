using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Low undergrowth beside the courtyard wall, outside the walkable lane.
    [DisallowMultipleComponent]
    public sealed class AlleyCourtyardVegetation : MonoBehaviour
    {
        bool prepared;
        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Courtyard vegetation already prepared");
            prepared = true;
            var fern = Resources.Load<GameObject>("TempleForest/fern-undergrowth-visual");
            if (!fern || fern.GetComponentInChildren<Collider>(true) || fern.GetComponentInChildren<Rigidbody>(true)) return;
            var positions = new[] { new Vector3(-7.75f, -.03f, -13.15f), new Vector3(-7.8f, -.03f, -14.25f),
                new Vector3(-7.8f, -.03f, -16.45f), new Vector3(-7.75f, -.03f, -17.25f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var holder = CorridorEntranceModels.Group(transform, "Courtyard wall weeds", Vector3.zero);
                Instantiate(fern, holder, false);
                CorridorEntranceModels.GroundAtHeight(holder, positions[i], .18f + .035f * (i % 3), Quaternion.Euler(0, 31f + 71f * i, 0));
                foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
