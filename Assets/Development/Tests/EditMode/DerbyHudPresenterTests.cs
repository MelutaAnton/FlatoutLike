using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
using FlatOutLike.Views.UI.Hud;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="DerbyHudPresenter"/> (Шаг 8d).
    ///
    /// DerbyHudPresenter конструируется от живого DerbyHudView — MonoBehaviour с SerializeField-
    /// ссылками на Image/TMP_Text, чей Update поднимает OnTick. Собрать корректный экземпляр
    /// в EditMode-тесте без сцены/префаба нельзя (тот же лимит, что и у GarageView, см.
    /// GaragePresenterTests) — заводить IView исключительно ради тестов запрещено заданием.
    ///
    /// Поэтому здесь тестируются ВЫЧИСЛИТЕЛЬНЫЕ ХЕЛПЕРЫ презентера — они публичные static
    /// и используют ровно ту же формулу, что HandleTick вызывает в рантайме:
    ///   • NormalizeHp — нормировка HP игрока против настоящего DamageService (GetHp/GetMaxArmor,
    ///     новый геттер, добавленный этим шагом);
    ///   • CountAlive — подсчёт живых по кэшу CarDestroyedEvent (альтернатива из задания,
    ///     выбранная в HandleTick вместо обхода ICarRegistry.Cars с GetHp>0 каждый тик).
    /// Реакции на события (SetVisible(false) по CarDestroyedEvent/DerbyMatchEndedEvent) требуют
    /// живого View и здесь не проверяются — см. класс-комментарий.
    /// </summary>
    [TestFixture]
    public class DerbyHudPresenterTests
    {
        private EventBus _bus;
        private DamageConfig _damageConfig;
        private DamageService _damage;

        private static readonly CarId Player = new CarId(0);
        private static readonly CarId Enemy = new CarId(1);

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _damageConfig = ScriptableObject.CreateInstance<DamageConfig>(); // дефолты: DamageScale=1, MinImpactSpeed=3, WallMassRatio=1
            _damage = new DamageService(_bus, _damageConfig);
        }

        [TearDown]
        public void TearDown()
        {
            _damage.Dispose();
            Object.DestroyImmediate(_damageConfig);
        }

        // ── NormalizeHp ────────────────────────────────────────────────────────

        [Test]
        public void NormalizeHp_FullHp_ReturnsOne()
        {
            _damage.RegisterCar(Player, maxArmor: 100f, mass: 1000f);

            float normalized = DerbyHudPresenter.NormalizeHp(_damage.GetHp(Player), _damage.GetMaxArmor(Player));

            Assert.AreEqual(1f, normalized, 0.001f);
        }

        [Test]
        public void NormalizeHp_AfterDamage_MatchesRatioOfMaxArmor()
        {
            _damage.RegisterCar(Player, maxArmor: 100f, mass: 1000f);
            _damage.RegisterCar(Enemy, maxArmor: 100f, mass: 1000f); // равные массы -> massRatio=1

            // damage = (impactSpeed - MinImpactSpeed) * massRatio * DamageScale = (13-3)*1*1 = 10
            _bus.Publish(new CarCollisionEvent(Enemy, Player, impactSpeed: 13f, point: Vector3.zero));

            Assert.AreEqual(90f, _damage.GetHp(Player), 0.001f, "Проверка предпосылки: урон должен списаться штатной формулой DamageService.");

            float normalized = DerbyHudPresenter.NormalizeHp(_damage.GetHp(Player), _damage.GetMaxArmor(Player));

            Assert.AreEqual(0.9f, normalized, 0.001f);
        }

        [Test]
        public void NormalizeHp_ZeroMaxArmor_DoesNotDivideByZero()
        {
            // Незарегистрированная машина -> GetMaxArmor возвращает 0 (см. DamageService.GetMaxArmor).
            float normalized = DerbyHudPresenter.NormalizeHp(hp: 0f, maxArmor: 0f);

            Assert.AreEqual(0f, normalized);
        }

        [Test]
        public void GetMaxArmor_UnregisteredCar_ReturnsZero()
        {
            Assert.AreEqual(0f, _damage.GetMaxArmor(Player));
        }

        // ── CountAlive ─────────────────────────────────────────────────────────

        [Test]
        public void CountAlive_SubtractsDestroyedFromTotal()
        {
            Assert.AreEqual(2, DerbyHudPresenter.CountAlive(totalRegistered: 3, destroyedCount: 1));
        }

        [Test]
        public void CountAlive_AllDestroyed_ReturnsZero()
        {
            Assert.AreEqual(0, DerbyHudPresenter.CountAlive(totalRegistered: 3, destroyedCount: 3));
        }

        [Test]
        public void CountAlive_NeverGoesNegative()
        {
            // Защитный случай (не должен происходить в рантайме) — не должен уйти в минус.
            Assert.AreEqual(0, DerbyHudPresenter.CountAlive(totalRegistered: 1, destroyedCount: 5));
        }
    }
}
