using UnityEngine;

namespace HappyToy.V2
{
    [CreateAssetMenu(menuName="HappyToy/School Emergency Backlight")]
    public sealed class SchoolEmergencyBacklightSettings : ScriptableObject
    {
        [Header("Fixed emergency fixture inside the corridor")]
        public Vector3 fixturePosition=new Vector3(.8f,2.82f,37.55f);
        [ColorUsage(false,true)] public Color lightColor=new Color(1f,.035f,.012f);
        [Min(0)] public float intensity=30f;
        [Min(.1f)] public float range=22f;
        [Range(1,179)] public float spotAngle=100f;
        [Range(0,45)] public float downAngle=7f;
        [Header("Short-range real end-wall illumination")]
        [Min(0)] public float fillIntensity=5f;
        [Min(.1f)] public float fillRange=7f;
    }
}
