using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlatOutLike.Core.Events
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Publish<T>(in T evt) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var del))
                return;

            // Copy delegate to local to avoid issues if subscribe/unsubscribe happens during publish
            var localDel = del;
            var invocationList = localDel.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<T>)invocationList[i]).Invoke(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
                _handlers[type] = Delegate.Combine(existing, handler);
            else
                _handlers[type] = handler;
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var existing))
                return;

            var updated = Delegate.Remove(existing, handler);
            if (updated == null)
                _handlers.Remove(type);
            else
                _handlers[type] = updated;
        }
    }
}
