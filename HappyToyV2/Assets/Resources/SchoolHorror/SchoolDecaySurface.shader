Shader "HappyToy/SchoolDecaySurface"
{
    Properties
    {
        _BaseMap("Scan albedo / glass opacity",2D)="white"{}
        _BumpMap("Correlated tangent normal",2D)="bump"{}
        _OcclusionMap("Scan and cavity occlusion",2D)="white"{}
        _MetallicGlossMap("Metallic R / smoothness A",2D)="black"{}
        _MoistureMap("World moisture variation",2D)="gray"{}
        _BaseColor("Material tint",Color)=(1,1,1,1)
        _BumpScale("Normal strength",Range(0,2))=1
        _Smoothness("Smoothness multiplier",Range(0,1))=1
        _OcclusionStrength("AO strength",Range(0,1))=.9
        _MacroStrength("World scale damp variation",Range(0,1))=.2
        _WorldScale("World moisture repeat metres",Float)=5.7
        _WetStrength("Water normal flattening",Range(0,1))=0
        _WorldFloor("Continuous school floor mapping",Float)=0
        _FloorSpan("School tile scan repeat metres",Float)=1.28
        _SchoolOrigin("School root world position",Vector)=(0,0,45,0)
        _SurfaceMode("Opaque 0 / glass 1 / puddle 2",Float)=0
        _SrcBlend("Blend source",Float)=1
        _DstBlend("Blend destination",Float)=0
        _ZWrite("Depth write",Float)=1
        _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_MoistureMap); SAMPLER(sampler_MoistureMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _SchoolOrigin;
            half4 _BaseColor;
            half _BumpScale,_Smoothness,_OcclusionStrength,_MacroStrength;
            half _WetStrength,_SurfaceMode;
            float _WorldScale,_WorldFloor,_FloorSpan;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float4 tangentOS:TANGENT;
            float2 uv:TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 tangentWS:TEXCOORD2;
            float2 uv:TEXCOORD3;
            half fogFactor:TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings SurfaceVertex(Attributes v)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(v.normalOS);
            o.tangentWS=half4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());
            o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
            o.fogFactor=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        float2 MacroUv(Varyings i)
        {
            half3 n=abs(i.normalWS);
            float2 uv=n.y>.65 ? i.positionWS.xz : (n.x>n.z ? i.positionWS.zy : i.positionWS.xy);
            return uv/max(_WorldScale,.2);
        }
        float2 SurfaceUv(Varyings i)
        {
            // Geometry is split for culling every 4.4m. World mapping keeps
            // 0.32m grout lines continuous across those renderer boundaries.
            return _WorldFloor>.5 ? (i.positionWS.xz-_SchoolOrigin.xz)/_FloorSpan : i.uv;
        }
        half SchoolWater(Varyings i,half4 moisture)
        {
            if(_WorldFloor<.5)return 0;
            float2 school=i.positionWS.xz-_SchoolOrigin.xz;
            float section=floor((school.x+7)/14);
            float2 local=school-section*float2(14,34);
            // Runoff from the first roof leak spreads toward the entry. Its
            // width and extent include the actual overhead-light reflection
            // footprints while leaving the wall edges matte.
            float2 first=(local-float2(-.35,9.8))/float2(1.2,3.7);
            float2 second=(local-float2(.55,24.3))/float2(1.3,2.4);
            half irregular=(moisture.r*.6+moisture.g*.4-.5)*.40;
            half pools=1-smoothstep(.65,1.16,min(dot(first,first),dot(second,second))+irregular);
            float drift=.17*sin(local.y*.28)+.10*sin(local.y*.63);
            half route=1-smoothstep(.70,1.65,abs(local.x+drift));
            half routeEnds=smoothstep(1.5,5.0,local.y)*(1-smoothstep(32.5,37.0,local.y));
            // A thin irregular film is only moderately glossy. It follows the
            // long route, not the repeating tile scan; dry wall edges remain.
            half damp=route*routeEnds*(.36+.12*(moisture.r*.6+moisture.g*.4));
            return max(pools,damp);
        }
        half SurfaceSmoothness(half packedSmoothness,half water)
        {
            return _WorldFloor>.5 ? lerp(.22+packedSmoothness*.26,.94,water) : saturate(packedSmoothness*_Smoothness);
        }
        half3 SurfaceNormal(Varyings i,half smoothness)
        {
            half3 n=normalize(i.normalWS);
            half3 tangent=normalize(i.tangentWS.xyz);
            half3 bitangent=cross(n,tangent)*i.tangentWS.w;
            half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,SurfaceUv(i)),_BumpScale);
            // A thin water film fills pores and flattens the scan's relief.
            half wet=smoothstep(.54,.93,smoothness)*_WetStrength;
            normalTS=normalize(lerp(normalTS,half3(0,0,1),wet*.92));
            return normalize(TransformTangentToWorld(normalTS,half3x3(tangent,bitangent,n)));
        }
        ENDHLSL
        Pass
        {
            Name "SchoolForward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // Runtime-owned glass/water materials enable these local variants.
            // multi_compile retains them in players without a serialized anchor.
            #pragma multi_compile_local_fragment _ _SURFACE_TYPE_TRANSPARENT
            #pragma multi_compile_local_fragment _ _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 SurfaceFragment(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=SurfaceUv(i);
                half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;
                half4 packed=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,uv);
                half4 moisture=SAMPLE_TEXTURE2D(_MoistureMap,sampler_MoistureMap,MacroUv(i));
                half water=SchoolWater(i,moisture);
                half smoothness=SurfaceSmoothness(packed.a,water);
                albedo.rgb*=1-_MacroStrength*(moisture.r*.65+moisture.g*.35);
                albedo.rgb*=1-water*.35;
                half alpha=1;
                half coverage=1;
                if(_SurfaceMode>.5)
                {
                    alpha=albedo.a;
                    if(_SurfaceMode>1.5)
                    {
                        // UV-centred irregular shallow film, with a soft meniscus.
                        // Its highlight comes only from real lights and probes.
                        float2 p=i.uv*2-1;
                        half shape=1-dot(p,p)+.19*(moisture.r-.5)+.11*(moisture.a-.5);
                        coverage=smoothstep(.03,.23,shape);
                    }
                    clip(alpha*coverage-.002);
                }
                half3 n=SurfaceNormal(i,smoothness);
                InputData input=(InputData)0;
                input.positionWS=i.positionWS;
                input.positionCS=i.positionCS;
                input.normalWS=n;
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.vertexLighting=VertexLighting(i.positionWS,n);
                input.bakedGI=SampleSH(n);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=albedo.rgb;
                surface.metallic=packed.r;
                surface.specular=half3(.04,.04,.04);
                surface.smoothness=smoothness;
                surface.normalTS=half3(0,0,1);
                surface.occlusion=lerp(1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,uv).r,_OcclusionStrength);
                surface.alpha=alpha;
                half4 lit=UniversalFragmentPBR(input,surface);
                lit.rgb=MixFog(lit.rgb,i.fogFactor);
                // URP's premultiplied BRDF attenuates diffuse by alpha while
                // preserving real glass highlights. Coverage is geometric and
                // must also fade specular at the boundary of a water film.
                lit.rgb*=coverage;
                return half4(lit.rgb,alpha*coverage);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull] ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection,_LightPosition;
            float4 ShadowVertex(Attributes input):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 p=TransformObjectToWorld(input.positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection=normalize(_LightPosition-p);
                #else
                    float3 lightDirection=_LightDirection;
                #endif
                float4 clipPos=TransformWorldToHClip(ApplyShadowBias(p,n,lightDirection));
                #if UNITY_REVERSED_Z
                    clipPos.z=min(clipPos.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    clipPos.z=max(clipPos.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clipPos;
            }
            half4 DepthFragment():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull] ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex SurfaceVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings i):SV_Target{return i.positionCS.z;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFragment(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half packed=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,SurfaceUv(i)).a;
                half4 moisture=SAMPLE_TEXTURE2D(_MoistureMap,sampler_MoistureMap,MacroUv(i));
                half smoothness=SurfaceSmoothness(packed,SchoolWater(i,moisture));
                half3 n=SurfaceNormal(i,smoothness);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(n)*.5+.5;
                    return half4(PackFloat2To888(saturate(oct)),smoothness);
                #else
                    return half4(n,smoothness);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
