using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlatOutLike.Core.StateMachine
{
    public sealed class StateMachine<TEnum> where TEnum : struct, Enum
    {
        private readonly IReadOnlyDictionary<TEnum, IState> _states;
        private readonly Stack<TEnum> _stack = new Stack<TEnum>();

        public TEnum Current => _stack.Peek();

        public StateMachine(IReadOnlyDictionary<TEnum, IState> states, TEnum initial)
        {
            // Validate that all enum values are covered
            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                if (!states.ContainsKey(value))
                    throw new ArgumentException(
                        $"StateMachine<{typeof(TEnum).Name}>: missing state for value '{value}'.");
            }

            _states = states;
            _stack.Push(initial);
            _states[initial].Enter();
        }

        /// <summary>
        /// Exit current state, replace top of stack, Enter new state.
        /// No-op if next == Current.
        /// </summary>
        public void ChangeState(TEnum next)
        {
            if (EqualityComparer<TEnum>.Default.Equals(Current, next))
                return;

            _states[Current].Exit();
            _stack.Pop();
            _stack.Push(next);
            _states[next].Enter();
        }

        /// <summary>
        /// Push next state on top of stack. Current state is paused (no Exit called).
        /// Throws InvalidOperationException if stack is empty.
        /// </summary>
        public void Push(TEnum next)
        {
            if (_stack.Count == 0)
                throw new InvalidOperationException(
                    $"StateMachine<{typeof(TEnum).Name}>: cannot Push on an empty stack.");

            _stack.Push(next);
            _states[next].Enter();
        }

        /// <summary>
        /// Exit top state, restore previous WITHOUT calling Enter on it again.
        /// Logs error and no-ops if stack has <= 1 element.
        /// </summary>
        public void Pop()
        {
            if (_stack.Count <= 1)
            {
                Debug.LogError(
                    $"StateMachine<{typeof(TEnum).Name}>: cannot Pop — stack has {_stack.Count} element(s).");
                return;
            }

            _states[Current].Exit();
            _stack.Pop();
            // Previous state is restored without re-entering
        }

        /// <summary>
        /// Tick only the top-most state.
        /// </summary>
        public void Tick()
        {
            if (_stack.Count > 0)
                _states[Current].Tick();
        }
    }
}
