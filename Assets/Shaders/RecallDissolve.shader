Shader "DetectiveGame/Recall Dissolve"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _Desaturation ("Desaturation", Range(0, 1)) = 0
        [HDR] _EdgeColor ("Edge Color", Color) = (0.65, 0.9, 1, 1)
        _EdgeWidth ("Edge Width", Range(0.001, 0.25)) = 0.07
        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "RecallDissolveForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                float _DissolveAmount;
                float _Desaturation;
                float _EdgeWidth;
                float _NoiseScale;
            CBUFFER_END

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float ValueNoise(float3 value)
            {
                float3 cell = floor(value);
                float3 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float x00 = lerp(n000, n100, local.x);
                float x10 = lerp(n010, n110, local.x);
                float x01 = lerp(n001, n101, local.x);
                float x11 = lerp(n011, n111, local.x);
                return lerp(lerp(x00, x10, local.y), lerp(x01, x11, local.y), local.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float noise = ValueNoise(input.positionWS * _NoiseScale);
                clip(_DissolveAmount >= 0.999 ? -1.0 : noise - _DissolveAmount);

                half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half luminance = dot(sampled.rgb, half3(0.2126, 0.7152, 0.0722));
                sampled.rgb = lerp(sampled.rgb, luminance.xxx, saturate(_Desaturation));

                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = SampleSH(normalWS) + mainLight.color * diffuse *
                    mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 litColor = sampled.rgb * max(lighting, half3(0.16, 0.16, 0.16));

                float edge = 1.0 - smoothstep(_DissolveAmount, _DissolveAmount + _EdgeWidth, noise);
                litColor = lerp(litColor, _EdgeColor.rgb, saturate(edge) * _EdgeColor.a);
                return half4(litColor, 1.0);
            }
            ENDHLSL
        }
    }
}
