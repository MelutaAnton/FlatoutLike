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
        [SerializeField] private float _scorePerMeter = 10f;
        [SerializeField] private int   _flipBonus = 50;
        [SerializeField] private float _heightToCoinRatio = 5f;
        [SerializeField] private float _flightTimeoutSeconds = 20f;

        /// <summary>Очков за метр набранной высоты (PeakHeight).</summary>
        public float ScorePerMeter => _scorePerMeter;

        /// <summary>Бонусных очков за каждый засчитанный флип.</summary>
        public int FlipBonus => _flipBonus;

        /// <summary>Монет за метр высоты: coins = Round(PeakHeight * HeightToCoinRatio).</summary>
        public float HeightToCoinRatio => _heightToCoinRatio;

        /// <summary>
        /// Предохранитель: если полёт длится дольше этого времени (ragdoll улетел мимо
        /// LandingZone), сессия принудительно завершает попытку.
        /// </summary>
        public float FlightTimeoutSeconds => _flightTimeoutSeconds;
    }
}
