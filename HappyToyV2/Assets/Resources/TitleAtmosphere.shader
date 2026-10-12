Shader "HappyToy/TitleAtmosphere"
{
    Properties
    {
        _BaseMap ("Existing authored surface", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Creature ("Silhouette", Float) = 0
        _FaceLight ("Weak local face light", Float) = 0
        _EyeGlow ("Original eye surface glow", Float) = 0
        _EyeLeft ("Left eye in head space", Vector) = (0,0,0,0)
        _EyeRight ("Right eye in head space", Vector) = (0,0,0,0)
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _StageOrigin;
                float _Creature, _FaceLight, _EyeGlow;
                float4 _EyeLeft, _EyeRight;
                float4x4 _WorldToFace;
                float4 _FootContacts[12];
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
                    float3 face = mul(_WorldToFace, float4(i.positionWS,1)).xyz;
                    float3 faceExtent = (face - float3(.013,-.01,-.04)) / float3(.075,.15,.05);
                    float facePool = exp(-dot(faceExtent,faceExtent) * 1.5) * saturate(dot(n,view));
                    colour += surface * float3(.65,.72,.82) * _FaceLight * facePool;
                    float3 left = (face - _EyeLeft.xyz) / float3(.013,.0045,.015);
                    float3 right = (face - _EyeRight.xyz) / float3(.013,.0045,.015);
                    float eyes = exp(-dot(left,left)*2.5) + exp(-dot(right,right)*2.5);
                    colour += float3(1,.025,.008) * eyes * _EyeGlow;
                }
                else
                {
                    // Warm depth haze belongs only to this camera's set; global game fog/light cannot wash out the silhouette.
                    float haze = saturate((p.z + 1) / 14) * .45;
                    colour = lerp(colour, float3(.18,.009,.006), haze);
                    // Broad dark contact pool under the planted creature, before the red floor reflection.
                    float contact = exp(-pow((p.x-2.0)/2.0,2) - pow((p.z-3.6)/3.2,2)) * saturate(n.y);
                    colour *= 1 - contact * .72;
                    float reflection = exp(-abs(p.x - 2.1) * 1.3) * saturate(n.y) * .025;
                    colour += float3(1,.09,.025) * reflection;
                    float footShadow = 0;
                    [unroll] for(int foot=0;foot<12;foot++)
                    {
                        float radius=_FootContacts[foot].w;
                        float2 offset=(p.xz-_FootContacts[foot].xz)/max(radius,.001);
                        footShadow += radius>.001 ? exp(-dot(offset,offset)*1.5) : 0;
                    }
                    colour *= 1 - saturate(footShadow*.96) * saturate(n.y);
                }
                // UI Toolkit presents display-referred RGB; keep the title texture UNorm
                // and encode exactly once here, independent of the offscreen panel target.
                return half4(LinearToSRGB(max(colour,0)),1);
            }
            ENDHLSL
        }
    }
}
