using FlatOutLike.Models;

namespace FlatOutLike.Core.Events
{
    /// <summary>
    /// Unified reward event published by every reward source (Derby, HighJump, missions).
    /// XpAmount may be 0 until the Player Level / XP system exists (§15.3 safeguard) —
    /// future progression services subscribe here without touching reward publishers.
    /// </summary>
    public readonly struct RewardEarnedEvent
    {
        public readonly RewardSource Source;
        public readonly int CoinAmount;
        public readonly int XpAmount;

        public RewardEarnedEvent(RewardSource source, int coinAmount, int xpAmount)
        {
            Source = source;
            CoinAmount = coinAmount;
            XpAmount = xpAmount;
        }
    }

    /// <summary>
    /// Публикуется <c>BuildProgressService</c> после каждой списанной пачки потокового вложения
    /// (game-design §6). Ценник здания (BuildingPanelView) обновляет «осталось X» без опроса.
    /// </summary>
    public readonly struct BuildingInvestProgressEvent
    {
        public readonly BuildingId Id;
        public readonly int CompletedStages;
        public readonly int InvestedInStage;
        public readonly int StageCost;

        public BuildingInvestProgressEvent(BuildingId id, int completedStages, int investedInStage, int stageCost)
        {
            Id = id;
            CompletedStages = completedStages;
            InvestedInStage = investedInStage;
            StageCost = stageCost;
        }
    }

    /// <summary>
    /// Публикуется <c>BuildProgressService</c>, когда этап стройки оплачен полностью.
    /// BuildSiteController меняет визуальную стадию, ценник — rate и следующий этап.
    /// </summary>
    public readonly struct BuildingStageInvestedEvent
    {
        public readonly BuildingId Id;
        public readonly int CompletedStages;
        public readonly int TotalStages;

        public BuildingStageInvestedEvent(BuildingId id, int completedStages, int totalStages)
        {
            Id = id;
            CompletedStages = completedStages;
            TotalStages = totalStages;
        }
    }

    /// <summary>
    /// Публикуется <c>BuildProgressService</c> ровно один раз, когда завершён последний этап здания.
    /// Разблокировки (запуск режима, доход) подписываются сюда — сервис стройки GameFlow не вызывает.
    /// </summary>
    public readonly struct BuildingCompletedEvent
    {
        public readonly BuildingId Id;

        public BuildingCompletedEvent(BuildingId id)
        {
            Id = id;
        }
    }
}
