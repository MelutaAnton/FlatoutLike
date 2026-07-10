using System;
using System.Collections.Generic;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Combat
{
    /// <summary>
    /// Хранит состояние бака нитро для каждой зарегистрированной машины.
    /// Чистый C#: никакого MonoBehaviour, никакого UnityEngine.
    /// </summary>
    public sealed class NitroService : INitroService
    {
        // CarId → (текущий уровень, максимальная ёмкость)
        private readonly Dictionary<CarId, (float current, float capacity)> _tanks
            = new Dictionary<CarId, (float, float)>();

        /// <summary>
        /// Регистрирует машину. Бак начинается ПОЛНЫМ (решение Шага 8d по итогам плейтеста:
        /// раньше стартовал пустым «чтобы подбирать пикапы» — теперь пикапы дозаправляют
        /// потраченное, а не являются единственным источником нитро).
        /// </summary>
        public void RegisterCar(CarId id, float capacity)
        {
            float cap = Math.Max(0f, capacity);
            _tanks[id] = (cap, cap);
        }

        public bool TryConsume(CarId id, float amount)
        {
            // Защита от amount<=0: нулевой расход не должен считаться успешным.
            // Без этого: если _nitroDrainRate=0 (поле не пересериализовано в SO-ассете),
            // TryConsume всегда возвращает true → AddForce каждый FixedUpdate с пустым баком.
            if (amount <= 0f) return false;
            if (!_tanks.TryGetValue(id, out var tank)) return false;
            if (tank.current < amount) return false;

            _tanks[id] = (tank.current - amount, tank.capacity);
            return true;
        }

        public void Refill(CarId id, float fraction)
        {
            if (!_tanks.TryGetValue(id, out var tank)) return;

            float added     = tank.capacity * Math.Max(0f, fraction);
            float newCurrent = Math.Min(tank.current + added, tank.capacity);
            _tanks[id] = (newCurrent, tank.capacity);
        }

        public float GetNitro01(CarId id)
        {
            if (!_tanks.TryGetValue(id, out var tank)) return 0f;
            if (tank.capacity <= 0f) return 0f;
            return tank.current / tank.capacity;
        }
    }
}
