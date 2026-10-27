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
        Color emission,sourceColor,backscatterColor,eventTint=Color.white;
        SchoolFluorescentClock clock;
        MeshRenderer haze;
        MaterialPropertyBlock values;
        float eventGain=1,voltageGain=1;
        public void SetEventGain(float gain){eventGain=Mathf.Clamp01(gain);Apply(voltageGain);}
        public void SetEventTint(Color tint){eventTint=tint;Apply(voltageGain);}
        public void Configure(Light source,Material tubeMaterial,Color glow,float phase,Light backscatter=null,
            Material[] additionalTubeMaterials=null)
        {
            Source=source;BaselineIntensity=source.intensity;sourceColor=source.color;tube=tubeMaterial;emission=glow;
            additionalTubes=additionalTubeMaterials;
            Backscatter=backscatter;backscatterIntensity=backscatter?backscatter.intensity:0;backscatterColor=backscatter?backscatter.color:Color.white;
            clock=new SchoolFluorescentClock(phase);values=new MaterialPropertyBlock();
        }
        public void SetHaze(MeshRenderer renderer){haze=renderer;Apply(1);}
        void Update()
        {
            if(!Source||clock==null)return;
            var session=GameSession.Current;
            bool playing=session&&session.InputAllowed&&!session.Finished&&!EnemyTimeStop.Active;
            // Pausing never changes an already rendered fixture, even for a newly opened settings panel.
            if(!playing)return;
            bool reduced=session.Shell&&session.Shell.ReducedMotion;
            Apply(clock.Advance(Time.deltaTime,true,reduced),reduced);
        }
        void Apply(float gain,bool reduced=false)
        {
            voltageGain=gain;gain*=eventGain;
            Source.color=sourceColor*eventTint;
            Source.intensity=BaselineIntensity*gain;
            if(Backscatter){Backscatter.color=backscatterColor*eventTint;Backscatter.intensity=backscatterIntensity*gain;}
            if(tube)tube.SetColor("_EmissionColor",emission*eventTint*gain);
            if(additionalTubes!=null)
                foreach(var other in additionalTubes)if(other)other.SetColor("_EmissionColor",emission*eventTint*gain);
            if(!haze)return;
            values.SetVector("_SourcePosition",Source.transform.position);
            values.SetColor("_ScatterColor",Source.color.linear);
            values.SetFloat("_SourcePower",Source.intensity*.06f);
            values.SetFloat("_FlowTime",reduced?0:(float)SimulationTime*.025f);
            haze.SetPropertyBlock(values);
        }
    }
}
