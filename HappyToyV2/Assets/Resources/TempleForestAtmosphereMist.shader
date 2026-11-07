Shader "HappyToy/TempleForestAtmosphereMist"
{
    Properties
    {
        _Density("Mist extinction per metre", Float) = 0.085
        _MistColor("Cold mist tint", Color) = (0.23, 0.28, 0.34, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Transparent" }
        Pass
        {
            Name "BoundedGroundMist"
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Density, _FlowTime;
                float4 _MistColor;
                float4 _LampPosition0, _LampPosition1, _LampPosition2, _LampPosition3;
                float4 _LampRadiance0, _LampRadiance1, _LampRadiance2, _LampRadiance3;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS); return o;
            }
            float3 Lamp(float3 worldSample, float4 lampLocation, float3 radiance)
            {
                float3 delta = worldSample - lampLocation.xyz;
                float distanceSquared = dot(delta, delta);
                float reach = saturate(1 - distanceSquared * lampLocation.w);
                return radiance * (reach * reach / (1 + distanceSquared * .5));
            }
            // Continuous world-space value noise forms irregular fog banks;
            // no screen-space pattern, new textures or additional ray steps.
            float MistHash(float2 cell)
            {
                float3 p = frac(float3(cell.xyx) * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float MistNoise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition), f = frac(samplePosition);
                f = f * f * (3 - 2 * f);
                float a = MistHash(cell), b = MistHash(cell + float2(1, 0));
                float c = MistHash(cell + float2(0, 1)), d = MistHash(cell + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 origin = GetCameraPositionWS();
                float3 direction = normalize(i.positionWS - origin);
                float3 localOrigin = TransformWorldToObject(origin);
                float3 localDirection = mul((float3x3)unity_WorldToObject, direction);
                float3 inverseDirection = rcp(localDirection + float3(0.000001, 0.000001, 0.000001));
                float3 a = (-.5 - localOrigin) * inverseDirection;
                float3 b = (.5 - localOrigin) * inverseDirection;
                float3 nearAxis = min(a, b), farAxis = max(a, b);
                float start = max(0, max(nearAxis.x, max(nearAxis.y, nearAxis.z)));
                float finish = min(farAxis.x, min(farAxis.y, farAxis.z));
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float depth = SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 surface = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);
                finish = min(finish, length(surface - origin));
                if (finish <= start) return 0;
                float stepLength = (finish - start) / 8;
                float transmittance = 1; float3 scattered = 0;
                [unroll] for (int stepIndex = 0; stepIndex < 8; stepIndex++)
                {
                    float rayDistance = start + (stepIndex + .5) * stepLength;
                    float3 at = origin + direction * rayDistance;
                    float3 local = localOrigin + localDirection * rayDistance;
                    // Smooth box edges prevent visible rectangular mist seams.
                    float3 boundary = smoothstep(0, .20, .5 - abs(local));
                    float edge = boundary.x * boundary.y * boundary.z;
                    // A slowly drifting bank is varied across the ground,
                    // with gaps rather than a uniform bright horizontal strip.
                    // A second frequency breaks up the bank edges; time is the
                    // same pause-aware atmosphere clock as the original mist.
                    float2 drift = float2(_FlowTime * .025, -_FlowTime * .017);
                    float broad = MistNoise(at.xz * .16 + drift);
                    float detail = .5 + .5 * sin(at.x * .47 - at.z * .39 - _FlowTime * .04);
                    float bank = lerp(.12, 1.30, smoothstep(.30, .68, broad * .78 + detail * .22));
                    float groundLayer = 1 - .38 * smoothstep(.12, .88, local.y + .5);
                    float extinction = max(0, _Density * edge * groundLayer * bank);
                    float alpha = 1 - exp(-extinction * stepLength);
                    float3 light = Lamp(at, _LampPosition0, _LampRadiance0.rgb)
                                 + Lamp(at, _LampPosition1, _LampRadiance1.rgb)
                                 + Lamp(at, _LampPosition2, _LampRadiance2.rgb)
                                 + Lamp(at, _LampPosition3, _LampRadiance3.rgb);
                    float3 color = _MistColor.rgb + light * .075;
                    scattered += transmittance * alpha * color;
                    transmittance *= 1 - alpha;
                }
                return half4(scattered, 1 - transmittance);
            }
            ENDHLSL
        }
    }
}
