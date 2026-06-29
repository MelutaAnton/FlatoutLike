using UnityEngine;

namespace FlatOutLike.Core.Pool
{
    /// <summary>
    /// Маркер-компонент, прикрепляемый к каждому инстансу при создании через PoolManager.
    /// Хранит ссылку на исходный prefab, чтобы Return мог вернуть объект в правильный пул.
    /// </summary>
    public sealed class PooledInstance : MonoBehaviour
    {
        /// <summary>Prefab, из которого был создан этот инстанс.</summary>
        public GameObject SourcePrefab;
    }
}
