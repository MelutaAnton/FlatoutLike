using System.Collections.Generic;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Services.Combat
{
    /// <summary>
    /// Стейтлесс-реализация алгоритма выбора стадии повреждения.
    /// Выбирает стадию с минимальным hpThreshold, который при этом >= hpRatio.
    /// По мере падения HP последовательно входим в более глубокие стадии.
    /// </summary>
    public sealed class DamageStageResolver : IDamageStageResolver
    {
        /// <inheritdoc/>
        public int ResolveStageIndex(IReadOnlyList<CarDamageVisualConfig.DamageStage> stages, float hpRatio)
        {
            if (stages == null || stages.Count == 0) return -1;

            int result = -1;
            float minThreshold = float.MaxValue;

            for (int i = 0; i < stages.Count; i++)
            {
                float threshold = stages[i].hpThreshold;
                if (threshold >= hpRatio && threshold < minThreshold)
                {
                    minThreshold = threshold;
                    result = i;
                }
            }

            // Fallback: ratio выше всех порогов (например, только что заспавнились) — берём первую стадию.
            if (result == -1)
                result = 0;

            return result;
        }
    }
}
