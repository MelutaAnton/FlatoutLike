using System;
using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
using FlatOutLike.Services.Match;
using UnityEngine;

namespace FlatOutLike.Views.UI.Hud
{
    /// <summary>
    /// Presenter Derby HUD (pure C#, Шаг 8d). Живёт в Derby-сцене — зависит только от
    /// IEventBus/IDamageService/INitroService/ICarRegistry (все регистрируются безусловно),
    /// мета-каталоги ему не нужны.
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

        // Кэш погибших машин по CarDestroyedEvent — считаем живых без обхода IDamageService
        // по каждой машине каждый тик (альтернатива из задания, выбрана она).
        private readonly HashSet<CarId> _destroyedCars = new HashSet<CarId>();

        public DerbyHudPresenter(DerbyHudView view, IEventBus eventBus, IDamageService damage,
            INitroService nitro, ICarRegistry carRegistry)
        {
            _view = view;
            _eventBus = eventBus;
            _damage = damage;
            _nitro = nitro;
            _carRegistry = carRegistry;

            _view.OnTick += HandleTick;
            _eventBus.Subscribe<CarDestroyedEvent>(OnCarDestroyed);
            _eventBus.Subscribe<DerbyMatchEndedEvent>(OnMatchEnded);
        }

        public void Dispose()
        {
            _view.OnTick -= HandleTick;
            _eventBus.Unsubscribe<CarDestroyedEvent>(OnCarDestroyed);
            _eventBus.Unsubscribe<DerbyMatchEndedEvent>(OnMatchEnded);
        }

        private void HandleTick()
        {
            // Гвард: до спавна машин ICarRegistry пуст — ничего не рисуем и не трогаем root.
            if (!_carRegistry.TryGetPlayerCarId(out var playerId))
                return;

            _view.SetHp(NormalizeHp(_damage.GetHp(playerId), _damage.GetMaxArmor(playerId)));
            _view.SetNitro(_nitro.GetNitro01(playerId));
            _view.SetAliveCount(CountAlive(_carRegistry.Cars.Count, _destroyedCars.Count));
        }

        private void OnCarDestroyed(CarDestroyedEvent e)
        {
            _destroyedCars.Add(e.Target);

            // HUD скрывается сразу по гибели МАШИНЫ ИГРОКА — независимо от исхода матча в целом.
            if (_carRegistry.TryGetPlayerCarId(out var playerId) && e.Target == playerId)
                _view.SetVisible(false);
        }

        private void OnMatchEnded(DerbyMatchEndedEvent e) => _view.SetVisible(false);

        /// <summary>
        /// Нормировка HP игрока в 0..1. Публичный static — вынесена ради юнит-теста без
        /// живого DerbyHudView (MonoBehaviour, требует сцену/префаб — см. GaragePresenterTests).
        /// </summary>
        public static float NormalizeHp(float hp, float maxArmor) =>
            maxArmor > 0f ? Mathf.Clamp01(hp / maxArmor) : 0f;

        /// <summary>Число живых участников = всего зарегистрировано минус погибшие. Публичный static — та же причина.</summary>
        public static int CountAlive(int totalRegistered, int destroyedCount) =>
            Mathf.Max(0, totalRegistered - destroyedCount);
    }
}
