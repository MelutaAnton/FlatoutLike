using System;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// No-op реализация IScreenFader для сцен без префаба фейда (Boot, тестовые сцены):
    /// переход мгновенный — FadeOut сразу сообщает о «чёрном» экране, FadeIn ничего не делает.
    /// </summary>
    public sealed class NullScreenFader : IScreenFader
    {
        public void FadeOut(Action onComplete)
        {
            // Делегат — не Unity-объект, обычная null-проверка безопасна.
            if (onComplete != null)
                onComplete();
        }

        public void FadeIn() { }
    }
}
