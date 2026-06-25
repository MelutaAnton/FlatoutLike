using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Services.Car;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Meta;
using VContainer;
using VContainer.Unity;

namespace FlatOutLike.Installers
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();
            builder.Register<KeyboardInputProvider>(Lifetime.Singleton).As<IInputProvider>();
            builder.Register<InputRouter>(Lifetime.Singleton);
            builder.Register<PlayerPrefsProfileStorage>(Lifetime.Singleton).As<IProfileStorage>();
            builder.Register<MetaProgressService>(Lifetime.Singleton).As<IMetaProgressService>();
            builder.Register<IPlayerProfile>(c => c.Resolve<IMetaProgressService>().Profile, Lifetime.Singleton);
            builder.Register<GameFlowService>(Lifetime.Singleton).As<IGameFlowService>();
            builder.RegisterComponentInHierarchy<GameBootstrap>();
            builder.Register<CarMovementService>(Lifetime.Singleton).As<ICarMovementService>();
            builder.RegisterComponentInHierarchy<CarController>();
        }
    }
}
