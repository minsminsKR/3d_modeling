// CPU-only partial pose/curve inspection through Pipeline MCP eval; no renders/saves.
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Lady/LadyRuntime.prefab");
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Lady/chase.anim");
var root = UnityEngine.Object.Instantiate(prefab);
root.hideFlags = HideFlags.HideAndDontSave;
root.SetActive(false);
try
{
    var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
    var ranges = bindings.Select(binding => {
        var keys = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding).keys;
        return new { binding.path, binding.propertyName, keys = keys.Length,
                     range = keys.Max(key => key.value) - keys.Min(key => key.value) };
    }).ToArray();
    var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(s => s.sharedMesh.vertexCount).First();
    var poses = new System.Collections.Generic.List<object>();
    foreach (float time in new[] { 0f, .2f, .71f, 1.8f, 3.35f, 6.1f, 9.8f, 12.55f, 16f, 19.7f, 23.45f, 28.1f, 31.95f, 35.2f, 37.1f, clip.length })
    {
        clip.SampleAnimation(root, time);
        var points = HappyToy.V2.EnemyStudioRig.PosedVertices(skin);
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity, largestOvershoot = 0;
        int outside = 0;
        foreach (var point in points)
        {
            minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
            var local = skin.transform.InverseTransformPoint(point);
            if (!skin.localBounds.Contains(local))
            {
                outside++;
                var closest = skin.transform.TransformPoint(skin.localBounds.ClosestPoint(local));
                largestOvershoot = Mathf.Max(largestOvershoot, Vector3.Distance(point, closest));
            }
        }
        poses.Add(new { time, minY, maxY, outside, largestOvershoot });
    }
    var indexUsage = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => new {
        s.name, unusedVertices = s.sharedMesh.vertexCount - s.sharedMesh.triangles.Distinct().Count(),
        meshMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(s.sharedMesh)
    }).ToArray();
    clip.SampleAnimation(root, 19.7f);
    var supportAcrossLods = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => {
        var points = HappyToy.V2.EnemyStudioRig.PosedVertices(s);
        return new { s.name, time = 19.7f, minY = points.Min(point => point.y) };
    }).ToArray();
    return Newtonsoft.Json.JsonConvert.SerializeObject(new {
        scope = "Sixteen CPU-only poses of lowest LOD; not all-frame culling or visual/FPS certification",
        curveProperties = ranges.GroupBy(row => row.propertyName).Select(group => new {
            property = group.Key, curves = group.Count(),
            minRange = group.Min(row => row.range), maxRange = group.Max(row => row.range) }).ToArray(),
        tinyCurves = ranges.Where(row => row.range < 0.00001f).ToArray(),
        clipMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip),
        indexUsage, supportAcrossLods, sampledSkin = skin.name, poses
    }, Newtonsoft.Json.Formatting.Indented);
}
finally { UnityEngine.Object.DestroyImmediate(root); }
