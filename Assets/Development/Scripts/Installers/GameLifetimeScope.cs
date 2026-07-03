using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Destructible;
using FlatOutLike.Controllers.Stunts;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Core.Pool;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Data.Configs.Pickups;
using FlatOutLike.Data.Configs.Rewards;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Factories.Car;
using FlatOutLike.Factories.Destructible;
using FlatOutLike.Factories.Pickup;
using FlatOutLike.Factories.Vfx;
using FlatOutLike.Services.AI;
using FlatOutLike.Services.Camera;
using FlatOutLike.Services.Car;
using FlatOutLike.Services.Combat;
using FlatOutLike.Services.Driver;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Match;
using FlatOutLike.Services.Meta;
using FlatOutLike.Services.Pickups;
using FlatOutLike.Services.Stunts;
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
        /// Конфиг наград за Derby-матч (монеты за килл/победу).
        /// Заполняется на арене вместе с _derbyMatchConfig; в _TestDriving оставить null.
        /// </summary>
        [SerializeField] private RewardConfig _rewardConfig;

        /// <summary>
        /// Точки спавна машин для Derby-матча. Index 0 = игрок, 1.. = враги.
        /// Создаются editor-скриптом на арене.
        /// </summary>
        [SerializeField] private Transform[] _spawnPoints;

        // ── Шаг 6: Eject + камера водителя ──────────────────────────────────────
        /// <summary>
        /// Camera rig арены — переключатель между chase-камерой (следование за машиной)
        /// и orbit-камерой (вокруг катапультированного водителя). Заполняется editor-скриптом;
        /// в тест-сценах без камеры оставить null.
        /// </summary>
        [SerializeField] private CameraRig _cameraRig;

        /// <summary>
        /// Параметры импульса катапультирования водителя. В тест-сценах без ragdoll — null:
        /// тогда в контейнер регистрируется fallback-инстанс (см. Configure) — VContainer
        /// не умеет опциональные параметры инъекции.
        /// </summary>
        [SerializeField] private DriverEjectConfig _driverEjectConfig;

        /// <summary>
        /// Параметры орбитальной камеры вокруг катапультированного водителя.
        /// В тест-сценах без камеры оставить null.
        /// </summary>
        [SerializeField] private OrbitCameraConfig _orbitCameraConfig;

        // ── Шаг 5c: Pickup-система ─────────────────────────────────────────────
        /// <summary>
        /// Конфиг пикапов: типы, prefab'ы пула, размеры пула, тайминги.
        /// Заполняется editor-скриптом; на сценах без пикапов оставить null.
        /// </summary>
        [SerializeField] private PickupConfig _pickupConfig;

        /// <summary>
        /// Зоны спавна пикапов на арене (Transform-точка + тип пикапа).
        /// Создаются editor-скриптом; порядок не важен.
        /// </summary>
        [SerializeField] private PickupZone[] _pickupZones;

        // ── Шаг 7: Stunts High Jump ──────────────────────────────────────────────
        /// <summary>
        /// Параметры управления водителем в полёте (steer-коррекция, буст). Регистрируется
        /// БЕЗУСЛОВНО (fallback-инстанс, паттерн DriverEjectConfig) — DriverFlightController
        /// живёт на префабе водителя и инжектится даже в Derby-сценах без трамплина.
        /// </summary>
        [SerializeField] private DriverFlightConfig _driverFlightConfig;

        /// <summary>
        /// Правила подсчёта очков «High Jump». Заполняется на сцене трамплина;
        /// на сценах без него оставить null — сессия и скоринг не регистрируются.
        /// </summary>
        [SerializeField] private HighJumpRulesConfig _highJumpRulesConfig;

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

            // Награды (Шаг 5e) — зачисляет RewardEarnedEvent в профиль игрока.
            // Регистрируется безусловно (не зависит от _derbyMatchConfig/_rewardConfig),
            // т.к. награды может публиковать любой источник (Derby, HighJump, миссии).
            builder.Register<RewardCrediterService>(Lifetime.Singleton);

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

            // Match (Шаг 5b) — реестр машин и выбор цели
            builder.Register<CarRegistry>(Lifetime.Singleton).As<ICarRegistry>();
            builder.Register<TargetSelectionService>(Lifetime.Singleton).As<ITargetSelectionService>();

            // Nitro (Шаг 5c)
            builder.Register<NitroService>(Lifetime.Singleton).As<INitroService>();

            // Pickup-система (Шаг 5c).
            // Все три регистрации — условные: если _pickupConfig не задан (тест-сцены без пикапов),
            // VContainer не попытается создать PickupFactory/PickupSpawnerService и не упадёт.
            if (_pickupConfig != null)
            {
                builder.RegisterInstance(_pickupConfig);
                builder.Register<PickupFactory>(Lifetime.Singleton).As<IPickupFactory>();
                // EntryPoint → VContainer вызывает ITickable.Tick через PlayerLoop-диспетчер автоматически.
                builder.RegisterEntryPoint<PickupSpawnerService>().As<IPickupSpawnerService>();
            }

            // Eject + камера водителя (Шаг 6).
            // DriverEjectConfig регистрируется БЕЗУСЛОВНО: VContainer игнорирует C#-значения
            // по умолчанию в [Inject] Construct и бросает VContainerException на незарегистрированный
            // тип — условная регистрация роняла бы InjectGameObject любой машины в тест-сценах.
            // С fallback-инстансом eject там всё равно не срабатывает (_driver == null у машин).
            builder.RegisterInstance(_driverEjectConfig != null
                ? _driverEjectConfig
                : ScriptableObject.CreateInstance<DriverEjectConfig>());

            // Камерные регистрации — условные (паттерн pickup-конфига): тест-сцены без камеры
            // не должны падать при резолве.

            if (_orbitCameraConfig != null)
                builder.RegisterInstance(_orbitCameraConfig);

            if (_cameraRig != null)
            {
                builder.RegisterComponent<ICameraRig>(_cameraRig);
                builder.Register<CameraService>(Lifetime.Singleton).As<ICameraService>();
            }

            // Полёт водителя (Шаг 7). DriverFlightConfig — БЕЗУСЛОВНО, тот же резон, что и
            // DriverEjectConfig: DriverFlightController сидит на префабе водителя и инжектится
            // во всех сценах, включая Derby, где трамплина и сессии нет.
            builder.RegisterInstance(_driverFlightConfig != null
                ? _driverFlightConfig
                : ScriptableObject.CreateInstance<DriverFlightConfig>());

            builder.Register<FlipDetectorService>(Lifetime.Singleton).As<IFlipDetectorService>();
            builder.Register<DriverFlightService>(Lifetime.Singleton).As<IDriverFlightService>();

            // Скоринг и сессия «High Jump» — условно: только на сценах с трамплином.
            if (_highJumpRulesConfig != null)
            {
                builder.RegisterInstance(_highJumpRulesConfig);
                builder.Register<HighJumpScoreService>(Lifetime.Singleton).As<IHighJumpScoreService>();
                // EntryPoint → VContainer вызывает ITickable.Tick через PlayerLoop-диспетчер автоматически.
                builder.RegisterEntryPoint<HighJumpSessionService>().As<IHighJumpSessionService>();
            }

            // Pool (Шаг 5a) — единственный источник пулов для всех фабрик
            builder.Register<PoolManager>(Lifetime.Singleton).As<IPoolManager>();

            // Car factory (Шаг 5a)
            builder.Register<CarFactory>(Lifetime.Singleton).As<ICarFactory>();

            // Разрушаемые объекты (Шаг 5d)
            builder.Register<DestructibleFactory>(Lifetime.Singleton).As<IDestructibleFactory>();

            // Derby match config (Шаг 5a); SO — RegisterInstance корректно, инъекция не нужна
            if (_derbyMatchConfig != null)
            {
                builder.RegisterInstance(_derbyMatchConfig);

                // DerbyMatchService (Шаг 5e) — требует ещё и RewardConfig, поэтому условие двойное.
                if (_rewardConfig != null)
                {
                    builder.RegisterInstance(_rewardConfig);
                    builder.Register<DerbyMatchService>(Lifetime.Singleton).As<IDerbyMatchService>();
                }
            }

            // Eager-инициализация DamageService, чтобы его ctor подписался на CarCollisionEvent
            // даже когда явный consumer не запрашивает его из container.
            builder.RegisterBuildCallback(c => c.Resolve<IDamageService>());

            // Eager-инициализация RewardCrediterService, чтобы он подписался на RewardEarnedEvent
            // до того как матч успеет завершиться и опубликовать награду.
            builder.RegisterBuildCallback(c => c.Resolve<RewardCrediterService>());

            // Путь _TestDriving: внедряем машины, расставленные вручную в сцене.
            // Заменяет RegisterComponentInHierarchy<CarController> и
            // RegisterComponentInHierarchy<CarDamageVisualController> — эти API
            // находят только один компонент каждого типа в иерархии. Здесь мы внедряем
            // каждый GameObject из _sceneCars, покрывая CarController, EnemyCarController
            // и CarDamageVisualController на каждом корне Prefab за один проход.
            builder.RegisterBuildCallback(container =>
            {
                // _TestDriving-путь: внедряем машины, расставленные вручную в сцене,
                // и регистрируем их в ICarRegistry (первая = игрок). Без регистрации
                // CameraService не узнаёт игрока и не переключает камеру на ragdoll при eject.
                if (_sceneCars != null && _sceneCars.Length > 0)
                {
                    var registry = container.Resolve<ICarRegistry>();
                    for (int i = 0; i < _sceneCars.Length; i++)
                    {
                        var go = _sceneCars[i];
                        if (go == null) continue;

                        container.InjectGameObject(go);

                        var sceneCar = go.GetComponent<CarController>();
                        if (sceneCar != null)
                            registry.Register(sceneCar, isPlayer: i == 0);
                    }
                }

                // Шаг 5d: инъекция разрушаемых объектов (заборов и т.п.), расставленных на сцене.
                // Безусловно — не зависит от _sceneCars. Если заборов нет — цикл ничего не делает.
                foreach (var d in Object.FindObjectsByType<DestructibleObjectController>(FindObjectsSortMode.None))
                    container.InjectGameObject(d.gameObject);

                // Шаг 7: инъекция триггеров и debug-оверлея трамплина, расставленных на сцене.
                // Безусловно (паттерн DestructibleObjectController выше) — если объектов нет, цикл пуст.
                foreach (var eject in Object.FindObjectsByType<EjectTriggerZone>(FindObjectsSortMode.None))
                    container.InjectGameObject(eject.gameObject);
                foreach (var landing in Object.FindObjectsByType<LandingZone>(FindObjectsSortMode.None))
                    container.InjectGameObject(landing.gameObject);
                foreach (var overlay in Object.FindObjectsByType<HighJumpDebugOverlayController>(FindObjectsSortMode.None))
                    container.InjectGameObject(overlay.gameObject);

                // _TestDriving-путь: chase-камера на первую сцено-машину (арена привязывает камеру
                // отдельно, в build-callback ниже, после спавна через DerbyMatchService).
                if (_cameraRig != null && _sceneCars != null && _sceneCars.Length > 0 && _sceneCars[0] != null)
                {
                    var firstCar = _sceneCars[0].GetComponent<CarController>();
                    if (firstCar != null)
                        container.Resolve<ICameraService>().FollowPlayerCar(firstCar.transform);
                }

                // Шаг 7: запускаем сессию «High Jump», если сцена трамплина сконфигурирована.
                if (_highJumpRulesConfig != null)
                    container.Resolve<IHighJumpSessionService>().StartSession();
            });

            // Триггер старта Derby-матча (Шаг 5e). Спавн и логика матча теперь в DerbyMatchService;
            // здесь только запуск и привязка camera-таргета к заспавненной машине игрока.
            // Арена использует фабрику через DerbyMatchService (_sceneCars пуст),
            // _TestDriving — старый _sceneCars путь; пути взаимоисключающие по сцене.
            builder.RegisterBuildCallback(container =>
            {
                if (_derbyMatchConfig != null
                    && _rewardConfig != null
                    && _spawnPoints != null
                    && _spawnPoints.Length > 0)
                {
                    var cars = container.Resolve<IDerbyMatchService>().StartMatch(_spawnPoints);

                    // Привязываем chase-камеру к машине игрока (index 0), заспавненной в рантайме.
                    // ICameraService зарегистрирован только если _cameraRig задан (условная регистрация выше).
                    if (_cameraRig != null && cars != null && cars.Count > 0 && cars[0] != null)
                        container.Resolve<ICameraService>().FollowPlayerCar(cars[0].transform);
                }
                else if (_derbyMatchConfig != null && _rewardConfig == null)
                {
                    // Диагностика: арена настроена, но reward-конфиг не проставлен — иначе спавн молча не произойдёт.
                    Debug.LogWarning("[GameLifetimeScope] _derbyMatchConfig задан, но _rewardConfig == null — " +
                                     "Derby-матч не запустится. Прогони FlatOutLike/Configs/Create Default RewardConfig.");
                }

                // Шаг 5c: запускаем спавнер пикапов только если конфиг и зоны настроены.
                if (_pickupConfig != null && _pickupZones != null && _pickupZones.Length > 0)
                    container.Resolve<IPickupSpawnerService>().StartSpawning(_pickupZones);
            });
        }
    }
}
