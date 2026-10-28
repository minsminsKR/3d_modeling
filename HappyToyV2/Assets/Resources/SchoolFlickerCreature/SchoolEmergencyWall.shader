Shader "HappyToy/SchoolEmergencyWall"
{
    Properties { _WallColor("Illuminated wall", Color) = (0.3, 0.01, 0.004, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "EmergencyWall"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _WallColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // Broad wall wash, not a radial, character-centred glow.
                half wash=lerp(.65,1.0,saturate(input.uv.y));
                return half4(_WallColor.rgb*wash,.70);
            }
            ENDHLSL
        }
    }
}
