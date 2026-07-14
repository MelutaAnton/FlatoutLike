using UnityEngine;

namespace FlatOutLike.Data.Configs.Camera
{
    /// <summary>
    /// Тюнинг <see cref="Unity.Cinemachine.CinemachineDeoccluder"/> (антиклиппинг камеры об
    /// окружение). Единый источник настроек для всех сцен — editor-скрипт при создании vcam
    /// запекает в компонент только safe-фоллбэк, рантайм-адаптеры (chase/orbit) применяют
    /// этот конфиг поверх него в Awake, так что правка ассета не требует перезапуска
    /// сборочного меню в каждой сцене.
    /// </summary>
    [CreateAssetMenu(fileName = "DeoccluderConfig", menuName = "FlatOutLike/Camera/DeoccluderConfig")]
    public sealed class DeoccluderConfig : ScriptableObject
    {
        [Tooltip("Слои, с которыми проверяется столкновение камеры (стены, препятствия арены).")]
        [SerializeField] private LayerMask _collideAgainst = 1; // слой Default

        [Tooltip("Слои, которые считаются прозрачными для рейкаста камеры (не блокируют обзор).")]
        [SerializeField] private LayerMask _transparentLayers = 0;

        [Tooltip("Минимальная дистанция от цели, ближе которой камера не подтягивается при обходе препятствия.")]
        [SerializeField] private float _minimumDistanceFromTarget = 1f;

        [Tooltip("Радиус камеры для проверки столкновений (сфера-каст вместо точечного луча).")]
        [SerializeField] private float _cameraRadius = 0.5f;

        [Tooltip("Демпфирование возврата камеры к штатной позиции после обхода препятствия.")]
        [SerializeField] private float _damping = 0.4f;

        [Tooltip("Демпфирование движения камеры, пока она перекрыта препятствием.")]
        [SerializeField] private float _dampingWhenOccluded = 0.2f;

        [Tooltip("Сглаживание переходов между состояниями окклюзии (0 = мгновенно, без сглаживания).")]
        [SerializeField] private float _smoothingTime = 0f;

        public LayerMask CollideAgainst => _collideAgainst;
        public LayerMask TransparentLayers => _transparentLayers;
        public float MinimumDistanceFromTarget => _minimumDistanceFromTarget;
        public float CameraRadius => _cameraRadius;
        public float Damping => _damping;
        public float DampingWhenOccluded => _dampingWhenOccluded;
        public float SmoothingTime => _smoothingTime;
    }
}
