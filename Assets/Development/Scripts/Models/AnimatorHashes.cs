using UnityEngine;

namespace FlatOutLike.Models
{
    /// <summary>
    /// Единственный источник имён Animator-параметров и их хэшей. Строковое имя публично,
    /// потому что editor-скрипты создают параметр контроллера по нему же — второго
    /// литерала с тем же именем в проекте быть не должно.
    /// </summary>
    public static class AnimatorHashes
    {
        /// <summary>Нормализованная скорость персонажа BaseHub (0 = стоит, 1 = бежит), float.</summary>
        public const string HubSpeedParam = "Speed";

        public static readonly int HubSpeed = Animator.StringToHash(HubSpeedParam);
    }
}
