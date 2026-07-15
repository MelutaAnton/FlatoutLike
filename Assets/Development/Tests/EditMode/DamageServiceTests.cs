using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="DamageService"/>: расчёт урона по массе/скорости,
    /// фильтрация ударов об окружение по DamageConfig.DamageIgnoreLayers.
    /// </summary>
    [TestFixture]
    public class DamageServiceTests
    {
        private EventBus _bus;
        private DamageConfig _config;
        private DamageService _service;

        // Слой "Floor" в проекте — намеренно не завязываемся на реальный индекс из
        // TagManager (в тестовом окружении он может отсутствовать), а используем
        // произвольный индекс 9 и прошиваем его же в маску конфига.
        private const int FloorLayer = 9;
        private const int WallLayer = 10; // слой НЕ входит в маску — ведёт себя как раньше

        private static readonly CarId Victim = new CarId(20);
        private static readonly CarId Attacker = new CarId(21);

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _config = MakeConfig(damageScale: 1f, minImpactSpeed: 0f, wallMassRatio: 1f,
                ignoreLayersMask: 1 << FloorLayer);
            _service = new DamageService(_bus, _config);
            _service.RegisterCar(Victim, maxArmor: 100f, mass: 1000f);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [Test]
        public void EnvironmentHit_OnIgnoredLayer_NoAttacker_DoesNotDamage()
        {
            bool eventPublished = false;
            _bus.Subscribe<CarDamagedEvent>(_ => eventPublished = true);

            // Удар о пол (слой в маске), атакующего нет — типичный случай "проехал по неровности".
            _bus.Publish(new CarCollisionEvent(CarId.None, Victim, 10f, Vector3.zero, FloorLayer));

            Assert.AreEqual(100f, _service.GetHp(Victim), 0.001f,
                "Удар об игнорируемый слой без атакующего не должен наносить урон.");
            Assert.IsFalse(eventPublished, "CarDamagedEvent не должен публиковаться.");
        }

        [Test]
        public void CarHit_OnSameLayerAsIgnored_Attacker_StillDamages()
        {
            // Тот же слой, но есть атакующая машина — фильтр по слою на удары машин не влияет.
            _service.RegisterCar(Attacker, maxArmor: 100f, mass: 1000f);

            _bus.Publish(new CarCollisionEvent(Attacker, Victim, 10f, Vector3.zero, FloorLayer));

            Assert.Less(_service.GetHp(Victim), 100f,
                "Удар другой машины наносит урон независимо от слоя.");
        }

        [Test]
        public void EnvironmentHit_OnNonIgnoredLayer_NoAttacker_StillDamages()
        {
            // Слой не в маске (например Wall) — поведение как до фикса: урон проходит.
            _bus.Publish(new CarCollisionEvent(CarId.None, Victim, 10f, Vector3.zero, WallLayer));

            Assert.Less(_service.GetHp(Victim), 100f,
                "Удар об окружение на слое вне маски должен наносить урон, как раньше.");
        }

        // ── Хелперы ─────────────────────────────────────────────────────────────

        private DamageConfig MakeConfig(float damageScale, float minImpactSpeed, float wallMassRatio, int ignoreLayersMask)
        {
            var cfg = ScriptableObject.CreateInstance<DamageConfig>();

            var so = new UnityEditor.SerializedObject(cfg);
            so.FindProperty("_damageScale").floatValue = damageScale;
            so.FindProperty("_minImpactSpeed").floatValue = minImpactSpeed;
            so.FindProperty("_wallMassRatio").floatValue = wallMassRatio;
            so.FindProperty("_damageIgnoreLayers").intValue = ignoreLayersMask;
            so.ApplyModifiedPropertiesWithoutUndo();

            return cfg;
        }
    }
}
