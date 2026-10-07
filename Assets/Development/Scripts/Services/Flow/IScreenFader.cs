namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Затемнение экрана при межсценовом переходе. Scope VContainer — per-scene, поэтому фейд
    /// двухфазный: старая сцена затемняется (FadeOut) → загрузка новой; новая сцена стартует
    /// уже чёрной и проявляется (FadeIn). Абстракция нужна, чтобы SceneLoaderService оставался
    /// тестируемым без Unity, а сцены без префаба фейда получали мгновенный NullScreenFader.
    /// </summary>
    public interface IScreenFader
    {
        /// <summary>
        /// Затемнить экран. onComplete вызывается ровно один раз — когда экран полностью чёрный.
        /// </summary>
        void FadeOut(System.Action onComplete);

        /// <summary>
        /// Проявить сцену из чёрного.
        /// </summary>
        void FadeIn();
    }
}
