using System;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using UnityEngine; // только математика Vector2/Vector3 — никаких MonoBehaviour/GameObject/Transform

namespace FlatOutLike.Services.AI
{
    /// <summary>
    /// Stateless FSM-мозг AI для машин врагов.
    ///
    /// Машина состояний: Idle → Pursue → Ram → Recover → Pursue → …
    ///
    /// Философия движения: ПОЛНЫЙ газ во время боя. Минимальный радиус поворота машины
    /// задаётся геометрией рулевого управления, поэтому замедление НЕ позволяет вписаться
    /// в более крутую дугу — вместо этого орбиту разрывает явный откат (Recover): как только
    /// машина оказалась близко к цели, но не могла прицелиться в неё в течение CircleBreakTime
    /// секунд (OffAxisTimer), или застряла на геометрии в течение StuckTime секунд
    /// (StuckTimer), она едет назад, чтобы оторваться и развернуть нос на цель, после чего
    /// делает новый прямой заход. Окно GraceTime после каждого Recover заставляет машину
    /// двигаться вперёд без откатов — чтобы она не дёргалась туда-сюда на месте.
    ///
    /// Все изменяемые данные лежат в <see cref="AIBrainState"/> (передаётся ref вызывающим).
    /// Pure C# — без MonoBehaviour и Unity lifecycle-методов.
    /// </summary>
    public sealed class EnemyAIService : IEnemyAIService
    {
        // Порог |PostRecoverSteerBias| / bias, выше которого руль считается «заданным».
        private const float SteerBiasThreshold = 0.5f;
        // Порог газа, выше которого нитро допустимо (газ не сброшен при side-stuck).
        private const float NitroThrottleThreshold = 0.5f;
        // Допуск равенства engageSteer и steer (обхода препятствия нет).
        private const float SteerEqualityEpsilon = 0.01f;

