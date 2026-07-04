using UnityEngine;

namespace FlatOutLike.Data.Configs.Camera
{
    [CreateAssetMenu(fileName = "ChaseCameraConfig", menuName = "FlatOutLike/Camera/ChaseCameraConfig")]
    public sealed class ChaseCameraConfig : ScriptableObject
    {
        [SerializeField] private float _followDistance = 6f;
        [SerializeField] private float _height = 2.5f;
        [SerializeField] private float _positionSmoothing = 0.2f;
        [SerializeField] private float _rotationSmoothing = 5f;
        [SerializeField] private float _lookAheadFactor = 0.3f;
        [SerializeField] private float _fieldOfView = 66f;
        [SerializeField] private float _lookAtHeightOffset = 1.2f;
        [SerializeField] private float _maxLookAhead = 5f;

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
    }
}
