Shader "HappyToy/SchoolFlickerHalo"
{
    Properties { _Tint("Red glow", Color) = (1.1, 0.035, 0.012, 0.80) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "SchoolCreatureBacklight"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 p=input.uv*2-1;
                float radius=length(p);
                // A softly fading filled ellipse gives a bright backdrop while
                // leaving a feathered red rim around the occluding body.
                half glow=saturate(1-radius);
                glow=glow*glow*(3-2*glow);
                return half4(_Tint.rgb,_Tint.a*glow);
            }
            ENDHLSL
        }
    }
}
