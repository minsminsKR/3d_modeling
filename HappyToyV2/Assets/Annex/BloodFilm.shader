Shader "HappyToy/BloodFilm"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 frag(V i):SV_Target
            {
                float d=length(i.uv-.5)*2;
                float alpha=1-smoothstep(.65,1,d);
                return half4(lerp(half3(.095,.001,.003),half3(.23,.007,.012),saturate(d)),alpha*.96);
            }
            ENDHLSL
        }
    }
}
