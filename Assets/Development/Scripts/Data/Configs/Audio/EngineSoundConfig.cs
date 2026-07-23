using UnityEngine;

namespace FlatOutLike.Data.Configs.Audio
{
    /// <summary>
    /// Настройки звука двигателя (RealisticEngineSound) для всех машин.
    /// Единственный источник параметров RPM/громкости/дистанции — сторонний компонент
    /// сам по себе не data-driven по проекту, значения прошивает <c>CarEngineSoundBridge</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "EngineSoundConfig", menuName = "FlatOutLike/Audio/EngineSoundConfig")]
    public sealed class EngineSoundConfig : ScriptableObject
    {
        [Header("RPM")]
        [Tooltip("RPM холостого хода (машина стоит, газ не нажат).")]
        [SerializeField] private float _idleRpm = 900f;
        [Tooltip("Максимальный RPM на пиковой скорости. Должен укладываться в maxRPMLimit префаба RES (~7000).")]
        [SerializeField] private float _maxRpm = 6500f;
        [Tooltip("Количество псевдо-передач для имитации пилообразного роста RPM по скорости.")]
        [SerializeField] private int _gearCount = 5;
        [Tooltip("Порог ThrottleInput, выше которого считаем что газ нажат (для gasPedalPressing).")]
        [SerializeField] private float _throttleThreshold = 0.05f;

        [Header("Громкость и дистанция")]
        [SerializeField, Range(0.1f, 1f)] private float _masterVolume = 0.6f;
        [SerializeField] private float _minDistance = 5f;
        [SerializeField] private float _maxDistance = 45f;

        public float IdleRpm => _idleRpm;
        public float MaxRpm => _maxRpm;
        public int GearCount => _gearCount;
        public float ThrottleThreshold => _throttleThreshold;
        public float MasterVolume => _masterVolume;
        public float MinDistance => _minDistance;
        public float MaxDistance => _maxDistance;
    }
}
