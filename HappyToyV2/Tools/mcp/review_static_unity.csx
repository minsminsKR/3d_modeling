// Read-only MCP eval. Set the Resources key for another static prop.
const string resourcesKey = "CorridorEntrance/buddhist-lantern-visual";
var prefab = Resources.Load<GameObject>(resourcesKey);
if (!prefab) throw new InvalidOperationException("Static visual missing: " + resourcesKey);
var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
Func<Vector3,float[]> xyz = value => new[]{value.x,value.y,value.z};
var details = new {
    unity=Application.unityVersion,resourcesKey,path=AssetDatabase.GetAssetPath(prefab),
    colliders=prefab.GetComponentsInChildren<Collider>(true).Length,
    rigidbodies=prefab.GetComponentsInChildren<Rigidbody>(true).Length,
    meshes=filters.Select(filter=>new {
        filter.name,path=AssetDatabase.GetAssetPath(filter.sharedMesh),
        vertices=filter.sharedMesh.vertexCount,triangles=filter.sharedMesh.triangles.Length/3,
        uv0=filter.sharedMesh.uv.Length,filter.sharedMesh.isReadable,
        bounds=xyz(filter.sharedMesh.bounds.size) }).ToArray(),
    materials=materials.Select(material=>new {
        material.name,path=AssetDatabase.GetAssetPath(material),shader=material.shader.name,material.enableInstancing,
        keywords=material.shaderKeywords,maps=new[]{"_BaseMap","_MetallicGlossMap","_BumpMap","_EmissionMap"}.Select(property=>new {
            property,path=AssetDatabase.GetAssetPath(material.GetTexture(property)),
            size=material.GetTexture(property)?new[]{material.GetTexture(property).width,material.GetTexture(property).height}:null
        }).ToArray() }).ToArray(),
    lods=prefab.GetComponentsInChildren<LODGroup>(true).Select(group=>new {
        group.size,transitions=group.GetLODs().Select(level=>new {
            level.screenRelativeTransitionHeight,renderers=level.renderers.Select(renderer=>renderer.name).ToArray()
        }).ToArray() }).ToArray()
};
if (filters.Any(filter=>!filter.sharedMesh || filter.sharedMesh.uv.Length!=filter.sharedMesh.vertexCount) ||
    materials.Any(material=>!material || !material.GetTexture("_BaseMap")))
    throw new InvalidOperationException("Static visual has missing actual meshes/UV/PBR");
return Newtonsoft.Json.JsonConvert.SerializeObject(details,Newtonsoft.Json.Formatting.Indented);