        public AIDecision Tick(ref AIBrainState brain, in AIPerception p,
            EnemyPersonalityConfig profile, float deltaTime)
        {
            // ── Нет цели → Idle и стоп ──────────────────────────────────────────
            if (!p.HasTarget)
            {
                brain.State                = AIState.Idle;
                brain.OffAxisTimer         = 0f;
                brain.StuckTimer           = 0f;
                brain.ApproachTimer        = 0f;
                brain.PostRecoverSteerBias = 0f;
                return new AIDecision(Vector2.zero, false, false);
            }

            // ── Геометрия ───────────────────────────────────────────────────────
            Vector3 toTarget = p.TargetPosition - p.SelfPosition;
            float   dist     = toTarget.magnitude;
            Vector3 dir      = dist > 0.001f ? toTarget / dist : p.SelfForward;

            float forwardDot = Vector3.Dot(p.SelfForward, dir);       // +1 = цель строго по курсу
            float crossY     = Vector3.Cross(p.SelfForward, dir).y;   // положительное = цель справа
            float steer      = ClampSigned(crossY * profile.SteerSharpness);

            // ── Боковая защита (side-clamp на raw steer) ────────────────────────
            // Перпендикулярные side-лучи ловят стену вплотную к боку. Раздельно
            // от active side-push (ниже): здесь МЯГКИЙ clamp на raw `steer` —
            // запрещаем target-steer заворачивать в блокированный бок. Это нужно
            // для безопасности Recover: TickRecover вызывает Reverse(steer), и
            // если steer тянет в стену, реверс уведёт нос туда же. Active push
            // на raw steer применять НЕЛЬЗЯ — реверс с принудительным разворотом
            // в противоположную сторону может загнать в стену сзади/спереди.
            bool leftSideBlocked  = p.ObstacleSideLeftDistance  < profile.ObstacleSideAvoidDistance;
            bool rightSideBlocked = p.ObstacleSideRightDistance < profile.ObstacleSideAvoidDistance;
            if (leftSideBlocked  && steer < 0f) steer = 0f;
            if (rightSideBlocked && steer > 0f) steer = 0f;

            // ── Обход препятствий ───────────────────────────────────────────────
            // Если центральный forward-луч задел что-то ближе порога И этот хит
            // ЯВНО не сама цель (дистанция меньше distToTarget с запасом) —
            // переопределяем steer в сторону более чистого бокового луча. Газ
            // не трогаем — машина идёт полным газом, по диагонали огибает стену.
            //
            // Гейт «obstacle closer than target by margin» заменил старый
            // `dist > RamTriggerDistance`: тот выключал avoidance, когда игрок
            // был ближе 12 м, и AI на близкой дистанции просто чесал target-axis
            // в стену между собой и игроком. Новый гейт срабатывает в любом
            // радиусе, кроме случая «центральный луч попал в самого игрока».
            // Чистый layer-exclude машин (Step 5) сделает это абсолютно надёжным.
            //
            // В Recover steer применяется через Reverse(steer) — там нужна target-axis,
            // иначе реверс уведёт в стену сбоку; поэтому avoidance считается ТОЛЬКО для
            // engageSteer и применяется только в Pursue/Ram switch-ветках.
            //
            // Picker учитывает side-блоки: бок занят = запрет свернуть в ту сторону.
            // Sandwich (оба бока + центр блокированы) → engageSteer=0, прямо в стену
            // → StuckTimer вытянет Recover, реверс выйдет ровно по target-axis.
            const float TargetHitMargin = 3f; // см. комментарий выше
            // Гейт проверяет ВСЕ три forward-луча (C, L, R), а не только C.
            // Иначе угловой wedge: нос машины прошёл кромку стены → C очистился →
            // gate выключал avoidance, хотя L-луч (под 30°) всё ещё видел стену
            // в 2-3 м. AI считал путь свободным и рулил target-axis обратно
            // в стену. Перпендикулярные side-лучи (sL/sR) короткие и под 90°,
            // в мёртвой зоне 30°-90° от forward стену не видят — поэтому угловые
            // forward-лучи единственный сигнал в этой геометрии.
            bool centerBlocked = p.ObstacleCenterDistance < profile.ObstacleAvoidDistance
                              && p.ObstacleCenterDistance < dist - TargetHitMargin;
            bool leftFwdBlocked  = p.ObstacleLeftDistance  < profile.ObstacleAvoidDistance
                                && p.ObstacleLeftDistance  < dist - TargetHitMargin;
            bool rightFwdBlocked = p.ObstacleRightDistance < profile.ObstacleAvoidDistance
                                && p.ObstacleRightDistance < dist - TargetHitMargin;
            bool obstacleBlocksPath = centerBlocked || leftFwdBlocked || rightFwdBlocked;

            float engageSteer = steer;
            if (obstacleBlocksPath)
            {
                if      (leftSideBlocked  && !rightSideBlocked) engageSteer =  1f;
                else if (rightSideBlocked && !leftSideBlocked)  engageSteer = -1f;
                else if (leftSideBlocked  &&  rightSideBlocked) engageSteer =  0f;
                else engageSteer = (p.ObstacleLeftDistance >= p.ObstacleRightDistance) ? -1f : 1f;
            }

            // Post-Recover bias: пока крутится GraceTimer, ПРИНУДИТЕЛЬНО держим тот
            // swerve, который выбрали в момент входа в Recover. Без этого после
            // реверса AI пересчитывает target-axis (target всё там же, за стеной)
            // и тут же возвращается в ту же стену → цикл «вперёд-назад-вперёд».
            // Бэйас перебивает target-steer, но не avoidance: если по ходу
            // grace-окна центральный луч снова показывает препятствие, picker
            // выше уже подкорректировал engageSteer на актуальное направление.
            if (brain.GraceTimer > 0f && Math.Abs(brain.PostRecoverSteerBias) > SteerBiasThreshold
                && !obstacleBlocksPath)
                engageSteer = brain.PostRecoverSteerBias;

            // ── Active side-push (финальный override engageSteer) ───────────────
            // Симметрично active-обходу forward'ом, но для бокового контакта.
            // Старый soft clamp `engageSteer→0` не помогал на кромке стены:
            // машина шла прямо вдоль угла, борт тёрся, forward-скорость падала
            // → StuckTimer → Recover → grace → снова прямо вдоль кромки → loop.
            // Принудительный руль ПРОЧЬ от трущегося борта превращает «трение
            // вдоль» в «дугу отрыва за один тик» — машина активно срывается с
            // угла. Перебивает и target-axis, и picker, и post-Recover bias:
            // если бок упёрся, всё остальное не имеет значения, нужно сначала
            // оторваться. Sandwich (оба бока) → 0; рулём не разрулить, FSM
            // вытянет Recover через StuckTimer.
            if (leftSideBlocked && rightSideBlocked)
                engageSteer = 0f;
            else if (leftSideBlocked)
                engageSteer =  1f;
            else if (rightSideBlocked)
                engageSteer = -1f;

            // ── Throttle release при side-stuck ──────────────────────────────────
            // Корневая физика wedge'а: на полном газу wall friction жёстко прижимает
            // кузов к стене, никакое изменение wheel angle его не развернёт — сила
            // тяги через wheels трансформируется в нормальную реакцию стены, а не
            // в боковой импульс. Active push выше выкручивает руль, но кузов стоит.
            // Если бок прижат И forward-скорость почти нулевая → сбрасываем газ в 0.
            // Без forward force wall friction отпускает; те же wheels с уже выкрученным
            // углом начинают физически разворачивать кузов, пока он катится по инерции.
            // Это работает БЫСТРЕЕ, чем StuckTimer → Recover (тот ждёт 0.6с, за это
            // время AI снова вдавливается). Если разгрузки не хватит и машина всё-таки
            // встанет — StuckTimer всё равно дотащит Recover как safety net.
            float forwardThrottle = 1f;
            bool sideTouching = leftSideBlocked || rightSideBlocked;
            if (sideTouching && Math.Abs(p.SelfSpeed) < profile.RecoverTriggerSpeed)
                forwardThrottle = 0f;

            // ── Отсчёт grace (окно обязательного движения вперёд после отката) ──
            // На истечении grace сбрасываем PostRecoverSteerBias — после окна
            // commit'а машина возвращается к нормальному target-axis steer.
            if (brain.GraceTimer > 0f)
            {
                brain.GraceTimer -= deltaTime;
                if (brain.GraceTimer <= 0f) brain.PostRecoverSteerBias = 0f;
            }

            // ── Опциональное отступление (обходит FSM) ──────────────────────────
            // У профиля Aggressor RetreatHpThreshold = 0 → не срабатывает никогда.
            // Bias = 0: отступление по HP — не обход стены, никакого направления
            // огибать нет, после реверса нормально пересчитываем target-axis.
            if (profile.RetreatHpThreshold > 0f && p.SelfHpRatio < profile.RetreatHpThreshold
                && brain.State != AIState.Recover)
            {
                EnterRecover(ref brain, profile, 0f);
                return Reverse(steer);
            }

            // ── Глобальные триггеры отката (покрывают и Pursue, и Ram) ──────────
            bool engaged = brain.State == AIState.Pursue || brain.State == AIState.Ram;

            // Застряли: в бою всегда подаётся полный газ, поэтому близкая к нулю МОДУЛЬНАЯ
            // скорость однозначно означает упор в стену/препятствие. Используем |SelfSpeed|
            // (не знаковую), чтобы машина, едущая назад или отброшенная ударом, НЕ читалась
            // как застрявшая (никаких moonwalk-эффектов). Гейтится на GraceTimer<=0: в
            // grace-окне машина как раз разгоняется с нуля после реверса, и без гейта
            // StuckTimer успевает накопить StuckTime ещё ВНУТРИ grace, после чего триггер
            // отката срабатывает в ту же микросекунду, как grace заканчивается → качание
            // взад-вперёд без реального заезда.
            if (brain.GraceTimer <= 0f && engaged && Math.Abs(p.SelfSpeed) < profile.RecoverTriggerSpeed)
                brain.StuckTimer += deltaTime;
            else
                brain.StuckTimer = 0f;

            // Орбита: достаточно близко для боя, но не получается развернуть нос на цель.
            // Гейтится на GraceTimer<=0, чтобы окно после отката было ПОЛНЫМ заездом вперёд:
            // начинаем отсчёт до следующего отката только после истечения grace. Без этого
            // orbit-таймер копится во время grace и срабатывает сразу, как только grace
            // закончится, давая ритм «назад / короткий вперёд / назад».
            if (brain.GraceTimer <= 0f && engaged
                && dist <= profile.RamExitDistance && forwardDot < profile.RamAlignmentDot)
                brain.OffAxisTimer += deltaTime;
            else
                brain.OffAxisTimer = 0f;

            // Не сближаемся: широкая орбита, при которой OffAxisTimer не срабатывает
            // (дистанция > RamExitDistance), но машина по факту не приближается — едет
            // по дуге вокруг цели. Скорость сближения = forward-velocity, спроецированная
            // на направление к цели = SelfSpeed * forwardDot. Если ниже порога слишком
            // долго — откатываемся и пытаемся зайти заново уже не по касательной.
            float approachSpeed = p.SelfSpeed * forwardDot;
            if (brain.GraceTimer <= 0f && engaged && approachSpeed < profile.MinApproachSpeed)
                brain.ApproachTimer += deltaTime;
            else
                brain.ApproachTimer = 0f;

            // Запуск манёвра отката — подавлен внутри grace-окна.
            // Bias выбирает направление руля в grace-окне ПОСЛЕ реверса:
            //   • obstacleBlocksPath  → engageSteer (picker уже выбрал сторону
            //     огибания forward-препятствия) — сохраняем через grace.
            //   • leftSideBlocked     → +1 (стена слева, держим руль ВПРАВО).
            //   • rightSideBlocked    → -1 (стена справа, держим руль ВЛЕВО).
            //     Этот кейс закрывает «угловой wedge»: машина прошла кромку
            //     стены, нос чист, но бортом упёрлась в угол → active push
            //     корректно крутит engageSteer, но физика прижимает кузов;
            //     StuckTimer вытягивает Recover. Без side-bias после реверса
            //     target-axis рулит обратно в ту же стену → бесконечный цикл.
            //     С side-bias grace-окно держит руль ПРОЧЬ от стены 1.5с,
            //     машина успевает развернуть нос и обогнуть угол.
            //   • иначе — bias = 0 (затык не геометрический, target-axis ок).
            if (brain.GraceTimer <= 0f && engaged
                && (brain.StuckTimer    >= profile.StuckTime
                 || brain.OffAxisTimer  >= profile.CircleBreakTime
                 || brain.ApproachTimer >= profile.ApproachTime))
            {
                float bias;
                if      (obstacleBlocksPath) bias = engageSteer;
                else if (leftSideBlocked)    bias =  1f;
                else if (rightSideBlocked)   bias = -1f;
                else                         bias =  0f;

                EnterRecover(ref brain, profile, bias);
                // Реверс с bias (не target-axis), если bias задан: при LeftBlocked
                // bias=+1 → Reverse(+1)=(-1,-1) → колесо ВЛЕВО в реверсе → нос
                // отворачивает ВПРАВО, прочь от стены (передне-левый кейс wedge'а).
                // Симметрично для RightBlocked. Без этого реверс шёл по target-axis
                // (обычно ≈0 = прямо назад), машина откатывалась параллельно стене
                // и на grace-форварде сразу налетала на тот же угол.
                return Reverse(Math.Abs(bias) > SteerBiasThreshold ? bias : steer);
            }

            // ── FSM ─────────────────────────────────────────────────────────────
            switch (brain.State)
            {
                case AIState.Idle:
                    if (dist <= profile.PursueStartDistance)
                        brain.State = AIState.Pursue;
                    return new AIDecision(Vector2.zero, false, false);

                case AIState.Pursue:
                    // → Ram: близко и нацелились
                    if (dist <= profile.RamTriggerDistance && forwardDot > profile.RamAlignmentDot)
                    {
                        brain.State = AIState.Ram;
                        return Drive(engageSteer, forwardThrottle, profile.NitroOnRam);
                    }
                    return Drive(engageSteer, forwardThrottle, false);

                case AIState.Ram:
                    // → Pursue: цель ушла (гистерезис RamExit > RamTrigger)
                    if (dist > profile.RamExitDistance)
                    {
                        brain.State = AIState.Pursue;
                        return Drive(engageSteer, forwardThrottle, false);
                    }
                    // Nitro только когда реально нацелились на цель — буст вне курса
                    // только расширяет радиус поворота и подкармливает орбиту.
                    // Газ заглушаем при обходе стены (engageSteer ≠ steer): forwardDot
                    // к цели может быть выше порога, но фактически руль вывернут на стену.
                    // Также nitro отключаем когда forwardThrottle=0 (side-stuck) — какой
                    // буст при разгрузке газа.
                    bool aimed = forwardDot > profile.RamAlignmentDot
                              && Math.Abs(engageSteer - steer) < SteerEqualityEpsilon;
                    return Drive(engageSteer, forwardThrottle,
                                 profile.NitroOnRam && aimed && forwardThrottle > NitroThrottleThreshold);

                case AIState.Recover:
                    return TickRecover(ref brain, profile, steer, deltaTime);

                default:
                    return new AIDecision(Vector2.zero, false, false);
            }
        }

