using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // The three repeated sections share the same two authored poster textures.
    public sealed class SchoolAwardPosterPresentation : MonoBehaviour
    {
        readonly List<MeshRenderer> posters=new List<MeshRenderer>();
        Material normal,anomaly;

        public void Prepare(SchoolLoopWorld world)
        {
            var normalTexture=Resources.Load<Texture2D>("SchoolAwardPoster/poster-normal");
            var ghostTexture=Resources.Load<Texture2D>("SchoolAwardPoster/poster-anomaly-specter");
            if(!normalTexture||!ghostTexture)throw new InvalidOperationException("School award poster textures are missing");
            var source=world.Surfaces.Get("paper-aged",Color.white);
            normal=MaterialFor(world,source,normalTexture,"Normal school award poster");
            anomaly=MaterialFor(world,source,ghostTexture,"Haunted school award poster");
            foreach(Transform section in world.Root)
            {
                if(!section.name.StartsWith("School loop section ",StringComparison.Ordinal))continue;
                var poster=GameObject.CreatePrimitive(PrimitiveType.Quad);
                poster.name="School award poster";poster.layer=8;
                Destroy(poster.GetComponent<Collider>());
                poster.transform.SetParent(section,false);
                // The generated wall finish protrudes past the structural wall.
                poster.transform.localPosition=new Vector3(2.04f,1.65f,13.6f);
                poster.transform.localRotation=Quaternion.Euler(0,90,0);
                poster.transform.localScale=new Vector3(.64f,.96f,1);
                var renderer=poster.GetComponent<MeshRenderer>();renderer.sharedMaterial=normal;
                renderer.shadowCastingMode=ShadowCastingMode.Off;posters.Add(renderer);
            }
        }
        static Material MaterialFor(SchoolLoopWorld world,Material source,Texture texture,string label)
        {
            var material=new Material(source){name=label};
            material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",Vector2.one);
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Cull",0);
            world.owned.Add(material);return material;
        }
        public void SetAnomaly(bool active)
        {
            foreach(var poster in posters)if(poster)poster.sharedMaterial=active?anomaly:normal;
        }
    }
}
