using UnityEngine;

namespace HappyToy.V2
{
    // Keeps the visual fixture and local bounce consistent with story-driven flicker.
    public sealed class FixtureLightLink : MonoBehaviour
    {
        public Light source, bounce;
        public Renderer diffuser;
        MaterialPropertyBlock block;
        Color emission;
        float baseIntensity, bounceIntensity;
        void Awake()
        {
            block=new MaterialPropertyBlock();
            baseIntensity=source?Mathf.Max(source.intensity,.01f):1;
            bounceIntensity=bounce?bounce.intensity:0;
            emission=diffuser&&diffuser.sharedMaterial?diffuser.sharedMaterial.GetColor("_EmissionColor"):Color.black;
        }
        void LateUpdate()
        {
            bool lit=source&&source.enabled&&source.gameObject.activeInHierarchy;
            float ratio=lit?source.intensity/baseIntensity:0;
            if(bounce){bounce.enabled=lit;bounce.intensity=bounceIntensity*ratio;bounce.color=source?source.color:Color.white;}
            if(diffuser)
            {
                diffuser.GetPropertyBlock(block);block.SetColor("_EmissionColor",emission*ratio);
                block.SetColor("_BaseColor",lit?new Color(.72f,.78f,.73f):new Color(.10f,.12f,.11f));diffuser.SetPropertyBlock(block);
            }
        }
    }
}
