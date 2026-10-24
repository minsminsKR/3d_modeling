Shader "HappyToy/PlayerItemSurface"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white"{}
        _MetalRough("Packed material",2D)="white"{}
        _BumpMap("Normal",2D)="bump"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Viewmodel("Viewmodel",Float)=0
        _Monochrome("Viewmodel monochrome",Float)=0
        _PbrLayout("Packed channels: GLB 0 / Unity 1",Float)=0
        _HasMetalRough("Packed map present",Float)=1
        _HasNormal("Normal map present",Float)=0
        _BumpScale("Normal scale",Float)=1
        _Skin("Close-up skin shading",Float)=0
        _Metallic("Metallic without map",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.45
        _Burn("Burn fraction",Range(0,1))=0
        _Burning("Burn enabled",Float)=0
        _BurnHeight("Prefab normalised height",Vector)=(0,5,0,.5)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MetalRough); SAMPLER(sampler_MetalRough);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST, _BurnHeight;
            float _Viewmodel, _Monochrome, _PbrLayout, _HasMetalRough;
            float _HasNormal, _BumpScale, _Metallic, _Smoothness, _Burn, _Burning, _Skin;
        CBUFFER_END
        float _RitualMonochrome;
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct A { float4 p:POSITION; float3 n:NORMAL; float4 t:TANGENT; float2 uv:TEXCOORD0; };
            struct V
            {
                float4 p:SV_POSITION; float3 w:TEXCOORD0; half3 n:TEXCOORD1;
                float2 uv:TEXCOORD2; float height:TEXCOORD3; half4 t:TEXCOORD4;
            };
            V vert(A a)
            {
                V o;
                o.w=TransformObjectToWorld(a.p.xyz);
                o.p=TransformWorldToHClip(o.w);
                o.n=TransformObjectToWorldNormal(a.n);
                o.t=half4(TransformObjectToWorldDir(a.t.xyz),a.t.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(a.uv,_BaseMap);
                o.height=dot(float4(a.p.xyz,1),_BurnHeight);
                return o;
            }
            half3 SkinLight(half3 colour, half3 n, half3 v, half3 l, half3 energy, half roughness)
            {
                // Energy-conserving dielectric GGX and a restrained warm wrap
                // approximate shallow skin scattering without a full-screen blur.
                half3 h=normalize(l+v);
                half nl=saturate(dot(n,l)), nv=max(.05h,saturate(dot(n,v)));
                half nh=saturate(dot(n,h)), vh=saturate(dot(v,h));
                half a=roughness*roughness, a2=a*a;
                half denominator=nh*nh*(a2-1)+1;
                half distribution=a2/max(.0001h,PI*denominator*denominator);
                half k=(roughness+1)*(roughness+1)*.125h;
                half visibility=(nv/(nv*(1-k)+k))*(nl/max(.001h,nl*(1-k)+k));
                half fresnel=.028h+.972h*pow(1-vh,5);
                half specular=distribution*visibility*fresnel/max(.001h,4*nv*nl);
                half wrap=saturate((dot(n,l)+.35h)/1.35h);
                half3 diffuse=colour*(1-fresnel)*(nl*.75h+wrap*half3(.25h,.15h,.11h));
                return (diffuse/PI+specular*nl)*energy;
            }
            half4 frag(V i, bool front:SV_IsFrontFace):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                // The cutoff is evaluated in upright prefab space, not exporter axes.
                float irregular=(sin(i.uv.x*173+sin(i.uv.y*107)*2.1)+sin(i.uv.x*71+i.uv.y*63))*.003;
                float distance=1-_Burn-i.height+irregular;
                if (_Burning>.5)
                {
                    clip(distance);
                    clip(.99999-_Burn);
                }
                half3 n=normalize(i.n)*(front?1:-1);
                if (_HasNormal>.5)
                {
                    half3 tangent=normalize(i.t.xyz);
                    half3 bitangent=cross(n,tangent)*i.t.w;
                    half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
                    n=normalize(TransformTangentToWorld(normalTS,half3x3(tangent,bitangent,n)));
                }
                half4 packed=SAMPLE_TEXTURE2D(_MetalRough,sampler_MetalRough,i.uv);
                half metallic=_HasMetalRough>.5 ? (_PbrLayout>.5 ? packed.r : packed.b) : _Metallic;
                half roughness=_HasMetalRough>.5 ? (_PbrLayout>.5 ? 1-packed.a*_Smoothness : packed.g) : 1-_Smoothness;
                roughness=clamp(roughness,.06,.98);
                half3 view=GetWorldSpaceNormalizeViewDir(i.w);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.w));
                half3 key=light.direction;
                half3 irradiance=light.color*light.shadowAttenuation;
                half3 ambient=SampleSH(n);
                if (_Viewmodel>.5)
                {
                    // A soft studio key lights these isolated close-up surfaces.
                    key=normalize(half3(-.4,.7,-.8));
                    irradiance=half3(.86,.83,.77);
                    ambient=half3(.24,.255,.28);
                }
                half ndl=saturate(dot(n,key));
                half3 f0=lerp(half3(.04,.04,.04),tex.rgb,metallic);
                half3 halfDirection=normalize(key+view);
                half fresnel=pow(1-saturate(dot(view,halfDirection)),5);
                half3 specular=lerp(f0,half3(1,1,1),fresnel)*pow(saturate(dot(n,halfDirection)),lerp(112,5,roughness));
                half3 lit=tex.rgb*(ambient+irradiance*ndl)*(1-metallic*.65)+specular*irradiance*ndl;
                if (_Skin>.5 && _Viewmodel>.5)
                {
                    // Close hands share one fixed, soft photographic lighting rig.
                    // Roughness variation and true normals replace the old hard
                    // Phong highlight that made fingers resemble polished rubber.
                    half skinRoughness=clamp(roughness,.36h,.78h);
                    lit=tex.rgb*half3(.105h,.112h,.125h);
                    lit+=SkinLight(tex.rgb,n,view,normalize(half3(-.4h,.7h,-.8h)),half3(2.05h,1.97h,1.85h),skinRoughness);
                    lit+=SkinLight(tex.rgb,n,view,normalize(half3(.8h,.15h,-.35h)),half3(.46h,.50h,.56h),skinRoughness);
                }
                if (_Viewmodel<.5)
                {
                    // Pickups receive the player's spotlight and nearby candle lights.
                    InputData input=(InputData)0;
                    input.positionWS=i.w;
                    input.positionCS=i.p;
                    input.normalWS=n;
                    input.viewDirectionWS=view;
                    input.shadowCoord=TransformWorldToShadowCoord(i.w);
                    input.vertexLighting=VertexLighting(i.w,n);
                    input.bakedGI=SampleSH(n);
                    input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
                    input.shadowMask=half4(1,1,1,1);
                    SurfaceData surface=(SurfaceData)0;
                    surface.albedo=tex.rgb;
                    surface.metallic=metallic;
                    surface.specular=half3(.04,.04,.04);
                    surface.smoothness=1-roughness;
                    surface.normalTS=half3(0,0,1);
                    surface.occlusion=1;
                    surface.alpha=1;
                    lit=UniversalFragmentPBR(input,surface).rgb;
                }
                if (_Viewmodel>.5 && _Burning<.5)
                    lit=lerp(lit,dot(lit,half3(.2126,.7152,.0722)).xxx,saturate(max(_Monochrome,_RitualMonochrome)));
                if (_Burning>.5)
                {
                    half charred=1-smoothstep(.004,.035,distance);
                    lit=lerp(lit,half3(.018,.012,.008),charred);
                    lit+=half3(4.8,.98,.045)*(1-smoothstep(.001,.012,distance));
                }
                return half4(lit,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment depthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            struct DepthA { float4 p:POSITION; float3 n:NORMAL; };
            float4 shadowVert(DepthA input):SV_POSITION
            {
                float3 p=TransformObjectToWorld(input.p.xyz);
                float3 n=TransformObjectToWorldNormal(input.n);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 direction=normalize(_LightPosition-p);
                #else
                    float3 direction=_LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p,n,direction)));
            }
            half4 depthFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            float4 depthVert(float4 position:POSITION):SV_POSITION { return TransformObjectToHClip(position.xyz); }
            half4 depthFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}



