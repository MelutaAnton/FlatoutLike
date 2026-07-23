using UnityEngine;

namespace FlatOutLike.Services.Audio
{
    /// <summary>
    /// Абстракция исполнителя звука (обёртка над AudioSource). Реализация — <c>AudioPlayerController</c>
    /// (MonoBehaviour, реальные источники) или <c>NullAudioPlayer</c> (no-op фоллбэк на сценах
    /// без прошитого контроллера). AudioClip — Unity-тип, но это данные (ассет), а не поведение,
    /// поэтому его наличие в контракте не нарушает "pure C#" сервисов.
    /// </summary>
    public interface IAudioPlayer
    {
        /// <summary>
        /// Идемпотентная настройка количества one-shot источников. Вызывается <c>AudioService</c>
        /// один раз при старте (до первого Play); если не вызвана — реализация использует дефолт.
        /// </summary>
        void Initialize(int oneShotSourcesCount);

        void PlayOneShot(AudioClip clip, float volume, float pitch);
        void StartLoop(AudioClip clip, float volume);
        void StopLoop();
        void PlayMusic(AudioClip clip, float volume, float fadeSeconds);
        void StopMusic(float fadeSeconds);
        void SetPaused(bool paused);
    }
}
