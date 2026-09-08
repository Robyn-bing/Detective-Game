Shader "DetectiveGame/Recall Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 0.75)
        _OutlineWidth ("Outline Width", Range(0.0001, 0.05)) = 0.008
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+20"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "RecallOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 expanded = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(expanded);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
