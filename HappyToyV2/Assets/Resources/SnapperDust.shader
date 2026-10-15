Shader "HappyToy/SnapperDust"
{
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
 struct V { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
 V vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.color=a.color;o.uv=a.uv;return o; }
 half4 frag(V i):SV_Target { half r=length(i.uv*2-1);i.color.a*=saturate((1-r)*2);return i.color; }
 ENDHLSL
 } }
}
