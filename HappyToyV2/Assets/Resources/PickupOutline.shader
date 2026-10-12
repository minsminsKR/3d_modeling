Shader "HappyToy/PickupOutline"
{
    Properties { _Width ("Border pixels", Float) = 1.25 _MeshCenter ("Mesh center", Vector) = (0,0,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Width;
            float4 _MeshCenter;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float4 clip = TransformWorldToHClip(world);
                // Convex pickup meshes include thin paper and hard-edged packs.
                // Radial expansion closes the seams left by split face normals.
                float4 center = TransformWorldToHClip(TransformObjectToWorld(_MeshCenter.xyz));
                float2 direction = clip.xy / max(.001, clip.w) - center.xy / max(.001, center.w);
                direction /= max(length(direction), .00001);
                clip.xy += direction * (_Width * 2 / _ScreenParams.xy) * clip.w;
                output.positionCS = clip; return output;
            }
            half4 frag(Varyings input) : SV_Target { return half4(.88, .88, .88, 1); }
            ENDHLSL
        }
    }
}
