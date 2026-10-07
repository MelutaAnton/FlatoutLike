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
        /// Асинхронно загружает сцену в режиме Single через фейд (затемнение → LoadSceneAsync).
        /// No-op, если sceneKey уже активна; повторный вызов во время уже идущей загрузки
        /// игнорируется.
        /// </summary>
        void Load(string sceneKey);
    }
}
