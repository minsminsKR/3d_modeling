using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Optional second skin pass: the original PBR material, rig and animation
    // stay untouched. LOD entries own matching rim renderers and restore on disposal.
    public sealed class SchoolFlickerCreatureRim : IDisposable
    {
        readonly LODGroup group;
        readonly LOD[] original;
        readonly List<SkinnedMeshRenderer> overlays=new List<SkinnedMeshRenderer>();
        readonly Material material;
        bool visible;
        public bool Visible=>visible;
        public void Configure(Color color,float intensity,float power,float sideStrength)
        {
            material.SetColor("_RimColor",color);
            material.SetFloat("_RimIntensity",Mathf.Max(0,intensity));
            material.SetFloat("_RimPower",Mathf.Clamp(power,1,8));
            material.SetFloat("_SideStrength",Mathf.Clamp01(sideStrength));
        }
        public IReadOnlyList<SkinnedMeshRenderer> Overlays=>overlays;
        public SchoolFlickerCreatureRim(Transform creature)
        {
            if(!creature)throw new ArgumentNullException(nameof(creature));
            group=creature.GetComponentInChildren<LODGroup>(true);
            var shader=Resources.Load<Shader>("SchoolFlickerCreature/SchoolCreatureRim");
            if(!group||!shader)throw new InvalidOperationException("Creature rim requires its LODs and rim shader");
            material=new Material(shader){name="Owned restrained creature red rim"};
            original=group.GetLODs();var levels=group.GetLODs();
            try
            {
                for(int i=0;i<levels.Length;i++)
                {
                    var renderers=new List<Renderer>(levels[i].renderers);
                    foreach(var item in levels[i].renderers)
                    {
                        var source=item as SkinnedMeshRenderer;if(!source||!source.sharedMesh)continue;
                        var go=new GameObject("Creature rim LOD "+i);go.layer=source.gameObject.layer;
                        go.transform.SetParent(source.transform,false);
                        var rim=go.AddComponent<SkinnedMeshRenderer>();
                        rim.sharedMesh=source.sharedMesh;rim.rootBone=source.rootBone;rim.bones=source.bones;
                        rim.localBounds=source.localBounds;rim.quality=source.quality;
                        rim.updateWhenOffscreen=source.updateWhenOffscreen;
                        rim.shadowCastingMode=ShadowCastingMode.Off;rim.receiveShadows=false;
                        rim.skinnedMotionVectors=false;rim.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                        var materials=new Material[Math.Max(1,source.sharedMesh.subMeshCount)];
                        for(int j=0;j<materials.Length;j++)materials[j]=material;
                        rim.sharedMaterials=materials;rim.enabled=false;overlays.Add(rim);renderers.Add(rim);
                    }
                    levels[i].renderers=renderers.ToArray();
                }
                if(overlays.Count==0)throw new InvalidOperationException("Creature rim requires skinned LOD meshes");
                group.SetLODs(levels);
            }
            catch{Dispose();throw;}
        }
        public void Set(bool active,bool dark)
        {
            bool next=active&&!dark;if(visible==next)return;visible=next;
            foreach(var overlay in overlays)if(overlay)overlay.enabled=visible;
        }
        public void Dispose()
        {
            if(group&&original!=null)group.SetLODs(original);
            foreach(var overlay in overlays)if(overlay)DestroyOwned(overlay.gameObject);
            overlays.Clear();if(material)DestroyOwned(material);visible=false;
        }
        static void DestroyOwned(UnityEngine.Object item)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
    }
}
