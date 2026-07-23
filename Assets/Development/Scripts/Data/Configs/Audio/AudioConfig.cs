using System;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Audio
{
    /// <summary>
    /// Единственный источник звуковых данных проекта: SFX-клипы (с вариантами и кулдауном),
    /// музыкальные треки и их привязка к GameState. Ассет с клипами (Universal Sound FX)
    /// импортируется отдельно — до его импорта все массивы клипов пустые, и весь звуковой
    /// стек (AudioService/AudioPlayerController) обязан молча переживать пустые слоты
    /// (не играть, без спама варнингов).
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "FlatOutLike/Audio/AudioConfig")]
    public sealed class AudioConfig : ScriptableObject
    {
        [SerializeField] private SfxEntry[] _sfxEntries;
        [SerializeField] private MusicEntry[] _musicEntries;
        [SerializeField] private StateMusicEntry[] _stateMusicEntries;

        [Header("Пороги удара (CarCollisionEvent.ImpactSpeed)")]
        [Tooltip("Ниже этой скорости удара звук столкновения не проигрывается.")]
        [SerializeField] private float _minCrashImpactSpeed = 4f;
        [Tooltip("Выше этой скорости удара играет CrashHeavy, иначе CrashLight.")]
        [SerializeField] private float _crashHeavyImpactSpeed = 12f;

        [Header("Музыка")]
        [SerializeField] private float _musicCrossfadeSeconds = 1f;

        [Header("Пул one-shot источников")]
        [SerializeField] private int _oneShotSourcesCount = 8;

        public float MinCrashImpactSpeed => _minCrashImpactSpeed;
        public float CrashHeavyImpactSpeed => _crashHeavyImpactSpeed;
        public float MusicCrossfadeSeconds => _musicCrossfadeSeconds;
        public int OneShotSourcesCount => _oneShotSourcesCount;

        /// <summary>Ищет запись SFX по id. <c>false</c>, если id не сконфигурирован — вызывающая
        /// сторона (AudioService) обязана молча пропустить проигрывание.</summary>
        public bool TryGetSfx(SfxId id, out SfxEntry entry)
        {
            if (_sfxEntries != null)
            {
                for (int i = 0; i < _sfxEntries.Length; i++)
                {
                    if (_sfxEntries[i].Id == id)
                    {
                        entry = _sfxEntries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        /// <summary>Ищет запись музыкального трека по id. <c>false</c>, если id не сконфигурирован.</summary>
        public bool TryGetMusic(MusicTrackId id, out MusicEntry entry)
        {
            if (_musicEntries != null)
            {
                for (int i = 0; i < _musicEntries.Length; i++)
                {
                    if (_musicEntries[i].Id == id)
                    {
                        entry = _musicEntries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        /// <summary>Трек для состояния игры. Нет маппинга — <see cref="MusicTrackId.None"/>.</summary>
        public MusicTrackId GetTrackForState(GameState state)
        {
            if (_stateMusicEntries != null)
            {
                for (int i = 0; i < _stateMusicEntries.Length; i++)
                {
                    if (_stateMusicEntries[i].State == state)
                        return _stateMusicEntries[i].Track;
                }
            }

            return MusicTrackId.None;
        }

        /// <summary>
        /// Запись SFX: варианты клипов (играется случайный), громкость, разброс питча,
        /// кулдаун между проигрываниями одного и того же Id. Класс (не struct) — нужны
        /// field-инициализаторы дефолтных значений для новых строк в инспекторе.
        /// </summary>
        [Serializable]
        public sealed class SfxEntry
        {
            public SfxId Id;
            public AudioClip[] Clips;
            public float Volume = 1f;
            public float PitchJitter = 0.05f;
            public float CooldownSeconds = 0.08f;
        }

        [Serializable]
        public sealed class MusicEntry
        {
            public MusicTrackId Id;
            public AudioClip Clip;
            public float Volume = 0.5f;
        }

        [Serializable]
        public sealed class StateMusicEntry
        {
            public GameState State;
            public MusicTrackId Track;
        }
    }
}
