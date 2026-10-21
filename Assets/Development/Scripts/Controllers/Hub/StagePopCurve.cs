using UnityEngine;

namespace FlatOutLike.Controllers.Hub
{
    /// <summary>
    /// Кривая «попа» появления визуала стадии стройки: множитель масштаба по нормализованному
    /// времени. Первая фаза (до <see cref="RiseEnd"/>) — ease-out от startScale до overshoot,
    /// вторая — плавный возврат к 1. На t ≥ 1 результат ровно 1 (без накопленной погрешности).
    /// Чистая функция без состояния — тестируется без сцены.
    /// </summary>
    public static class StagePopCurve
    {
        /// <summary>Доля времени, отведённая на рост до overshoot.</summary>
        private const float RiseEnd = 0.7f;

        public static float Evaluate(float t01, float startScale, float overshoot)
        {
            if (t01 <= 0f) return startScale;
            if (t01 >= 1f) return 1f;

            if (t01 < RiseEnd)
            {
                // Ease-out (квадратичный): быстрый старт, мягкий выход на overshoot.
                float k = t01 / RiseEnd;
                float eased = 1f - (1f - k) * (1f - k);
                return Mathf.LerpUnclamped(startScale, overshoot, eased);
            }

            // Возврат overshoot -> 1 по smoothstep.
            float back = (t01 - RiseEnd) / (1f - RiseEnd);
            float smooth = back * back * (3f - 2f * back);
            return Mathf.LerpUnclamped(overshoot, 1f, smooth);
        }
    }
}
