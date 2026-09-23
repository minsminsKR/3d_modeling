Shader "HappyToy/ShallowWater"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            V vert(A v){V o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
            half4 frag(V i):SV_Target
            {
                float t=_Time.y;
                float wave=sin(i.world.x*6+t*.8)*cos(i.world.z*5-t*.6);
                float3 n=normalize(float3(.075*cos(i.world.x*6+t),1,.075*sin(i.world.z*5-t)));
                float fresnel=pow(1-saturate(dot(n,normalize(_WorldSpaceCameraPos-i.world))),3);
                float ripple=pow(saturate(wave),12);
                return half4(half3(.035,.10,.105)+fresnel*half3(.10,.17,.18)+ripple*.045,.35+fresnel*.3);
            }
            ENDHLSL
        }
    }
}
