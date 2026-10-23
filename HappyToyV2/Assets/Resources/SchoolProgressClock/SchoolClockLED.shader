Shader "HappyToy/SchoolClockLED"
{
    Properties { _BaseColor("LED colour",Color)=(1.4,.095,.025,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "SchoolClockLED"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output;
            }
            half4 Frag(Varyings input):SV_Target {return _BaseColor;}
            ENDHLSL
        }
    }
}
