using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Enforce non-emissive skin throughout the event. The actual material is
    // lit by the environment and may still respond to the player's flashlight.
    public sealed class SchoolFlickerCreatureVisibility : IDisposable
    {
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        readonly Renderer[] renderers;
        readonly MaterialPropertyBlock[] original;
        readonly MaterialPropertyBlock[] applied;
        bool controlled;
        public SchoolFlickerCreatureVisibility(Transform creature)
        {
            if(!creature)throw new ArgumentNullException(nameof(creature));
            renderers=creature.GetComponentsInChildren<Renderer>(true);
            original=new MaterialPropertyBlock[renderers.Length];
            applied=new MaterialPropertyBlock[renderers.Length];
            for(int i=0;i<renderers.Length;i++)
            {
                original[i]=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(original[i]);
                applied[i]=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(applied[i]);
            }
        }
        public void Set(bool active,bool dark)
        {
            if(!active){Restore();return;}
            if(controlled)return;
            controlled=true;
            for(int i=0;i<renderers.Length;i++)if(renderers[i])
            {
                applied[i].SetColor(EmissionColor,Color.black);
                renderers[i].SetPropertyBlock(applied[i]);
            }
        }
        void Restore()
        {
            if(!controlled)return;
            for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].SetPropertyBlock(original[i]);
            controlled=false;
        }
        public void Dispose()=>Restore();
    }
}
