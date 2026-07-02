using FlatOutLike.Data.Configs.Destructible;
using FlatOutLike.Factories.Destructible;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Car
{
    /// <summary>
    /// Разбивающееся лобовое стекло (как во FlatOut 2): в момент вылета водителя
    /// узел лобового (<c>*_Glass_F</c> из пака Low Poly Destructible Cars) скрывается,
    /// а на его месте фабрика спавнит pooled-осколки, наследующие скорость машины.
    /// Если у модели машины нет узла лобового — эффект молча пропускается.
    /// Только Unity API; вызывается из eject-потока CarController.
    /// </summary>
    public sealed class WindshieldController : MonoBehaviour
    {
        // Суффикс узла лобового стекла в иерархии визуала (конвенция пака машин)
        private const string WindshieldSuffix = "_Glass_F";

        /// <summary>Конфиг осколков (Debris_GlassShard). Заполняется editor-скриптом на префабе машины.</summary>
        [SerializeField] private DestructibleConfig _debrisConfig;

        private IDestructibleFactory _destructibleFactory;
        private bool _shattered;

        [Inject]
        public void Construct(IDestructibleFactory destructibleFactory)
        {
            _destructibleFactory = destructibleFactory;
        }

        /// <summary>
        /// Скрывает лобовое и спавнит осколки. <paramref name="baseVelocity"/> —
        /// скорость машины в момент вылета (осколки летят по ходу движения).
        /// Повторный вызов игнорируется.
        /// </summary>
        public void Shatter(Vector3 baseVelocity)
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
