Shader "HappyToy/BloodFilm"
{
    Properties
    {
        _FilmColor ("Wet blood color", Color) = (0.30, 0.025, 0.035, 1)
        _ClotColor ("Dried clot color", Color) = (0.115, 0.013, 0.018, 1)
        _Opacity ("Film opacity", Range(0, 1)) = 0.96
        _EdgeFeather ("Edge feather", Range(0.05, 0.5)) = 0.28
        _ClotScale ("Clot detail per meter", Range(1, 24)) = 8
        _ClotStrength ("Clotted coverage", Range(0, 1)) = 0.7
        _NormalStrength ("Clot normal strength", Range(0, 0.2)) = 0.065
        _WetSmoothness ("Wet smoothness", Range(0, 1)) = 0.82
        _SpecularStrength ("Wet glint strength", Range(0, 2)) = 0.85
        _AmbientStrength ("Ambient response", Range(0, 2)) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Name "BloodFilmForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            // Cluster iteration uses bit scans; keep its desktop feature floor explicit.
            #pragma target 4.5 _CLUSTER_LIGHT_LOOP
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FilmColor;
                half4 _ClotColor;
                float _Opacity;
                float _EdgeFeather;
                float _ClotScale;
                float _ClotStrength;
                float _NormalStrength;
                float _WetSmoothness;
                float _SpecularStrength;
                float _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 fogAndVertexLight : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogAndVertexLight = half4(ComputeFogFactor(output.positionCS.z),
                    VertexLighting(output.positionWS, output.normalWS));
                return output;
            }

            float Hash21(float2 p)
            {
                float3 h = frac(float3(p.x, p.y, p.x) * 0.1031);
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }

            float3 ClotNoise(float2 p)
            {
                // Value plus analytic spatial gradient. No textures, mesh tangents,
                // animation, screen-space bump derivatives or vertex displacement.
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float2 du = 6.0 * f * (1.0 - f);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                float mixed = a - b - c + d;
                float value = a + (b - a) * u.x + (c - a) * u.y + mixed * u.x * u.y;
                float2 gradient = du * float2(b - a + mixed * u.y, c - a + mixed * u.x);
                return float3(value, gradient);
            }

            float3 BloodLight(Light light, float3 normalWS, float3 viewWS,
                float3 albedo, float smoothness, float wetness)
            {
                float nDotL = saturate(dot(normalWS, light.direction));
                float3 halfway = SafeNormalize(light.direction + viewWS);
                float exponent = lerp(12.0, 128.0, saturate(smoothness));
                float highlight = pow(saturate(dot(normalWS, halfway)), exponent);
                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(viewWS, halfway)), 5.0);
                float specular = highlight * fresnel * (exponent + 2.0) * 0.125
                    * lerp(0.14, 1.0, wetness) * clamp(_SpecularStrength, 0.0, 2.0);
                float attenuation = min(max(light.distanceAttenuation, 0.0), 16.0)
                    * saturate(light.shadowAttenuation);
                // The small neutral glint makes illuminated liquid distinct from
                // the dark, matte clots without giving the whole stain a glow.
                return (albedo + specular) * light.color * (nDotL * attenuation);
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // Authored stains have normals but no tangents. This stable plane
                // basis supports floor pools, vertical smears and the existing drips.
                float3 tangentWS = abs(normalWS.y) > 0.75
                    ? float3(1, 0, 0) : SafeNormalize(cross(float3(0, 1, 0), normalWS));
                float3 bitangentWS = SafeNormalize(cross(normalWS, tangentWS));
                float2 plane = float2(dot(input.positionWS, tangentWS), dot(input.positionWS, bitangentWS));
                float2 detailUV = plane * clamp(_ClotScale, 1.0, 24.0);
                float coarseFilter = 1.0 - smoothstep(0.35, 1.25, max(fwidth(detailUV.x), fwidth(detailUV.y)));
                float fineFilter = 1.0 - smoothstep(0.35, 1.25, max(fwidth(detailUV.x), fwidth(detailUV.y)) * 2.71);
                float3 coarse = lerp(float3(0.5, 0, 0), ClotNoise(detailUV), coarseFilter);
                float3 fine = lerp(float3(0.5, 0, 0), ClotNoise(detailUV * 2.71 + 7.13), fineFilter);
                float clot = smoothstep(0.32, 0.74, coarse.x * 0.72 + fine.x * 0.28)
                    * saturate(_ClotStrength);
                float radius = length(input.uv - 0.5) * 2.0;
                float dryRim = smoothstep(0.42, 0.94, radius);
                float wetness = saturate(1.0 - clot * 0.88 - dryRim * 0.35);
                float3 albedo = lerp(_FilmColor.rgb, _ClotColor.rgb, saturate(clot + dryRim * 0.3));
                albedo *= lerp(0.82, 1.08, fine.x);
                float2 slope = (coarse.yz + fine.yz * 0.23)
                    * clamp(_NormalStrength, 0.0, 0.2);
                normalWS = SafeNormalize(normalWS - tangentWS * slope.x - bitangentWS * slope.y);
                float smoothness = lerp(0.24, saturate(_WetSmoothness), wetness);
                float3 color = albedo * max(SampleSH(normalWS), 0.06)
                    * clamp(_AmbientStrength, 0.0, 2.0);
                color += input.fogAndVertexLight.yzw * albedo;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS),
                    input.positionWS, half4(1, 1, 1, 1));
                color += BloodLight(mainLight, normalWS, viewWS, albedo, smoothness, wetness);
                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                            color += BloodLight(light, normalWS, viewWS, albedo, smoothness, wetness);
                        }
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        color += BloodLight(light, normalWS, viewWS, albedo, smoothness, wetness);
                    LIGHT_LOOP_END
                #endif

                // Respect the existing radial UV silhouette; never grow the stain
                // outside its authored mesh or reveal a square decal boundary.
                float edge = 1.0 - smoothstep(1.0 - clamp(_EdgeFeather, 0.05, 0.5), 1.0, radius);
                float alpha = edge * saturate(_Opacity) * lerp(0.9, 1.0, clot);
                color = MixFog(color, input.fogAndVertexLight.x);
                return half4(min(max(color, 0.0), 64.0), alpha);
            }
            ENDHLSL
        }
    }
}
