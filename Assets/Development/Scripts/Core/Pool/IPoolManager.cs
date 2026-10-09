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

        /// <summary>
        /// Возвращает активный инстанс из пула, поставленный в <paramref name="position"/> /
        /// <paramref name="rotation"/>. Если пул пуст — инстанцирует новый.
        /// Инстанс из очереди: поза ставится, пока он ВЫКЛЮЧЕН, и только затем он активируется.
        /// Свежий инстанс: создаётся сразу в заданной позе (Instantiate с позой), поэтому его
        /// Awake/OnEnable первичной активации префаба проходят уже в ней; затем он выключается
        /// и активируется повторно. В обоих случаях Rigidbody инициализируется в точке спавна —
        /// без кадра «объект включён в старой позиции (нуле координат / месте прошлого
        /// использования)» и без интерполяционного рывка.
        /// Null-prefab → Warning и null.
        /// </summary>
        GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation);

        /// <summary>Деактивирует инстанс и возвращает его в пул источника.</summary>
        void Return(GameObject instance);
    }
}
