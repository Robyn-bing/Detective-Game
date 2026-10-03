Shader "DetectiveGame/Tutorial/Window Glass"
{
    Properties
    {
        [MainTexture] _BaseMap("Glass Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Glass Tint (A = Opacity)", Color) = (0.86,0.91,0.93,0.12)
        _Smoothness("Smoothness", Range(0,1)) = 0.92
        [Header(Exterior Blur)]
        _BlurAmount("Blur Amount", Range(0,1)) = 0.6
        _BlurRadius("Blur Radius (pixels at 1080p)", Range(0,12)) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face Culling", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 1
        [HideInInspector] _Blend("Blend", Float) = 0
        [HideInInspector] _BlendModePreserveSpecular("Preserve Specular", Float) = 1
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        [HideInInspector] _Metallic("Metallic", Float) = 0
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,1)
        [HideInInspector] _SpecularHighlights("Specular", Float) = 1
        [HideInInspector] _EnvironmentReflections("Reflections", Float) = 1
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
        [HideInInspector] _SrcBlendAlpha("Source Alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha("Destination Alpha", Float) = 10
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
        [HideInInspector] _ReceiveShadows("Receive Shadows", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "WindowGlass"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _ALPHAPREMULTIPLY_ON 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                float _BlurAmount;
                float _BlurRadius;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.eyeDepth = -p.positionVS.z;
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            float3 BlurredExterior(float2 uv, float glassDepth)
            {
                // Binomial 5x5 kernel approximates a Gaussian; radius is resolution-independent.
                const float weights[5] = { 1, 4, 6, 4, 1 };
                float2 pixel = rcp(_ScaledScreenParams.xy) * (_ScaledScreenParams.y / 1080.0);
                float3 total = 0;
                float weightSum = 0;
                [unroll] for (int y = -2; y <= 2; y++)
                [unroll] for (int x = -2; x <= 2; x++)
                {
                    float2 sampleUV = clamp(uv + float2(x,y) * pixel * (_BlurRadius * 0.5), pixel * 0.5, 1 - pixel * 0.5);
                    float depth = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                    // Reject opaque foreground (window frames, furniture) instead of bleeding it into glass.
                    float weight = weights[x+2] * weights[y+2] * step(glassDepth - 0.01, depth);
                    total += SampleSceneColor(sampleUV) * weight;
                    weightSum += weight;
                }
                return weightSum > 0 ? total / weightSum : SampleSceneColor(uv);
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = tint.rgb;
                surface.alpha = tint.a;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.vertexLighting = VertexLighting(input.positionWS, lighting.normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                float4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, input.fog);
                if (_BlurAmount > 0.0001 && _BlurRadius > 0.0001 && color.a < 0.9999)
                {
                    float2 uv = lighting.normalizedScreenSpaceUV;
                    float3 sharp = SampleSceneColor(uv);
                    float3 soft = BlurredExterior(uv, input.eyeDepth);
                    // Premultiplied blending: add ONLY the blur delta. This preserves the
                    // destination's volumetric light and the surface's PBR reflections/highlights.
                    color.rgb += (soft - sharp) * (_BlurAmount * (1 - color.a));
                }
                return color;
            }
            ENDHLSL
        }
    }
}