        // ── Recover ────────────────────────────────────────────────────────────
        /// <summary>
        /// Едем назад со встречным рулём: реверс с -steer разворачивает нос в сторону
        /// цели, пока машина отъезжает, набирая дистанцию для прямого захода.
        /// → Pursue, когда RecoverTimer истечёт, открывая GraceTime-окно фиксации курса.
        /// </summary>
        private static AIDecision TickRecover(ref AIBrainState brain, EnemyPersonalityConfig p,
            float steer, float dt)
        {
            brain.RecoverTimer -= dt;

            if (brain.RecoverTimer <= 0f)
            {
                brain.State         = AIState.Pursue;
                brain.GraceTimer    = p.GraceTime;
                brain.OffAxisTimer  = 0f;
                brain.StuckTimer    = 0f;
                brain.ApproachTimer = 0f;
                // Throttle=1 на выходной тик — следующий FixedUpdate войдёт в основной
                // Tick и пересчитает throttle через side-stuck-release, если нужно.
                return Drive(steer, 1f, false);
            }

            // Реверс с bias (если задан): bias направляет руль в реверсе так,
            // чтобы нос отворачивал прочь от стены (для side-stuck-wedge'а), а не
            // по target-axis. Без bias — обычная семантика Reverse(steer):
            // -steer разворачивает нос к цели во время отката.
            float reverseSteer = Math.Abs(brain.PostRecoverSteerBias) > SteerBiasThreshold
                ? brain.PostRecoverSteerBias
                : steer;
            return Reverse(reverseSteer);
        }

        // ── Хелперы ────────────────────────────────────────────────────────────
        private static AIDecision Drive(float steer, float throttle, bool nitro) =>
            new AIDecision(new Vector2(steer, throttle), false, nitro);

        private static AIDecision Reverse(float steer) =>
            new AIDecision(new Vector2(-steer, -1f), false, false);

        private static void EnterRecover(ref AIBrainState brain, EnemyPersonalityConfig p,
            float postRecoverBias)
        {
            brain.State                = AIState.Recover;
            brain.RecoverTimer         = p.RecoverDuration;
            brain.StuckTimer           = 0f;
            brain.OffAxisTimer         = 0f;
            brain.ApproachTimer        = 0f;
            brain.PostRecoverSteerBias = postRecoverBias;
        }

        /// <summary>Зажимает в [-1, 1] через System.Math (а не Mathf), по прецеденту DamageService.</summary>
        private static float ClampSigned(float value) =>
            (float)Math.Max(-1.0, Math.Min(1.0, (double)value));
    }
}
