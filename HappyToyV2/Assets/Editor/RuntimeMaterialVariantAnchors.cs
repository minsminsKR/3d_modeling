using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2.Editor
{
    // Retain precisely the shader_feature combinations created by runtime materials.
    // All assets created here are new build inputs. No scene/model/source material is edited.
    public static class RuntimeMaterialVariantAnchors
    {
        public const string Folder="Assets/Resources/FeedbackShaderVariants";
        sealed class Definition
        {
            public Material prototype;
            public string signature,path;
            public readonly List<string> sources=new List<string>();
        }
        [Serializable] sealed class Entry { public string asset,shader;public string[] keywords,sources; }
        [Serializable] sealed class Manifest
        {
            public string scope="Owned Resources material anchors retain runtime shader_feature combinations; no original asset, scene, model or shader stripping setting modified.";
            public string scene,unity;public int count;public Entry[] entries;
        }
        public static int Ensure(string scenePath)
        {
            var scene=SceneManager.GetActiveScene();
            if(!scene.IsValid() || scene.path!=scenePath || EditorApplication.isPlaying)
                throw new InvalidOperationException("Inspect the validated authored scene outside Play mode before retaining shaders");
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            if(!lit) throw new InvalidOperationException("Missing actual URP Lit shader");
            var normal=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Corridor/aged-floor-normal-v2.png");
            if(!normal) throw new InvalidOperationException("Missing imported runtime floor normal map");
            var definitions=new Dictionary<string,Definition>(StringComparer.Ordinal);
            var prototypes=new List<Material>();
            try
            {
                void Add(Material material,string source)
                {
                    prototypes.Add(material);
                    string[] keywords=material.shaderKeywords.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                    string signature=material.shader.name+"|"+string.Join(";",keywords);
                    if(!definitions.TryGetValue(signature,out var definition))
                    {
                        definition=new Definition {prototype=material,signature=signature,
                            path=Folder+"/Lit-"+Digest(signature)+".mat"};definitions.Add(signature,definition);
                    }
                    definition.sources.Add(source);
                }
                var surface=new Material(lit);surface.SetTexture("_BumpMap",normal);surface.EnableKeyword("_NORMALMAP");
                Add(surface,"CorridorRun floor/ceiling and HauntedCorridorPresentation timber surfaces");
                var emissive=new Material(lit);emissive.SetColor("_EmissionColor",new Color(1,.49f,.1f));emissive.EnableKeyword("_EMISSION");
                emissive.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                Add(emissive,"CorridorRun lantern shade, HauntedCorridorPresentation paper shade, LightExplorationRun candle/locator flame");
                var keys=new[]{"Cyclopse","Uncat","Hwacat_angry","Baby"};
                var controllers=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<V1MonsterMotion>(true)).ToArray();
                foreach(string key in keys)
                {
                    var controller=controllers.SingleOrDefault(motion=>motion.name.IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0);
                    if(!controller || !controller.model) throw new InvalidOperationException("Missing authored enemy shader template: "+key);
                    var normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/EnemyRefinement/"+key+"-normal.png");
                    var packed=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/EnemyRefinement/"+key+"-metallic-smoothness.png");
                    if(!normalMap || !packed) throw new InvalidOperationException("Missing imported enemy shader maps: "+key);
                    var materials=controller.model.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(skin=>skin.sharedMaterials).Distinct().ToArray();
                    if(materials.Length==0) throw new InvalidOperationException("Missing authored enemy materials: "+key);
                    foreach(var prior in materials)
                    {
                        if(!prior || !prior.shader || !prior.HasProperty("_BumpMap") || !prior.HasProperty("_MetallicGlossMap"))
                            throw new InvalidOperationException("Enemy runtime shader map contract differs: "+key);
                        // Match EnemyVisualRefinement exactly, including inherited flags such as
                        // Cyclopse's specular/environment-reflections-off combination.
                        var material=new Material(prior);
                        material.SetTexture("_BumpMap",normalMap);material.SetFloat("_BumpScale",.75f);material.EnableKeyword("_NORMALMAP");
                        material.SetTexture("_MetallicGlossMap",packed);material.SetFloat("_Smoothness",1);
                        material.SetFloat("_SmoothnessTextureChannel",0);material.EnableKeyword("_METALLICSPECGLOSSMAP");
                        Add(material,key+" / "+AssetDatabase.GetAssetPath(prior)+" / "+prior.name);
                        var eyeGlow=new Material(material);eyeGlow.EnableKeyword("_EMISSION");
                        eyeGlow.SetColor("_EmissionColor",new Color(1.1f,.012f,.008f));
                        eyeGlow.globalIlluminationFlags=(material.globalIlluminationFlags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack) | MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        Add(eyeGlow,key+" / original eye surface emission / "+prior.name);
                    }
                }
                // Static faces keep their own original surface properties too;
                // these exact inherited keyword combinations need emission.
                var staticFaces=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Renderer>(true))
                    .Where(renderer=>renderer.sharedMaterials.Any(m=>m&&(m.name=="LanternMaskSkin"||m.name=="MannequinSkin")))
                    .SelectMany(renderer=>renderer.sharedMaterials).Where(m=>m).Distinct();
                foreach(var prior in staticFaces)
                {
                    var eyeGlow=new Material(prior);eyeGlow.EnableKeyword("_EMISSION");
                    eyeGlow.SetColor("_EmissionColor",new Color(1.1f,.012f,.008f));
                    eyeGlow.globalIlluminationFlags=(prior.globalIlluminationFlags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack) | MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    Add(eyeGlow,"Static original eye surface emission / "+AssetDatabase.GetAssetPath(prior));
                }
                if(!AssetDatabase.IsValidFolder(Folder))
                {
                    if(!AssetDatabase.IsValidFolder("Assets/Resources")) throw new InvalidOperationException("Missing Resources folder");
                    AssetDatabase.CreateFolder("Assets/Resources","FeedbackShaderVariants");
                }
                var entries=new List<Entry>();
                foreach(var definition in definitions.Values.OrderBy(x=>x.signature,StringComparer.Ordinal))
                {
                    // Unity serializes an asset's name as its file stem on CreateAsset.
                    // Use that stable name on first creation and subsequent builds.
                    string ownedName=Path.GetFileNameWithoutExtension(definition.path);
                    var material=AssetDatabase.LoadAssetAtPath<Material>(definition.path);
                    if(material && (material.name!=ownedName || material.shader!=definition.prototype.shader))
                        throw new InvalidOperationException("Shader anchor path already contains an unrelated asset: "+definition.path);
                    if(!material)
                    {
                        material=new Material(definition.prototype) {name=ownedName,hideFlags=HideFlags.None};
                        AssetDatabase.CreateAsset(material,definition.path);
                    }
                    else {material.CopyPropertiesFromMaterial(definition.prototype);EditorUtility.SetDirty(material);}
                    if(!material.shaderKeywords.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(definition.prototype.shaderKeywords.OrderBy(x=>x,StringComparer.Ordinal)))
                        throw new InvalidOperationException("Runtime shader anchor keywords differ: "+definition.path);
                    entries.Add(new Entry {asset=definition.path,shader=material.shader.name,
                        keywords=material.shaderKeywords.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),sources=definition.sources.ToArray()});
                }
                AssetDatabase.SaveAssets();
                foreach(var definition in definitions.Values)
                {
                    var saved=AssetDatabase.LoadAssetAtPath<Material>(definition.path);
                    if(!saved || saved.name!=Path.GetFileNameWithoutExtension(definition.path) ||
                        saved.shader!=definition.prototype.shader ||
                        !saved.shaderKeywords.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(definition.prototype.shaderKeywords.OrderBy(x=>x,StringComparer.Ordinal)))
                        throw new InvalidOperationException("Saved runtime shader anchor contract differs: "+definition.path);
                }
                Directory.CreateDirectory("Verification/native-readiness");
                File.WriteAllText("Verification/native-readiness/runtime-shader-anchors.json",JsonUtility.ToJson(new Manifest {
                    scene=scenePath,unity=Application.unityVersion,count=entries.Count,entries=entries.ToArray() },true));
                Debug.Log("HAPPYTOY_RUNTIME_SHADER_ANCHORS_PASS "+entries.Count+" actual shader/keyword combinations");
                return entries.Count;
            }
            finally {foreach(var material in prototypes)if(material)UnityEngine.Object.DestroyImmediate(material);}
        }
        static string Digest(string signature)
        {
            using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(signature))).Replace("-","").ToLowerInvariant();
        }
    }
}
