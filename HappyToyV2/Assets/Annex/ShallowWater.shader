Shader "HappyToy/ShallowWater"
{
    Properties
    {
        _WaterColor ("Water tint", Color) = (0.13, 0.26, 0.24, 1)
        _GrazingColor ("Grazing reflection tint", Color) = (0.23, 0.34, 0.33, 1)
        _Opacity ("Water opacity", Range(0, 1)) = 0.29
        _GrazingOpacity ("Grazing opacity", Range(0, 0.5)) = 0.30
        _NormalStrength ("Surface normal strength", Range(0, 0.3)) = 0.095
        _Smoothness ("Wet smoothness", Range(0, 1)) = 0.78
        _SpecularStrength ("Light reflection strength", Range(0, 2)) = 0.85
        _AmbientStrength ("Ambient response", Range(0, 2)) = 0.65
        _RippleStrength ("Footstep ripple strength", Range(0, 2)) = 0.85
        [HideInInspector] _SurfaceTime ("Surface time", Float) = 0
        [HideInInspector] _SurfaceMotion ("Surface motion", Float) = 1
        [HideInInspector] _RippleClock ("Ripple clock", Float) = 0
        [HideInInspector] _Ripple0 ("Ripple 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Ripple1 ("Ripple 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Ripple2 ("Ripple 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Ripple3 ("Ripple 3", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ShallowWaterForward"
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
            // Transparent receivers use the shadow map, never camera-depth shadows.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _GrazingColor;
                float _Opacity;
                float _GrazingOpacity;
                float _NormalStrength;
                float _Smoothness;
                float _SpecularStrength;
                float _AmbientStrength;
                float _RippleStrength;
                float _SurfaceTime;
                float _SurfaceMotion;
                float _RippleClock;
                float4 _Ripple0;
                float4 _Ripple1;
                float4 _Ripple2;
                float4 _Ripple3;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 fogAndVertexLight : TEXCOORD2;
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
                output.fogAndVertexLight = half4(ComputeFogFactor(output.positionCS.z),
                    VertexLighting(output.positionWS, output.normalWS));
                return output;
            }

            float2 RippleSlope(float2 positionXZ, float4 ripple)
            {
                // Runtime contract: (world X, world Z, start time, strength).
                // Expired/default slots cost no trig; the finite ring cannot affect
                // the whole pool. No geometry displacement or depth texture needed.
                float age = _RippleClock - ripple.z;
                if (ripple.w <= 0.0 || age < 0.0 || age >= 2.4)
                    return float2(0.0, 0.0);
                float2 offset = positionXZ - ripple.xy;
                float distanceToCenter = length(offset);
                float ring = distanceToCenter - age * 1.35;
                float envelope = exp2(-min(ring * ring * 10.0, 32.0));
                float lifetime = smoothstep(0.0, 0.12, age) * (1.0 - smoothstep(0.3, 2.4, age));
                float slope = cos(ring * 15.0) * envelope * lifetime * saturate(ripple.w) * 0.18;
                return offset / max(distanceToCenter, 0.04) * slope;
            }

            float3 WaterNormal(float3 positionWS, float3 geometricNormal)
            {
                float2 p = positionWS.xz;
                // Three nonparallel scales replace the regular checker-like waves.
                // Wrap temporal phases before trig to keep long sessions stable.
                float3 phase = frac(max(_SurfaceTime, 0.0) * float3(0.021, 0.034, 0.057)) * TWO_PI;
                float a = dot(p, float2(1.7, 0.85)) + phase.x;
                float b = dot(p, float2(-3.2, 4.1)) - phase.y;
                float c = dot(p, float2(10.3, 6.7)) + phase.z;
                // Suppress fine normals when the projected wavelength is subpixel.
                float fineFilter = 1.0 - smoothstep(0.35, 1.5, fwidth(c));
                float2 slope = float2(0.82, 0.41) * cos(a)
                    + float2(-0.43, 0.57) * cos(b) * 0.55
                    + float2(0.39, 0.25) * cos(c) * (0.24 * fineFilter);
                slope *= clamp(_NormalStrength, 0.0, 0.3);
                float2 ripple = RippleSlope(p, _Ripple0) + RippleSlope(p, _Ripple1)
                    + RippleSlope(p, _Ripple2) + RippleSlope(p, _Ripple3);
                slope += ripple * clamp(_RippleStrength, 0.0, 2.0);
                // Reduced motion removes all animated perturbation, including rings.
                slope = clamp(slope, -0.65, 0.65) * saturate(_SurfaceMotion);
                float3 perturbation = float3(-slope.x, 0.0, -slope.y);
                perturbation -= geometricNormal * dot(perturbation, geometricNormal);
                return SafeNormalize(geometricNormal + perturbation);
            }

            float3 WaterLight(Light light, float3 normalWS, float3 viewWS, float3 albedo)
            {
                float nDotL = saturate(dot(normalWS, light.direction));
                float3 halfway = SafeNormalize(light.direction + viewWS);
                float exponent = lerp(24.0, 160.0, saturate(_Smoothness));
                float highlight = pow(saturate(dot(normalWS, halfway)), exponent);
                float fresnel = 0.025 + 0.975 * pow(1.0 - saturate(dot(viewWS, halfway)), 5.0);
                float specular = highlight * fresnel * (exponent + 2.0) * 0.125
                    * clamp(_SpecularStrength, 0.0, 2.0);
                // Full float attenuation; bound pathological near-light intensities.
                float attenuation = min(max(light.distanceAttenuation, 0.0), 16.0)
                    * saturate(light.shadowAttenuation);
                return (albedo * 0.75 + specular) * light.color * (nDotL * attenuation);
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 geometricNormal = normalize(input.normalWS)
                    * IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 normalWS = WaterNormal(input.positionWS, geometricNormal);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 4.0);
                float3 albedo = max(_WaterColor.rgb, 0.0);
                float3 ambient = max(SampleSH(normalWS), 0.025);
                float3 color = albedo * ambient * clamp(_AmbientStrength, 0.0, 2.0);
                // Restrained probe-lit grazing sheen, not a screen-space reflection.
                color += _GrazingColor.rgb * ambient * fresnel * 0.55;
                color += input.fogAndVertexLight.yzw * albedo * 0.75;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS),
                    input.positionWS, half4(1, 1, 1, 1));
                color += WaterLight(mainLight, normalWS, viewWS, albedo);
                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                            color += WaterLight(light, normalWS, viewWS, albedo);
                        }
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        color += WaterLight(light, normalWS, viewWS, albedo);
                    LIGHT_LOOP_END
                #endif

                float alpha = saturate(_Opacity + fresnel * _GrazingOpacity);
                color = MixFog(color, input.fogAndVertexLight.x);
                return half4(min(max(color, 0.0), 64.0), alpha);
            }
            ENDHLSL
        }
    }
}
