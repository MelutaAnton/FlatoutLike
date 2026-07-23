using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Audio;
using UnityEngine;

namespace FlatOutLike.Bridge
{
    /// <summary>
    /// Мост между нашим CarController и сторонним RealisticEngineSound (пак без asmdef,
    /// класс RealisticEngineSound лежит в глобальном неймспейсе Assembly-CSharp).
    /// Поэтому этот файл сознательно кладётся в Assets/Development/BridgeScripts/ — папку
    /// БЕЗ собственного asmdef: она тоже компилируется в Assembly-CSharp и видит RealisticEngineSound,
    /// а благодаря autoReferenced=true у FlatOutLike.asmdef видит и публичное API наших классов
    /// (CarController, EngineSoundConfig). Из FlatOutLike.asmdef сам RealisticEngineSound недоступен —
    /// граница сборок и есть причина существования этого моста.
    ///
    /// Чистая проводка префаба, без DI: и CarController, и RealisticEngineSound уже присутствуют
    /// на том же префабе машины и назначаются через Inspector/editor-скрипт. В VContainer
    /// не регистрируется.
    /// </summary>
    public sealed class CarEngineSoundBridge : MonoBehaviour
    {
        [SerializeField] private CarController _car;
        [SerializeField] private RealisticEngineSound _engineSound;
        [SerializeField] private EngineSoundConfig _config;

        private bool _engineStopped;

        private void Awake()
        {
            if (_engineSound == null || _config == null) return;

            _engineSound.masterVolume = _config.MasterVolume;
            _engineSound.minDistance = _config.MinDistance;
            _engineSound.maxDistance = _config.MaxDistance;
            _engineSound.maxRPMLimit = _config.MaxRpm;
        }

        private void OnEnable()
        {
            // Страховка на случай переиспользования машины (пул/респавн): сейчас CarFactory
            // машины не пулит, но перманентный SetActive(false) двигателя без обратного пути
            // сделал бы переиспользованную машину немой навсегда.
            _engineStopped = false;
            if (_engineSound != null && !_engineSound.gameObject.activeSelf)
                _engineSound.gameObject.SetActive(true);
        }

        // Пауза отдельно не обрабатывается: AudioListener.pause (см. AudioService) глушит
        // все AudioSource сцены, включая созданные RealisticEngineSound — этого достаточно.
        private void Update()
        {
            if (_car == null || _engineSound == null || _config == null) return;

            if (_car.IsWrecked)
            {
                // Двигатель заглох при вреке — гасим источник звука один раз.
                if (!_engineStopped)
                {
                    _engineStopped = true;
                    _engineSound.gameObject.SetActive(false);
                }
                return;
            }

            // Страховка ограничения пака: RES резолвит Camera.main один раз в своём Start
            // и при null больше не пытается — двигатель остался бы нем навсегда.
            if (_engineSound.mainCamera == null)
                _engineSound.mainCamera = UnityEngine.Camera.main;

            float maxSpeed = Mathf.Max(_car.TopSpeed, 1f);
            float speed01 = Mathf.Clamp01(_car.Speed / maxSpeed);

            // Псевдо-передачи: имитируем пилообразный рост RPM внутри каждой передачи по скорости.
            float gearFrac = speed01 >= 1f ? 1f : Mathf.Repeat(speed01 * _config.GearCount, 1f);
            // Пол 0.15 — RPM не проваливается в чистый idle между переключениями передач.
            float rpm01 = Mathf.Clamp01(0.15f + 0.85f * gearFrac);

            _engineSound.engineCurrentRPM = Mathf.Lerp(_config.IdleRpm, _config.MaxRpm, rpm01);

            // Режим NotSimulated: RES игнорирует gasPedalPressing и читает НЕПРЕРЫВНУЮ ось
            // gasPedalValue (0..1) напрямую — от неё зависит баланс громкостей on/off-клипов
            // (тон «под нагрузкой» vs «накат»). Ниже порога — чистый накат.
            // Известные упрощения (осознанно, аркада): Speed — полный модуль скорости
            // (в полёте RPM растёт без газа), задний ход звучит как разгон (isReversing не задаём).
            _engineSound.gasPedalValue =
                _car.ThrottleInput > _config.ThrottleThreshold ? _car.ThrottleInput : 0f;
        }
    }
}
