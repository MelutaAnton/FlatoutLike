using FlatOutLike.Core.Input;
using FlatOutLike.Data.Configs.Hub;
using FlatOutLike.Models;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Hub
{
    /// <summary>
    /// Персонаж BaseHub (game-design §6): бег по базе через CharacterController + анимация
    /// бега по нормализованной скорости. Только Unity API и чтение конфига: ввод берётся из
    /// IInputProvider (в контексте Hub Move — полный вектор), бизнес-логики здесь нет.
    /// Камера хаба не вращается по yaw, поэтому ввод маппится прямо в мировые оси (x→X, y→Z).
    /// </summary>
    public sealed class HubCharacterController : MonoBehaviour
    {
        // Небольшая прижимающая скорость на земле: держит isGrounded стабильным на неровностях
        // и спусках (при нулевой вертикальной скорости CharacterController «отлипает» от пола).
        private const float GroundedStickVelocity = -2f;

        // Порог квадрата длины ввода, ниже которого персонаж не доворачивается (мёртвая зона).
        private const float RotationInputThresholdSqr = 0.0001f;

        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Animator _animator;

        private IInputProvider _input;
        private HubCharacterConfig _config;
        private bool _initialized;

        private float _verticalVelocity;

        [Inject]
        public void Construct(IInputProvider input, HubCharacterConfig config)
        {
            _input = input;
            _config = config;
            _initialized = true;
        }

        private void Update()
        {
            // OnEnable/Update могут отработать раньше Construct() (VContainer) — до инъекции молчим.
            if (!_initialized) return;
            if (_characterController == null) return;

            float dt = Time.deltaTime;

            // Диагональ клавиатуры даёт длину √2 — ограничиваем до 1, стик и так в пределах единицы.
            Vector2 move = Vector2.ClampMagnitude(_input.Move, 1f);
            Vector3 direction = new Vector3(move.x, 0f, move.y);

            if (_characterController.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = GroundedStickVelocity;
            _verticalVelocity += _config.Gravity * dt;

            Vector3 velocity = direction * _config.MoveSpeed;
            velocity.y = _verticalVelocity;
            _characterController.Move(velocity * dt);

            if (direction.sqrMagnitude > RotationInputThresholdSqr)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, _config.RotationSpeed * dt);
            }

            UpdateAnimator(dt);
        }

        private void UpdateAnimator(float dt)
        {
            if (_animator == null) return;

            // Фактическая планарная скорость (после столкновений): упёрся в стену — анимация
            // бега затухает, а не «буксует» на месте.
            Vector3 actual = _characterController.velocity;
            float planarSpeed = new Vector2(actual.x, actual.z).magnitude;
            float normalized = _config.MoveSpeed > 0f
                ? Mathf.Clamp01(planarSpeed / _config.MoveSpeed)
                : 0f;

            _animator.SetFloat(AnimatorHashes.HubSpeed, normalized, _config.AnimatorDampTime, dt);
        }
    }
}
