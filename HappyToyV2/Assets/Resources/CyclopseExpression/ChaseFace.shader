Shader "HappyToy/CyclopseChaseFace"
{
    Properties
    {
        _BaseMap("Original albedo",2D)="white"{}
        _BaseColor("Base colour",Color)=(1,1,1,1)
        _ChaseFace("Authored chase face",2D)="white"{}
        _ExpressionWeight("Expression weight",Range(0,1))=0
        _CabinetMouthWeight("Cabinet jaw opening",Range(0,1))=0
        _BumpMap("Normal",2D)="bump"{}
        _BumpScale("Normal strength",Float)=1
        _MetallicGlossMap("Metallic smoothness",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.4
        _OcclusionMap("Occlusion",2D)="white"{}
        _OcclusionStrength("Occlusion strength",Range(0,1))=1
        _EmissionMap("Eye emission",2D)="white"{}
        [HDR]_EmissionColor("Emission",Color)=(0,0,0,1)
        _SpecColor("Specular",Color)=(.2,.2,.2,1)
        _Cutoff("Cutoff",Range(0,1))=.5
        _Cull("Cull",Float)=2
        [HideInInspector]_Surface("Surface",Float)=0
        [HideInInspector]_AlphaClip("Alpha clip",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        float _CabinetMouthWeight;
        void ClipCabinetMouth(float3 mouth)
        {
            // UV3 follows the original lip surface through both skinning and
            // sculpting. Only the front of the head opens; rear/support surfaces
            // and all other creatures retain the default zero-weight shader.
            if (_CabinetMouthWeight > .025 && abs(mouth.z) > .5)
            {
                // Aperture topology stays attached to the authored lips. The
                // paired mesh poses supply the opening, including the dropped,
                // tapered lower jaw, in all rendering passes.
                clip(dot(mouth.xy,mouth.xy)-1);
            }
        }
        struct MouthAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float3 mouth:TEXCOORD2; };
        struct MouthVaryings { float4 positionCS:SV_POSITION; float3 mouth:TEXCOORD0; half3 normalWS:TEXCOORD1; };
        MouthVaryings MouthDepthVertex(MouthAttributes v)
        {
            MouthVaryings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
            o.mouth=v.mouth; o.normalWS=TransformObjectToWorldNormal(v.normalOS); return o;
        }
        half4 MouthDepthFragment(MouthVaryings i):SV_Target { ClipCabinetMouth(i.mouth); return 0; }
        half4 MouthNormalsFragment(MouthVaryings i):SV_Target
        {
            ClipCabinetMouth(i.mouth);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(normalize(i.normalWS));
                return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
            #else
                return half4(normalize(i.normalWS),0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_ChaseFace); SAMPLER(sampler_ChaseFace);
            float _ExpressionWeight;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; float2 faceUV:TEXCOORD1; float3 mouth:TEXCOORD2; float4 colour:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; float2 faceUV:TEXCOORD4; half mask:TEXCOORD5; half fog:TEXCOORD6; float3 mouth:TEXCOORD7; };
            Varyings Vert(Attributes v)
            {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=n.normalWS;
                o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.faceUV=v.faceUV; o.mask=v.colour.r;
                o.fog=ComputeFogFactor(p.positionCS.z); o.mouth=v.mouth; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                ClipCabinetMouth(i.mouth);
                SurfaceData surface; InitializeStandardLitSurfaceData(i.uv,surface);
                half blend=saturate(i.mask*_ExpressionWeight);
                surface.albedo=lerp(surface.albedo,SAMPLE_TEXTURE2D(_ChaseFace,sampler_ChaseFace,i.faceUV).rgb,blend);
                surface.metallic=lerp(surface.metallic,0,blend);
                surface.smoothness=lerp(surface.smoothness,.32,blend);
                surface.normalTS=normalize(lerp(surface.normalTS,half3(0,0,1),blend));
                // The original UV0 eye mask remains authoritative through the
                // generated-face blend; changing expression must not extinguish it.
                InputData input=(InputData)0;
                input.positionWS=i.positionWS;
                half3 n=normalize(i.normalWS), t=normalize(i.tangentWS.xyz);
                half3 b=cross(n,t)*i.tangentWS.w;
                input.normalWS=normalize(TransformTangentToWorld(surface.normalTS,half3x3(t,b,n)));
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.bakedGI=SampleSH(input.normalWS);
                input.vertexLighting=VertexLighting(i.positionWS,input.normalWS);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1); input.fogCoord=i.fog;
                half4 result=UniversalFragmentPBR(input,surface); result.rgb=MixFog(result.rgb,i.fog); return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MouthShadowVertex
            #pragma fragment MouthDepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            MouthVaryings MouthShadowVertex(MouthAttributes v)
            {
                MouthVaryings o=MouthDepthVertex(v);
                float3 positionWS=TransformObjectToWorld(v.positionOS.xyz);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction=normalize(_LightPosition-positionWS);
                #else
                    float3 direction=_LightDirection;
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(positionWS,o.normalWS,direction));
                #if UNITY_REVERSED_Z
                    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MouthDepthVertex
            #pragma fragment MouthDepthFragment
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MouthDepthVertex
            #pragma fragment MouthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
