Shader "PCGLand/TerrainTriplanar"
{
    // 体素地形着色器（URP）：以顶点色作为 Biome 基色，
    // 用世界法线做坡度（triplanar 思路）混合，区分平台与陡坡（岩石）。
    // 体素网格无干净 UV，故完全基于世界空间与顶点色，无需纹理。
    // Cull Off 作为 Dual Contouring 四边形绕序的兜底，避免孔洞。
    Properties
    {
        _RockColor ("陡坡岩石色", Color) = (0.40, 0.37, 0.34, 1)
        _SlopeStart ("坡度起始(法线Y)", Range(0,1)) = 0.75
        _SlopeEnd ("坡度结束(法线Y)", Range(0,1)) = 0.35
        _Ambient ("环境光强度", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RockColor;
                float _SlopeStart;
                float _SlopeEnd;
                float _Ambient;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);

                // 坡度混合：法线 Y 越小越陡 → 越偏岩石色
                float slope = smoothstep(_SlopeEnd, _SlopeStart, n.y);
                float3 albedo = lerp(_RockColor.rgb, IN.color.rgb, slope);

                // 主光 Lambert + 环境
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(n, mainLight.direction));
                float3 diffuse = albedo * mainLight.color * ndotl;
                float3 ambient = albedo * _Ambient;

                return half4(diffuse + ambient, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
