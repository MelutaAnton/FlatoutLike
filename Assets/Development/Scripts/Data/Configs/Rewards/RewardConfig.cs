using UnityEngine;

namespace FlatOutLike.Data.Configs.Rewards
{
    /// <summary>
    /// Конфиг наград за Derby-матч: сколько монет даётся за убийство и за победу.
    /// Значения по умолчанию совпадают с числами, которые ожидают юнит-тесты
    /// (см. DerbyMatchServiceTests) — инициализаторы полей задают значения ассета.
    /// </summary>
    [CreateAssetMenu(fileName = "RewardConfig", menuName = "FlatOutLike/Rewards/RewardConfig")]
    public sealed class RewardConfig : ScriptableObject
    {
        [SerializeField] private int _coinsPerKill = 10;
        [SerializeField] private int _winBonus = 50;

        public int CoinsPerKill => _coinsPerKill;
        public int WinBonus     => _winBonus;
    }
}
