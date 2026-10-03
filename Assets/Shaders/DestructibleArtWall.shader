Shader "Catapulto/DestructibleArtWall"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.55, 0.5, 0.45, 1)
        _GridOrigin ("Grid Origin", Vector) = (0,0,0,0)
        _CellSize ("Cell Size", Vector) = (0.4,0.4,0.4,0)
        _GridDims ("Grid Dims", Vector) = (16,12,2,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE3D(_DamageMask);
            SAMPLER(sampler_DamageMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GridOrigin;
                float4 _CellSize;
                float4 _GridDims;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS);
                o.positionCS = pos.positionCS;
                o.positionOS = input.positionOS.xyz;
                o.normalWS = n.normalWS;
                return o;
            }

            float SampleDestroyed(float3 positionOS)
            {
                float3 local = positionOS - _GridOrigin.xyz;
                float3 cellSize = max(_CellSize.xyz, float3(0.001, 0.001, 0.001));
                int3 dims = int3(_GridDims.xyz);
                int3 id = int3(floor(local / cellSize));
                // Вне сетки не клипаем — иначе съедает края модели
                if (id.x < 0 || id.y < 0 || id.z < 0 || id.x >= dims.x || id.y >= dims.y || id.z >= dims.z)
                    return 0;
                float3 uvw = (float3(id) + 0.5) / float3(dims);
                return SAMPLE_TEXTURE3D_LOD(_DamageMask, sampler_DamageMask, uvw, 0).r;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(0.5 - SampleDestroyed(i.positionOS));

                float3 normalWS = normalize(i.normalWS);
                Light light = GetMainLight();
                half ndl = saturate(dot(normalWS, light.direction));
                half3 col = _BaseColor.rgb * (ndl * light.color + 0.28);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
