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
        /// true с момента принятого Load до выгрузки текущей сцены (включая проявление экрана
        /// загрузки и асинхронную загрузку). Пока true, GameFlowService отбрасывает новые
        /// переходы, а пауза и музыка состояния не переключаются.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Асинхронно загружает сцену в режиме Single через экран загрузки (проявление экрана →
        /// LoadSceneAsync с прогрессом). No-op, если sceneKey уже активна; повторный вызов во время
        /// уже идущей загрузки игнорируется.
        /// </summary>
        void Load(string sceneKey);
    }
}
