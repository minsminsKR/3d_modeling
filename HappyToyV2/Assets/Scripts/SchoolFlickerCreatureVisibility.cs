using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Brighten only the creature's textured surface during the red flash.
    // In darkness it reflects the player's flashlight but emits no light of its own.
    public sealed class SchoolFlickerCreatureVisibility : IDisposable
    {
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        readonly Renderer[] renderers;
        readonly MaterialPropertyBlock[] original;
        readonly MaterialPropertyBlock[] applied;
        readonly Camera camera;
        readonly SchoolFlickerCreatureReadabilitySettings settings;
        readonly Transform creature;
        bool controlled,previousDark;
        public SchoolFlickerCreatureVisibility(Transform creature, Camera camera=null, SchoolFlickerCreatureReadabilitySettings settings=null)
        {
            if(!creature)throw new ArgumentNullException(nameof(creature));
            this.creature=creature;this.camera=camera;this.settings=settings;
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
            if(controlled&&previousDark==dark&&(dark||!settings||!camera))return;
            controlled=true;previousDark=dark;
            float fill=settings&&camera?settings.FillAtDistance(Vector3.Distance(camera.transform.position,creature.position)):.65f;
            var colour=new Color(fill,fill*(.48f/.65f),fill*(.40f/.65f),1);
            for(int i=0;i<renderers.Length;i++)if(renderers[i])
            {
                applied[i].SetColor(EmissionColor,dark?Color.black:colour);
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
