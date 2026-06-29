---
name: project-ai-approachtimer-reverse-gap
description: EnemyAIService — moonwalk-guard (|SelfSpeed|) защищает только StuckTimer; ApproachTimer копится при отрицательной approachSpeed → знакобойный реверс может реверсить дальше. Watch в дерби шага 5
metadata:
  type: project
---

В `EnemyAIService.Tick` (Services/AI/EnemyAIService.cs) есть три триггера Recover: StuckTimer, OffAxisTimer, ApproachTimer.

**Факт:** moonwalk-guard (использование `Math.Abs(SelfSpeed)`) применён ТОЛЬКО к StuckTimer (~стр.184). ApproachTimer (~стр.205-209) считает `approachSpeed = SelfSpeed * forwardDot` со ЗНАКОМ. Если машину откинуло назад (SelfSpeed<0) при цели спереди (forwardDot>0), approachSpeed отрицательная → ApproachTimer копится; через ApproachTime (дефолт 1.5с) непрерывного несближения → Recover → реверс (Move.y=-1), т.е. машину, уже едущую назад, заставит реверсить дальше — ровно тот moonwalk-эффект, от которого StuckTimer защищён.

**Why это НЕ хард-баг:** нет бесконечного цикла — после Recover открывается GraceTime-окно (1.5с), внутри которого все три таймера гейтятся на `GraceTimer<=0` и не копятся, машина едет вперёд и восстанавливает сближение. Худший случай — самокорректирующийся «икающий» эпизод ~3с. Плюс полный газ в Pursue обычно гасит откидку за доли секунды до накопления 1.5с.

**How to apply:** в ревью шага 5 (дерби-свалка, частые откидывания на >1.5с) — если появятся жалобы на «врага, который зачем-то долго пятится», смотреть сюда. Возможный фикс: гейтить ApproachTimer на `SelfSpeed>0` или брать `Math.Abs(approachSpeed)` по аналогии со StuckTimer. EnemyAIService правильно НЕ трогается в пакете «фундамент+тесты»; тест 9 корректно ограничивает прогон 8 тиками (<ApproachTime/dt=15), чтобы не задеть эту ветку — это осознанное, а не замаскированный дефект теста.
