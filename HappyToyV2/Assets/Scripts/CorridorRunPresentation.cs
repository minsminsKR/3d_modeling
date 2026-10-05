using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class CorridorRun
    {
        public HauntedCorridorPresentation Presentation { get; private set; }

        void ApplyCorridorPresentation()
        {
            Presentation = world.gameObject.AddComponent<HauntedCorridorPresentation>();
            Presentation.Prepare(this);
        }
    }
}
