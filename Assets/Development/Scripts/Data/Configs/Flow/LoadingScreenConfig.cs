using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Flow
{
    /// <summary>
    /// Настройки экрана загрузки межсценового перехода (Шаг 11d): длительности проявления/
    /// растворения, фейковая кривая прогресса, минимальное время показа по местам назначения,
    /// заголовки и пул подсказок. Единственный источник этих чисел — LoadingScreenView и
    /// SceneLoaderService читают их только отсюда.
    /// </summary>
    [CreateAssetMenu(fileName = "LoadingScreenConfig", menuName = "FlatOutLike/Configs/Loading Screen Config")]
    public sealed class LoadingScreenConfig : ScriptableObject
    {
        [Header("Анимация экрана")]
        [Tooltip("Проявление экрана загрузки поверх старой сцены, сек.")]
        [SerializeField] private float _showSeconds = 0.2f;
        [Tooltip("Растворение экрана загрузки в новой сцене, сек.")]
        [SerializeField] private float _hideSeconds = 0.35f;
        [Tooltip("Гашение подсказки, когда прогресс уходит на 100%, сек.")]
        [SerializeField] private float _hintFadeSeconds = 0.15f;

        [Header("Фейковая кривая прогресса")]
        [Tooltip("До этого значения прогресс растёт ease-out за _fakeSeconds.")]
        [SerializeField] private float _fakeTarget = 0.75f;
        [SerializeField] private float _fakeSeconds = 2f;
        [Tooltip("Если сцена ещё не готова — медленный рост от _fakeTarget до этого значения (выше не поднимается).")]
        [SerializeField] private float _creepTarget = 0.9f;
        [SerializeField] private float _creepSeconds = 6f;
        [Tooltip("Рывок от текущего значения до 100%, когда сцена готова и минимум показа выдержан, сек.")]
        [SerializeField] private float _finishSeconds = 0.2f;

        [Header("Места назначения")]
        [Tooltip("Минимальное время показа, если места назначения нет в списке, сек.")]
        [SerializeField] private float _defaultMinSeconds = 0.8f;
        [SerializeField] private List<LoadingDestinationEntry> _destinations = new();

        [Header("Подсказки")]
        [SerializeField] private string[] _hints = new string[0];

        public float ShowSeconds => _showSeconds;
        public float HideSeconds => _hideSeconds;
        public float HintFadeSeconds => _hintFadeSeconds;
        public float FakeTarget => _fakeTarget;
        public float FakeSeconds => _fakeSeconds;
        public float CreepTarget => _creepTarget;
        public float CreepSeconds => _creepSeconds;
        public float FinishSeconds => _finishSeconds;
        public float DefaultMinSeconds => _defaultMinSeconds;

        /// <summary>Пул подсказок; пустой список — подсказка не показывается.</summary>
        public IReadOnlyList<string> Hints => _hints ?? Array.Empty<string>();

        /// <summary>
        /// Запись места назначения для состояния. <c>false</c>, если состояние не сконфигурировано —
        /// вызывающая сторона берёт пустой заголовок и <see cref="DefaultMinSeconds"/>.
        /// </summary>
        public bool TryGetDestination(GameState state, out LoadingDestinationEntry entry)
        {
            if (_destinations != null)
            {
                for (int i = 0; i < _destinations.Count; i++)
                {
                    var candidate = _destinations[i];
                    if (candidate != null && candidate.State == state)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Место назначения перехода: заголовок экрана загрузки и минимальное время показа
        /// (короткие переходы не мелькают, «тяжёлые» сцены показывают экран дольше).
        /// </summary>
        [Serializable]
        public sealed class LoadingDestinationEntry
        {
            public GameState State;
            public string Title;
            public float MinSeconds;
        }
    }
}
