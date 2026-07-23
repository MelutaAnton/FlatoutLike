using UnityEngine;

namespace FlatOutLike.Services.Audio
{
    /// <summary>
    /// No-op реализация <see cref="IAudioPlayer"/> — фоллбэк для сцен без прошитого
    /// <c>AudioPlayerController</c> (паттерн NullAudioPlayer/DriverEjectConfig-фоллбэков проекта).
    /// AudioService всегда получает валидный IAudioPlayer, не проверяя null на каждый вызов.
    /// </summary>
    public sealed class NullAudioPlayer : IAudioPlayer
    {
        public void Initialize(int oneShotSourcesCount) { }
        public void PlayOneShot(AudioClip clip, float volume, float pitch) { }
        public void StartLoop(AudioClip clip, float volume) { }
        public void StopLoop() { }
        public void PlayMusic(AudioClip clip, float volume, float fadeSeconds) { }
        public void StopMusic(float fadeSeconds) { }
        public void SetPaused(bool paused) { }
    }
}
