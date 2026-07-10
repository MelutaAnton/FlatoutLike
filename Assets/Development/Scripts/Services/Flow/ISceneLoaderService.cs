namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Обёртка над SceneManager. Абстракция нужна, чтобы состояния FSM (SceneEntryState)
    /// оставались тестируемыми без реальной загрузки сцен — тесты подставляют фейк.
    /// </summary>
    public interface ISceneLoaderService
    {
        /// <summary>
        /// Имя активной сцены (SceneManager.GetActiveScene().name).
        /// </summary>
        string ActiveSceneName { get; }

        /// <summary>
        /// Загружает сцену в режиме Single. No-op, если sceneKey уже активна.
        /// </summary>
        void Load(string sceneKey);
    }
}
