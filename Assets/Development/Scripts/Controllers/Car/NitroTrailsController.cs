using FlatOutLike.Core.Events;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Car
{
    /// <summary>
    /// Включает/выключает готовые огненные трейлы из задней части машины по
    /// <see cref="NitroStateChangedEvent"/>. Эмиттеры — уже настроенные partical-объекты
    /// на префабе машины (не пул, не VFX-сервис — просто SetActive), поэтому включение/выключение
    /// делается напрямую, без обращения к IVfxService/IPoolManager.
    /// Висит на корне машины; инъекцию даёт CarFactory через InjectGameObject.
    /// </summary>
    public sealed class NitroTrailsController : MonoBehaviour
    {
        [SerializeField] private CarController _car;
        [SerializeField] private GameObject[]  _trailEmitters;

        private IEventBus _eventBus;
        private bool _initialized;

        [Inject]
        public void Construct(IEventBus eventBus)
        {
            _eventBus = eventBus;
            // OnEnable отрабатывает до Construct — подписываемся здесь явно (по образцу CarDamageVisualController).
            _eventBus.Subscribe<NitroStateChangedEvent>(OnNitroStateChanged);

            // Защитно гасим все трейлы: до первого NitroStateChangedEvent(true) они должны быть выключены,
            // даже если в префабе по ошибке оставили их активными.
            SetTrailsActive(false);

            _initialized = true;
        }

        private void OnEnable()
        {
            // Guard: при старте сцены OnEnable срабатывает раньше Construct — подписку уже сделает Construct.
            // Здесь только повторная подписка при возврате объекта из пула/повторном включении.
            if (!_initialized) return;
            _eventBus.Subscribe<NitroStateChangedEvent>(OnNitroStateChanged);
        }

        private void OnDisable()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<NitroStateChangedEvent>(OnNitroStateChanged);
        }

        private void OnDestroy()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<NitroStateChangedEvent>(OnNitroStateChanged);
        }

        private void OnNitroStateChanged(NitroStateChangedEvent evt)
        {
            if (_car == null || evt.CarId != _car.CarId) return;
            SetTrailsActive(evt.IsActive);
        }

        private void SetTrailsActive(bool active)
        {
            if (_trailEmitters == null) return;
            foreach (var emitter in _trailEmitters)
            {
                if (emitter == null) continue;
                emitter.SetActive(active);
            }
        }
    }
}
