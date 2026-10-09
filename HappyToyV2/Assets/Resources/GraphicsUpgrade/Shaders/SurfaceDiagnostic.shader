Shader "HappyToy/Diagnostics/UniformSurface"
{
    Properties { _BaseColor("Uniform colour", Color) = (0.25,0.25,0.25,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Output { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            Output Vert(Input input)
            { Output output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.normalWS=TransformObjectToWorldNormal(input.normalOS); return output; }
            half4 Frag(Output input):SV_Target
            { return half4(_BaseColor.rgb*(0.35+0.65*abs(normalize(input.normalWS).y)),1); }
            ENDHLSL
        }
    }
}
