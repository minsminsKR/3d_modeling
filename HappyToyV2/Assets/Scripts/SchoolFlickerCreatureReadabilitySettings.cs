using UnityEngine;

namespace HappyToy.V2
{
    // Persistent inspector controls for the creature-only presentation. The
    // encounter applies them only while the emergency lights are on.
    [CreateAssetMenu(menuName="HappyToy/School Creature Readability")]
    public sealed class SchoolFlickerCreatureReadabilitySettings : ScriptableObject
    {
        [Header("Creature surface lighting")]
        [ColorUsage(false, true)] public Color rimColor = new Color(1f,.24f,.16f,1f);
        [Range(0,2)] public float sideStrength = .32f;
        [Range(0,2)] public float rimIntensity = .7f;
        [Range(.5f,8)] public float rimPower = 3f;
        [Range(0,2)] public float nearFill = .65f;
        [Range(0,2)] public float farFill = .85f;
        [Min(0)] public float nearDistance = 4f;
        [Min(.1f)] public float farDistance = 22f;
        [Header("Red silhouette backdrop")]
        [Min(.1f)] public float haloWidth = 4.8f;
        [Min(.1f)] public float haloHeight = 3.6f;
        [Min(0)] public float haloBehind = 1.35f;
        [Min(0)] public float haloCentreHeight = 1.5f;
        [Range(0,2)] public float haloIntensity = .8f;
        public float FillAtDistance(float metres)
        {
            float span=Mathf.Max(.01f,farDistance-nearDistance);
            return Mathf.Lerp(nearFill,farFill,Mathf.Clamp01((metres-nearDistance)/span));
        }
    }
}
