using FlatOutLike.Data.Configs.Destructible;
using FlatOutLike.Factories.Destructible;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Destructible
{
    /// <summary>
    /// Контроллер разрушаемого объекта (забор, ящик и т.п.).
    /// Детектирует удар через OnCollisionEnter и делегирует разрушение фабрике.
    /// Бизнес-логики нет — только Unity Physics API и вызов фабрики.
    /// </summary>
    public sealed class DestructibleObjectController : MonoBehaviour
    {
        // Конфиг разрушаемого объекта (порог удара, обломки и т.д.)
        [SerializeField] private DestructibleConfig _config;

        // Вложенный визуал (ThirdParty-меш), который скрывается при разрушении
        [SerializeField] private GameObject _intactVisual;

        // Собственный коллайдер объекта — выключается после разрушения
        [SerializeField] private Collider _collider;

        private IDestructibleFactory _factory;
        private bool _broken;
        // Флаг инициализации: OnEnable вызывается ДО Construct() — см. CLAUDE.md
        private bool _initialized;

        [Inject]
        public void Construct(IDestructibleFactory factory)
        {
            _factory     = factory;
            _initialized = true;
        }

        private void OnCollisionEnter(Collision c)
        {
            // Гарды: инъекция ещё не произошла, объект уже разрушен или конфиг не задан
            if (!_initialized) return;
            if (_broken) return;
            if (_config == null) return;

            if (c.relativeVelocity.magnitude >= _config.BreakImpulseThreshold)
                Break();
        }

        private void Break()
        {
            _broken = true;

            // Скрываем визуал и коллайдер — машина больше не тормозит об объект
            if (_intactVisual != null)
                _intactVisual.SetActive(false);

            if (_collider != null)
                _collider.enabled = false;

            // Спавн обломков делегируется фабрике
            _factory.SpawnDebris(transform.position, _config);
        }
    }
}
