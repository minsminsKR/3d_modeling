using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Brighten only the creature's textured surface during the red flash.
    // In darkness it reflects the player's flashlight but emits no light of its own.
    public sealed class SchoolFlickerCreatureVisibility : IDisposable
    {
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        static readonly Color FlashEmission=new Color(.65f,.48f,.40f,1);
        readonly Renderer[] renderers;
        readonly MaterialPropertyBlock[] original;
        readonly MaterialPropertyBlock[] applied;
        bool controlled,previousDark;
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
            if(controlled&&previousDark==dark)return;
            controlled=true;previousDark=dark;
            for(int i=0;i<renderers.Length;i++)if(renderers[i])
            {
                applied[i].SetColor(EmissionColor,dark?Color.black:FlashEmission);
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
