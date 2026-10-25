using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Measured-barrel contact finish, bounded around the authored grip.</summary>
    public sealed class HandCylinderContact
    {
        [Serializable] public sealed class Diagnostic
        {
            public string digit;
            public float axial, radiusBefore, radiusAfter, targetError, maximumDegrees;
            public bool corrected, outsideShaft, skippedThumb;
        }
        static readonly string[] Digits = { "thumb", "index", "middle", "ring", "little" };
        readonly Transform cylinder;
        readonly Diagnostic[] diagnostics = new Diagnostic[Digits.Length];
        public float Radius { get; }
        public Vector3 Centre { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public IReadOnlyList<Diagnostic> LastDiagnostics => diagnostics;

        public HandCylinderContact(Transform model)
        {
            if (!model) throw new ArgumentNullException(nameof(model));
            cylinder = model;
            var vertices = new List<Vector3>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                foreach (var point in filter.sharedMesh.vertices)
                    vertices.Add(model.InverseTransformPoint(filter.transform.TransformPoint(point)));
            }
            if (vertices.Count == 0) throw new InvalidOperationException("A cylinder contact needs its actual imported surface.");
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var point in vertices) { low = Mathf.Min(low, point.y); high = Mathf.Max(high, point.y); }
            float height = high - low;
            Minimum = low + height * .31f; Maximum = low + height * .88f;
            var middle = new List<Vector3>();
            Vector3 minimum = Vector3.one * float.PositiveInfinity, maximum = Vector3.one * float.NegativeInfinity;
            foreach (var point in vertices)
                if (point.y >= low + height * .45f && point.y <= low + height * .70f)
                { middle.Add(point); minimum = Vector3.Min(minimum, point); maximum = Vector3.Max(maximum, point); }
            if (middle.Count == 0) throw new InvalidOperationException("The actual flashlight has no central barrel surface.");
            Centre = new Vector3((minimum.x + maximum.x) * .5f, 0, (minimum.z + maximum.z) * .5f);
            var radii = new List<float>(middle.Count);
            foreach (var point in middle)
                radii.Add(new Vector2(point.x - Centre.x, point.z - Centre.z).magnitude);
            radii.Sort(); Radius = radii[radii.Count / 2];
            if (Radius < .01f || Radius > .05f || height <= .05f)
                throw new InvalidOperationException("The measured cylinder is not a supported flashlight grip.");
            for (int i = 0; i < diagnostics.Length; i++) diagnostics[i] = new Diagnostic { digit = Digits[i] };
        }

        public void Correct(HandAnimationRig rig, bool skipThumb = false, float maxDegrees = 6)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));
            for (int i = 0; i < Digits.Length; i++)
            {
                var entry = diagnostics[i];
                Vector3 local = cylinder.InverseTransformPoint(rig.Tip(Digits[i]));
                Vector3 radial = new Vector3(local.x - Centre.x, 0, local.z - Centre.z);
                float before = radial.magnitude;
                entry.axial = local.y; entry.radiusBefore = entry.radiusAfter = before;
                entry.corrected = false; entry.targetError = 0; entry.maximumDegrees = 0;
                entry.outsideShaft = local.y < Minimum || local.y > Maximum;
                entry.skippedThumb = skipThumb && i == 0;
                if (entry.outsideShaft || entry.skippedThumb || before >= Radius - .0025f && before <= Radius + .010f) continue;
                if (before < .000001f) radial = Vector3.forward; else radial /= before;
                Vector3 targetLocal = Centre + radial * (Radius + .003f); targetLocal.y = local.y;
                Vector3 target = cylinder.TransformPoint(targetLocal);
                entry.targetError = rig.CorrectTip(Digits[i], target, maxDegrees);
                Vector3 after = cylinder.InverseTransformPoint(rig.Tip(Digits[i]));
                entry.radiusAfter = new Vector2(after.x - Centre.x, after.z - Centre.z).magnitude;
                entry.maximumDegrees = rig.CorrectionDegrees(Digits[i]); entry.corrected = true;
            }
        }
    }
}
