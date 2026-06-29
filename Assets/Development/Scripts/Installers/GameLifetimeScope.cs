using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Core.Pool;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Factories.Car;
using FlatOutLike.Factories.Vfx;
using FlatOutLike.Services.AI;
using FlatOutLike.Services.Car;
using FlatOutLike.Services.Combat;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Meta;
using FlatOutLike.Services.Vfx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FlatOutLike.Installers
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private DamageConfig    _damageConfig;
        [SerializeField] private VfxConfig       _vfxConfig;

        /// <summary>
        /// Корневые GameObject всех машин в сцене (игрок + враги).
        /// Используется только в _TestDriving-сцене (ручной спавн).
        /// Заполняется editor-скриптом "Fill GameLifetimeScope Refs".
        /// VContainer внедряет все компоненты (CarController, EnemyCarController,
        /// CarDamageVisualController) через InjectGameObject в build callback.
        /// </summary>
        [SerializeField] private GameObject[] _sceneCars;

        // ── Шаг 5a: Derby-матч ─────────────────────────────────────────────────
        /// <summary>
        /// Конфиг Derby-матча (игрок + враги + точки спавна).
        /// Заполняется на арене; в _TestDriving оставить null.
        /// </summary>
        [SerializeField] private DerbyMatchConfig _derbyMatchConfig;

        /// <summary>
        /// Точки спавна машин для Derby-матча. Index 0 = игрок, 1.. = враги.
        /// Создаются editor-скриптом на арене.
        /// </summary>
        [SerializeField] private Transform[] _spawnPoints;

        /// <summary>
        /// Chase-камера арены. Цель выставляется в рантайме на заспавненную машину игрока
        /// (машины создаёт CarFactory, поэтому в сцене камеру не к чему привязать заранее).
        /// </summary>
        [SerializeField] private ChaseCarCameraBehaviour _chaseCamera;

        protected override void Configure(IContainerBuilder builder)
        {
            // Core
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();
            builder.Register<KeyboardInputProvider>(Lifetime.Singleton).As<IInputProvider>();
            builder.Register<InputRouter>(Lifetime.Singleton);

            // Meta
            builder.Register<PlayerPrefsProfileStorage>(Lifetime.Singleton).As<IProfileStorage>();
            builder.Register<MetaProgressService>(Lifetime.Singleton).As<IMetaProgressService>();
            builder.Register<IPlayerProfile>(c => c.Resolve<IMetaProgressService>().Profile, Lifetime.Singleton);

            // Flow
            builder.Register<GameFlowService>(Lifetime.Singleton).As<IGameFlowService>();
            builder.RegisterComponentInHierarchy<GameBootstrap>();

            // Car
            builder.Register<CarMovementService>(Lifetime.Singleton).As<ICarMovementService>();

            // Combat (Шаг 3)
            builder.RegisterInstance(_damageConfig);
            builder.Register<DamageService>(Lifetime.Singleton).As<IDamageService>();
            builder.Register<DamageStageResolver>(Lifetime.Singleton).As<IDamageStageResolver>();

            // VFX (Шаг 3)
            builder.RegisterInstance(_vfxConfig);
            builder.Register<VfxFactory>(Lifetime.Singleton).As<IVfxFactory>();
            builder.Register<VfxService>(Lifetime.Singleton).As<IVfxService>();

            // AI (Шаг 4)
            builder.Register<EnemyAIService>(Lifetime.Singleton).As<IEnemyAIService>();

            // Pool (Шаг 5a) — единственный источник пулов для всех фабрик
            builder.Register<PoolManager>(Lifetime.Singleton).As<IPoolManager>();

            // Car factory (Шаг 5a)
            builder.Register<CarFactory>(Lifetime.Singleton).As<ICarFactory>();

            // Derby match config (Шаг 5a); SO — RegisterInstance корректно, инъекция не нужна
            if (_derbyMatchConfig != null)
                builder.RegisterInstance(_derbyMatchConfig);

            // Eager-инициализация DamageService, чтобы его ctor подписался на CarCollisionEvent
            // даже когда явный consumer не запрашивает его из container.
            builder.RegisterBuildCallback(c => c.Resolve<IDamageService>());

            // Путь _TestDriving: внедряем машины, расставленные вручную в сцене.
            // Заменяет RegisterComponentInHierarchy<CarController> и
            // RegisterComponentInHierarchy<CarDamageVisualController> — эти API
            // находят только один компонент каждого типа в иерархии. Здесь мы внедряем
            // каждый GameObject из _sceneCars, покрывая CarController, EnemyCarController
            // и CarDamageVisualController на каждом корне Prefab за один проход.
            builder.RegisterBuildCallback(container =>
            {
                if (_sceneCars == null) return;
                foreach (var go in _sceneCars)
                    if (go != null) container.InjectGameObject(go);
            });

            // ВРЕМЕННЫЙ триггер спавна для шага 5a.
            // На шаге 5e спавн переедет в DerbyMatchService (там же будет логика матча).
            // Арена использует фабрику (_sceneCars пуст), _TestDriving — старый _sceneCars путь;
            // пути взаимоисключающие по сцене.
            builder.RegisterBuildCallback(container =>
            {
                if (_derbyMatchConfig != null
                    && _spawnPoints != null
                    && _spawnPoints.Length > 0)
                {
                    var cars = container.Resolve<ICarFactory>().SpawnMatch(_derbyMatchConfig, _spawnPoints);

                    // Привязываем chase-камеру к машине игрока (index 0), заспавненной в рантайме.
                    if (_chaseCamera != null && cars != null && cars.Count > 0 && cars[0] != null)
                        _chaseCamera.SetTarget(cars[0].transform);
                }
            });
        }
    }
}
