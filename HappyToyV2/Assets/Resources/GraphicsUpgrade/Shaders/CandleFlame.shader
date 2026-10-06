Shader "HappyToy/GraphicsUpgrade/CandleFlame"
{
    Properties
    {
        _FlameTex("Photographic transparent flame", 2D) = "white" {}
        [HDR] _FlameColor("Flame tint", Color) = (1,1,1,1)
        _Emission("Moderate flame radiance", Float) = 1.55
        _Opacity("Soft edge opacity", Range(0,1)) = .76
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "PhotographicFlame"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_FlameTex); SAMPLER(sampler_FlameTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _FlameTex_ST;
                half4 _FlameColor;
                half _Emission;
                half _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=TRANSFORM_TEX(input.uv,_FlameTex);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 flame=SAMPLE_TEXTURE2D(_FlameTex,sampler_FlameTex,input.uv);
                return half4(flame.rgb*_FlameColor.rgb*_Emission,flame.a*_Opacity);
            }
            ENDHLSL
        }
    }
}
