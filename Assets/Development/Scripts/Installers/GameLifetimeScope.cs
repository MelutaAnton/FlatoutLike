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
        /// Root GameObjects of all cars in the scene (player + enemies).
        /// Populated by the "Fill GameLifetimeScope Refs" editor script.
        /// VContainer injects all components (CarController, EnemyCarController,
        /// CarDamageVisualController) via InjectGameObject in the build callback.
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

            // Combat (Step 3)
            builder.RegisterInstance(_damageConfig);
            builder.Register<DamageService>(Lifetime.Singleton).As<IDamageService>();

            // VFX (Step 3)
            builder.RegisterInstance(_vfxConfig);
            builder.Register<VfxFactory>(Lifetime.Singleton).As<IVfxFactory>();
            builder.Register<VfxService>(Lifetime.Singleton).As<IVfxService>();

            // AI (Step 4)
            builder.Register<EnemyAIService>(Lifetime.Singleton).As<IEnemyAIService>();

            // Eager-init DamageService so its ctor subscribes to CarCollisionEvent
            // even when no explicit consumer requests it from the container.
            builder.RegisterBuildCallback(c => c.Resolve<IDamageService>());

            // Inject all scene cars (player + enemies).
            // Replaces RegisterComponentInHierarchy<CarController> and
            // RegisterComponentInHierarchy<CarDamageVisualController> — those APIs
            // only find one component per type in the hierarchy. Here we inject every
            // GameObject in _sceneCars, covering CarController, EnemyCarController and
            // CarDamageVisualController on each prefab root in one pass.
            builder.RegisterBuildCallback(container =>
            {
                if (_sceneCars == null) return;
                foreach (var go in _sceneCars)
                    if (go != null) container.InjectGameObject(go);
            });
        }
    }
}
