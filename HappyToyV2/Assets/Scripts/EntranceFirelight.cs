using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>A small, steady flame variation owned by its visible fixture.</summary>
    [DisallowMultipleComponent]
    public sealed class EntranceFirelight : MonoBehaviour
    {
        public Light Source {get;private set;}
        public Transform Fixture {get;private set;}
        public float BaselineIntensity {get;private set;}
        public float SimulationTime {get;private set;}
        float phase;
        public void Configure(Light source,Transform fixture,float seed)
        {
            Source=source;Fixture=fixture;BaselineIntensity=source.intensity;phase=seed;
        }
        void Update()
        {
            var session=GameSession.Current;
            if(!Source||!Source.isActiveAndEnabled||!session||!session.InputAllowed||session.Finished)return;
            SimulationTime+=Time.deltaTime;
            // Slow six-percent breathing; no flashes or sharp danger strobe.
            float brightness=session.Shell.ReducedMotion?1:.94f+.06f*Mathf.PerlinNoise(phase,SimulationTime*.65f);
            Source.intensity=BaselineIntensity*brightness;
        }
    }
}
