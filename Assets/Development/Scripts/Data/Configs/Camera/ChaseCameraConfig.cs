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

        public float FollowDistance => _followDistance;
        public float Height => _height;
        public float PositionSmoothing => _positionSmoothing;
        public float RotationSmoothing => _rotationSmoothing;
        public float LookAheadFactor => _lookAheadFactor;
    }
}
