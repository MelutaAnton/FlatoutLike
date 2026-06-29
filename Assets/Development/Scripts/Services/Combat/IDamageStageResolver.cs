using System.Collections.Generic;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Services.Combat
{
    /// <summary>
    /// Возвращает индекс активной стадии для HP-соотношения (0..1); -1 если стадий нет.
    /// Стадии должны идти по убыванию hpThreshold.
    /// </summary>
    public interface IDamageStageResolver
    {
        int ResolveStageIndex(IReadOnlyList<CarDamageVisualConfig.DamageStage> stages, float hpRatio);
    }
}
