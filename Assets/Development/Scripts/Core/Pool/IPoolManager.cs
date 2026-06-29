using UnityEngine;

namespace FlatOutLike.Core.Pool
{
    /// <summary>
    /// Prefab-keyed пул игровых объектов. Ключ — ссылка на prefab (без magic-строк).
    /// Инъекции зависимостей и вызов Launch — ответственность вызывающей фабрики.
    /// </summary>
    public interface IPoolManager
    {
        /// <summary>Создаёт <paramref name="count"/> неактивных инстансов для <paramref name="prefab"/> заранее.</summary>
        void Prewarm(GameObject prefab, int count);

        /// <summary>
        /// Возвращает активный инстанс из пула. Если пул пуст — инстанцирует новый.
        /// </summary>
        GameObject Get(GameObject prefab);

        /// <summary>Деактивирует инстанс и возвращает его в пул источника.</summary>
        void Return(GameObject instance);
    }
}
