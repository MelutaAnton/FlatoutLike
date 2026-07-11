using System;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Сервис паузы (Шаг 10a). Опрашивает ввод и переключает GameFlow между
    /// геймплейным состоянием и <see cref="Models.GameState.Paused"/>.
    /// </summary>
    public interface IPauseService : IDisposable
    {
        /// <summary>Опрашивает PausePressed и переключает GameFlow. Вызывать раз в кадр из GameBootstrap.</summary>
        void Tick();
    }
}
