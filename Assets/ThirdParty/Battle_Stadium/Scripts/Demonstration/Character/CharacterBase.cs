using System;
using System.Collections.Generic;
using UnityEngine;

namespace ithappy.Battle_Stadium
{
    public enum CharacterType
    {
        None = 0,
    }

    public abstract class CharacterBase : MonoBehaviour
    {
        [SerializeField] private CharacterType _characterType;
        [SerializeField] protected MovementBase _movement;
        [SerializeField] protected CharacterAnimator _characterAnimator;
        
        protected abstract Dictionary<Type, CharacterStateBase> States { get; }
        protected CharacterStateBase _currentState;

        public CharacterAnimator CharacterAnimator => _characterAnimator;
        public CharacterType CharacterType => _characterType;

        public bool IsFree { get; set; } = true;
        protected bool _isInitialized = false;

        public virtual void Initialize()
        {
            _movement.Initialize(this);
            _characterAnimator.Initialize();
        }

        public virtual void Dispose()
        {
            _currentState?.Exit();
            _movement.Dispose();
            _characterAnimator.Dispose();
            _isInitialized = false;
        }
        
        private void Update()
        {
            if (_isInitialized)
            {
                _currentState.Update();
                _movement.UpdateMovement();
            }
        }

        public void NextState()
        {
            foreach (var state in States)
            {
                if (state.Value.ShouldEnter())
                {
                    TransitionToState(state.Value);
                }
            }
        }

        public void TransitionToState(CharacterStateBase newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();
        }

        public void TransitionToState(Type stateType)
        {
            if (States.TryGetValue(stateType, out var state))
            {
                TransitionToState(state);
            }
            else
            {
                Debug.LogError($"State {stateType.Name} not found!");
            }
        }

        public void SetPriority(int priority)
        {
            _movement.Agent.avoidancePriority = priority;
        }

        protected virtual void OnValidate()
        {
            if (_movement == null)
            {
                _movement = GetComponent<MovementBase>();
            }

            if (_characterAnimator == null)
            {
                _characterAnimator = GetComponent<CharacterAnimator>();
            }
        }
    }
}
