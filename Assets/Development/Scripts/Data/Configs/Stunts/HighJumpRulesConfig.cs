using UnityEngine;

namespace FlatOutLike.Data.Configs.Stunts
{
    /// <summary>
    /// Правила подсчёта очков и наград трюка «High Jump» (Шаг 7).
    /// Значения по умолчанию — база для юнит-тестов (см. HighJumpScoreServiceTests).
    /// </summary>
    [CreateAssetMenu(fileName = "HighJumpRulesConfig", menuName = "FlatOutLike/Stunts/HighJumpRulesConfig")]
    public sealed class HighJumpRulesConfig : ScriptableObject
    {
        // Композитный счёт (scorePerMeter/flipBonus) удалён решением геймдизайна:
        // результат трюка — только высота, флипы ни на что не влияют.
        [SerializeField] private float _heightToCoinRatio = 5f;
        [SerializeField] private float _flightTimeoutSeconds = 20f;

        /// <summary>Монет за метр высоты: coins = Round(PeakHeight * HeightToCoinRatio).</summary>
        public float HeightToCoinRatio => _heightToCoinRatio;

        /// <summary>
        /// Предохранитель: если полёт длится дольше этого времени (ragdoll улетел мимо
        /// LandingZone), сессия принудительно завершает попытку.
        /// </summary>
        public float FlightTimeoutSeconds => _flightTimeoutSeconds;
    }
}
