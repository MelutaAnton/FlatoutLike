using UnityEngine;

namespace FlatOutLike.Data.Configs.Camera
{
    /// <summary>
    /// Параметры тряски камеры (Cinemachine Impulse) от столкновений машин в дерби.
    /// Единственный источник тюнинга для <c>CameraShakeService</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraShakeConfig", menuName = "FlatOutLike/Camera/CameraShakeConfig")]
    public sealed class CameraShakeConfig : ScriptableObject
    {
        [SerializeField] private float _minImpactSpeed = 4f;
        [SerializeField] private float _speedToForce = 0.08f;
        [SerializeField] private float _maxForce = 1.2f;

        /// <summary>Порог скорости удара, ниже которого тряски нет (мелкие касания).</summary>
        public float MinImpactSpeed => _minImpactSpeed;

        /// <summary>Множитель перевода избыточной скорости удара (сверх MinImpactSpeed) в силу импульса.</summary>
        public float SpeedToForce => _speedToForce;

        /// <summary>Потолок силы импульса — защищает от чрезмерной тряски при мощных столкновениях.</summary>
        public float MaxForce => _maxForce;
    }
}
