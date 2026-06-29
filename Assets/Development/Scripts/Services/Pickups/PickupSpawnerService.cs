using System.Collections.Generic;
using FlatOutLike.Controllers.Pickups;
using FlatOutLike.Data.Configs.Pickups;
using FlatOutLike.Factories.Pickup;
using UnityEngine;
using VContainer.Unity;

namespace FlatOutLike.Services.Pickups
{
    /// <summary>
    /// Тикающий сервис (VContainer EntryPoint → ITickable).
    /// Следит за состоянием каждой зоны: если пикап собрали, отсчитывает таймер и спавнит новый.
    /// VContainer вызывает <see cref="Tick"/> автоматически в Update-такте через свой диспетчер.
    /// </summary>
    public sealed class PickupSpawnerService : IPickupSpawnerService, ITickable
    {
        private readonly IPickupFactory _factory;
        private readonly PickupConfig   _config;

        private readonly List<ZoneState> _zones = new List<ZoneState>();

        public PickupSpawnerService(IPickupFactory factory, PickupConfig config)
        {
            _factory = factory;
            _config  = config;
        }

        public void StartSpawning(IReadOnlyList<PickupZone> zones)
        {
            _zones.Clear();

            foreach (var zone in zones)
            {
                // Защитная проверка: если Transform не назначен в Inspector — пропускаем зону.
                if (zone.point == null)
                {
                    Debug.LogWarning($"[PickupSpawner] Зона типа {zone.type} не имеет Transform-point — пропущена. Назначьте point в Inspector.");
                    continue;
                }

                var state = new ZoneState { Zone = zone };
                // Сразу спавним пикап в каждой зоне.
                state.Active = _factory.Spawn(zone.type, zone.point.position, OnCollected);
                // Если спавн не удался (например, нет prefab в конфиге) — взводим таймер,
                // чтобы Tick повторил попытку, а зона не «умирала» молча с RespawnTimer=0.
                if (state.Active == null)
                    state.RespawnTimer = _config.RespawnInterval;
                _zones.Add(state);
            }
        }

        /// <summary>
        /// Вызывается каждый Update VContainer-диспетчером.
        /// Для зон без активного пикапа — тикает таймер и спавнит при достижении 0.
        /// </summary>
        public void Tick()
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                var state = _zones[i];
                if (state.Active != null) continue;
                if (state.RespawnTimer <= 0f) continue;

                state.RespawnTimer -= Time.deltaTime;

                if (state.RespawnTimer <= 0f)
                {
                    state.RespawnTimer = 0f;
                    // zone.point уже проверен в StartSpawning — незаполненные зоны туда не попадают.
                    state.Active = _factory.Spawn(
                        state.Zone.type,
                        state.Zone.point.position,
                        OnCollected);
                    // Спавн снова не удался — повторим через интервал (не застреваем на 0).
                    if (state.Active == null)
                        state.RespawnTimer = _config.RespawnInterval;
                }
            }
        }

        /// <summary>
        /// Колбэк, который пикап передаёт в <see cref="IPickup.Activate"/>.
        /// Находит соответствующую зону, возвращает пикап в пул и запускает таймер респавна.
        /// </summary>
        private void OnCollected(IPickup pickup)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                var state = _zones[i];
                if (state.Active != pickup) continue;

                _factory.Despawn(pickup);
                state.Active       = null;
                state.RespawnTimer = _config.RespawnInterval;
                // ZoneState — класс (ссылочный тип), изменения видны без re-assign.
                return;
            }
        }

        // ── Внутреннее состояние зоны ──────────────────────────────────────────
        private sealed class ZoneState
        {
            public PickupZone Zone;
            public IPickup    Active;
            /// <summary>Время до следующего появления (> 0 — ждём, 0 — спавнить).</summary>
            public float RespawnTimer;
        }
    }
}
