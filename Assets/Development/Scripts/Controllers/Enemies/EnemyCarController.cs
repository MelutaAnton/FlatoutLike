using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Input;
using FlatOutLike.Data.Configs.AI;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using FlatOutLike.Services.AI;
using FlatOutLike.Services.Combat;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Enemies
{
    /// <summary>
    /// Гоняет AI FSM-тик каждый FixedUpdate и пушит получившийся ввод в соседний
    /// <see cref="CarController"/> через <see cref="AIInputProvider"/>.
    ///
    /// Замечание про порядок: EnemyCarController пишет в <c>_aiInput</c> в своём FixedUpdate;
    /// CarController читает эти значения в своём FixedUpdate. Unity не гарантирует порядок
    /// выполнения компонентов на разных GameObject'ах, поэтому возможна задержка до 1 физ-кадра —
    /// незаметная на 50 Hz и приемлемая для аркадного AI.
    /// </summary>
    // Инвариант: этот компонент, его CarController (_car) и Rigidbody (_rigidbody)
    // живут на ОДНОМ GameObject. Perception считается из transform'а _car, чтобы AI
    // рассуждал об управляемой машине, а не о transform'е этого компонента.
    public sealed class EnemyCarController : MonoBehaviour
    {
        [SerializeField] private CarController          _car;         // соседняя машина, которой управляем
        [SerializeField] private CarController          _target;      // текущая цель (кэш; обновляется сервисом выбора цели)
        [SerializeField] private CarStatsConfig         _statsConfig; // зарезервировано под per-enemy override'ы статов
        [SerializeField] private EnemyPersonalityConfig _profile;
        [SerializeField] private AISensorConfig         _sensorConfig;
        [SerializeField] private Rigidbody              _rigidbody;

        [Header("Diagnostics")]
        // Печать состояния AI в Console каждые _diagInterval секунд. Используется
        // для отладки wedge-сценариев — видно state, таймеры, скорость, дистанции
        // лучей и итоговое решение. В релизной сборке выключить.
        [SerializeField] private bool  _diagEnabled  = true;
        [SerializeField] private float _diagInterval = 0.25f;
        private float _diagTimer;

        private IEnemyAIService         _ai;
        private IDamageService          _damage;
        private ITargetSelectionService _targetSelection;
        private AIInputProvider         _aiInput;
        private AIBrainState            _brain;
        private float                   _targetSwitchTimer;
        private bool                    _initialized;

        // Буфер для RaycastNonAlloc, чтобы не аллоцировать массив каждый FixedUpdate.
        // 8 хитов — с запасом: путь луча обычно пересекает 1-2 коллайдера (свой кузов
        // + одна стена). Если в сцене будет частокол объектов, увеличить.
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        [Inject]
        public void Construct(IEnemyAIService ai, IDamageService damage, ITargetSelectionService targetSelection)
        {
            _ai              = ai;
            _damage          = damage;
            _targetSelection = targetSelection;

            _aiInput = new AIInputProvider();
            _brain   = new AIBrainState { State = AIState.Idle, RecoverTimer = 0f };

            // Подменяем keyboard-провайдер на записываемый AI-провайдер.
            // OnEnable мог уже отработать до Construct — guard внутри CarController
            // здесь не релевантен, потому что вызываем публичный API напрямую.
            if (_car != null)
                _car.SetInputSource(_aiInput);

            _initialized = true;
        }

        /// <summary>
        /// Задаёт профиль и статы до инъекции VContainer.
        /// Вызывается фабрикой до InjectGameObject, чтобы Construct читал актуальные значения.
        /// Sensor config (_sensorConfig) остаётся из prefab — не переопределяется.
        /// Цель больше не задаётся здесь: её выбирает <see cref="ITargetSelectionService"/> в FixedUpdate.
        /// </summary>
        public void Configure(EnemyPersonalityConfig profile, CarStatsConfig stats)
        {
            _profile     = profile;
            _statsConfig = stats;
        }

        private void FixedUpdate()
        {
            if (!_initialized || _car == null || _profile == null || _rigidbody == null || _sensorConfig == null) return;

            // ── Обновление цели по кулдауну ──────────────────────────────────────
            // Первый вызов: _targetSwitchTimer = 0 → условие сразу истинно, цель выбирается мгновенно.
            // Далее — раз в TargetSwitchCooldown секунд или при гибели текущей цели.
            _targetSwitchTimer -= Time.fixedDeltaTime;
            bool targetDead = _target != null && _damage.GetHp(_target.CarId) <= 0f;
            if (_targetSwitchTimer <= 0f || _target == null || targetDead)
            {
                _target            = _targetSelection.SelectTarget(_car, _profile.TargetSelectionMode);
                _targetSwitchTimer = _profile.TargetSwitchCooldown;
            }

            bool hasTarget = _target != null;

            // ── Своё HP-соотношение ──────────────────────────────────────────────
            float selfMaxArmor = _car.MaxArmor;
            float selfHp       = _damage.GetHp(_car.CarId);
            float selfRatio    = selfMaxArmor > 0f ? selfHp / selfMaxArmor : 1f;

            // ── HP-соотношение цели ──────────────────────────────────────────────
            float targetRatio = 1f;
            if (hasTarget)
            {
                float targetMaxArmor = _target.MaxArmor;
                float targetHp       = _damage.GetHp(_target.CarId);
                targetRatio = targetMaxArmor > 0f ? targetHp / targetMaxArmor : 1f;
            }

            // ── Скорость вперёд (Unity 6: linearVelocity, НЕ legacy .velocity) ───
            // Perception использует _car.transform, чтобы AI рассуждал об управляемой машине.
            Vector3 selfPos = _car.transform.position;
            Vector3 selfFwd = _car.transform.forward;
            float selfSpeed = _rigidbody != null ? Vector3.Dot(_rigidbody.linearVelocity, selfFwd) : 0f;

            // ── Рейкасты для обхода препятствий ──────────────────────────────────
            // 5 лучей из центра машины: центральный forward + два под углом + два
            // перпендикулярных в стороны. CastDistance отсеивает свой Rigidbody,
            // так что origin может быть внутри собственного коллайдера. Service
            // сам решит, включать ли swerve / side-clamp, сравнив с порогами.
            Vector3 rayOrigin    = selfPos + Vector3.up * _sensorConfig.RayOriginUp;
            Vector3 dirLeftFwd   = Quaternion.AngleAxis(-_sensorConfig.RaySideAngle, Vector3.up) * selfFwd;
            Vector3 dirRightFwd  = Quaternion.AngleAxis( _sensorConfig.RaySideAngle, Vector3.up) * selfFwd;
            Vector3 selfRight    = _car.transform.right;

            float distCenter    = CastDistance(rayOrigin,  selfFwd,     _sensorConfig.RayLength);
            float distLeft      = CastDistance(rayOrigin,  dirLeftFwd,  _sensorConfig.RayLength);
            float distRight     = CastDistance(rayOrigin,  dirRightFwd, _sensorConfig.RayLength);
            float distSideLeft  = CastDistance(rayOrigin, -selfRight,   _sensorConfig.SideRayLength);
            float distSideRight = CastDistance(rayOrigin,  selfRight,   _sensorConfig.SideRayLength);

            // ── Собираем снапшот perception ──────────────────────────────────────
            var perception = new AIPerception(
                hasTarget:                 hasTarget,
                selfPosition:              selfPos,
                selfForward:               selfFwd,
                selfSpeed:                 selfSpeed,
                selfHpRatio:               selfRatio,
                targetPosition:            hasTarget ? _target.transform.position : Vector3.zero,
                targetHpRatio:             targetRatio,
                obstacleCenterDistance:    distCenter,
                obstacleLeftDistance:      distLeft,
                obstacleRightDistance:     distRight,
                obstacleSideLeftDistance:  distSideLeft,
                obstacleSideRightDistance: distSideRight
            );

            // ── AI-тик ───────────────────────────────────────────────────────────
            AIDecision decision = _ai.Tick(ref _brain, perception, _profile, Time.fixedDeltaTime);

            // ── Пушим результат в источник ввода, который читает CarController ───
            _aiInput.Set(decision.Move, decision.Handbrake, decision.Nitro);

            // ── Диагностика (раз в _diagInterval секунд) ─────────────────────────
            if (_diagEnabled)
            {
                _diagTimer -= Time.fixedDeltaTime;
                if (_diagTimer <= 0f)
                {
                    _diagTimer = _diagInterval;
                    string ds(float d) => d > 100f ? "∞" : d.ToString("F1");
                    Debug.Log(
                        $"[AI] state={_brain.State} hasTarget={hasTarget} " +
                        $"speed={selfSpeed:F2} dist={(hasTarget ? Vector3.Distance(selfPos, _target.transform.position).ToString("F1") : "-")}" +
                        $" | rays: C={ds(distCenter)} L={ds(distLeft)} R={ds(distRight)} sL={ds(distSideLeft)} sR={ds(distSideRight)}" +
                        $" | timers: stuck={_brain.StuckTimer:F2} off={_brain.OffAxisTimer:F2} appr={_brain.ApproachTimer:F2} grace={_brain.GraceTimer:F2} rec={_brain.RecoverTimer:F2}" +
                        $" | bias={_brain.PostRecoverSteerBias:F2}" +
                        $" | OUT steer={decision.Move.x:F2} throttle={decision.Move.y:F2} nitro={decision.Nitro}");
                }
            }
        }

        /// <summary>
        /// Возвращает дистанцию до ближайшего НЕ-СВОЕГО хита по лучу или float.MaxValue,
        /// если чисто. Хиты, чей attachedRigidbody == собственного Rigidbody, отсеиваются —
        /// это позволяет стартовать луч из центра машины без боязни задеть свой кузов.
        /// Триггеры игнорируются — пикапы и зоны эффектов в сканер обхода не входят.
        /// </summary>
        private float CastDistance(Vector3 origin, Vector3 direction, float length)
        {
            int n = Physics.RaycastNonAlloc(origin, direction, _hitBuffer, length,
                                            _sensorConfig.ObstacleLayerMask, QueryTriggerInteraction.Ignore);
            float minDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.collider.attachedRigidbody == _rigidbody) continue;
                if (hit.distance < minDist) minDist = hit.distance;
            }
            return minDist;
        }
    }
}
