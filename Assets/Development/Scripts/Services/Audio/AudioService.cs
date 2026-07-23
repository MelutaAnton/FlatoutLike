using System;
using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Audio;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Match;
using UnityEngine;
using VContainer.Unity;

namespace FlatOutLike.Services.Audio
{
    /// <summary>
    /// Центральный звуковой сервис: SFX по событиям геймплея + музыка по GameState.
    /// EntryPoint (VContainer) — Start() инициализирует плеер и включает музыку текущего
    /// состояния, Dispose() отписывается. Все зависимости регистрируются в GameLifetimeScope
    /// безусловно, поэтому сервис живёт на любой сцене (мета-UI просто не публикует
    /// геймплейные события, SFX там не проигрываются).
    /// </summary>
    public sealed class AudioService : IAudioService, IStartable, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly AudioConfig _config;
        private readonly IAudioPlayer _player;
        private readonly ICarRegistry _carRegistry;
        private readonly IGameFlowService _gameFlow;

        // Per-Id троттлинг по Time.unscaledTime — без него 7 машин, бьющихся одновременно,
        // завалят one-shot источники CrashLight/CrashHeavy за один кадр.
        private readonly Dictionary<SfxId, float> _lastPlayTime = new Dictionary<SfxId, float>();

        private MusicTrackId _currentMusic = MusicTrackId.None;

        public AudioService(IEventBus eventBus, AudioConfig config, IAudioPlayer player,
            ICarRegistry carRegistry, IGameFlowService gameFlow)
        {
            _eventBus = eventBus;
            _config = config;
            _player = player;
            _carRegistry = carRegistry;
            _gameFlow = gameFlow;

            _eventBus.Subscribe<CarCollisionEvent>(OnCarCollision);
            _eventBus.Subscribe<CarDestroyedEvent>(OnCarDestroyed);
            _eventBus.Subscribe<NitroStateChangedEvent>(OnNitroStateChanged);
            _eventBus.Subscribe<PickupCollectedEvent>(OnPickupCollected);
            _eventBus.Subscribe<DriverEjectedEvent>(OnDriverEjected);
            _eventBus.Subscribe<MidairBoostUsedEvent>(OnMidairBoostUsed);
            _eventBus.Subscribe<DriverLandedEvent>(OnDriverLanded);
            _eventBus.Subscribe<StuntsScoredEvent>(OnStuntsScored);
            _eventBus.Subscribe<DerbyMatchEndedEvent>(OnDerbyMatchEnded);
            _eventBus.Subscribe<CarPurchasedEvent>(OnCarPurchased);
            _eventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            _eventBus.Subscribe<GamePausedEvent>(OnGamePaused);
            _eventBus.Subscribe<GameResumedEvent>(OnGameResumed);
            _eventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        public void Start()
        {
            // Страховка: AudioListener.pause — глобальный static, переживает загрузку сцены
            // (как Time.timeScale). Выход из паузного оверлея в другую сцену (Wrecked → Leave)
            // оставил бы весь звук новой сцены немым.
            _player.SetPaused(false);

            _player.Initialize(_config.OneShotSourcesCount);
            // Музыка текущего состояния сразу при старте сцены (без ожидания первого GameStateChangedEvent).
            PlayMusicForState(_gameFlow.Current);
        }

        public void PlaySfx(SfxId id)
        {
            if (id == SfxId.None) return;
            if (!_config.TryGetSfx(id, out var entry)) return;
            if (entry.Clips == null || entry.Clips.Length == 0) return; // пустой слот — молча не играть

            float now = Time.unscaledTime;
            if (_lastPlayTime.TryGetValue(id, out var last) && now - last < entry.CooldownSeconds) return;

            var clip = entry.Clips[UnityEngine.Random.Range(0, entry.Clips.Length)];
            if (clip == null) return; // пустой элемент массива вариантов — молча пропустить

            _lastPlayTime[id] = now;

            float pitch = 1f + UnityEngine.Random.Range(-entry.PitchJitter, entry.PitchJitter);
            _player.PlayOneShot(clip, entry.Volume, pitch);
        }

        // ── Derby: столкновения и урон ───────────────────────────────────────────
        private void OnCarCollision(CarCollisionEvent evt)
        {
            if (evt.ImpactSpeed < _config.MinCrashImpactSpeed) return;
            var id = evt.ImpactSpeed >= _config.CrashHeavyImpactSpeed ? SfxId.CrashHeavy : SfxId.CrashLight;
            PlaySfx(id);
        }

        private void OnCarDestroyed(CarDestroyedEvent evt) => PlaySfx(SfxId.CarWrecked);

        // ── Нитро: луп только для машины игрока ──────────────────────────────────
        private void OnNitroStateChanged(NitroStateChangedEvent evt)
        {
            if (!_carRegistry.TryGetPlayerCarId(out var playerId)) return;
            if (evt.CarId != playerId) return;

            if (!evt.IsActive)
            {
                _player.StopLoop();
                return;
            }

            if (!_config.TryGetSfx(SfxId.NitroLoop, out var entry)) return;
            if (entry.Clips == null || entry.Clips.Length == 0) return;

            var clip = entry.Clips[0]; // луп — единственный, а не случайный вариант
            if (clip == null) return;

            _player.StartLoop(clip, entry.Volume);
        }

        // ── Пикапы ────────────────────────────────────────────────────────────
        private void OnPickupCollected(PickupCollectedEvent evt)
        {
            var id = evt.Type == PickupType.Repair ? SfxId.PickupRepair : SfxId.PickupNitro;
            PlaySfx(id);
        }

        // ── Eject / High Jump ─────────────────────────────────────────────────
        private void OnDriverEjected(DriverEjectedEvent evt) => PlaySfx(SfxId.Eject);

        private void OnMidairBoostUsed(MidairBoostUsedEvent evt) => PlaySfx(SfxId.MidairBoost);

        private void OnDriverLanded(DriverLandedEvent evt) => PlaySfx(SfxId.Landing);

        private void OnStuntsScored(StuntsScoredEvent evt)
        {
            if (evt.IsNewRecord) PlaySfx(SfxId.NewRecord);
        }

        // ── Derby: конец матча ────────────────────────────────────────────────
        private void OnDerbyMatchEnded(DerbyMatchEndedEvent evt)
        {
            // Нитро-луп не должен пережить конец матча (машина игрока может остаться
            // с зажатым нитро в момент финального удара).
            _player.StopLoop();

            bool playerWon = _carRegistry.TryGetPlayerCarId(out var playerId) && evt.Winner == playerId;
            PlaySfx(playerWon ? SfxId.MatchWin : SfxId.MatchLose);
        }

        // ── Мета: покупки ─────────────────────────────────────────────────────
        private void OnCarPurchased(CarPurchasedEvent evt) => PlaySfx(SfxId.Purchase);

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => PlaySfx(SfxId.Purchase);

        // ── Пауза ─────────────────────────────────────────────────────────────
        private void OnGamePaused(GamePausedEvent evt) => _player.SetPaused(true);

        private void OnGameResumed(GameResumedEvent evt) => _player.SetPaused(false);

        // ── Музыка по GameState ───────────────────────────────────────────────
        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            // Push/Pop паузы (To == Paused или From == Paused) не должны трогать музыку —
            // пауза глушит звук целиком через SetPaused, а не сменой трека.
            if (evt.To == GameState.Paused || evt.From == GameState.Paused) return;

            PlayMusicForState(evt.To);
        }

