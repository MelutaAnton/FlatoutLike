// Fullscreen-пасс радиального блюра по краям экрана — визуальный отклик на нитро игрока.
// Управляется исключительно через глобальный float _NitroBlurIntensity
// (NitroCameraEffectController.Update → Shader.SetGlobalFloat), значение материала не используется.
// Подключается через штатный FullScreenPassRendererFeature URP (Unity 6).
Shader "FlatOutLike/NitroRadialBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "NitroRadialBlur"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Единственный источник интенсивности — прошивается снаружи через Shader.SetGlobalFloat
            // (ключ _NitroBlurIntensity задан один раз в NitroCameraEffectController.BlurIntensityId).
            // По умолчанию 0 — картинка нетронутая, даже если фича активна без явного вызова.
            float _NitroBlurIntensity;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // Направление от центра экрана — блюр растягивается радиально к краям.
                float2 dir = uv - 0.5;

                // Краевая маска: центр кадра остаётся чистым, интенсивность нарастает к краям.
                float edge = smoothstep(0.15, 0.75, length(dir) * 2.0);

                float2 offset = dir * _NitroBlurIntensity * edge * 0.12;

                // 6 развёрнутых семплов вдоль направления блюра, равные веса — без циклов
                // с переменной границей (мобильный таргет).
                half4 col = 0;
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * 1.0);
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * 0.8);
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset * 0.6);
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * 0.6);
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * 0.8);
                col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * 1.0);
                col *= (1.0 / 6.0);

                return col;
            }
            ENDHLSL
        }
    }
}
