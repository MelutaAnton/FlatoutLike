using UnityEngine;

namespace FlatOutLike.Data.Configs.Camera
{
    [CreateAssetMenu(fileName = "ChaseCameraConfig", menuName = "FlatOutLike/Camera/ChaseCameraConfig")]
    public sealed class ChaseCameraConfig : ScriptableObject
    {
        [SerializeField] private float _followDistance = 7.5f;
        [SerializeField] private float _height = 3.2f;
        [SerializeField] private float _positionSmoothing = 0.09f;
        [SerializeField] private float _rotationSmoothing = 10f;
        [SerializeField] private float _lookAheadFactor = 0.3f;
        [SerializeField] private float _fieldOfView = 66f;
        [SerializeField] private float _lookAtHeightOffset = 1.2f;
        [SerializeField] private float _maxLookAhead = 5f;
        [SerializeField] private float _maxLagDistance = 2.5f;

        public float FollowDistance => _followDistance;
        public float Height => _height;
        public float PositionSmoothing => _positionSmoothing;
        public float RotationSmoothing => _rotationSmoothing;
        public float LookAheadFactor => _lookAheadFactor;

        /// <summary>FOV камеры; 0 означает «не трогать текущее значение».</summary>
        public float FieldOfView => _fieldOfView;

        /// <summary>Подъём точки взгляда над пивотом машины, м.</summary>
        public float LookAtHeightOffset => _lookAtHeightOffset;

        /// <summary>Потолок сдвига точки взгляда вперёд по скорости, м.</summary>
        public float MaxLookAhead => _maxLookAhead;

        /// <summary>
        /// Максимальное отставание камеры от желаемой точки, м (0 = без ограничения).
        /// SmoothDamp на скорости и в дрифте копит лаг — потолок гарантирует,
        /// что машина не выпадет из кадра при резких манёврах.
        /// </summary>
        public float MaxLagDistance => _maxLagDistance;
    }
}
