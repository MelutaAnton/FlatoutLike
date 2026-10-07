using System;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Экран загрузки межсценового перехода (Шаг 11d, заменил чёрный фейд 11c). Scope VContainer —
    /// per-scene, поэтому переход двухфазный: старая сцена закрывается экраном (Show) → асинхронная
    /// загрузка с прогрессом (SetProgress) → новая сцена стартует уже закрытой (Cover) и открывается
    /// (Hide). Абстракция нужна, чтобы SceneLoaderService оставался тестируемым без Unity, а сцены
    /// без префаба экрана получали мгновенный NullLoadingScreen.
    /// </summary>
    public interface ILoadingScreen
    {
        /// <summary>
        /// Мгновенно: экран виден, прогресс 100%, подсказка скрыта, клики блокируются.
        /// Вызывается при старте новой сцены — стык с концом загрузки в старой сцене.
        /// </summary>
        void Cover(string title);

        /// <summary>
        /// Проявить экран (прогресс 0, подсказка видна). onShown вызывается ровно один раз —
        /// когда экран полностью закрыл сцену.
        /// </summary>
        void Show(string title, string hint, Action onShown);

        /// <summary>Отображаемый прогресс 0..1.</summary>
        void SetProgress(float progress01);

        /// <summary>
        /// Плавно погасить подсказку (на 100% — чтобы картинка в конце загрузки совпала с Cover новой сцены).
        /// </summary>
        void HideHint();

        /// <summary>Растворить экран; клики разблокируются сразу.</summary>
        void Hide();
    }
}
