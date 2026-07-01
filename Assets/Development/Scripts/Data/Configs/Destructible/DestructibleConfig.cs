using UnityEngine;

namespace FlatOutLike.Data.Configs.Destructible
{
    // Конфиг разрушаемого объекта: порог удара, обломки, физика разлёта.
    // Magic numbers хранятся только здесь — в инициализаторах полей.
    [CreateAssetMenu(fileName = "DestructibleConfig", menuName = "Flatuot2_Like/DestructibleConfig")]
    public sealed class DestructibleConfig : ScriptableObject
    {
        // Минимальная скорость относительного удара (м/с), при которой объект разрушается
        [SerializeField] private float _breakImpulseThreshold = 12f;

        // Массив префабов обломков — выбираются циклически при разрушении
        [SerializeField] private GameObject[] _debrisPrefabs;

        // Количество обломков, создаваемых при разрушении
        [SerializeField] private int _debrisPieceCount = 6;

        // Время жизни обломка (сек) до возврата в пул
        [SerializeField] private float _debrisLifetime = 5f;

        // Сила разлёта обломков (ForceMode.VelocityChange)
        [SerializeField] private float _debrisScatterForce = 6f;

        // Размер пула обломков (прогрев)
        [SerializeField] private int _debrisPoolSize = 12;

        public float      BreakImpulseThreshold => _breakImpulseThreshold;
        public GameObject[] DebrisPrefabs        => _debrisPrefabs;
        public int        DebrisPieceCount       => _debrisPieceCount;
        public float      DebrisLifetime         => _debrisLifetime;
        public float      DebrisScatterForce     => _debrisScatterForce;
        public int        DebrisPoolSize         => _debrisPoolSize;
    }
}
