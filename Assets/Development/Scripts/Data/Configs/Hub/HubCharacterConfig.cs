using UnityEngine;

namespace FlatOutLike.Data.Configs.Hub
{
    /// <summary>
    /// Параметры персонажа BaseHub (game-design §6): бег по базе через CharacterController
    /// и потоковое вложение монет в стройку, пока персонаж стоит в зоне здания (12a).
    /// Поля стопки монет появятся в шаге 13.
    /// </summary>
    [CreateAssetMenu(fileName = "HubCharacterConfig", menuName = "FlatOutLike/Hub/HubCharacterConfig")]
    public sealed class HubCharacterConfig : ScriptableObject
    {
        [Tooltip("Скорость бега, м/с.")]
        [SerializeField] private float _moveSpeed = 5f;

        [Tooltip("Скорость доворота к направлению движения, град/с.")]
        [SerializeField] private float _rotationSpeed = 720f;

        [Tooltip("Ускорение свободного падения персонажа, м/с² (отрицательное — вниз).")]
        [SerializeField] private float _gravity = -20f;

        [Tooltip("Время сглаживания параметра скорости в Animator, с.")]
        [SerializeField] private float _animatorDampTime = 0.1f;

        [Tooltip("Скорость потока вложения в стройку, монет/с (персонаж стоит в зоне недостроенного здания).")]
        [SerializeField] private float _investStreamRate = 25f;

        [Tooltip("Период списания пачки монет, с. Накопленный поток списывается целыми монетами раз " +
                 "в этот период. ≤ 0 — пачка на каждом кадре.")]
        [SerializeField] private float _investBatchSeconds = 0.25f;

        public float MoveSpeed => _moveSpeed;
        public float RotationSpeed => _rotationSpeed;
        public float Gravity => _gravity;
        public float AnimatorDampTime => _animatorDampTime;
        public float InvestStreamRate => _investStreamRate;
        public float InvestBatchSeconds => _investBatchSeconds;
    }
}
