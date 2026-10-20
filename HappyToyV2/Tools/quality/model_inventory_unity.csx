// Read-only Unity MCP eval body. Covers all imported model sources and Resources prefabs.
var paths = AssetDatabase.FindAssets("t:Model", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p=>p).ToArray();
Func<Mesh, object> meshInfo = mesh => new {
    mesh.name, vertices=mesh.vertexCount, triangles=Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i)/3),
    mesh.subMeshCount, mesh.isReadable, blendShapes=mesh.blendShapeCount, bindposes=mesh.bindposeCount,
    memory=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh),
    unusedVertices=mesh.isReadable ? mesh.vertexCount-mesh.triangles.Distinct().Count() : -1,
    invalidVertices=mesh.isReadable ? mesh.vertices.Count(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z)) : -1
};
Func<AnimationClip, object> clipInfo = clip => {
    var bindings=AnimationUtility.GetCurveBindings(clip);
    var curves=bindings.Select(b=>new {b.path,b.propertyName,keys=AnimationUtility.GetEditorCurve(clip,b).keys}).ToArray();
    return new {clip.name,clip.length,clip.frameRate,clip.legacy, curves=bindings.Length,
        keys=curves.Sum(c=>c.keys.Length), memory=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip),
        constantScale=curves.Where(c=>c.propertyName.StartsWith("m_LocalScale.")&&c.keys.Length>0&&c.keys.Max(k=>k.value)-c.keys.Min(k=>k.value)<.00001f)
            .Select(c=>new {c.path,c.propertyName,keys=c.keys.Length,value=c.keys[0].value}).ToArray()};
};
var models=paths.Select(path=>{
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    return new {path,meshes=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Select(meshInfo).ToArray(),
        skins=skins.Select(s=>new{s.name,bones=s.bones.Length,missingBones=s.bones.Count(b=>!b)}).ToArray(),
        clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).Select(clipInfo).ToArray()};
}).ToArray();
var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).Select(path=>{
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var renders=model.GetComponentsInChildren<Renderer>(true);
    return new {path,skins=renders.OfType<SkinnedMeshRenderer>().Where(s=>s.sharedMesh).Select(s=>new{s.name,mesh=meshInfo(s.sharedMesh),bones=s.bones.Length,missingBones=s.bones.Count(b=>!b),s.updateWhenOffscreen}).ToArray(),
        materials=renders.SelectMany(r=>r.sharedMaterials).Distinct().Where(m=>m).Select(m=>new {path=AssetDatabase.GetAssetPath(m),shader=m.shader?m.shader.name:"MISSING",textures=m.GetTexturePropertyNames().Select(n=>new{name=n,path=AssetDatabase.GetAssetPath(m.GetTexture(n))}).ToArray()}).ToArray(),
        clips=AnimationUtility.GetAnimationClips(model).Distinct().Select(clipInfo).ToArray(),lodGroups=model.GetComponentsInChildren<LODGroup>(true).Length,
        missingMaterials=renders.Sum(r=>r.sharedMaterials.Count(m=>!m))};
}).ToArray();
var standalone=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Resources"}).Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p=>p.EndsWith(".anim")).OrderBy(p=>p)
    .Select(path=>new {path,clip=clipInfo(AssetDatabase.LoadAssetAtPath<AnimationClip>(path))}).ToArray();
var output="Builds/ModelPolish-20261021/unity-model-inventory.json";
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
System.IO.File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new {unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceType.ToString(),models,prefabs,standalone},Newtonsoft.Json.Formatting.Indented));
return "MODEL_INVENTORY_PASS models="+models.Length+" prefabs="+prefabs.Length+" clips="+standalone.Length;
