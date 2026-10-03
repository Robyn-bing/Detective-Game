Shader "DetectiveGame/Tutorial/Window Dust"
{
    Properties { _BaseColor("Tint", Color) = (1,0.9,0.7,0.35) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float eyeDepth : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.eyeDepth = -p.positionVS.z;
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radial = 1.0 - smoothstep(0.08, 0.5, length(input.uv - 0.5));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float softFade = saturate((sceneDepth - input.eyeDepth) / 0.12);
                float nearFade = saturate((input.eyeDepth - 0.15) / 0.35);
                return half4(input.color.rgb, input.color.a * radial * softFade * nearFade);
            }
            ENDHLSL
        }
    }
}
