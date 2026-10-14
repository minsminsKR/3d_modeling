Shader "Hidden/HappyToy/CyclopseExpressionPreview"
{
    Properties
    {
        _BaseMap("Original albedo", 2D) = "white" {}
        _FaceMap("Generated chase face projection", 2D) = "white" {}
        _ExpressionWeight("Expression", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FaceMap); SAMPLER(sampler_FaceMap);
            CBUFFER_START(UnityPerMaterial)
                float _ExpressionWeight;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 faceUV : TEXCOORD1; float4 colour : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 faceUV : TEXCOORD1; float mask : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv; output.faceUV=input.faceUV; output.mask=input.colour.r; return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                half3 chase=SAMPLE_TEXTURE2D(_FaceMap,sampler_FaceMap,input.faceUV).rgb;
                return half4(lerp(original,chase,saturate(input.mask*_ExpressionWeight)),1);
            }
            ENDHLSL
        }
    }
}
