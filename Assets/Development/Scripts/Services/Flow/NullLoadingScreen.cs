using System;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// No-op реализация ILoadingScreen для сцен без префаба экрана загрузки (тестовые сцены):
    /// переход мгновенный — Show сразу сообщает, что сцена «закрыта», остальное ничего не делает.
    /// </summary>
    public sealed class NullLoadingScreen : ILoadingScreen
    {
        public void Cover(string title) { }

        public void Show(string title, string hint, Action onShown)
        {
            // Делегат — не Unity-объект, обычная null-проверка безопасна.
            if (onShown != null)
                onShown();
        }

        public void SetProgress(float progress01) { }

        public void HideHint() { }

        public void Hide() { }
    }
}
