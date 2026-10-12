Shader "HappyToy/TitleAtmosphere"
{
    Properties
    {
        _BaseMap ("Existing authored surface", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Creature ("Silhouette", Float) = 0
        _StageOrigin ("Owned set origin", Vector) = (0,-3000,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _StageOrigin;
                float _Creature;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 p = i.positionWS - _StageOrigin.xyz;
                float3 n = normalize(i.normalWS);
                float3 weights = pow(abs(n), 8); weights /= max(dot(weights, 1), .001);
                float3 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy * .6).rgb * weights.x
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz * .6).rgb * weights.y
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy * .6).rgb * weights.z;
                if (_Creature > .5) surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 toLight = float3(2.4, 2.6, 6.2) - p;
                float red = (max(dot(n, normalize(toLight)), 0) * .65 + .35) * 3.8 / (1 + dot(toLight,toLight) * .17);
                float3 colour = surface * _BaseColor.rgb * (float3(.016,.009,.009) + float3(.82,.044,.012) * red);
                float3 view = normalize(_WorldSpaceCameraPos - i.positionWS);
                if (_Creature > .5)
                {
                    float rim = pow(1 - saturate(dot(n,view)), 3);
                    colour = surface * float3(.003,.0015,.0015) + rim * float3(.035,.007,.004) * saturate(red);
                }
                else
                {
                    // Warm depth haze belongs only to this camera's set; global game fog/light cannot wash out the silhouette.
                    float haze = saturate((p.z + 1) / 14) * .45;
                    colour = lerp(colour, float3(.18,.009,.006), haze);
                    float reflection = exp(-abs(p.x - 2.1) * 1.3) * saturate(n.y) * .045;
                    colour += float3(1,.09,.025) * reflection;
                }
                return half4(colour,1);
            }
            ENDHLSL
        }
    }
}
