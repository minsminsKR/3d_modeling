Shader "HappyToy/AlleyDuskSky"
{
    Properties
    {
        _DuskWeight("Dusk weight", Range(0,1)) = 1
        _DuskClouds("Dusk cloud panorama", 2D) = "black" {}
        _DuskCloudsStrength("Cloud panorama strength", Range(0,1)) = 0
        _MoonDirection("Direction to moon", Vector) = (-0.10, 0.43, 0.90, 0)
        _SkyTime("Cloud simulation time", Float) = 0
        _CloudBrightness("Cloud brightness", Float) = 1
        _MoonHaloStrength("Moon halo strength", Float) = 1
        _CloudScale("Cloud spatial frequency", Float) = 1
        _CloudDetail("Cloud high-frequency detail", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DuskClouds); SAMPLER(sampler_DuskClouds);

            CBUFFER_START(UnityPerMaterial)
                float4 _MoonDirection;
                float _DuskWeight;
                float _DuskCloudsStrength;
                float _SkyTime;
                float _CloudBrightness;
                float _MoonHaloStrength;
                float _CloudScale;
                float _CloudDetail;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            float Hash(float2 cell)
            {
                float3 p = frac(float3(cell.xyx) * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float2 coordinate)
            {
                float2 cell = floor(coordinate), f = frac(coordinate);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x), f.y);
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float elevation = saturate(ray.y);
                float horizon = pow(1 - elevation, 3.5);
                // Linear values deliberately stay low; the previous brighter
                // values became a flat grey after the project's output transform.
                float3 sky = lerp(float3(.0018, .0035, .0065), float3(.0055, .009, .014), horizon);
                // Distance haze meets the unlit far ground near the horizon.
                // The broad world-direction band avoids a crisp straight edge
                // while leaving the overhead canopy against a dark night sky.
                float horizonHaze = smoothstep(-.35, -.08, ray.y) * (1 - smoothstep(.01, .20, ray.y));
                sky = lerp(sky, float3(.026, .035, .046), horizonHaze * .94);

                // A procedural sky is sampled by world direction; it is not a
                // camera-facing image or a plane inside the playable forest.
                float2 drift = float2(_SkyTime * .0007, -_SkyTime * .00045);
                float2 coordinate = ray.xz / max(ray.y + .38, .20) * 2.6 * _CloudScale + drift;
                float cloudDetail = saturate(_CloudDetail);
                // Redistribute the existing three octaves only; the default
                // exactly retains their original weights and frequencies.
                float cloud = (.57 - .17 * cloudDetail) * Noise(coordinate)
                    + (.28 + .05 * cloudDetail) * Noise(coordinate * 2.1 + 7.1)
                    + (.15 + .12 * cloudDetail) * Noise(coordinate * (4.2 + 2.1 * cloudDetail) - 3.4);
                float cover = smoothstep(.46, .72, cloud) * smoothstep(-.06, .12, ray.y);
                float moonDot = saturate(dot(ray, normalize(_MoonDirection.xyz)));
                float halo = pow(moonDot, 150);
                sky += cover * (float3(.003, .0055, .0085) + halo * float3(.008, .011, .016) * _MoonHaloStrength) * _CloudBrightness;
                sky += halo * float3(.009, .013, .021) * _MoonHaloStrength;
                // A small world-direction lunar disc. The local coordinates
                // describe the visible sphere, not a plane in front of the
                // player. Layered wisps veil the outline unevenly rather than
                // cutting it into a perfect top and bottom half.
                if (moonDot > .99972)
                {
                    float3 moonDirection = normalize(_MoonDirection.xyz);
                    float3 moonRight = normalize(cross(float3(0, 1, 0), moonDirection));
                    float3 moonUp = normalize(cross(moonDirection, moonRight));
                    float2 moonPlane = float2(dot(ray, moonRight), dot(ray, moonUp));
                    float2 lunar = moonPlane / .022;
                    float radial = dot(lunar, lunar);
                    float disc = 1 - smoothstep(.88, 1.02, radial);
                    float maria = .58 * Noise(lunar * 2.3 + float2(9.1, 4.6))
                        + .28 * Noise(lunar * 5.4 - float2(2.4, 6.1))
                        + .14 * Noise(lunar * 12.2 + float2(1.9, 8.4));
                    float moonTexture = lerp(.48, .90, maria) * lerp(.78, 1, sqrt(saturate(1 - radial)));
                    float2 wisps = moonPlane * 40 + float2(13.1, 7.3) + drift * 8;
                    float haze = .58 * Noise(wisps) + .28 * Noise(wisps * 2.1 + 6.7)
                        + .14 * Noise(wisps * 4.3 - 2.8);
                    float veil = smoothstep(.43, .72, haze);
                    float visibility = 1 - max(.70 * veil, .38 * cover);
                    sky += disc * moonTexture * visibility * float3(.20, .23, .27);
                }
                // Scene-specific dusk fades to the authored night sky by the mountain entry.
                float sunset = pow(1 - elevation, 2.4);
                float3 dusk = lerp(float3(.026, .035, .062), float3(.22, .065, .045), sunset);
                dusk += cover * float3(.027, .018, .022);
                // The image covers horizon-to-zenith. Crossfade the wrap seam
                // against the opposite section so no hard vertical edge appears.
                float u = frac(atan2(ray.x, ray.z) * .159154943 + .5);
                float v = asin(saturate(ray.y)) * .636619772;
                float3 clouds = SAMPLE_TEXTURE2D(_DuskClouds, sampler_DuskClouds, float2(u, v)).rgb;
                float3 seamClouds = SAMPLE_TEXTURE2D(_DuskClouds, sampler_DuskClouds, float2(frac(u + .5), v)).rgb;
                float seam = 1 - smoothstep(0, .08, min(u, 1 - u));
                clouds = lerp(clouds, seamClouds, seam);
                float zenithFade = 1 - smoothstep(.48, .82, ray.y);
                dusk = lerp(dusk, clouds * .36, .85 * _DuskCloudsStrength * smoothstep(-.08, .03, ray.y) * zenithFade);
                return half4(lerp(sky, dusk, saturate(_DuskWeight)), 1);
            }
            ENDHLSL
        }
    }
}
