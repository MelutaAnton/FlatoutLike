using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Factories.Vfx;
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
        [SerializeField] private DamageConfig _damageConfig;
        [SerializeField] private VfxConfig _vfxConfig;

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
            builder.RegisterComponentInHierarchy<CarController>();

            // Combat (Step 3)
            builder.RegisterInstance(_damageConfig);
            builder.Register<DamageService>(Lifetime.Singleton).As<IDamageService>();

            // VFX (Step 3)
            builder.RegisterInstance(_vfxConfig);
            builder.Register<VfxFactory>(Lifetime.Singleton).As<IVfxFactory>();
            builder.Register<VfxService>(Lifetime.Singleton).As<IVfxService>();

            // Damage visual (Step 3)
            builder.RegisterComponentInHierarchy<CarDamageVisualController>();

            // Eager-init DamageService so its ctor subscribes to CarCollisionEvent
            // even when no explicit consumer requests it from the container.
            builder.RegisterBuildCallback(c => c.Resolve<IDamageService>());
        }
    }
}
