using UnityEngine;

namespace HappyToy.V2
{
    [CreateAssetMenu(menuName="HappyToy/School Emergency Backlight")]
    public sealed class SchoolEmergencyBacklightSettings : ScriptableObject
    {
        [Header("Fixed corridor emergency fixture")]
        public Vector3 fixturePosition=new Vector3(0,2.85f,37.75f);
        public Vector3 backdropPosition=new Vector3(0,1.55f,38.26f);
        public Vector2 backdropSize=new Vector2(4.15f,2.95f);
        [ColorUsage(false,true)] public Color lightColor=new Color(1f,.035f,.012f);
        [Min(0)] public float intensity=8f;
        [Min(.1f)] public float range=38f;
        [Range(1,179)] public float spotAngle=70f;
        [Range(0,1)] public float wallBrightness=.30f;
    }
}
