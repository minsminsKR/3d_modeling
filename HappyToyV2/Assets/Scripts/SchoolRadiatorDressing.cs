using UnityEngine;

namespace HappyToy.V2
{
    // The generated cast-iron assembly includes its feet, end valves and short
    // capped connections. It replaces the old covers without adding route bodies.
    public static class SchoolRadiatorDressing
    {
        public static void Build(Transform section)
        {
            for(int index=0;index<10;index++)
            {
                float z=4.8f+index*3.1f;
                // Front is source -Z. A quarter turn faces the west/clear aisle;
                // the back connections meet the existing east wall at x=2.11.
                SchoolRenewalModels.AtFeet("radiator",section,new Vector3(2.035f,.006f,z),
                    new Vector3(.98f,.64f,.23f),90);
            }
        }
    }
}
