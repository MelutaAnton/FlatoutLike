namespace FlatOutLike.Services.Progression
{
    /// <summary>
    /// Хранилище save-блока хаба (прогресс стройки). Отделено от IProfileStorage: профиль
    /// игрока и прогресс базы сохраняются независимыми блоками.
    /// </summary>
    public interface IHubSaveStorage
    {
        /// <summary>Загружает сейв хаба. Сейва нет — пустые данные, не null.</summary>
        HubSaveData Load();

        void Save(HubSaveData data);
    }
}
