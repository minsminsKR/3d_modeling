Shader "HappyToy/SchoolCreatureRim"
{
    Properties
    {
        _RimColor("Rim tint", Color) = (0.58, 0.055, 0.035, 1)
        _RimIntensity("Rim intensity", Range(0, 3)) = 0.7
        _RimPower("Rim falloff", Range(1, 8)) = 3.0
        _SideStrength("Side fill", Range(0, 1)) = 0.32
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "CreatureFresnelRim"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimIntensity;
                half _RimPower;
                half _SideStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 normal=normalize(input.normalWS);
                half3 view=normalize(GetWorldSpaceViewDir(input.positionWS));
                half edge=pow(saturate(1-abs(dot(normal,view))),_RimPower);
                half3 side=normalize(cross(half3(0,1,0),view)+half3(0.0001,0,0));
                half sideFill=saturate(dot(normal,side))*_SideStrength;
                return half4(_RimColor.rgb * (_RimIntensity*(edge+sideFill)),0);
            }
            ENDHLSL
        }
    }
}
