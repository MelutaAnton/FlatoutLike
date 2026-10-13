using System.Collections.Generic;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Progression;
using UnityEngine;
using VContainer.Unity;

namespace FlatOutLike.Services.Hub
{
    /// <summary>
    /// Таймер подтверждения запуска режима в BaseHub (pure C#, EntryPoint). Сервис помнит все
    /// занятые зоны (порядок входа); активна последняя вошедшая, вход в новую зону начинает
    /// отсчёт с нуля. При выходе из активной зоны активной становится последняя из оставшихся
    /// (отсчёт с нуля) — повторного OnTriggerEnter для неё не будет. Время идёт только
    /// в состоянии BaseHub (в паузе отсчёт стоит). По истечении LaunchConfirmSeconds —
    /// ровно один ChangeState(LaunchState), после чего зона деактивируется, а занятые зоны очищаются.
    /// Гейт стройкой (12a): зона недостроенного здания запуск не начинает (IBuildProgressService.IsBuilt).
    /// Авто-запуска при завершении стройки нет — персонаж стоит в зоне, но OnTriggerEnter уже был;
    /// чтобы запустить режим, нужно выйти и перезайти в зону (без внезапного ухода со стройки в режим).
    /// </summary>
    public sealed class HubLaunchService : IHubLaunchService, ITickable
    {
        private readonly BuildingCatalogConfig _catalog;
        private readonly IGameFlowService _gameFlow;
        private readonly IBuildProgressService _build;

        // Занятые зоны в порядке входа; последняя — активная. Недопустимые id сюда не попадают.
        private readonly List<BuildingId> _occupiedZones = new List<BuildingId>();

        private bool _hasActiveZone;
        private BuildingId _activeZone;
        private GameState _activeLaunchState;
        private float _elapsed;

        public HubLaunchService(BuildingCatalogConfig catalog, IGameFlowService gameFlow, IBuildProgressService build)
        {
            _catalog = catalog;
            _gameFlow = gameFlow;
            _build = build;
        }

        public float Progress01
        {
            get
            {
                if (!_hasActiveZone) return 0f;

                float seconds = _catalog.LaunchConfirmSeconds;
                if (seconds <= 0f) return 1f;

                return Mathf.Clamp01(_elapsed / seconds);
            }
        }

        public void EnterZone(BuildingId id)
        {
            if (!_catalog.TryGetEntry(id, out var entry)) return;
            if (!entry.Enabled || !entry.HasLaunchZone) return;

            // Недостроенное здание: та же зона сейчас принимает вложения (BuildProgressService), не запуск.
            if (!_build.IsBuilt(id)) return;

            // Повторный вход не дублирует запись: id уходит в конец списка (последняя вошедшая).
            _occupiedZones.Remove(id);
            _occupiedZones.Add(id);

            ActivateZone(id, entry.LaunchState);
        }

        public void ExitZone(BuildingId id)
        {
            if (!_occupiedZones.Remove(id)) return;

            // Выход из перекрытой (неактивной) зоны не сбрасывает активную.
            if (!_hasActiveZone || _activeZone != id) return;

            // Вышли из активной: если персонаж ещё стоит в другой зоне — она становится активной
            // (OnTriggerEnter для неё уже не придёт), иначе активной зоны нет.
            ResetActiveZone();
            if (_occupiedZones.Count == 0) return;

            var fallback = _occupiedZones[_occupiedZones.Count - 1];
            if (_catalog.TryGetEntry(fallback, out var entry))
                ActivateZone(fallback, entry.LaunchState);
        }

        public bool TryGetActiveZone(out BuildingId id)
        {
            id = _activeZone;
            return _hasActiveZone;
        }

        public void Tick() => Advance(UnityEngine.Time.deltaTime);

        /// <summary>Вся логика таймера; Tick лишь подставляет deltaTime (тесты зовут напрямую).</summary>
        public void Advance(float deltaTime)
        {
            if (!_hasActiveZone) return;

            // Отсчёт только в самом хабе: в паузе (Paused поверх BaseHub) время не идёт.
            if (_gameFlow.Current != GameState.BaseHub) return;

            _elapsed += deltaTime;
            if (_elapsed < _catalog.LaunchConfirmSeconds) return;

            // Деактивируем ДО ChangeState: смена состояния синхронно публикует события,
            // повторный выстрел из реентерабельного Tick исключён.
            // Занятые зоны очищаем: сцена всё равно уходит, повторного выстрела быть не должно.
            var launchState = _activeLaunchState;
            _occupiedZones.Clear();
            ResetActiveZone();
            _gameFlow.ChangeState(launchState);
        }

        private void ActivateZone(BuildingId id, GameState launchState)
        {
            _hasActiveZone = true;
            _activeZone = id;
            _activeLaunchState = launchState;
            _elapsed = 0f;
        }

        private void ResetActiveZone()
        {
            _hasActiveZone = false;
            _activeZone = default;
            _elapsed = 0f;
        }
    }
}
