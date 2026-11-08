using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Candidate production integration. It owns only temporary visual instances
    // and forceRenderingOff leases on the old decorative renderers. The hidden
    // Stone approach reference, its bounds, collision, footsteps and saves remain
    // owned by the existing TempleForestPath / TempleProloguePresentation.
    [DisallowMultipleComponent]
    public sealed class TempleIndividualStonePath : MonoBehaviour
    {
        static readonly string[] ResourceKeys = {
            "TempleForest/temple-stepping-stone-reference-20261107/visual",
            "TempleForest/temple-stepping-stone-long-20261108/visual" };

        // Values are selected by the caller only after visual/performance review.
        // AcceptUnderfill must be explicit: a requested 240 with an actual 190
        // must never silently be reported as 240.
        public readonly struct Settings
        {
            public readonly int Requested, Minimum, Attempts;
            public readonly float TopAboveSoil;
            public readonly bool AcceptUnderfill;
            public readonly uint Seed;
            public Settings(int requested, int minimum, int attempts, float topAboveSoil, bool acceptUnderfill, uint seed = 43093)
            { Requested=requested; Minimum=minimum; Attempts=attempts; TopAboveSoil=topAboveSoil; AcceptUnderfill=acceptUnderfill; Seed=seed; }
        }
        public readonly struct Result
        {
            public readonly bool Installed, Underfilled;
            public readonly int Requested, Actual, Attempts;
            public readonly string Reason;
            internal Result(bool installed, int requested, int actual, int attempts, string reason)
            { Installed=installed; Requested=requested; Actual=actual; Attempts=attempts; Underfilled=actual<requested; Reason=reason; }
        }
        struct RenderLease { public Renderer renderer; public bool prior; }
        GameObject ownedRoot;
        TempleStoneGapFill gapFill;
        RenderLease[] leases;
        public int GapStoneCount => gapFill == null ? 0 : gapFill.Count;
        public bool GapUnderfilled => gapFill != null && gapFill.Underfilled;
        public string GapFailure { get; private set; }
        bool preparing;
        public bool IsPrepared => ownedRoot;
        public Result LastResult { get; private set; }

        // Resources must be imported before this method is called. A missing or
        // invalid visual safely keeps the original path; no material or mesh is
        // instantiated or destroyed by this component.
        public Result Prepare(Settings settings)
        {
            return Prepare(settings, new[] { Resources.Load<GameObject>(ResourceKeys[0]), Resources.Load<GameObject>(ResourceKeys[1]) });
        }

        // Explicit inputs enable deterministic error-path verification. Do not
        // pass prefabs with scripts, physics, sound or other non-visual behavior.
        public Result Prepare(Settings settings, GameObject[] prefabs)
        {
            if (preparing || ownedRoot) throw new InvalidOperationException("Individual path already preparing or installed");
            if (!isActiveAndEnabled) throw new InvalidOperationException("Individual path component must be enabled");
            preparing = true;
            GameObject candidate = null;
            TempleStoneGapFill candidateFill = null;
            int actual = 0, attempts = 0;
            try
            {
                ValidateSettings(settings);
                var approach = transform.Find("Stone approach");
                var ground = transform.Find("Forest ground");
                var relief = transform.Find("Authored forest stone approach");
                var bed = transform.Find("Recessed forest stone bed");
                var reference = approach ? approach.GetComponent<Renderer>() : null;
                if (!approach || !ground || !relief || !bed || !reference || !reference.enabled || !reference.forceRenderingOff)
                    throw new InvalidOperationException("Original reference and both decorative layers are required");
                if (approach.parent != transform || ground.parent != transform || relief.parent != transform || bed.parent != transform ||
                    Quaternion.Angle(approach.localRotation, Quaternion.identity) > .001f ||
                    Quaternion.Angle(ground.localRotation, Quaternion.identity) > .001f ||
                    approach.localScale.x <= 0 || approach.localScale.z <= 0 || ground.localScale.y <= 0)
                    throw new InvalidOperationException("Expected the original axis-aligned path and ground transforms");
                var oldRelief = relief.GetComponentsInChildren<Renderer>(true);
                var oldBed = bed.GetComponentsInChildren<Renderer>(true);
                if (oldRelief.Length == 0 || oldBed.Length == 0)
                    throw new InvalidOperationException("Original decorative renderers are missing");
                if (prefabs == null || prefabs.Length != 2)
                    throw new ArgumentException("Two visual-only stone prefabs are required");
                var bounds = new[] { PrefabBounds(prefabs[0]), PrefabBounds(prefabs[1]) };
                var footprints = new[] {
                    new TempleStoneLayoutRules.Footprint(bounds[0].extents.x, bounds[0].extents.z, .52, .65),
                    new TempleStoneLayoutRules.Footprint(bounds[1].extents.x, bounds[1].extents.z, .50, .72) };
                Vector3 p=approach.localPosition, s=approach.localScale;
                var path = new TempleStoneLayoutRules.Rectangle(p.x-s.x*.5, p.z-s.z*.5, p.x+s.x*.5, p.z+s.z*.5);
                var layout = TempleStoneLayoutRules.Generate(path, footprints, null, settings.Seed, .025, .012, .18,
                    settings.Requested, settings.Attempts);
                actual=layout.Count; attempts=layout.Attempts;
                if (actual < settings.Minimum || (layout.Underfilled && !settings.AcceptUnderfill))
                    throw new InvalidOperationException("Placement shortfall: " + actual + "/" + settings.Requested +
                        ", minimum=" + settings.Minimum + ", acceptUnderfill=" + settings.AcceptUnderfill);
                if (actual == 0) throw new InvalidOperationException("Placement produced no stones");
                float soil = ground.localPosition.y + ground.localScale.y * .5f;
                int pathLayer = approach.gameObject.layer;
                candidate = new GameObject("Individual forest stone approach");
                candidate.SetActive(false); candidate.layer = pathLayer; candidate.transform.SetParent(transform, false);
                for (int i = 0; i < layout.Count; i++)
                {
                    var item=layout[i]; int kind=item.FootprintIndex; float scale=(float)item.Scale;
                    var holder=new GameObject("Forest stone " + i); holder.layer=pathLayer;
                    holder.transform.SetParent(candidate.transform, false);
                    var tile=Instantiate(prefabs[kind], holder.transform, false);
                    tile.transform.localRotation=Quaternion.Euler(0, (float)-item.HeadingDegrees, 0);
                    tile.transform.localScale=Vector3.one * scale;
                    foreach (var part in tile.GetComponentsInChildren<Transform>(true)) part.gameObject.layer=pathLayer;
                    Vector3 offset=tile.transform.localRotation * new Vector3(bounds[kind].center.x*scale, 0, bounds[kind].center.z*scale);
                    holder.transform.localPosition = new Vector3((float)item.CenterX,
                        soil + settings.TopAboveSoil - bounds[kind].max.y*scale, (float)item.CenterZ) - offset;
                    ValidatePlacedBounds(tile.transform, bounds[kind], path, soil + settings.TopAboveSoil);
                }
                if (candidate.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    candidate.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Candidate path unexpectedly owns physics");
                // Fill remaining spaces with smaller copies of the same authored
                // stone models, combined into short visual-only LOD segments.
                GapFailure = null;
                try
                {
                    candidateFill = TempleStoneGapFill.Create(transform, path, footprints, layout, prefabs,
                        bounds, soil, Mathf.Min(settings.TopAboveSoil, .017f), pathLayer);
                    if (candidateFill.Count < 100)
                        throw new InvalidOperationException("Too few small gap stones: " + candidateFill.Count);
                }
                catch (Exception error)
                {
                    if (candidateFill != null) { candidateFill.Dispose(); candidateFill = null; }
                    GapFailure = error.Message;
                    Debug.LogWarning("Small stone dressing unavailable; keeping the original individual stones: " + GapFailure, this);
                }
                var captured=new List<RenderLease>(oldRelief.Length + oldBed.Length);
                foreach (var r in oldRelief) { if (!r) throw new InvalidOperationException("Missing old path renderer"); captured.Add(new RenderLease { renderer=r, prior=r.forceRenderingOff }); }
                foreach (var r in oldBed) { if (!r) throw new InvalidOperationException("Missing old bed renderer"); captured.Add(new RenderLease { renderer=r, prior=r.forceRenderingOff }); }
                leases=captured.ToArray();
                foreach (var entry in leases) entry.renderer.forceRenderingOff=true;
                ownedRoot=candidate; candidate=null;
                gapFill=candidateFill; candidateFill=null;
                ownedRoot.SetActive(true); if (gapFill != null) gapFill.SetActive(true);
                LastResult=new Result(true, settings.Requested, actual, attempts, "");
                return LastResult;
            }
            catch (Exception error)
            {
                if (candidate) { candidate.SetActive(false); candidate.name="Retired individual forest stones"; Destroy(candidate); }
                if (candidateFill != null) candidateFill.Dispose();
                Release();
                LastResult=new Result(false, settings.Requested, actual, attempts, error.Message);
                return LastResult;
            }
            finally { preparing=false; }
        }

        static void ValidateSettings(Settings s)
        {
            if (s.Requested < 1 || s.Requested > 1000 || s.Minimum < 1 || s.Minimum > s.Requested ||
                s.Attempts < s.Requested || s.Attempts > 50000 || (long)s.Requested * s.Attempts > 20000000 ||
                float.IsNaN(s.TopAboveSoil) || float.IsInfinity(s.TopAboveSoil) || s.TopAboveSoil <= 0 || s.TopAboveSoil > .1f)
                throw new ArgumentException("Invalid requested count, minimum, attempt budget or stone height");
        }
        static Bounds PrefabBounds(GameObject prefab)
        {
            if (!prefab || prefab.GetComponentsInChildren<Collider>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                prefab.GetComponentsInChildren<AudioSource>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Animator>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Camera>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("Stone prefab must be visual-only");
            var group=prefab.GetComponent<LODGroup>();
            if (!group || group.lodCount != 3) throw new InvalidOperationException("Three stone LODs are required");
            bool found=false; Bounds bounds=new Bounds();
            foreach (var lod in group.GetLODs())
            {
                if (lod.renderers.Length != 1 || !lod.renderers[0]) throw new InvalidOperationException("Each LOD needs one renderer");
                var filter=lod.renderers[0].GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || !lod.renderers[0].sharedMaterial)
                    throw new InvalidOperationException("Stone LOD mesh and material are required");
                Matrix4x4 matrix=prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b=filter.sharedMesh.bounds;
                for (int i=0; i<8; i++)
                {
                    Vector3 q=matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if (!Finite(q.x) || !Finite(q.y) || !Finite(q.z)) throw new InvalidOperationException("Nonfinite stone bounds");
                    if (!found) { bounds=new Bounds(q,Vector3.zero); found=true; } else bounds.Encapsulate(q);
                }
            }
            if (!found || bounds.size.x < .01f || bounds.size.y < .001f || bounds.size.z < .01f)
                throw new InvalidOperationException("Degenerate stone bounds");
            return bounds;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void ValidatePlacedBounds(Transform tile, Bounds b, TempleStoneLayoutRules.Rectangle path, float top)
        {
            for (int i=0; i<8; i++)
            {
                Vector3 corner=b.center + Vector3.Scale(b.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 point=transform.InverseTransformPoint(tile.TransformPoint(corner));
                if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z) ||
                    point.x < path.MinX-.0002 || point.x > path.MaxX+.0002 ||
                    point.z < path.MinZ-.0002 || point.z > path.MaxZ+.0002 || point.y > top+.0002f)
                    throw new InvalidOperationException("Placed stone bounds exceeded the approved path or height");
            }
        }
        // Idempotent and safe during disabled/destroyed hierarchy teardown.
        public void Release()
        {
            if (gapFill != null) { gapFill.Dispose(); gapFill=null; }
            if (ownedRoot) { ownedRoot.SetActive(false); ownedRoot.name="Retired individual forest stones"; Destroy(ownedRoot); ownedRoot=null; }
            if (leases != null)
            {
                foreach (var entry in leases) if (entry.renderer) entry.renderer.forceRenderingOff=entry.prior;
                leases=null;
            }
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
    }
}
