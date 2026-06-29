using UnityEngine;

namespace FlatOutLike.Data.Configs.AI
{
    /// <summary>
    /// Геометрия сенсорных лучей AI-контроллера.
    /// Пороги реакции (ObstacleAvoidDistance / ObstacleSideAvoidDistance) живут
    /// в <see cref="FlatOutLike.Data.Configs.Enemies.EnemyPersonalityConfig"/> — они
    /// являются частью "личности" врага. Здесь — только физическая геометрия лучей.
    /// </summary>
    [CreateAssetMenu(fileName = "AISensorConfig", menuName = "FlatOutLike/AI/AISensorConfig")]
    public sealed class AISensorConfig : ScriptableObject
    {
        // Подъём origin'а над землёй — чтобы лучи шли на уровне «груди» машины,
        // а не цепляли текстуру земли. Используется и для forward-, и для side-лучей.
        [SerializeField, Min(0f)] private float _rayOriginUp = 0.5f;

        // Длина forward-сканирования (Service сам сравнит с ObstacleAvoidDistance).
        [SerializeField, Min(0f)] private float _rayLength = 12f;

        // Угол отклонения боковых forward-лучей от forward (градусы).
        [SerializeField, Range(0f, 89f)] private float _raySideAngle = 30f;

        // Длина перпендикулярных side-лучей. Покрывает половину ширины машины + scan.
        [SerializeField, Min(0f)] private float _sideRayLength = 2.5f;

        // Слои, которые считаются препятствиями. По умолчанию Everything (~0).
        // Сам коллайдер машины отсеивается через сравнение attachedRigidbody — поэтому
        // origin'у не нужен forward-offset; лучи стартуют из центра selfPos.
        [SerializeField] private LayerMask _obstacleLayerMask = ~0;

        public float    RayOriginUp        => _rayOriginUp;
        public float    RayLength          => _rayLength;
        public float    RaySideAngle       => _raySideAngle;
        public float    SideRayLength      => _sideRayLength;
        public LayerMask ObstacleLayerMask => _obstacleLayerMask;
    }
}
