using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Vfx;
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
        /// Заполняется editor-скриптом "Fill GameLifetimeScope Refs".
        /// VContainer внедряет все компоненты (CarController, EnemyCarController,
        /// CarDamageVisualController) через InjectGameObject в build callback.
        /// </summary>
        [SerializeField] private GameObject[] _sceneCars;

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

            // Eager-инициализация DamageService, чтобы его ctor подписался на CarCollisionEvent
            // даже когда явный consumer не запрашивает его из container.
            builder.RegisterBuildCallback(c => c.Resolve<IDamageService>());

            // Внедряем все машины из сцены (игрок + враги).
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
        }
    }
}
