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
                brain.State         = AIState.Idle;
                brain.OffAxisTimer  = 0f;
                brain.StuckTimer    = 0f;
                brain.ApproachTimer = 0f;
                return new AIDecision(Vector2.zero, false, false);
            }

            // ── Геометрия ───────────────────────────────────────────────────────
            Vector3 toTarget = p.TargetPosition - p.SelfPosition;
            float   dist     = toTarget.magnitude;
            Vector3 dir      = dist > 0.001f ? toTarget / dist : p.SelfForward;

            float forwardDot = Vector3.Dot(p.SelfForward, dir);       // +1 = цель строго по курсу
            float crossY     = Vector3.Cross(p.SelfForward, dir).y;   // положительное = цель справа
            float steer      = ClampSigned(crossY * profile.SteerSharpness);

            // ── Отсчёт grace (окно обязательного движения вперёд после отката) ──
            if (brain.GraceTimer > 0f)
                brain.GraceTimer -= deltaTime;

            // ── Опциональное отступление (обходит FSM) ──────────────────────────
            // У профиля Aggressor RetreatHpThreshold = 0 → не срабатывает никогда.
            if (profile.RetreatHpThreshold > 0f && p.SelfHpRatio < profile.RetreatHpThreshold
                && brain.State != AIState.Recover)
            {
                EnterRecover(ref brain, profile);
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
            if (brain.GraceTimer <= 0f && engaged
                && (brain.StuckTimer    >= profile.StuckTime
                 || brain.OffAxisTimer  >= profile.CircleBreakTime
                 || brain.ApproachTimer >= profile.ApproachTime))
            {
                EnterRecover(ref brain, profile);
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
                        return Drive(steer, profile.NitroOnRam);
                    }
                    return Drive(steer, false);

                case AIState.Ram:
                    // → Pursue: цель ушла (гистерезис RamExit > RamTrigger)
                    if (dist > profile.RamExitDistance)
                    {
                        brain.State = AIState.Pursue;
                        return Drive(steer, false);
                    }
                    // Nitro только когда реально нацелились на цель — буст вне курса
                    // только расширяет радиус поворота и подкармливает орбиту.
                    return Drive(steer, profile.NitroOnRam && forwardDot > profile.RamAlignmentDot);

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

        private static void EnterRecover(ref AIBrainState brain, EnemyPersonalityConfig p)
        {
            brain.State         = AIState.Recover;
            brain.RecoverTimer  = p.RecoverDuration;
            brain.StuckTimer    = 0f;
            brain.OffAxisTimer  = 0f;
            brain.ApproachTimer = 0f;
        }

        /// <summary>Зажимает в [-1, 1] через System.Math (а не Mathf), по прецеденту DamageService.</summary>
        private static float ClampSigned(float value) =>
            (float)Math.Max(-1.0, Math.Min(1.0, (double)value));
    }
}
