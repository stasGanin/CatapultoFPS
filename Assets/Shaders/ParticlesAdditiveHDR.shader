// Аддитивные частицы для URP с HDR-яркостью и мягким пересечением с геометрией.
// Замена Mobile/Particles/Additive в паке EffectCore: тот отдаёт цвет не ярче 1,
// поэтому снаряды не светятся в Bloom и выглядят плоско по сравнению с превью пака.
// Имя _MainTex совпадает с мобильным шейдером, чтобы текстуры материалов перенеслись как есть.
Shader "Catapulto/Particles/Additive HDR"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Intensity ("HDR Intensity", Range(0, 8)) = 2.5
        _SoftFade ("Soft Fade Distance", Range(0.01, 3)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Intensity;
                half _SoftFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = tex * input.color;

                // Мягкие частицы: гасим там, где квад упирается в стену/пол, — без резких срезов.
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float particleDepth = input.screenPos.w;
                color.a *= saturate((sceneDepth - particleDepth) / _SoftFade);

                color.rgb *= _Intensity;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
