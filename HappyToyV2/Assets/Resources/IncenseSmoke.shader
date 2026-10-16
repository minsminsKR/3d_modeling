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
            CBUFFER_START(UnityPerMaterial)
                float _FlowTime;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; float4 screen:TEXCOORD1; float eye:TEXCOORD2; };
            V vert(A a)
            {
                V o; o.p=TransformObjectToHClip(a.p.xyz); o.uv=a.uv; o.c=a.c;
                o.screen=ComputeScreenPos(o.p); o.eye=-TransformWorldToView(TransformObjectToWorld(a.p.xyz)).z;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),
                    lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
            float fbm(float2 p)
            {
                return noise(p)*.57+noise(p*2.03+17.4)*.28+noise(p*4.11-9.2)*.15;
            }
            half4 frag(V i):SV_Target
            {
                // Stretched, advected density has no circular sprite boundary.
                float2 flow=float2(i.uv.x*2.5,i.uv.y*2.1-_FlowTime*.23);
                float curl=fbm(flow*.83+float2(_FlowTime*.07,0))-.5;
                float lateral=i.uv.x*2-1+curl*.26;
                float edge=exp(-lateral*lateral*5.8)*(1-smoothstep(.68,1,abs(lateral)));
                float density=.44+.56*fbm(flow+float2(curl*.8,_FlowTime*.06));
                float scene=LinearEyeDepth(SampleSceneDepth(i.screen.xy/i.screen.w),_ZBufferParams);
                float soft=saturate((scene-i.eye)*6)*smoothstep(.04,.15,i.eye);
                return half4(i.c.rgb,i.c.a*edge*density*soft);
            }
            ENDHLSL
        }
    }
}
