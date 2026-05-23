using UnityEngine;

namespace FlatOutLike.Data.Configs.Car
{
    [CreateAssetMenu(fileName = "CarStatsConfig", menuName = "FlatOutLike/Car/CarStatsConfig")]
    public sealed class CarStatsConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float _acceleration = 2000f;
        [SerializeField] private float _topSpeed = 25f;
        [SerializeField] private float _mass = 1200f;
        [SerializeField] private float _steerAngle = 30f;
        [SerializeField, Range(0f, 1f)] private float _steerSpeedFalloff = 0.5f;
        [SerializeField] private float _brakeForce = 3000f;
        [SerializeField] private float _engineBrakeForce = 600f;
        [SerializeField] private float _wheelFriction = 1f;
        [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0f, -0.4f, 0f);

        [Header("Reserved for later steps")]
        [SerializeField] private float _maxArmor = 100f;
        [SerializeField] private float _nitroCapacity = 100f;
        [SerializeField] private float _nitroForce = 2000f;

        public float Acceleration => _acceleration;
        public float TopSpeed => _topSpeed;
        public float Mass => _mass;
        public float SteerAngle => _steerAngle;
        public float SteerSpeedFalloff => _steerSpeedFalloff;
        public float BrakeForce => _brakeForce;
        public float EngineBrakeForce => _engineBrakeForce;
        public float WheelFriction => _wheelFriction;
        public Vector3 CenterOfMassOffset => _centerOfMassOffset;
        public float MaxArmor => _maxArmor;
        public float NitroCapacity => _nitroCapacity;
        public float NitroForce => _nitroForce;
    }
}
