using FlatOutLike.Models;

namespace FlatOutLike.Services.Audio
{
    public interface IAudioService
    {
        /// <summary>
        /// Проигрывает SFX по Id (случайный вариант клипа, случайный питч, per-Id кулдаун).
        /// Молча не делает ничего, если Id не сконфигурирован, слот клипов пуст или сработал кулдаун.
        /// </summary>
        void PlaySfx(SfxId id);
    }
}
