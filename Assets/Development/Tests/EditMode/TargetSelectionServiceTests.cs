using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Models;
using FlatOutLike.Services.AI;
using FlatOutLike.Services.Combat;
using FlatOutLike.Services.Match;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="TargetSelectionService"/>.
    /// Проверяют корректность выбора цели в разных режимах и граничные случаи.
    ///
    /// Не запускают Unity Physics — только чистая логика сервиса.
    /// CarController создаётся на временном GameObject с вызовом Configure,
    /// чтобы задать CarId и MaxArmor; HP контролируется через FakeDamageService.
    /// </summary>
    [TestFixture]
    public class TargetSelectionServiceTests
    {
        // ── Зависимости ─────────────────────────────────────────────────────────

        private FakeDamageService        _damage;
        private CarRegistry              _registry;
        private ITargetSelectionService  _service;

        // Хранит все созданные в тесте объекты для очистки в TearDown.
        private readonly List<GameObject>       _createdGOs  = new List<GameObject>();
        private readonly List<ScriptableObject> _createdSOs  = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            _damage   = new FakeDamageService();
            _registry = new CarRegistry();
            _service  = new TargetSelectionService(_registry, _damage);
        }

        [TearDown]
        public void TearDown()
        {
            // EditMode: DestroyImmediate обязателен (Destroy откладывает удаление)
            foreach (var go in _createdGOs)
                if (go != null) Object.DestroyImmediate(go);
            _createdGOs.Clear();

            foreach (var so in _createdSOs)
                if (so != null) Object.DestroyImmediate(so);
            _createdSOs.Clear();
        }

        // ── Хелперы ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Создаёт CarController, задаёт id, maxArmor и позицию; регистрирует для очистки.
        /// </summary>
        private CarController MakeCar(int carId, float maxArmor, Vector3 position)
        {
            var go  = new GameObject($"Car_{carId}");
            var car = go.AddComponent<CarController>();
            _createdGOs.Add(go);

            var stats = ScriptableObject.CreateInstance<CarStatsConfig>();
            _createdSOs.Add(stats);

            // Configure задаёт _carId и _config (откуда берётся MaxArmor).
            // Примечание: CarStatsConfig._maxArmor по умолчанию = 100f.
            // Для кастомного MaxArmor используем SerializedObject.
            if (!Mathf.Approximately(maxArmor, 100f))
            {
                var so = new UnityEditor.SerializedObject(stats);
                so.FindProperty("_maxArmor").floatValue = maxArmor;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            car.Configure(carId, stats);
            go.transform.position = position;
            return car;
        }

        /// <summary>
        /// Быстрый вариант: maxArmor=100, позиция задаётся явно.
        /// </summary>
        private CarController MakeCar(int carId, Vector3 position)
            => MakeCar(carId, 100f, position);

        // ── Тесты ───────────────────────────────────────────────────────────────

        // 1. Nearest выбирает ближайшего живого не-self
        [Test]
        public void Nearest_ReturnsClosestAliveNonSelf()
        {
            var self  = MakeCar(0, Vector3.zero);
            var near  = MakeCar(1, new Vector3(5f, 0f, 0f));
            var far   = MakeCar(2, new Vector3(50f, 0f, 0f));

            _registry.Register(self,  isPlayer: true);
            _registry.Register(near,  isPlayer: false);
            _registry.Register(far,   isPlayer: false);

            _damage.SetHp(self.CarId, 100f);
            _damage.SetHp(near.CarId, 100f);
            _damage.SetHp(far.CarId,  100f);

            var result = _service.SelectTarget(self, TargetSelectionMode.Nearest);

            Assert.AreEqual(near, result,
                "Nearest должен выбрать машину на расстоянии 5, а не 50");
        }

        // 2. Weakest выбирает машину с наименьшим соотношением HP / MaxArmor
        [Test]
        public void Weakest_ReturnsCarWithLowestHpRatio()
        {
            var self   = MakeCar(0, Vector3.zero);
            var strong = MakeCar(1, 100f, new Vector3(10f, 0f, 0f));
            var weak   = MakeCar(2, 100f, new Vector3(20f, 0f, 0f));

            _registry.Register(self,   isPlayer: true);
            _registry.Register(strong, isPlayer: false);
            _registry.Register(weak,   isPlayer: false);

            _damage.SetHp(self.CarId,   100f);
            _damage.SetHp(strong.CarId,  90f); // ratio = 0.90
            _damage.SetHp(weak.CarId,    30f); // ratio = 0.30 → самый слабый

            var result = _service.SelectTarget(self, TargetSelectionMode.Weakest);

            Assert.AreEqual(weak, result,
                "Weakest должен выбрать машину с HP=30 (ratio=0.30), а не с HP=90 (ratio=0.90)");
        }

        // 3. Player выбирает машину игрока
        [Test]
        public void Player_ReturnsPlayerCar()
        {
            var self   = MakeCar(0, Vector3.zero);
            var player = MakeCar(1, new Vector3(10f, 0f, 0f));
            var enemy  = MakeCar(2, new Vector3(3f,  0f, 0f)); // ближе, но не игрок

            _registry.Register(player, isPlayer: true);
            _registry.Register(enemy,  isPlayer: false);
            _registry.Register(self,   isPlayer: false);

            _damage.SetHp(self.CarId,   100f);
            _damage.SetHp(player.CarId, 100f);
            _damage.SetHp(enemy.CarId,  100f);

            var result = _service.SelectTarget(self, TargetSelectionMode.Player);

            Assert.AreEqual(player, result,
                "Player должен выбрать именно машину игрока, даже если есть более близкая");
        }

        // 4. Player при мёртвом игроке → откат к Nearest
        [Test]
        public void Player_WhenPlayerDead_FallsBackToNearest()
        {
            var self   = MakeCar(0, Vector3.zero);
            var player = MakeCar(1, new Vector3(5f, 0f, 0f));
            var enemy  = MakeCar(2, new Vector3(8f, 0f, 0f));

            _registry.Register(player, isPlayer: true);
            _registry.Register(enemy,  isPlayer: false);
            _registry.Register(self,   isPlayer: false);

            _damage.SetHp(self.CarId,   100f);
            _damage.SetHp(player.CarId, 0f);   // игрок мёртв
            _damage.SetHp(enemy.CarId,  100f);

            var result = _service.SelectTarget(self, TargetSelectionMode.Player);

            Assert.AreEqual(enemy, result,
                "Когда игрок мёртв, Player-режим должен откатиться к Nearest и выбрать ближайшего живого врага");
        }

        // 5. Self исключается из кандидатов
        [Test]
        public void SelectTarget_ExcludesSelf()
        {
            var self = MakeCar(0, Vector3.zero);

            _registry.Register(self, isPlayer: false);
            _damage.SetHp(self.CarId, 100f);

            // В реестре только self — все режимы должны вернуть null
            var resultNearest = _service.SelectTarget(self, TargetSelectionMode.Nearest);
            var resultWeakest = _service.SelectTarget(self, TargetSelectionMode.Weakest);
            var resultPlayer  = _service.SelectTarget(self, TargetSelectionMode.Player);

            Assert.IsNull(resultNearest, "Nearest: self должен быть исключён из кандидатов");
            Assert.IsNull(resultWeakest, "Weakest: self должен быть исключён из кандидатов");
            Assert.IsNull(resultPlayer,  "Player: self должен быть исключён из кандидатов");
        }

        // 6. Мёртвые машины (GetHp == 0) исключаются из выборки
        [Test]
        public void SelectTarget_ExcludesDeadCars()
        {
            var self  = MakeCar(0, Vector3.zero);
            var dead  = MakeCar(1, new Vector3(3f, 0f, 0f));
            var alive = MakeCar(2, new Vector3(10f, 0f, 0f));

            _registry.Register(self,  isPlayer: false);
            _registry.Register(dead,  isPlayer: false);
            _registry.Register(alive, isPlayer: false);

            _damage.SetHp(self.CarId,  100f);
            _damage.SetHp(dead.CarId,  0f);   // мёртвая, ближе
            _damage.SetHp(alive.CarId, 100f);  // живая, дальше

            var result = _service.SelectTarget(self, TargetSelectionMode.Nearest);

            Assert.AreEqual(alive, result,
                "Ближайшая мёртвая машина должна быть исключена; выбирается ближайшая живая");
        }

        // 7. Пустой набор кандидатов → null
        [Test]
        public void SelectTarget_EmptyRegistry_ReturnsNull()
        {
            var self = MakeCar(0, Vector3.zero);
            // self в реестре не регистрируем — реестр полностью пуст

            var resultNearest = _service.SelectTarget(self, TargetSelectionMode.Nearest);
            var resultWeakest = _service.SelectTarget(self, TargetSelectionMode.Weakest);
            var resultPlayer  = _service.SelectTarget(self, TargetSelectionMode.Player);

            Assert.IsNull(resultNearest, "Nearest: пустой реестр должен вернуть null");
            Assert.IsNull(resultWeakest, "Weakest: пустой реестр должен вернуть null");
            Assert.IsNull(resultPlayer,  "Player: пустой реестр должен вернуть null");
        }

        // ── Фейк DamageService ───────────────────────────────────────────────────

        /// <summary>
        /// Минимальная реализация <see cref="IDamageService"/> для тестов.
        /// HP хранятся в словаре; <c>RegisterCar</c> / <c>UnregisterCar</c> — пустышки.
        /// Незарегистрированные машины возвращают HP = 0 (считаются мёртвыми).
        /// </summary>
        private sealed class FakeDamageService : IDamageService
        {
            private readonly Dictionary<int, float> _hp = new Dictionary<int, float>();

            public void SetHp(CarId id, float hp) => _hp[id.Value] = hp;

            public void RegisterCar(CarId id, float maxArmor, float mass) { }
            public void UnregisterCar(CarId id) { }
            public float GetHp(CarId id) => _hp.TryGetValue(id.Value, out var hp) ? hp : 0f;
            public void Repair(CarId id, float fraction) { } // не используется в этих тестах
        }
    }
}
