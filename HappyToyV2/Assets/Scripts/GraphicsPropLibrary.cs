using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Imported models/meshes/textures remain shared immutable Resources assets.
    // Instances belong to their visual parent; the caller's material pool owns resolved materials.
    public static class GraphicsPropLibrary
    {
        const string Prefix = "GraphicsUpgrade/Props/";
        static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal)
        { "candle-waymark", "battery-supply", "paper-lantern", "seal-altar", "cabinet-shell", "cabinet-timber", "door-hardware" };

        public static Transform Attach(string key, Transform parent, Func<string, Material> resolver,
            ICollection<UnityEngine.Object> owned = null)
        {
            if (!Keys.Contains(key) || !parent || resolver == null) throw new ArgumentException("Invalid graphics prop request");
            var prefab = Resources.Load<GameObject>(Prefix + key);
            if (!prefab) throw new InvalidOperationException("Missing modeled graphics prop " + key);
            var instance = new GameObject("Authored realistic prop — " + key);
            instance.transform.SetParent(parent, false);
            // The wrapper has identity local TRS. The imported child's unit/axis conversion stays intact.
            UnityEngine.Object.Instantiate(prefab, instance.transform, false);
            try
            {
                ValidateVisual(instance);
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var imported = renderer.sharedMaterials;
                    var assigned = new Material[imported.Length];
                    for (int i = 0; i < imported.Length; i++)
                    {
                        if (!imported[i]) throw new InvalidOperationException("Prop material slot absent " + key);
                        assigned[i] = resolver(imported[i].name);
                        if (!assigned[i]) throw new InvalidOperationException("Prop material unresolved " + key + ": " + imported[i].name);
                    }
                    renderer.sharedMaterials = assigned;
                    renderer.receiveShadows = true;
                }
                owned?.Add(instance);
                return instance.transform;
            }
            catch
            {
                UnityEngine.Object.Destroy(instance);
                throw;
            }
        }

        static void ValidateVisual(GameObject instance)
        {
            if (instance.GetComponentsInChildren<Collider>(true).Length != 0 ||
                instance.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Graphics prop unexpectedly has physics components");
            foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) throw new InvalidOperationException("Graphics prop has no authored mesh");
            foreach (var filter in filters)
                if (!filter.sharedMesh || filter.sharedMesh.vertexCount == 0)
                    throw new InvalidOperationException("Graphics prop mesh is empty");
        }

        // Uses imported local mesh bounds, including its preserved FBX conversion transforms.
        public static Bounds LocalBounds(Transform root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            var bounds = new Bounds(); bool found = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var source = filter.sharedMesh.bounds;
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    var point = matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents, sign));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found) throw new InvalidOperationException("Graphics prop has no measurable local bounds");
            return bounds;
        }

        // Visual fit only. The target is expressed in the existing parent coordinates.
        // Caller preserves original colliders, markers and moving-leaf attachment.
        public static void Fit(Transform visual, Bounds targetInParent)
        {
            if (!visual || !visual.parent || targetInParent.size.x <= 0 || targetInParent.size.y <= 0 || targetInParent.size.z <= 0)
                throw new ArgumentException("Invalid prop fit");
            visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
            var source = LocalBounds(visual);
            if (source.size.x < .0001f || source.size.y < .0001f || source.size.z < .0001f)
                throw new InvalidOperationException("Degenerate prop bounds");
            var scale = new Vector3(targetInParent.size.x / source.size.x, targetInParent.size.y / source.size.y, targetInParent.size.z / source.size.z);
            visual.localScale = scale;
            visual.localPosition = targetInParent.center - Vector3.Scale(source.center, scale);
        }

        public static Transform CreateFlame(Transform parent, ICollection<UnityEngine.Object> owned)
        {
            if (!parent || owned == null) throw new ArgumentException("Invalid flame owner");
            var prefab = Resources.Load<GameObject>(Prefix + "candle-flame");
            var shader = Resources.Load<Shader>("GraphicsUpgrade/Shaders/CandleFlame");
            var texture = Resources.Load<Texture2D>("GraphicsUpgrade/Textures/candle-flame-v3");
            if (!prefab || !shader || !texture) throw new InvalidOperationException("Realistic candle flame assets missing");
            var instance = new GameObject("Visible tapered flame");
            instance.transform.SetParent(parent, false);
            UnityEngine.Object.Instantiate(prefab, instance.transform, false);
            try
            {
                ValidateVisual(instance);
                Material material = null;
                foreach (var resource in owned)
                    if (resource is Material shared && shared && shared.shader == shader && shared.name == "Owned photographic candle flame")
                    { material = shared; break; }
                if (!material)
                {
                    material = new Material(shader) { name = "Owned photographic candle flame", enableInstancing = true };
                    material.SetTexture("_FlameTex", texture);
                    material.SetColor("_FlameColor", Color.white);
                    // The source sprite's visible body occupies only ~24% of its padded width.
                    // Crop transparent horizontal padding so the already tapered 3D ribbons
                    // show a roughly 17mm body instead of compressing it to a 7mm needle.
                    // Alpha >=4 lies entirely in U=.367..636; U=.30..70 keeps every soft edge.
                    material.SetTextureScale("_FlameTex", new Vector2(.40f, 1));
                    material.SetTextureOffset("_FlameTex", new Vector2(.30f, 0));
                    material.SetFloat("_Emission", 1.55f);
                    material.SetFloat("_Opacity", .76f);
                    owned.Add(material);
                }
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                }
                return instance.transform;
            }
            catch { UnityEngine.Object.Destroy(instance); throw; }
        }
    }
}
