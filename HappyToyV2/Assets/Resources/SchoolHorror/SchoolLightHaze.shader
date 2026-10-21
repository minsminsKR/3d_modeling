Shader "HappyToy/SchoolLightHaze"
{
    Properties
    {
        _Density("Moisture extinction per metre",Float)=0.0025
        _SourcePower("Actual lamp scattering power",Float)=0.25
        _ScatterColor("Actual lamp color",Color)=(1,0.86,0.62,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Transparent"}
        Pass
        {
            Name "LocalSingleScattering"
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Density,_SourcePower,_FlowTime;
                float4 _ScatterColor;
                float3 _SourcePosition;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            V vert(A i)
            {
                V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;
            }
            half4 frag(V i):SV_Target
            {
                float3 origin=GetCameraPositionWS(),direction=normalize(i.world-origin);
                float3 localOrigin=TransformWorldToObject(origin);
                float3 localDirection=mul((float3x3)unity_WorldToObject,direction);
                float3 inverseDirection=rcp(localDirection+float3(0.000001,0.000001,0.000001));
                float3 a=(-.5-localOrigin)*inverseDirection,b=(.5-localOrigin)*inverseDirection;
                float3 nearAxis=min(a,b),farAxis=max(a,b);
                float start=max(0,max(nearAxis.x,max(nearAxis.y,nearAxis.z)));
                float finish=min(farAxis.x,min(farAxis.y,farAxis.z));
                float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 surface=ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
                finish=min(finish,length(surface-origin));
                if(finish<=start)return 0;
                float stepLength=(finish-start)/12;
                float transmittance=1;float3 radiance=0;
                [unroll]for(int sample=0;sample<12;sample++)
                {
                    float3 at=origin+direction*(start+(sample+.5)*stepLength);
                    float3 local=localOrigin+localDirection*(start+(sample+.5)*stepLength);
                    float3 boundary=saturate((.5-abs(local))*8);
                    float edge=boundary.x*boundary.y*boundary.z;
                    float wisps=.74+.13*sin(at.x*3.2+at.z*.43+_FlowTime)+.13*sin(at.z*1.74-at.y*2.3-_FlowTime*.7);
                    float extinction=max(0,_Density*edge*wisps);
                    float alpha=1-exp(-extinction*stepLength);
                    float distanceSquared=dot(at-_SourcePosition,at-_SourcePosition);
                    float illumination=_SourcePower/(1+distanceSquared*.3);
                    radiance+=transmittance*alpha*_ScatterColor.rgb*illumination;
                    transmittance*=1-alpha;
                }
                // Twelve world-space samples, opaque-depth termination and a bounded light volume.
                // This is local single scattering, not a full scene volumetric shadow solution.
                return half4(radiance,1-transmittance);
            }
            ENDHLSL
        }
    }
}
