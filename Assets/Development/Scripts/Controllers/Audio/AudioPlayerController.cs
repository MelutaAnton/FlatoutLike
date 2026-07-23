using System;
using System.Collections;
using FlatOutLike.Services.Audio;
using UnityEngine;

namespace FlatOutLike.Controllers.Audio
{
    /// <summary>
    /// Реальный исполнитель звука: дочерние AudioSource'ы под этим GameObject.
    /// Только Unity API — реализует <see cref="IAudioPlayer"/>, инъекция не нужна (зависимостей нет).
    /// Round-robin пул one-shot источников (кол-во задаёт AudioConfig через Initialize),
    /// один луп-канал (нитро), два музыкальных канала для кроссфейда.
    /// </summary>
    public sealed class AudioPlayerController : MonoBehaviour, IAudioPlayer
    {
        private const int DefaultOneShotCount = 8;

        private AudioSource[] _oneShotSources;
        private int _oneShotIndex;

        private AudioSource _loopSource;
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _activeMusicSource;

        private Coroutine _musicFadeCoroutine;

        private void Awake()
        {
            // Безопасный дефолт, если Initialize по какой-то причине не будет вызван сервисом
            // до первого Play — сами источники уже готовы.
            EnsureLoopAndMusicSources();
            EnsureOneShotSources(DefaultOneShotCount);
        }

        /// <summary>Идемпотентно: повторный вызов с тем же количеством ничего не пересоздаёт.</summary>
        public void Initialize(int oneShotSourcesCount)
        {
            EnsureLoopAndMusicSources();
            EnsureOneShotSources(oneShotSourcesCount > 0 ? oneShotSourcesCount : DefaultOneShotCount);
        }

        public void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            if (_oneShotSources == null || _oneShotSources.Length == 0) return;

            var source = _oneShotSources[_oneShotIndex];
            _oneShotIndex = (_oneShotIndex + 1) % _oneShotSources.Length;

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        public void StartLoop(AudioClip clip, float volume)
        {
            if (clip == null || _loopSource == null) return;

            _loopSource.clip = clip;
            _loopSource.volume = volume;
            _loopSource.loop = true;
            _loopSource.Play();
        }

        public void StopLoop()
        {
            if (_loopSource == null) return;
            _loopSource.Stop();
        }

        public void PlayMusic(AudioClip clip, float volume, float fadeSeconds)
        {
            if (clip == null) return;
            EnsureLoopAndMusicSources();

            var next = _activeMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA;

            if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
            _musicFadeCoroutine = StartCoroutine(CrossfadeMusic(next, clip, volume, fadeSeconds));
        }

        public void StopMusic(float fadeSeconds)
        {
            if (_activeMusicSource == null) return;

            if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
            _musicFadeCoroutine = StartCoroutine(FadeOutAndStop(_activeMusicSource, fadeSeconds));
            _activeMusicSource = null;
        }

        public void SetPaused(bool paused)
        {
            // Заморозка ВСЕГО звука на паузе — осознанное решение (timeScale сам звук не стопит).
            AudioListener.pause = paused;
        }

        // ── Кроссфейд музыки (unscaled time — должен работать и на паузе) ────────
        private IEnumerator CrossfadeMusic(AudioSource next, AudioClip clip, float targetVolume, float fadeSeconds)
        {
            var previous = _activeMusicSource;

            next.clip = clip;
            next.loop = true;
            next.volume = fadeSeconds > 0f ? 0f : targetVolume;
            next.Play();
            _activeMusicSource = next;

            if (fadeSeconds > 0f)
            {
                float previousStartVolume = previous != null ? previous.volume : 0f;
                float t = 0f;

                while (t < fadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    float f = Mathf.Clamp01(t / fadeSeconds);
                    next.volume = Mathf.Lerp(0f, targetVolume, f);
                    if (previous != null) previous.volume = Mathf.Lerp(previousStartVolume, 0f, f);
                    yield return null;
                }

                next.volume = targetVolume;
            }

            if (previous != null && previous != next)
                previous.Stop();

            _musicFadeCoroutine = null;
        }

        private IEnumerator FadeOutAndStop(AudioSource source, float fadeSeconds)
        {
            float startVolume = source.volume;

            if (fadeSeconds > 0f)
            {
                float t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(t / fadeSeconds));
                    yield return null;
                }
            }

            source.Stop();
            source.volume = startVolume; // следующий Play() выставит громкость сам

            _musicFadeCoroutine = null;
        }

        // ── Создание источников ──────────────────────────────────────────────
        private void EnsureLoopAndMusicSources()
        {
            if (_loopSource == null) _loopSource = CreateChildSource("LoopChannel");
            if (_musicSourceA == null) _musicSourceA = CreateChildSource("MusicChannelA");
            if (_musicSourceB == null) _musicSourceB = CreateChildSource("MusicChannelB");
        }

        private void EnsureOneShotSources(int count)
        {
            if (_oneShotSources != null && _oneShotSources.Length == count) return; // уже нужное количество

            if (_oneShotSources == null)
                _oneShotSources = Array.Empty<AudioSource>();

            if (_oneShotSources.Length < count)
            {
                var extended = new AudioSource[count];
                Array.Copy(_oneShotSources, extended, _oneShotSources.Length);
                for (int i = _oneShotSources.Length; i < count; i++)
                    extended[i] = CreateChildSource($"OneShot_{i}");
                _oneShotSources = extended;
            }
            else
            {
                // count меньше текущего — уничтожаем лишние дочерние источники.
                for (int i = count; i < _oneShotSources.Length; i++)
                {
                    if (_oneShotSources[i] != null)
                        Destroy(_oneShotSources[i].gameObject);
                }

                var trimmed = new AudioSource[count];
                Array.Copy(_oneShotSources, trimmed, count);
                _oneShotSources = trimmed;
            }
        }

        private AudioSource CreateChildSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D — все звуки проекта не позиционные
            source.loop = false;
            return source;
        }
    }
}
