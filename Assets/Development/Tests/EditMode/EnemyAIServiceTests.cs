using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using FlatOutLike.Services.AI;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты pure-C# мозга <see cref="EnemyAIService"/>.
    /// Проверяются структурные инварианты FSM — не конкретные числа,
    /// а переходы состояний и знак выходных инпутов.
    ///
    /// Тесты привязаны к публичным геттерам <see cref="EnemyPersonalityConfig"/>,
    /// поэтому переживут ретюнинг констант.
    /// </summary>
    [TestFixture]
    public class EnemyAIServiceTests
    {
        private IEnemyAIService _service;
        private EnemyPersonalityConfig _profile;

        [SetUp]
        public void SetUp()
        {
            _service = new EnemyAIService();
            // C#-дефолты задают профиль Aggressor (RetreatHpThreshold=0, NitroOnRam=true и т.д.)
            _profile = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profile);
        }

        // ── Хелперы ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Создаёт восприятие: цель строго по курсу (Vector3.forward), без препятствий.
        /// Используется как базовый набор — большинство тестов не нуждаются в боковых смещениях.
        /// </summary>
        private static AIPerception MakeForwardPerception(float dist, float selfSpeed = 5f, float selfHpRatio = 1f)
        {
            return new AIPerception(
                hasTarget:               true,
                selfPosition:            Vector3.zero,
                selfForward:             Vector3.forward,
                selfSpeed:               selfSpeed,
                selfHpRatio:             selfHpRatio,
                targetPosition:          Vector3.forward * dist,
                targetHpRatio:           1f,
                obstacleCenterDistance:  float.MaxValue,
                obstacleLeftDistance:    float.MaxValue,
                obstacleRightDistance:   float.MaxValue,
                obstacleSideLeftDistance:  float.MaxValue,
                obstacleSideRightDistance: float.MaxValue
            );
        }

        /// <summary>
        /// Прогоняет <paramref name="n"/> тиков и возвращает решение последнего тика.
        /// </summary>
        private AIDecision TickN(ref AIBrainState brain, AIPerception perception, float dt, int n)
        {
            AIDecision last = default;
            for (int i = 0; i < n; i++)
                last = _service.Tick(ref brain, perception, _profile, dt);
            return last;
        }

        // ── Тесты ───────────────────────────────────────────────────────────────

        // 1. Нет цели — Idle и нулевой ввод
        [Test]
        public void NoTarget_ReturnsIdleAndZeroMove()
        {
            var brain = new AIBrainState();
            var perception = new AIPerception(
                hasTarget:               false,
                selfPosition:            Vector3.zero,
                selfForward:             Vector3.forward,
                selfSpeed:               0f,
                selfHpRatio:             1f,
                targetPosition:          Vector3.zero,
                targetHpRatio:           0f,
                obstacleCenterDistance:  float.MaxValue,
                obstacleLeftDistance:    float.MaxValue,
                obstacleRightDistance:   float.MaxValue,
                obstacleSideLeftDistance:  float.MaxValue,
                obstacleSideRightDistance: float.MaxValue
            );

            var decision = _service.Tick(ref brain, perception, _profile, 0.1f);

            Assert.AreEqual(AIState.Idle, brain.State,
                "Без цели FSM должен оставаться в Idle");
            Assert.AreEqual(Vector2.zero, decision.Move,
                "Без цели ввод движения должен быть нулевым");
        }

        // 2. Idle + цель в пределах PursueStartDistance → вход в бой
        [Test]
        public void Idle_TargetWithinPursueRange_TransitionsToCombat()
        {
            var brain = new AIBrainState { State = AIState.Idle };
            // Цель на середине зоны преследования, строго по курсу
            float dist = _profile.PursueStartDistance * 0.5f;
            var perception = MakeForwardPerception(dist: dist, selfSpeed: 5f);

            _service.Tick(ref brain, perception, _profile, 0.1f);

            bool enteredCombat = brain.State == AIState.Pursue || brain.State == AIState.Ram;
            Assert.IsTrue(enteredCombat,
                $"Из Idle при цели в {dist:F1} м должен перейти в Pursue или Ram, фактически: {brain.State}");
        }

        // 3. Pursue + близкая прицеленная цель → Ram, газ вперёд
        [Test]
        public void Pursue_CloseAlignedTarget_TransitionsToRam_WithForwardThrottle()
        {
            var brain = new AIBrainState { State = AIState.Pursue };
            // Цель строго по курсу внутри RamTriggerDistance
            float dist = _profile.RamTriggerDistance * 0.67f;
            var perception = MakeForwardPerception(dist: dist, selfSpeed: 5f);

            var decision = _service.Tick(ref brain, perception, _profile, 0.1f);

            Assert.AreEqual(AIState.Ram, brain.State,
                "В Pursue при близкой прицеленной цели должен войти в Ram");
            Assert.Greater(decision.Move.y, 0f,
                "В Ram должна быть подача газа вперёд (Move.y > 0)");
        }

        // 4. Застревание: низкая скорость в бою дольше StuckTime → Recover + реверс
        [Test]
        public void Engaged_LowSpeedLongerThanStuckTime_EntersRecover()
        {
            const float dt = 0.1f;
            var brain = new AIBrainState { State = AIState.Pursue, GraceTimer = 0f };
            // Цель за пределами RamTriggerDistance, чтобы не уйти в Ram сразу
            float dist       = _profile.RamTriggerDistance * 1.25f;
            float slowSpeed  = _profile.RecoverTriggerSpeed * 0.05f; // заметно ниже порога
            var perception   = MakeForwardPerception(dist: dist, selfSpeed: slowSpeed);

            // StuckTimer накапливается на каждом тике; +2 тика сверх минимума для надёжности
            int ticks = Mathf.CeilToInt(_profile.StuckTime / dt) + 2;
            AIDecision last = TickN(ref brain, perception, dt, ticks);

            Assert.AreEqual(AIState.Recover, brain.State,
                $"После {ticks} тиков с низкой скоростью в бою должен войти в Recover");
            Assert.Less(last.Move.y, 0f,
                "В Recover Move.y должен быть отрицательным (реверс)");
        }

        // 5. Орбита: машина рядом с целью, но не может прицелиться дольше CircleBreakTime → Recover
        [Test]
        public void Engaged_OrbitNearTargetLongerThanCircleBreakTime_EntersRecover()
        {
            const float dt = 0.1f;
            var brain = new AIBrainState { State = AIState.Pursue, GraceTimer = 0f };

            // Цель смещена вправо: dist ≈ 10.77 м (≤ RamExitDistance=18), forwardDot ≈ 0.37 (< RamAlignmentDot=0.5).
            // При этом условие Pursue → Ram: dist<=12 && forwardDot>0.5 — НЕ выполняется (forwardDot=0.37 < 0.5).
            // approachSpeed = 5f * 0.37 ≈ 1.85 > MinApproachSpeed=1 → ApproachTimer не копится.
            var perception = new AIPerception(
                hasTarget:               true,
                selfPosition:            Vector3.zero,
                selfForward:             Vector3.forward,
                selfSpeed:               5f,
                selfHpRatio:             1f,
                targetPosition:          new Vector3(10f, 0f, 4f),
                targetHpRatio:           1f,
                obstacleCenterDistance:  float.MaxValue,
                obstacleLeftDistance:    float.MaxValue,
                obstacleRightDistance:   float.MaxValue,
                obstacleSideLeftDistance:  float.MaxValue,
                obstacleSideRightDistance: float.MaxValue
            );

            // OffAxisTimer накапливается каждый тик; +1 тик запаса
            int ticks = Mathf.CeilToInt(_profile.CircleBreakTime / dt) + 1;
            TickN(ref brain, perception, dt, ticks);

            Assert.AreEqual(AIState.Recover, brain.State,
                $"После {ticks} тиков орбиты вблизи цели должен войти в Recover");
        }

        // 6. В Recover при положительном RecoverTimer → реверс (Move.y < 0)
        [Test]
        public void InRecover_PositiveTimer_ReturnsReverseMove()
        {
            const float dt = 0.1f;
            var brain = new AIBrainState
            {
                State        = AIState.Recover,
                RecoverTimer = _profile.RecoverDuration, // долго до истечения
            };
            var perception = MakeForwardPerception(dist: 15f, selfSpeed: 0f);

            var decision = _service.Tick(ref brain, perception, _profile, dt);

            Assert.Less(decision.Move.y, 0f,
                "В Recover при положительном RecoverTimer должен ехать назад (Move.y < 0)");
        }

        // 7. RecoverTimer истёк → Pursue + открылось GraceTimer-окно
        [Test]
        public void InRecover_TimerExpired_TransitionsToPursueWithOpenGraceWindow()
        {
            const float dt = 0.1f;
            // RecoverTimer меньше одного тика — истечёт на этом же тике
            var brain = new AIBrainState
            {
                State        = AIState.Recover,
                RecoverTimer = dt * 0.5f,
            };
            var perception = MakeForwardPerception(dist: 15f, selfSpeed: 0f);

            _service.Tick(ref brain, perception, _profile, dt);

            Assert.AreEqual(AIState.Pursue, brain.State,
                "После истечения RecoverTimer должен вернуться в Pursue");
            Assert.Greater(brain.GraceTimer, 0f,
                "После Recover должно открыться GraceTimer > 0 (защита от немедленного повторного отката)");
        }

        // 8. Grace подавляет обнаружение застревания
        [Test]
        public void GraceTimer_WhileActive_BlocksStuckDetection()
        {
            const float dt = 0.1f;
            var brain = new AIBrainState
            {
                State      = AIState.Pursue,
                GraceTimer = _profile.GraceTime, // долгое grace-окно
            };
            // Низкая скорость — без grace это запустило бы StuckTimer
            float slowSpeed = _profile.RecoverTriggerSpeed * 0.05f;
            var perception  = MakeForwardPerception(dist: 15f, selfSpeed: slowSpeed);

            // Тиков намеренно больше, чем потребовалось бы без grace (StuckTime / dt)
            int ticks = Mathf.CeilToInt(_profile.StuckTime / dt) + 2;
            TickN(ref brain, perception, dt, ticks);

            Assert.AreNotEqual(AIState.Recover, brain.State,
                "Активный GraceTimer должен блокировать StuckTimer и не давать войти в Recover");
        }

        // 9. Moonwalk-guard: большая отрицательная скорость не читается как застревание
        [Test]
        public void NegativeSelfSpeed_LargeAbsoluteValue_NotTreatedAsStuck()
        {
            const float dt = 0.1f;
            var brain = new AIBrainState { State = AIState.Pursue, GraceTimer = 0f };

            // Цель за пределами RamExitDistance: OffAxisTimer не копится
            float dist = _profile.RamExitDistance + 5f;
            // Большая отрицательная скорость: |SelfSpeed| >> RecoverTriggerSpeed → NOT stuck
            // Stuck-детект использует Math.Abs(SelfSpeed), а не знаковую скорость
            float negSpeed = -(_profile.RecoverTriggerSpeed + 10f);
            var perception = MakeForwardPerception(dist: dist, selfSpeed: negSpeed);

            // Прогоняем больше тиков, чем StuckTime / dt — но |SelfSpeed| большой → StuckTimer должен оставаться нулём
            // Примечание: ApproachTimer накапливается (approachSpeed = negSpeed * 1 < MinApproachSpeed),
            // поэтому тиков берём меньше, чем ApproachTime / dt, чтобы не задействовать ApproachTimer.
            int ticks = Mathf.CeilToInt(_profile.StuckTime / dt) + 2; // ~8 тиков; ApproachTime/dt=15
            TickN(ref brain, perception, dt, ticks);

            Assert.AreNotEqual(AIState.Recover, brain.State,
                "Большая отрицательная скорость (машину откинуло) не должна восприниматься как застревание (moonwalk-guard)");
        }
    }
}
