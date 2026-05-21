using System;

namespace FlatOutLike.Core.Events
{
    public interface IEventBus
    {
        void Publish<T>(in T evt) where T : struct;
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
    }
}
