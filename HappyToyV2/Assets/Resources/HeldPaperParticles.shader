Shader "HappyToy/HeldPaperParticles"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; half4 c:COLOR; };
            V vert(A a)
            {
                V o; o.p=TransformObjectToHClip(a.p.xyz); o.uv=a.uv; o.kind=a.kind; o.c=a.c; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),
                    lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float smoke=exp(-dot(p,p)*2.6)*(1-smoothstep(.68,1,abs(p.x)))*
                    (1-smoothstep(.7,1,abs(p.y)))*(.5+.5*noise(i.uv*4+float2(i.kind.y*1.3,i.kind.y)));
                float flake=1-smoothstep(.65,.92,max(abs(p.x+p.y*.3),abs(p.y)));
                half alpha=i.c.a*lerp(smoke,flake,step(.5,i.kind.x));
                clip(alpha-.001);
                return half4(i.c.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
