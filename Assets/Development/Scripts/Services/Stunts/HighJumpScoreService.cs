using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Models;
using FlatOutLike.Services.Meta;
using UnityEngine;

namespace FlatOutLike.Services.Stunts
{
    /// <summary>
    /// Чистый C#: формула очков «High Jump» + обновление рекорда профиля + начисление монет.
    /// </summary>
    public sealed class HighJumpScoreService : IHighJumpScoreService
    {
        private readonly HighJumpRulesConfig _rules;
        private readonly IMetaProgressService _meta;
        private readonly IEventBus _eventBus;

        public HighJumpScoreService(HighJumpRulesConfig rules, IMetaProgressService meta, IEventBus eventBus)
        {
            _rules = rules;
            _meta = meta;
            _eventBus = eventBus;
        }

        public int ScoreAttempt(DriverLandedEvent landed)
        {
            int score = Mathf.RoundToInt(landed.PeakHeight * _rules.ScorePerMeter)
                      + landed.FlipCount * _rules.FlipBonus;

            // Рекорд — по ВЫСОТЕ (суть соревнования), а не по композитному счёту:
            // флипы дают монеты, но не двигают личный рекорд.
            var profile = _meta.Profile;
            bool isNewRecord = landed.PeakHeight > profile.BestHighJumpHeight;
            if (isNewRecord)
            {
                profile.BestHighJumpHeight = landed.PeakHeight;
                _meta.Save();
            }

            int coins = Mathf.RoundToInt(landed.PeakHeight * _rules.HeightToCoinRatio);

            _eventBus.Publish(new StuntsScoredEvent(StuntType.HighJump, score, isNewRecord));
            _eventBus.Publish(new RewardEarnedEvent(RewardSource.HighJump, coins, 0));

            return score;
        }
    }
}
