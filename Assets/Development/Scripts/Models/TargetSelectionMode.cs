namespace FlatOutLike.Models
{
    /// <summary>
    /// Режим выбора цели для AI-врага. Порядок enum-значений — контракт:
    /// Nearest=0 — дефолт для Aggressor; изменение порядка ломает сохранённые инстансы SO.
    /// </summary>
    public enum TargetSelectionMode
    {
        /// <summary>Ближайший живой противник (дефолт для агрессивных профилей).</summary>
        Nearest = 0,

        /// <summary>Противник с наименьшим соотношением HP/MaxArmor (самый повреждённый).</summary>
        Weakest = 1,

        /// <summary>
        /// Всегда атаковать машину игрока.
        /// Если игрок мёртв или недоступен — откат к Nearest.
        /// </summary>
        Player  = 2
    }
}
