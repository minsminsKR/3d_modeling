using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Old school geometry/events are absent from the v3 playing world. Their
    // serialized source remains available only for existing v1/v2 saves/tests.
    public sealed class LegacySchoolRetirement
    {
        readonly List<(GameObject owner,bool active)> actors=new List<(GameObject,bool)>();
        readonly List<(Behaviour owner,bool enabled)> behaviours=new List<(Behaviour,bool)>();
        readonly List<(Renderer owner,bool enabled)> renderers=new List<(Renderer,bool)>();
        readonly List<(Collider owner,bool enabled)> colliders=new List<(Collider,bool)>();
        readonly List<(NavMeshSurface owner,bool enabled)> navigation=new List<(NavMeshSurface,bool)>();
        public void Apply(GameSession session)
        {
            var roots=session.gameObject.scene.GetRootGameObjects();
            bool Player(Component c)=>c.transform.IsChildOf(session.player.transform);
            foreach(var surface in roots.SelectMany(r=>r.GetComponentsInChildren<NavMeshSurface>(true)))
            {navigation.Add((surface,surface.enabled));surface.RemoveData();surface.enabled=false;}
            foreach(var renderer in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).Where(r=>!Player(r)))
            {renderers.Add((renderer,renderer.enabled));renderer.enabled=false;}
            foreach(var collider in roots.SelectMany(r=>r.GetComponentsInChildren<Collider>(true)).Where(c=>!Player(c)))
            {colliders.Add((collider,collider.enabled));collider.enabled=false;}
            foreach(var behaviour in roots.SelectMany(r=>r.GetComponentsInChildren<Behaviour>(true)))
            {
                if(Player(behaviour))continue;
                bool retire=behaviour is Light || behaviour is NavMeshAgent || behaviour is AudioSource ||
                    behaviour is StoryDirector || behaviour is StalkerBrain || behaviour is V1HwacatEvent ||
                    behaviour is AnnexEncounter || behaviour is UncatAnnexEvent || behaviour is LovelyDollGuide ||
                    behaviour is WeepingAngelEncounter || behaviour is LanternMaskEncounter ||
                    behaviour is AnnexRoomPresentation || behaviour is GraphicsSchoolSurfaces || behaviour is Interactable;
                if(!retire)continue;
                behaviours.Add((behaviour,behaviour.enabled));
                if(behaviour is AudioSource audio)audio.Stop();
                behaviour.enabled=false;
                if(behaviour is StalkerBrain || behaviour is WeepingAngelEncounter || behaviour is LanternMaskEncounter || behaviour is LovelyDollGuide)
                {actors.Add((behaviour.gameObject,behaviour.gameObject.activeSelf));behaviour.gameObject.SetActive(false);}
            }
        }
        public void Restore()
        {
            foreach(var state in navigation)if(state.owner){state.owner.enabled=state.enabled;if(state.enabled)state.owner.AddData();}
            foreach(var state in actors)if(state.owner)state.owner.SetActive(state.active);
            foreach(var state in behaviours)if(state.owner)state.owner.enabled=state.enabled;
            foreach(var state in renderers)if(state.owner)state.owner.enabled=state.enabled;
            foreach(var state in colliders)if(state.owner)state.owner.enabled=state.enabled;
        }
    }
}
