Shader "HappyToy/SchoolDecaySurface"
{
    Properties
    {
        _BaseMap("Scan albedo / glass opacity",2D)="white"{}
        _BumpMap("Correlated tangent normal",2D)="bump"{}
        _OcclusionMap("Scan and cavity occlusion",2D)="white"{}
        _MetallicGlossMap("Metallic R / smoothness A",2D)="black"{}
        _MoistureMap("World moisture variation",2D)="gray"{}
        _PuddleField("Isotropic runoff R / breakup G / shallow normal BA",2D)="gray"{}
        _PuddleSpan("Runoff field repeat metres",Float)=24
        _BaseColor("Material tint",Color)=(1,1,1,1)
        _BumpScale("Normal strength",Range(0,2))=1
        _Smoothness("Smoothness multiplier",Range(0,1))=1
        _OcclusionStrength("AO strength",Range(0,1))=.9
        _MacroStrength("World scale damp variation",Range(0,1))=.2
        _WorldScale("World moisture repeat metres",Float)=5.7
        _WetStrength("Water normal flattening",Range(0,1))=0
        _WorldFloor("Continuous school floor mapping",Float)=0
        _AuthoredFloor("Generated floor atlas UVs",Float)=0
        _FloorSpan("School tile scan repeat metres",Float)=1.28
        _WorldCeiling("Continuous ceiling scan mapping",Float)=0
        _CeilingSpan("Ceiling scan repeat metres",Float)=2.4
        _SchoolPlanarStrength("Wet floor planar reflection",Range(0,1))=.95
        _SchoolPlanarRoughness("Wet reflection roughness",Range(0,2))=1
        _SchoolPlanarDistortion("Water film normal distortion",Range(0,.6))=.35
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
        TEXTURE2D(_PuddleField); SAMPLER(sampler_PuddleField);
        TEXTURE2D(_SchoolPlanarTexture); SAMPLER(sampler_SchoolPlanarTexture);
        float4x4 _SchoolPlanarVP;
        float4 _SchoolPlanarTexture_TexelSize;
        float _SchoolPlanarAvailable,_SchoolPlanarCapture;
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _SchoolOrigin;
            half4 _BaseColor;
            half _BumpScale,_Smoothness,_OcclusionStrength,_MacroStrength;
            half _WetStrength,_SurfaceMode;
            float _WorldScale,_WorldFloor,_FloorSpan,_WorldCeiling,_CeilingSpan,_PuddleSpan,_AuthoredFloor;
            half _SchoolPlanarStrength,_SchoolPlanarRoughness,_SchoolPlanarDistortion;
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
            if(_WorldFloor>.5&&_AuthoredFloor<.5)return (i.positionWS.xz-_SchoolOrigin.xz)/max(.1,_FloorSpan);
            if(_WorldCeiling>.5&&abs(i.normalWS.y)>.65)return (i.positionWS.xz-_SchoolOrigin.xz)/max(.1,_CeilingSpan);
            return i.uv;
        }
        float2 PuddleUv(Varyings i)
        {
            float2 school=i.positionWS.xz-_SchoolOrigin.xz;
            float section=floor((school.x+7)/14);
            return (school-section*float2(14,34))/max(1,_PuddleSpan);
        }
        half SchoolWater(Varyings i,half4 field)
        {
            if(_WorldFloor<.5)return 0;
            float2 local=PuddleUv(i)*max(1,_PuddleSpan);
            // This dedicated, inspectable field has isotropic 2-3.4m pools and 1m gentle breakup.
            // The old wall-runoff/glass-streak macro is deliberately never sampled for floor water.
            float drift=.22*sin(local.y*.31)+.12*sin(local.y*.71)+(field.g-.5)*.22;
            half channel=1-smoothstep(1.03,2.09,abs(local.x+drift));
            half level=field.r+(field.g-.5)*.075+channel*.18;
            half breakup=smoothstep(.53,.69,level);
            half wallMargin=1-smoothstep(1.82,2.15,abs(local.x));
            half routeEnds=smoothstep(.4,3.1,local.y)*(1-smoothstep(35.2,38.0,local.y));
            return saturate(breakup*wallMargin*routeEnds);
        }
        half SurfaceSmoothness(half packedSmoothness,half water)
        {
            half dry=saturate(packedSmoothness*_Smoothness);
            // Dusty ceramic stays rough between pools. The generated atlas has
            // fully glossy texels which otherwise create square dry mirror patches.
            if(_AuthoredFloor>.5)dry=min(dry,.52);
            return _WorldFloor>.5 ? lerp(dry,.92,smoothstep(.10,.93,water)) : (_SurfaceMode>1.5?min(dry,.92):dry);
        }
        half3 SurfaceNormal(Varyings i,half smoothness,half water,half4 puddle)
        {
            half3 n=normalize(i.normalWS);
            half3 tangent=normalize(i.tangentWS.xyz);
            half3 bitangent=cross(n,tangent)*i.tangentWS.w;
            half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,SurfaceUv(i)),_BumpScale);
            // A thin water film fills pores and flattens the scan's relief.
            half wet=(_WorldFloor>.5?smoothstep(.18,.75,water):smoothstep(.54,.93,smoothness))*_WetStrength;
            normalTS=normalize(lerp(normalTS,half3(0,0,1),wet*.97));
            if(_WorldFloor>.5)
            {
                // Sub-degree shallow water slope preserves long reflected tubes instead of tiger-striped glitter.
                normalTS.xy+=(puddle.ba*2-1)*(.05*wet);
                normalTS=normalize(normalTS);
            }
            half3 worldNormal=normalize(TransformTangentToWorld(normalTS,half3x3(tangent,bitangent,n)));
            if(_AuthoredFloor>.5&&_WorldFloor>.5)
            {
                // Standing water levels across the generated tile facets;
                // flattening only the tangent map leaves a separate mirror on each tile.
                half level=smoothstep(.18,.85,water)*smoothstep(.65,.93,n.y);
                half3 waterNormal=normalize(half3((puddle.b-.5)*.018,1,(puddle.a-.5)*.018));
                worldNormal=normalize(lerp(worldNormal,waterNormal,level*.98));
            }
            return worldNormal;
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
            half3 SchoolPlanarSpecular(Varyings i,InputData input,SurfaceData surface,half water,half3 lit)
            {
                half wet=_WorldFloor>.5?water:(_SurfaceMode>1.5?1:0);
                half upward=smoothstep(.80,.97,_AuthoredFloor>.5?input.normalWS.y:i.normalWS.y);
                if(_SchoolPlanarAvailable<.5||_SchoolPlanarCapture>.5||wet<.02||upward<.01)return lit;
                float4 projected=mul(_SchoolPlanarVP,float4(i.positionWS,1));
                if(projected.w<=.001)return lit;
                float2 uv=projected.xy/projected.w*.5+.5;
                // Small water slopes break the reflected line without moving its physical footprint.
                // Clamp displacement so residual grout slopes cannot smear across adjacent pools.
                uv+=clamp(input.normalWS.xz*_SchoolPlanarDistortion,-.008,.008);
                half edge=saturate(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y))*40);
                if(edge<=0)return lit;
                half mip=(1-surface.smoothness)*5*_SchoolPlanarRoughness;
                half3 planar=SAMPLE_TEXTURE2D_LOD(_SchoolPlanarTexture,sampler_SchoolPlanarTexture,saturate(uv),mip).rgb;
                BRDFData brdf;InitializeBRDFData(surface,brdf);
                half fresnel=Pow4(1-saturate(dot(input.normalWS,input.viewDirectionWS)));
                half3 response=EnvironmentBRDFSpecular(brdf,fresnel);
                half3 probe=GlossyEnvironmentReflection(reflect(-input.viewDirectionWS,input.normalWS),i.positionWS,
                    brdf.perceptualRoughness,1,input.normalizedScreenSpaceUV);
                AmbientOcclusionFactor ao=CreateAmbientOcclusionFactor(input,surface);
                half weight=smoothstep(.12,.92,wet)*upward*edge*_SchoolPlanarStrength;
                // Replace only indirect specular. Actual lights, diffuse, shadowing and AO remain PBR.
                return max(0,lit+(planar-probe)*response*ao.indirectAmbientOcclusion*weight);
            }
            half4 SurfaceFragment(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // The reflected view cannot see this same floor/water film or recursively sample its texture.
                if(_SchoolPlanarCapture>.5&&(_WorldFloor>.5||_SurfaceMode>1.5))clip(-1);
                float2 uv=SurfaceUv(i);
                half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;
                half4 packed=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,uv);
                half4 moisture=SAMPLE_TEXTURE2D(_MoistureMap,sampler_MoistureMap,MacroUv(i));
                half4 puddle=half4(.5,.5,.5,.5);
                if(_WorldFloor>.5)puddle=SAMPLE_TEXTURE2D(_PuddleField,sampler_PuddleField,PuddleUv(i));
                half water=SchoolWater(i,puddle);
                half smoothness=SurfaceSmoothness(packed.a,water);
                half dirt=_WorldFloor>.5?puddle.r*.5+puddle.g*.5:moisture.r*.65+moisture.g*.35;
                albedo.rgb*=1-_MacroStrength*dirt;
                albedo.rgb*=1-water*.20;
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
                half3 n=SurfaceNormal(i,smoothness,water,puddle);
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
                lit.rgb=SchoolPlanarSpecular(i,input,surface,water,lit.rgb);
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
                half4 puddle=half4(.5,.5,.5,.5);
                if(_WorldFloor>.5)puddle=SAMPLE_TEXTURE2D(_PuddleField,sampler_PuddleField,PuddleUv(i));
                half water=SchoolWater(i,puddle);
                half smoothness=SurfaceSmoothness(packed,water);
                half3 n=SurfaceNormal(i,smoothness,water,puddle);
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
