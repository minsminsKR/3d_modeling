using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>One physical tube, its local light and its optional dust volume share one clock.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolFluorescent : MonoBehaviour
    {
        public Light Source {get;private set;}
        public Light Backscatter {get;private set;}
        public float BaselineIntensity {get;private set;}
        public double SimulationTime=>clock==null?0:clock.Elapsed;
        Material tube;
        Material[] additionalTubes;
        float backscatterIntensity;
        Color emission;
        SchoolFluorescentClock clock;
        MeshRenderer haze;
        MaterialPropertyBlock values;
        public void Configure(Light source,Material tubeMaterial,Color glow,float phase,Light backscatter=null,
            Material[] additionalTubeMaterials=null)
        {
            Source=source;BaselineIntensity=source.intensity;tube=tubeMaterial;emission=glow;
            additionalTubes=additionalTubeMaterials;
            Backscatter=backscatter;backscatterIntensity=backscatter?backscatter.intensity:0;
            clock=new SchoolFluorescentClock(phase);values=new MaterialPropertyBlock();
        }
        public void SetHaze(MeshRenderer renderer){haze=renderer;Apply(1);}
        void Update()
        {
            if(!Source||clock==null)return;
            var session=GameSession.Current;
            bool playing=session&&session.InputAllowed&&!session.Finished;
            // Pausing never changes an already rendered fixture, even for a newly opened settings panel.
            if(!playing)return;
            bool reduced=session.Shell&&session.Shell.ReducedMotion;
            Apply(clock.Advance(Time.deltaTime,true,reduced),reduced);
        }
        void Apply(float gain,bool reduced=false)
        {
            Source.intensity=BaselineIntensity*gain;
            if(Backscatter)Backscatter.intensity=backscatterIntensity*gain;
            if(tube)tube.SetColor("_EmissionColor",emission*gain);
            if(additionalTubes!=null)
                foreach(var other in additionalTubes)if(other)other.SetColor("_EmissionColor",emission*gain);
            if(!haze)return;
            values.SetVector("_SourcePosition",Source.transform.position);
            values.SetColor("_ScatterColor",Source.color.linear);
            values.SetFloat("_SourcePower",Source.intensity*.06f);
            values.SetFloat("_FlowTime",reduced?0:(float)SimulationTime*.025f);
            haze.SetPropertyBlock(values);
        }
    }
}
