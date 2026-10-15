Shader "HappyToy/IncenseSmoke"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; float4 screen:TEXCOORD1; float eye:TEXCOORD2; };
            V vert(A a) { V o; o.p=TransformObjectToHClip(a.p.xyz); o.uv=a.uv; o.c=a.c; o.screen=ComputeScreenPos(o.p); o.eye=-TransformWorldToView(TransformObjectToWorld(a.p.xyz)).z; return o; }
            half4 frag(V i):SV_Target
            {
                float r=length(i.uv*2-1); float a=pow(saturate(1-r),2);
                float edge=LinearEyeDepth(SampleSceneDepth(i.screen.xy/i.screen.w),_ZBufferParams)-i.eye;
                return half4(i.c.rgb,i.c.a*a*saturate(edge*8));
            }
            ENDHLSL
        }
    }
}
