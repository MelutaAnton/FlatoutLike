using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Models;
using FlatOutLike.Services.Meta;
using UnityEngine;

namespace FlatOutLike.Services.Stunts
{
    /// <summary>
    /// Чистый C#: результат «High Jump» = пиковая ВЫСОТА (композитного счёта нет —
    /// решение геймдизайна) + обновление рекорда профиля + начисление монет от высоты.
    /// Флипы ни на что не влияют (FlipDetector остаётся про запас для будущих трюков).
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

        public void ScoreAttempt(DriverLandedEvent landed)
        {
            // Рекорд и результат — по ВЫСОТЕ, единственной метрике соревнования.
            var profile = _meta.Profile;
            bool isNewRecord = landed.PeakHeight > profile.BestHighJumpHeight;
            if (isNewRecord)
            {
                profile.BestHighJumpHeight = landed.PeakHeight;
                _meta.Save();
            }

            int coins = Mathf.RoundToInt(landed.PeakHeight * _rules.HeightToCoinRatio);

            _eventBus.Publish(new StuntsScoredEvent(StuntType.HighJump, landed.PeakHeight, isNewRecord));
            _eventBus.Publish(new RewardEarnedEvent(RewardSource.HighJump, coins, 0));
        }
    }
}
