using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Car
{
    [CreateAssetMenu(fileName = "CarStatsConfig", menuName = "FlatOutLike/Car/CarStatsConfig")]
    public sealed class CarStatsConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float _acceleration = 3500f;
        [SerializeField] private float _topSpeed = 30f;
        [SerializeField] private float _mass = 1200f;
        [SerializeField] private float _steerAngle = 30f;
        [SerializeField, Range(0f, 1f)] private float _steerSpeedFalloff = 0.35f;
        [SerializeField] private float _brakeForce = 3000f;
        [SerializeField] private float _engineBrakeForce = 400f;
        [SerializeField] private float _wheelFriction = 1f;
        [SerializeField] private Vector3 _centerOfMassOffset = new Vector3(0f, -0.4f, 0f);

        [Header("Steering feel")]
        /// <summary>Скорость доворота руля к целевому углу, град/с.</summary>
        [SerializeField] private float _steerRate = 300f;
        /// <summary>Скорость возврата руля к центру (или смены стороны), град/с. Быстрее SteerRate — ключ к отзывчивости на клавиатуре.</summary>
        [SerializeField] private float _steerReturnRate = 520f;

        [Header("Handbrake")]
        /// <summary>Множитель бокового сцепления задних колёс при зажатом ручнике (0..1) — чем меньше, тем охотнее машина уходит в занос.</summary>
        [SerializeField, Range(0f, 1f)] private float _handbrakeGripFactor = 0.45f;
        /// <summary>Тормозной момент задних колёс при ручнике — частичный, в отличие от полного BrakeForce, чтобы ручник давал занос, а не мгновенную остановку.</summary>
        [SerializeField] private float _handbrakeBrakeForce = 800f;

        [Header("Reserved for later steps")]
        [SerializeField] private float _maxArmor = 100f;
        [SerializeField] private float _nitroCapacity = 100f;
        [SerializeField] private float _nitroForce = 2000f;
        [SerializeField] private float _nitroDrainRate = 25f;
        // Минимальная скорость (м/с) для срабатывания нитро: стоя на месте кнопка не жжёт
        // бак и не включает эффекты. Инициализатор покрывает уже собранные .asset'ы
        // (отсутствующее в YAML поле берёт дефолт из кода).
        [SerializeField] private float _nitroMinSpeed = 1.5f;

        public float Acceleration => _acceleration;
        public float TopSpeed => _topSpeed;
        public float Mass => _mass;
        public float SteerAngle => _steerAngle;
        public float SteerSpeedFalloff => _steerSpeedFalloff;
        public float BrakeForce => _brakeForce;
        public float EngineBrakeForce => _engineBrakeForce;
        public float WheelFriction => _wheelFriction;
        public Vector3 CenterOfMassOffset => _centerOfMassOffset;
        /// <summary>Скорость доворота руля к целевому углу, град/с.</summary>
        public float SteerRate => _steerRate;
        /// <summary>Скорость возврата руля к центру/смены стороны, град/с.</summary>
        public float SteerReturnRate => _steerReturnRate;
        /// <summary>Множитель бокового сцепления задних колёс при ручнике (0..1).</summary>
        public float HandbrakeGripFactor => _handbrakeGripFactor;
        /// <summary>Тормозной момент задних колёс при ручнике (частичный, не полный BrakeForce).</summary>
        public float HandbrakeBrakeForce => _handbrakeBrakeForce;
        public float MaxArmor => _maxArmor;
        public float NitroCapacity => _nitroCapacity;
        public float NitroForce => _nitroForce;
        /// <summary>Расход нитро в единицах/секунду пока кнопка нитро зажата.</summary>
        public float NitroDrainRate => _nitroDrainRate;
        public float NitroMinSpeed => _nitroMinSpeed;

        /// <summary>
        /// Создаёт runtime-клон конфига с применёнными множителями апгрейдов
        /// (<c>CarStatsService</c> вызывает это на базовых статах каждой машины).
        /// При нейтральных множителях (все = 1.0) клонирование не выполняется —
        /// возвращается исходный конфиг (тот же reference), чтобы не плодить лишние ассеты.
        /// </summary>
        public CarStatsConfig CreateModifiedClone(in CarStatsModifiers mods)
        {
            if (mods.ArmorMultiplier == 1f && mods.PowerMultiplier == 1f && mods.NitroMultiplier == 1f)
                return this;

            var clone = Instantiate(this);
            clone._maxArmor *= mods.ArmorMultiplier;
            clone._acceleration *= mods.PowerMultiplier;
            clone._topSpeed *= mods.PowerMultiplier;
            clone._nitroCapacity *= mods.NitroMultiplier;
            clone._nitroForce *= mods.NitroMultiplier;
            return clone;
        }
    }
}
