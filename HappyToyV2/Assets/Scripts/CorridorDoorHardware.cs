using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Raised physical-looking pulls on both visual faces of the actual moving door leaf.</summary>
    [DisallowMultipleComponent]
    public sealed class CorridorDoorHardware : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<Transform> faces = new List<Transform>();
        public bool Prepared { get; private set; }
        public Interactable Owner { get; private set; }
        public Transform Leaf { get; private set; }
        public IReadOnlyList<Transform> FaceRoots => faces;
        public Vector3 LeafDimensions { get; private set; }

        // Dimensions are metres, centered leaf coordinates X=width/Y=height/Z=thickness.
        // pullEdge is +/-1 in the leaf X axis. Existing interaction/physics/NavMesh state stays owned by the door.
        public static CorridorDoorHardware Attach(Interactable owner, Transform movingLeaf,
            float widthMetres, float heightMetres, float thicknessMetres, int pullEdge = 1)
        {
            if (!owner || !movingLeaf || owner.kind != Interactable.Kind.Door ||
                owner.movingLeaf != movingLeaf && owner.secondaryLeaf != movingLeaf)
                throw new ArgumentException("Door hardware needs the actual interactable moving leaf");
            if (widthMetres <= .35f || heightMetres <= .7f || thicknessMetres <= 0 || Mathf.Abs(pullEdge) != 1)
                throw new ArgumentException("Invalid physical moving-leaf dimensions");
            var prior = movingLeaf.GetComponentInChildren<CorridorDoorHardware>(true);
            if (prior) return prior;
            var root = new GameObject("Raised pulls on both moving leaf faces"); root.transform.SetParent(movingLeaf, false);
            root.layer = 8;
            // Counter the real leaf's non-uniform unit-cube scale. Rotations and
            // all future open/restore translations remain inherited from that leaf.
            root.transform.localScale = new Vector3(
                1 / movingLeaf.TransformVector(Vector3.right).magnitude,
                1 / movingLeaf.TransformVector(Vector3.up).magnitude,
                1 / movingLeaf.TransformVector(Vector3.forward).magnitude);
            var hardware = root.AddComponent<CorridorDoorHardware>();
            hardware.Owner = owner; hardware.Leaf = movingLeaf;
            hardware.LeafDimensions = new Vector3(widthMetres, heightMetres, thicknessMetres);
            try
            {
                float width = Mathf.Min(.125f, widthMetres * .14f);
                float height = Mathf.Min(.31f, heightMetres * .20f);
                float depth = .061f;
                float x = pullEdge * (widthMetres * .5f - Mathf.Max(.20f, width * 1.6f));
                float y = Mathf.Clamp(1.10f - heightMetres * .5f, -heightMetres * .32f, heightMetres * .12f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var mount = new GameObject(side < 0 ? "Front raised door pull" : "Rear raised door pull").transform;
                    mount.SetParent(root.transform, false); mount.gameObject.layer = 8;
                    mount.localPosition = new Vector3(x, y, side * (thicknessMetres * .5f + .002f));
                    mount.localRotation = Quaternion.Euler(0, side > 0 ? 180 : 0, 0);
                    hardware.faces.Add(mount);
                    // Existing editable forged pull has screws, a finger recess,
                    // slotted escutcheon and a modeled U handle, not a flat decal.
                    var model = GraphicsPropLibrary.Attach("door-hardware", mount, hardware.Resolve);
                    GraphicsPropLibrary.Fit(model, new Bounds(new Vector3(0, 0, -depth * .5f - .005f),
                        new Vector3(width, height, depth)));
                    var material = hardware.surfaces.Get("brass-tarnished", new Color(.76f, .67f, .46f));
                    var backing = new GameObject("Bevelled escutcheon backing"); backing.transform.SetParent(mount, false); backing.layer = 8;
                    backing.transform.localPosition = new Vector3(0, 0, -.003f);
                    var size = new Vector3(width + .035f, height + .035f, .006f);
                    backing.transform.localScale = size;
                    backing.AddComponent<MeshFilter>().sharedMesh = hardware.surfaces.MetreBoxMesh(size, .0015f,
                        GraphicsSurfaceLibrary.TileSpan(material));
                    var renderer = backing.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                hardware.Prepared = true; return hardware;
            }
            catch { GraphicsSurfaceLibrary.DestroyOwned(root); throw; }
        }
        Material Resolve(string slot)
        {
            if (slot == "GU_aged_iron") return surfaces.Get("metal-rust", new Color(.93f, .91f, .83f));
            if (slot == "GU_tarnished_brass") return surfaces.Get("brass-tarnished", new Color(1f, .94f, .81f));
            return surfaces.Resolve(slot);
        }
        void OnDestroy() => surfaces.Dispose();
    }
}
