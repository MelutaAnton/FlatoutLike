// HighJumpNet — процедурная сетка-рабица для измерительной стены режима High Jump.
// Полупрозрачная плёнка-подложка + ромбический узор проволоки. Никаких текстур —
// узор строится чисто математически (поворот UV на 45° + frac + расстояние до границ
// ячейки), поэтому остаётся чётким на любой дистанции и при любом _Tiling.
// Unlit-декор (свет не нужен), но учитывает URP-фог, чтобы вписаться в атмосферу арены.
Shader "FlatOutLike/HighJumpNet"
{
    Properties
    {
        _WireColor ("Wire Color", Color) = (0.85, 0.85, 0.85, 1)
        _BackingColor ("Backing Color", Color) = (0.1, 0.12, 0.15, 0.18)
        _Tiling ("Tiling (X, Y ячеек)", Vector) = (88, 120, 0, 0)
        _WireThickness ("Wire Thickness (доля ячейки)", Range(0.01, 0.4)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Все свойства материала — в единый CBUFFER для совместимости со SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                half4 _WireColor;
                half4 _BackingColor;
                float4 _Tiling;
                half _WireThickness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.uv         = IN.uv;
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Масштабируем UV числом ячеек по каждой оси.
                float2 p = IN.uv * _Tiling.xy;

                // Поворот координат на 45°, чтобы линии сетки образовали ромбический
                // узор рабицы (сама решётка остаётся прямоугольной, меняется лишь базис).
                const float kSin45 = 0.70710678;
                const float kCos45 = 0.70710678;
                float2 rp = float2(p.x * kCos45 - p.y * kSin45,
                                    p.x * kSin45 + p.y * kCos45);

                // Позиция внутри ячейки [0..1) и расстояние до ближайшей границы ячейки —
                // именно на границах проходит проволока.
                float2 f = frac(rp);
                float2 distToEdge = min(f, 1.0 - f);

                // Толщина проволоки в долях ячейки (половина — т.к. граница общая для двух ячеек).
                float halfThickness = _WireThickness * 0.5;

                // Сглаживание кромки через экранные производные — чтобы узор не мерцал
                // при движении камеры/объекта на любом расстоянии.
                float2 aa = max(fwidth(distToEdge), 1e-5);
                float wireX = 1.0 - smoothstep(halfThickness - aa.x, halfThickness + aa.x, distToEdge.x);
                float wireY = 1.0 - smoothstep(halfThickness - aa.y, halfThickness + aa.y, distToEdge.y);

                // Проволока видна, если попали в полосу по X ИЛИ по Y (два семейства линий решётки).
                float wireMask = saturate(wireX + wireY - wireX * wireY);

                half4 color = lerp(_BackingColor, _WireColor, wireMask);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
