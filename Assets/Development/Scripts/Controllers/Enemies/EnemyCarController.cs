using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Input;
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
        [SerializeField] private CarController         _car;        // соседняя машина, которой управляем
        [SerializeField] private CarController         _target;     // машина игрока (переназначается через SetTarget)
        [SerializeField] private CarStatsConfig        _statsConfig; // зарезервировано под per-enemy override'ы статов
        [SerializeField] private EnemyPersonalityConfig _profile;
        [SerializeField] private Rigidbody             _rigidbody;

        [Header("Obstacle Avoidance")]
        // Подъём origin'а над землёй — чтобы лучи шли на уровне «груди» машины,
        // а не цепляли текстуру земли. Используется и для forward-, и для side-лучей.
        [SerializeField, Min(0f)]  private float     _obstacleRayOriginUp      = 0.5f;
        // Длина forward-сканирования (Service сам сравнит с ObstacleAvoidDistance).
        [SerializeField, Min(0f)]  private float     _obstacleRayLength        = 12f;
        // Угол отклонения боковых forward-лучей от forward (градусы).
        [SerializeField, Range(0f, 89f)] private float _obstacleRaySideAngle   = 30f;
        // Длина перпендикулярных side-лучей. Покрывает половину ширины машины + scan.
        [SerializeField, Min(0f)]  private float     _obstacleSideRayLength    = 2.5f;
        // Слои, которые считаются препятствиями. По умолчанию Everything (~0).
        // Сам коллайдер машины отсеивается через сравнение attachedRigidbody — поэтому
        // origin'у не нужен forward-offset; лучи стартуют из центра selfPos.
        [SerializeField] private LayerMask _obstacleLayerMask = ~0;

        private IEnemyAIService _ai;
        private IDamageService  _damage;
        private AIInputProvider _aiInput;
        private AIBrainState    _brain;
        private bool            _initialized;

        // Буфер для RaycastNonAlloc, чтобы не аллоцировать массив каждый FixedUpdate.
        // 8 хитов — с запасом: путь луча обычно пересекает 1-2 коллайдера (свой кузов
        // + одна стена). Если в сцене будет частокол объектов, увеличить.
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        [Inject]
        public void Construct(IEnemyAIService ai, IDamageService damage)
        {
            _ai     = ai;
            _damage = damage;

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
        /// Заменяет текущую цель. Вызывается Factory / targeting-сервисом (Step 5).
        /// Безопасно вызывать до Construct — target читается только внутри FixedUpdate.
        /// </summary>
        public void SetTarget(CarController target) => _target = target;

        private void FixedUpdate()
        {
            if (!_initialized || _car == null || _profile == null || _rigidbody == null) return;

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
            Vector3 rayOrigin    = selfPos + Vector3.up * _obstacleRayOriginUp;
            Vector3 dirLeftFwd   = Quaternion.AngleAxis(-_obstacleRaySideAngle, Vector3.up) * selfFwd;
            Vector3 dirRightFwd  = Quaternion.AngleAxis( _obstacleRaySideAngle, Vector3.up) * selfFwd;
            Vector3 selfRight    = _car.transform.right;

            float distCenter    = CastDistance(rayOrigin,  selfFwd,     _obstacleRayLength);
            float distLeft      = CastDistance(rayOrigin,  dirLeftFwd,  _obstacleRayLength);
            float distRight     = CastDistance(rayOrigin,  dirRightFwd, _obstacleRayLength);
            float distSideLeft  = CastDistance(rayOrigin, -selfRight,   _obstacleSideRayLength);
            float distSideRight = CastDistance(rayOrigin,  selfRight,   _obstacleSideRayLength);

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
                                            _obstacleLayerMask, QueryTriggerInteraction.Ignore);
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
