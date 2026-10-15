Shader "HappyToy/DoorBreakDust"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct A { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V
            {
                float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0;
                float3 positionWS:TEXCOORD1; float eyeDepth:TEXCOORD2; half3 light:TEXCOORD3; half fog:TEXCOORD4;
            };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            float Cloud(float2 p)
            {
                float density=0,weight=.55;
                [unroll] for(int n=0;n<4;n++){density+=Noise(p)*weight;p=p*2.03+13.7;weight*=.5;}
                return density;
            }
            V vert(A a)
            {
                V o;o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.eyeDepth=-TransformWorldToView(o.positionWS).z;o.color=a.color;o.uv=a.uv;
                o.light=SampleSH(half3(0,1,0))+GetMainLight().color*.2h;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count=min(GetAdditionalLightsCount(),4u);
                for(uint index=0;index<count;index++)
                {
                    Light source=GetAdditionalLight(index,o.positionWS);
                    o.light+=source.color*source.distanceAttenuation*.4h;
                }
                #endif
                o.light=clamp(o.light,.07h,1.2h);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float noise=Cloud(i.uv*4.3+i.positionWS.xz*.3);
                p+=(float2(Noise(i.uv*2.7),Noise(i.uv*2.7+17.3))-.5)*.45;
                half edge=pow(saturate(1-dot(p,p)),1.6);
                half density=smoothstep(.18h,.8h,noise+edge*.2h)*edge;
                float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneDepth=LinearEyeDepth(SampleSceneDepth(screenUV),_ZBufferParams);
                half soft=saturate((sceneDepth-i.eyeDepth)/.28);
                half alpha=i.color.a*density*soft*saturate((i.eyeDepth-.12)/.2);
                return half4(MixFog(i.color.rgb*i.light,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
