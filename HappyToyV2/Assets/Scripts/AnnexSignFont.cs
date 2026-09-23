using UnityEngine;

namespace HappyToy.V2
{
    [RequireComponent(typeof(TextMesh))]
    public sealed class AnnexSignFont : MonoBehaviour
    {
        static Font korean;
        void Awake(){Apply();}
        public void Apply()
        {
            if(!korean)korean=Font.CreateDynamicFontFromOSFont("Malgun Gothic",64);
            var label=GetComponent<TextMesh>();label.font=korean;
            GetComponent<MeshRenderer>().sharedMaterial=korean.material;
        }
    }
}
