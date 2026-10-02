using FlatOutLike.Data.Configs.Camera;
using Unity.Cinemachine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Применяет <see cref="DeoccluderConfig"/> к <see cref="CinemachineDeoccluder"/> —
    /// единый источник тюнинга антиклиппинга для всех сцен и обеих vcam (chase/orbit).
    /// Вынесен из адаптеров, чтобы настройки не расходились между ними (был копипаст
    /// байт-в-байт). Editor-скрипт при сборке vcam запекает в компонент только
    /// safe-фоллбэк; если конфиг или компонент отсутствуют — запечённые значения
    /// остаются в силе.
    /// </summary>
    public static class DeoccluderApplier
    {
        // Макс. число попыток CinemachineDeoccluder найти позицию без препятствий за кадр.
        private const int MaxDeoccluderEffort = 4;

        public static void Apply(CinemachineDeoccluder deoccluder, DeoccluderConfig config)
        {
            // Нет компонента/конфига — остаются значения, запечённые editor-скриптом.
            if (deoccluder == null || config == null) return;

            deoccluder.CollideAgainst = config.CollideAgainst;
            deoccluder.TransparentLayers = config.TransparentLayers;
            deoccluder.MinimumDistanceFromTarget = config.MinimumDistanceFromTarget;

            var avoid = deoccluder.AvoidObstacles; // поле-структура: копия → правка → запись обратно
            avoid.Enabled = true;
            avoid.DistanceLimit = 0f;
            avoid.MinimumOcclusionTime = 0f;
            avoid.CameraRadius = config.CameraRadius;
            avoid.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward; // стратегия — осознанно в коде, не в конфиге
            avoid.MaximumEffort = MaxDeoccluderEffort;
            avoid.SmoothingTime = config.SmoothingTime;
            avoid.Damping = config.Damping;
            avoid.DampingWhenOccluded = config.DampingWhenOccluded;
            avoid.UseFollowTarget.Enabled = false;
            deoccluder.AvoidObstacles = avoid;
        }
    }
}
