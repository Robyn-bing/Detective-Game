Shader "DetectiveGame/UI/InvestigationBackgroundBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurRadius ("Blur Radius", Range(0, 8)) = 3.5
        _Tint ("Tint", Color) = (0.12, 0.16, 0.2, 0.72)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "InvestigationBackgroundBlur"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float _BlurRadius;
                half4 _Tint;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
#if UNITY_UV_STARTS_AT_TOP
                // UI fragment coordinates start at the top on Direct3D, while URP's
                // opaque camera texture is sampled bottom-up in this pass.
                uv.y = 1.0 - uv.y;
#endif
                float2 offset = _CameraOpaqueTexture_TexelSize.xy * _BlurRadius;

                half3 blurred = SampleSceneColor(uv) * 0.20h;
                blurred += SampleSceneColor(uv + float2(offset.x, 0.0)) * 0.12h;
                blurred += SampleSceneColor(uv - float2(offset.x, 0.0)) * 0.12h;
                blurred += SampleSceneColor(uv + float2(0.0, offset.y)) * 0.12h;
                blurred += SampleSceneColor(uv - float2(0.0, offset.y)) * 0.12h;
                blurred += SampleSceneColor(uv + offset) * 0.08h;
                blurred += SampleSceneColor(uv - offset) * 0.08h;
                blurred += SampleSceneColor(uv + float2(offset.x, -offset.y)) * 0.08h;
                blurred += SampleSceneColor(uv + float2(-offset.x, offset.y)) * 0.08h;

                half3 tinted = lerp(blurred, _Tint.rgb, _Tint.a);
                return half4(tinted, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
