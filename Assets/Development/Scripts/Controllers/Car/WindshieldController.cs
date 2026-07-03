using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Destructible;
using FlatOutLike.Factories.Destructible;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Car
{
    /// <summary>
    /// Разбивающееся лобовое стекло (как во FlatOut 2): в момент вылета водителя
    /// узел лобового (<c>*_Glass_F</c> из пака Low Poly Destructible Cars) скрывается,
    /// а на его месте фабрика спавнит pooled-осколки, летящие вместе с водителем.
    /// Срабатывает по <see cref="DriverEjectedEvent"/> СВОЕЙ машины — единый путь
    /// для любого источника eject'а (гибель в Derby, трамплин в High Jump).
    /// Если у модели машины нет узла лобового — эффект молча пропускается.
    /// </summary>
    public sealed class WindshieldController : MonoBehaviour
    {
        // Суффикс узла лобового стекла в иерархии визуала (конвенция пака машин)
        private const string WindshieldSuffix = "_Glass_F";

        /// <summary>Конфиг осколков (Debris_GlassShard). Заполняется editor-скриптом на префабе машины.</summary>
        [SerializeField] private DestructibleConfig _debrisConfig;

        private IDestructibleFactory _destructibleFactory;
        private IEventBus _eventBus;
        private CarController _car;
        private bool _shattered;

        private void Awake()
        {
            _car = GetComponent<CarController>();
        }

        [Inject]
        public void Construct(IDestructibleFactory destructibleFactory, IEventBus eventBus)
        {
            _destructibleFactory = destructibleFactory;
            _eventBus = eventBus;
            _eventBus.Subscribe<DriverEjectedEvent>(OnDriverEjected);
        }

        private void OnDriverEjected(DriverEjectedEvent e)
        {
            // Реагируем только на вылет из СВОЕЙ машины.
            if (_car == null || e.Source != _car.CarId) return;

            Shatter(e.EjectVelocity);
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
                _eventBus.Unsubscribe<DriverEjectedEvent>(OnDriverEjected);
        }

        /// <summary>
        /// Скрывает лобовое и спавнит осколки. <paramref name="baseVelocity"/> —
        /// скорость вылета водителя (осколки летят вместе с ним «через лобовое»).
        /// Повторный вызов игнорируется.
        /// </summary>
        private void Shatter(Vector3 baseVelocity)
        {
            if (_shattered) return;
            _shattered = true;

            var windshield = FindWindshield();
            if (windshield == null) return; // у этой модели нет отдельного лобового — пропускаем

            Vector3 origin = GetVisualCenter(windshield);
            windshield.gameObject.SetActive(false);

            if (_destructibleFactory != null && _debrisConfig != null)
                _destructibleFactory.SpawnDebris(origin, _debrisConfig, baseVelocity);
        }

        private Transform FindWindshield()
        {
            // Поиск по суффиксу имени: визуал машины — вложенный ThirdParty prefab-instance,
            // прямой ссылки на узел стекла в нашем префабе нет.
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.EndsWith(WindshieldSuffix))
                    return child;
            }
            return null;
        }

        private static Vector3 GetVisualCenter(Transform windshield)
        {
            // Центр bounds рендерера точнее pivot'а меша (pivot у пака может быть в нуле машины)
            var renderer = windshield.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds.center : windshield.position;
        }
    }
}
