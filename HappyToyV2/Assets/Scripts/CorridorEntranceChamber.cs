using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns only the fixed, physical entry space. It joins the existing cell-zero
    // boundary without changing seeded maze edges, pickups or checkpoint IDs.
    // The independent presentation owns the cave, temple and generated gate art.
    [DisallowMultipleComponent]
    public sealed class CorridorEntranceChamber : MonoBehaviour
    {
        public const float Width = 28, Depth = 30, Height = 12;
        public Transform VisualRoot { get; private set; }
        public Interactable Gate { get; private set; }
        public Bounds Bounds => new Bounds(transform.position + Vector3.up * (Height * .5f), new Vector3(Width, Height, Depth));
        public Vector3 StartPoint => transform.TransformPoint(new Vector3(0, .03f, 2));
        public Quaternion StartRotation => Quaternion.LookRotation(Vector3.back);
        public Vector3 MazeApproach => transform.TransformPoint(new Vector3(0, .03f, 19));
        public Vector3 ChamberApproach => transform.TransformPoint(new Vector3(0, .03f, 15));
        public Vector3 GatePosition => transform.TransformPoint(new Vector3(0, 0, -14));
        BoxCollider gateBody;
        Transform gateGeometry, rockGeometry;

        public void Prepare(Vector3 firstCell)
        {
            if (VisualRoot) throw new InvalidOperationException("Entrance chamber is already prepared");
            firstCell.y = 0;
            transform.SetPositionAndRotation(firstCell + Vector3.back * 22, Quaternion.identity);
            var visuals = new GameObject("Entrance cave and temple visual presentation");
            visuals.transform.SetParent(transform, false); VisualRoot = visuals.transform;
            Body("Entrance chamber physical floor", new Vector3(0, -.12f, 0), new Vector3(Width, .24f, Depth));
            Body("Entrance chamber physical ceiling", new Vector3(0, Height + .15f, 0), new Vector3(Width, .3f, Depth));
            foreach (float side in new[] { -1f, 1f })
            {
                Body("Entrance chamber physical side wall", new Vector3(side * Width * .5f, Height * .5f, 0), new Vector3(.22f, Height, Depth));
                Body("Entrance chamber physical front wall", new Vector3(side * 8.2f, Height * .5f, 15), new Vector3(11.6f, Height, .22f));
                Body("Entrance chamber connecting wall", new Vector3(side * 2.55f, 1.9f, 17), new Vector3(.3f, 3.8f, 4));
            }
            Body("Entrance chamber physical rear wall", new Vector3(0, Height * .5f, -15), new Vector3(Width, Height, .22f));
            Body("Entrance chamber front lintel", new Vector3(0, 7.9f, 15), new Vector3(4.8f, 8.2f, .22f));
            Body("Entrance chamber connecting floor", new Vector3(0, -.12f, 17), new Vector3(4.8f, .24f, 4));
            Body("Entrance chamber connecting ceiling", new Vector3(0, 3.95f, 17), new Vector3(4.8f, .3f, 4));
            var gate = new GameObject("Sealed entrance"); gate.layer = 8;
            gate.transform.SetParent(transform, false); gate.transform.position = GatePosition;
            gateBody = gate.AddComponent<BoxCollider>();
            Gate = gate.AddComponent<Interactable>(); Gate.kind = Interactable.Kind.Inspect;
            Gate.stableId = "corridor-entrance"; Gate.label = "대문 열기";
            Gate.inspectionText = "문은 굳게 닫혀있다";
            ConfigureGateBody(new Vector3(0, 4, 0), new Vector3(10, 8, .65f));
        }

        // The generated model may put the actual closed leaves forward of its
        // overall roof/base bounds. The visual owner supplies that measured leaf
        // box explicitly, so player collision and the E target share its front.
        public void ConfigureGateBody(Vector3 localCenter, Vector3 size)
        {
            if (!gateBody || !Finite(localCenter) || !Finite(size) || size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentException("A finite closed gate body is required");
            gateBody.center = localCenter; gateBody.size = size;
        }

        // Preserve the generated steps, plinth and pillars as actual static
        // surfaces. Children remain beneath the same gate interaction owner.
        public void ConfigureGateGeometry(Transform visual)
        {
            if (!Gate || !visual) throw new ArgumentException("Prepared gate and its generated visual are required");
            if (gateGeometry) return;
            gateGeometry = new GameObject("Generated gate static collision").transform;
            gateGeometry.SetParent(Gate.transform, false);
            foreach (var source in visual.GetComponentsInChildren<MeshFilter>(true))
                StaticGeometry(source, gateGeometry, "Gate collision — ");
        }

        public void AddRockGeometry(Transform visual)
        {
            var group = visual ? visual.GetComponentInChildren<LODGroup>(true) : null;
            if (!group) throw new InvalidOperationException("Generated cavern rock requires its authored LOD group");
            var levels = group.GetLODs();
            if (levels.Length == 0 || levels[levels.Length - 1].renderers.Length == 0)
                throw new InvalidOperationException("Generated cavern rock has no collision LOD");
            if (!rockGeometry)
            {
                rockGeometry = new GameObject("Generated cavern rock static collision").transform;
                rockGeometry.SetParent(transform, false);
            }
            foreach (var renderer in levels[levels.Length - 1].renderers)
            {
                var source = renderer ? renderer.GetComponent<MeshFilter>() : null;
                if (!source || !source.sharedMesh) throw new InvalidOperationException("Cavern collision LOD requires a static mesh");
                StaticGeometry(source, rockGeometry, "Rock collision — ");
            }
        }

        static void StaticGeometry(MeshFilter source, Transform parent, string prefix)
        {
            var body = new GameObject(prefix + source.name); body.layer = 8;
            body.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            body.transform.localScale = source.transform.lossyScale;
            body.transform.SetParent(parent, true);
            body.AddComponent<MeshCollider>().sharedMesh = source.sharedMesh;
        }

        static bool Finite(Vector3 value) => StealthRules.Finite(value.x) && StealthRules.Finite(value.y) && StealthRules.Finite(value.z);

        void Body(string name, Vector3 localCenter, Vector3 size)
        {
            var part = new GameObject(name); part.layer = 8;
            part.transform.SetParent(transform, false); part.transform.localPosition = localCenter;
            part.AddComponent<BoxCollider>().size = size;
        }
    }
}
