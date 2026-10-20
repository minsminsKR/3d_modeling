// Run through Unity Pipeline MCP eval in an idle, nographics Editor. No asset saves.
// eval wraps a method body and already imports System/Linq/Unity namespaces.

var folder = "Assets/Resources/Lady/";
Func<Vector3, float[]> xyz = value => new[] { value.x, value.y, value.z };
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "LadyRuntime.prefab");
var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "chase.anim");
var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "Skin.mat");
if (!prefab || !clip || !material) throw new InvalidOperationException("Lady assets missing");
var root = UnityEngine.Object.Instantiate(prefab);
root.hideFlags = HideFlags.HideAndDontSave;
root.SetActive(false);
try
{
    var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    var bindings = AnimationUtility.GetCurveBindings(clip);
    var lod = root.GetComponent<LODGroup>();
    var meshes = skins.Select(skin => new {
        skin.name, vertices = skin.sharedMesh.vertexCount,
        triangles = skin.sharedMesh.triangles.Length / 3,
        bones = skin.bones.Length, missingBones = skin.bones.Count(bone => !bone),
        skin.sharedMesh.isReadable, skin.updateWhenOffscreen,
        boundsCenter = xyz(skin.localBounds.center), boundsSize = xyz(skin.localBounds.size),
        boneQuality = skin.quality.ToString()
    }).ToArray();
    var textures = new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }
        .Select(property => new {
            property,
            path = material.HasProperty(property) ? AssetDatabase.GetAssetPath(material.GetTexture(property)) : "unsupported"
        }).ToArray();
    var textureSettings = new[] { "albedo.png", "normal.png", "metallic-smoothness.png", "game-orm.png" }
        .Select(file => {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + file);
            var importer = (TextureImporter)AssetImporter.GetAtPath(folder + file);
            return new { file, texture.width, texture.height, format = texture.format.ToString(),
                         importer.sRGBTexture, importer.mipmapEnabled, importer.mipMapBias,
                         importer.anisoLevel, importer.maxTextureSize,
                         compression = importer.textureCompression.ToString() };
        }).ToArray();
    var details = new {
        unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(),
        playing = EditorApplication.isPlaying, prefab = AssetDatabase.GetAssetPath(prefab),
        meshes, textures, textureSettings,
        shader = material.shader.name, cull = material.GetFloat("_Cull"),
        clip = new { clip.length, clip.frameRate, clip.legacy, wrap = clip.wrapMode.ToString(),
                     curves = bindings.Length, keyframes = bindings.Sum(binding => AnimationUtility.GetEditorCurve(clip, binding).length),
                     missingPaths = bindings.Where(binding => !root.transform.Find(binding.path)).Select(binding => binding.path).Distinct().ToArray() },
        lod = new { lod.size, localReferencePoint = xyz(lod.localReferencePoint), transitions = lod.GetLODs().Select(level => new {
            level.screenRelativeTransitionHeight, level.fadeTransitionWidth,
            renderers = level.renderers.Select(renderer => renderer.name).ToArray() }).ToArray() }
    };
    return Newtonsoft.Json.JsonConvert.SerializeObject(details, Newtonsoft.Json.Formatting.Indented);
}
finally { UnityEngine.Object.DestroyImmediate(root); }
