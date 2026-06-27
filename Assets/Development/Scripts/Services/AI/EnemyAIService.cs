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

            // ── Боковая защита (side-clamp) ─────────────────────────────────────
            // Перпендикулярные side-лучи ловят стену вплотную к боку. Если бок
            // занят — запрещаем target-steer заворачивать в ту сторону. Иначе
            // у края стены машина почти проехала, бок прилип к стене, цель тянет
            // обратно в стену → стук боком в угол → StuckTimer → Recover → тот же
            // вектор → опять стук. С клампом steer=0 машина проедет прямо вдоль
            // стены и оторвётся от угла.
            //
            // Клампится САМ steer (а не engageSteer), чтобы Reverse(steer) тоже
            // не разворачивал нос в боковую стену во время Recover.
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
            bool obstacleBlocksPath = p.ObstacleCenterDistance < profile.ObstacleAvoidDistance
                                   && p.ObstacleCenterDistance < dist - TargetHitMargin;

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
            if (brain.GraceTimer > 0f && Math.Abs(brain.PostRecoverSteerBias) > 0.5f
                && !obstacleBlocksPath)
                engageSteer = brain.PostRecoverSteerBias;

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
            // Bias = engageSteer если препятствие БЫЛО причиной (или подкравшейся
            // причиной) затыка — picker уже выбрал сторону огибания, и мы хотим
            // сохранить это направление через grace-окно после реверса.
            // Если затык не от стены (нет obstacleBlocksPath) — bias = 0, после
            // реверса нормальный target-axis.
            if (brain.GraceTimer <= 0f && engaged
                && (brain.StuckTimer    >= profile.StuckTime
                 || brain.OffAxisTimer  >= profile.CircleBreakTime
                 || brain.ApproachTimer >= profile.ApproachTime))
            {
                float bias = obstacleBlocksPath ? engageSteer : 0f;
                EnterRecover(ref brain, profile, bias);
                return Reverse(steer);
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
                        return Drive(engageSteer, profile.NitroOnRam);
                    }
                    return Drive(engageSteer, false);

                case AIState.Ram:
                    // → Pursue: цель ушла (гистерезис RamExit > RamTrigger)
                    if (dist > profile.RamExitDistance)
                    {
                        brain.State = AIState.Pursue;
                        return Drive(engageSteer, false);
                    }
                    // Nitro только когда реально нацелились на цель — буст вне курса
                    // только расширяет радиус поворота и подкармливает орбиту.
                    // Газ заглушаем при обходе стены (engageSteer ≠ steer): forwardDot
                    // к цели может быть выше порога, но фактически руль вывернут на стену.
                    bool aimed = forwardDot > profile.RamAlignmentDot
                              && Math.Abs(engageSteer - steer) < 0.01f;
                    return Drive(engageSteer, profile.NitroOnRam && aimed);

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
                return Drive(steer, false);
            }

            return Reverse(steer);
        }

        // ── Хелперы ────────────────────────────────────────────────────────────
        private static AIDecision Drive(float steer, bool nitro) =>
            new AIDecision(new Vector2(steer, 1f), false, nitro);

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
