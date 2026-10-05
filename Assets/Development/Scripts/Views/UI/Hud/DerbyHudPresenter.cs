using System;
using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Match;
using UnityEngine;

namespace FlatOutLike.Views.UI.Hud
{
    /// <summary>
    /// Presenter Derby HUD (pure C#, Шаг 8d + Шаг 10b/10c). Живёт в Derby-сцене — зависит только
    /// от IEventBus/IDamageService/INitroService/ICarRegistry/IGameFlowService (все регистрируются
    /// безусловно), мета-каталоги ему не нужны.
    ///
    /// Машины спавнятся ПОЗЖЕ построения scope (DerbyMatchService.StartMatch вызывается из
    /// build callback уже после Configure) — до появления игрока в ICarRegistry презентер
    /// ничего не рисует и не трогает root (см. HandleTick).
    /// </summary>
    public sealed class DerbyHudPresenter : IDisposable
    {
        private readonly DerbyHudView _view;
        private readonly IEventBus _eventBus;
        private readonly IDamageService _damage;
        private readonly INitroService _nitro;
        private readonly ICarRegistry _carRegistry;
        private readonly IGameFlowService _gameFlow;

        // Кэш погибших машин по CarDestroyedEvent — считаем живых без обхода IDamageService
        // по каждой машине каждый тик (альтернатива из задания, выбрана она).
        private readonly HashSet<CarId> _destroyedCars = new HashSet<CarId>();

        public DerbyHudPresenter(DerbyHudView view, IEventBus eventBus, IDamageService damage,
            INitroService nitro, ICarRegistry carRegistry, IGameFlowService gameFlow)
        {
            _view = view;
            _eventBus = eventBus;
            _damage = damage;
            _nitro = nitro;
            _carRegistry = carRegistry;
            _gameFlow = gameFlow;

            _view.OnTick += HandleTick;
            _view.OnWreckedWatchClicked += HandleWreckedWatchClicked;
            _view.OnWreckedLeaveClicked += HandleWreckedLeaveClicked;
            _eventBus.Subscribe<CarDestroyedEvent>(OnCarDestroyed);
            _eventBus.Subscribe<DerbyMatchEndedEvent>(OnMatchEnded);
        }

        public void Dispose()
        {
            _view.OnTick -= HandleTick;
            _view.OnWreckedWatchClicked -= HandleWreckedWatchClicked;
            _view.OnWreckedLeaveClicked -= HandleWreckedLeaveClicked;
            _eventBus.Unsubscribe<CarDestroyedEvent>(OnCarDestroyed);
            _eventBus.Unsubscribe<DerbyMatchEndedEvent>(OnMatchEnded);
        }

        private void HandleTick()
        {
            // Гвард: до спавна машин ICarRegistry пуст — ничего не рисуем и не трогаем root.
            if (!_carRegistry.TryGetPlayerCarId(out var playerId))
                return;

            // После гибели игрока бары не поллим (данные разрушенной машины не определены);
            // счётчик живых остаётся актуальным для зрительского режима (Watch).
            if (!_destroyedCars.Contains(playerId))
            {
                _view.SetHp(NormalizeHp(_damage.GetHp(playerId), _damage.GetMaxArmor(playerId)));
                _view.SetNitro(_nitro.GetNitro01(playerId));
            }

            _view.SetAliveCount(CountAlive(_carRegistry.Cars.Count, _destroyedCars.Count));
        }

        private void OnCarDestroyed(CarDestroyedEvent e)
        {
            _destroyedCars.Add(e.Target);

            _view.PushKillFeedEntry(FormatKillFeedEntry(e.Killer, e.Target, ResolveCarName(e.Killer), ResolveCarName(e.Target)));

            // HUD скрывается сразу по гибели МАШИНЫ ИГРОКА — независимо от исхода матча в целом.
            // Экран Wrecked (Шаг 10c) показывается по тому же условию.
            if (_carRegistry.TryGetPlayerCarId(out var playerId) && ShouldShowWreckedPanel(e.Target, playerId))
            {
                _view.SetVisible(false);
                _view.ShowWreckedPanel();
            }
        }

        private void OnMatchEnded(DerbyMatchEndedEvent e)
        {
            _view.SetVisible(false);

            // Игрок мог погибнуть предпоследним — конец матча приходит следом, и без скрытия
            // Wrecked-панель наложилась бы на карточку MatchEnd (две центральные панели разом).
            _view.HideWreckedPanel();
        }

        private void HandleWreckedWatchClicked()
        {
            _view.HideWreckedPanel();

            // Зрительский режим: возвращаем HUD ради счётчика живых и kill-feed'а
            // (бары игрока заморожены — HandleTick их больше не поллит, см. гвард).
            _view.SetVisible(true);
        }

        private void HandleWreckedLeaveClicked() => _gameFlow.ChangeState(GameState.BaseHub);

        /// <summary>Резолвит отображаемое имя машины через ICarRegistry, с фоллбэком "CAR &lt;id&gt;".</summary>
        private string ResolveCarName(CarId id) =>
            _carRegistry.TryGetName(id, out var name) ? name : $"CAR {id.Value}";

        /// <summary>
        /// Нормировка HP игрока в 0..1. Публичный static — вынесена ради юнит-теста без
        /// живого DerbyHudView (MonoBehaviour, требует сцену/префаб — см. GaragePresenterTests).
        /// </summary>
        public static float NormalizeHp(float hp, float maxArmor) =>
            maxArmor > 0f ? Mathf.Clamp01(hp / maxArmor) : 0f;

        /// <summary>Число живых участников = всего зарегистрировано минус погибшие. Публичный static — та же причина.</summary>
        public static int CountAlive(int totalRegistered, int destroyedCount) =>
            Mathf.Max(0, totalRegistered - destroyedCount);

        /// <summary>
        /// Формат записи kill-feed (Шаг 10b). Публичный static — та же причина, что у NormalizeHp/CountAlive.
        /// Killer есть и отличается от victim -> "KILLER ▸ VICTIM"; иначе (суицид/нет killer'а,
        /// например урон стенкой) -> "VICTIM WRECKED".
        /// </summary>
        public static string FormatKillFeedEntry(CarId killer, CarId victim, string killerName, string victimName) =>
            killer != CarId.None && killer != victim
                ? $"{killerName} ▸ {victimName}"
                : $"{victimName} WRECKED";

        /// <summary>Экран Wrecked (Шаг 10c) показывается, только если погибла машина игрока. Публичный static — та же причина.</summary>
        public static bool ShouldShowWreckedPanel(CarId target, CarId playerId) => target == playerId;
    }
}