        private void PlayMusicForState(GameState state)
        {
            var track = _config.GetTrackForState(state);

            if (track == _currentMusic) return; // тот же трек уже играет — не перезапускаем

            if (track == MusicTrackId.None)
            {
                _player.StopMusic(_config.MusicCrossfadeSeconds);
                _currentMusic = MusicTrackId.None;
                return;
            }

            if (!_config.TryGetMusic(track, out var entry) || entry.Clip == null)
            {
                // Пустой слот клипа (ассет ещё не импортирован): прошлую музыку глушим —
                // иначе трек предыдущего состояния продолжит играть под чужим состоянием.
                // _currentMusic запоминаем, чтобы не дёргаться на каждый GameStateChangedEvent.
                _player.StopMusic(_config.MusicCrossfadeSeconds);
                _currentMusic = track;
                return;
            }

            _player.PlayMusic(entry.Clip, entry.Volume, _config.MusicCrossfadeSeconds);
            _currentMusic = track;
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<CarCollisionEvent>(OnCarCollision);
            _eventBus.Unsubscribe<CarDestroyedEvent>(OnCarDestroyed);
            _eventBus.Unsubscribe<NitroStateChangedEvent>(OnNitroStateChanged);
            _eventBus.Unsubscribe<PickupCollectedEvent>(OnPickupCollected);
            _eventBus.Unsubscribe<DriverEjectedEvent>(OnDriverEjected);
            _eventBus.Unsubscribe<MidairBoostUsedEvent>(OnMidairBoostUsed);
            _eventBus.Unsubscribe<DriverLandedEvent>(OnDriverLanded);
            _eventBus.Unsubscribe<StuntsScoredEvent>(OnStuntsScored);
            _eventBus.Unsubscribe<DerbyMatchEndedEvent>(OnDerbyMatchEnded);
            _eventBus.Unsubscribe<CarPurchasedEvent>(OnCarPurchased);
            _eventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            _eventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
            _eventBus.Unsubscribe<GameResumedEvent>(OnGameResumed);
            _eventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }
    }
}
