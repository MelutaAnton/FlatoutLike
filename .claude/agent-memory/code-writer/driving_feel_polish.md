---
name: driving-feel-polish
description: CarMovementService steering smoothing + handbrake drift + acceleration tuning (FlatOut-style arcade feel)
metadata:
  type: project
---

Реализован полишинг вождения (сглаживание руля, ручник-дрифт, резвость), см. [[project_architecture]] и [[step5a_pool_and_car_factory]] для базовой архитектуры Car-слоя.

## Что добавлено
- `ICarMovementService.CalculateSmoothedSteerAngle(currentAngle, steerInput, currentForwardSpeed, dt, stats)` — чистая функция без состояния. Считает target через существующий `CalculateSteerAngle` (clamp+speed-falloff), затем `Mathf.MoveTowards` к target: rate = `SteerReturnRate` если `Mathf.Abs(target) < Mathf.Abs(currentAngle)` (отпускание/смена стороны), иначе `SteerRate` (доворот). SteerReturnRate > SteerRate — возврат отзывчивее доворота на клавиатуре.
- Состояние `_currentSteerAngle` живёт в `CarController` (per-instance), НЕ в сервисе — важно, т.к. сервис Singleton и общий для игрока и всех AI-машин (`EnemyCarController` использует тот же `CarController.FixedUpdate`, отдельного AI-movement-пути нет).
- Ручник-дрифт: `CarController.UpdateHandbrakeFriction(bool)` переключает `sidewaysFriction.stiffness` только у `_driveWheels` (задних, привод) между `WheelFriction` и `WheelFriction * HandbrakeGripFactor`. Кэширует `_handbrakeActive`, пишет WheelFrictionCurve только при смене состояния (структура копируется по значению — писать каждый тик дорого и бессмысленно).
- `CalculateBrakeTorque(handbrake=true)` теперь возвращает `HandbrakeBrakeForce` (частичный тормоз) вместо полного `BrakeForce` — ручник даёт занос, а не блокировку.

## Новые поля CarStatsConfig (Data/Configs/Car/CarStatsConfig.cs)
`SteerRate` (300°/с), `SteerReturnRate` (520°/с), `HandbrakeGripFactor` (0.45, Range 0..1), `HandbrakeBrakeForce` (800). Тюнинг дефолтов на резвость: `Acceleration` 2000→3500, `EngineBrakeForce` 600→400 (600 у старого ассета фактически было 1000 — см. ниже), `SteerSpeedFalloff` 0.5→0.35, `TopSpeed` 25→30.

## КРИТИЧНЫЙ нюанс — "SO new-field-gap"
Единственный ассет `CarStatsConfig_Default.asset` (Assets/Development/Data/Configs/Car/) существует до добавления новых полей. Если просто добавить `[SerializeField]` в класс и не тронуть YAML — Unity десериализует существующий ассет и **новые поля останутся 0**, а не C#-дефолт класса (это не CreateInstance, это deserialize существующих данных). Прецедент уже был в проекте: `_nitroDrainRate` не попал в старый ассет и понадобился отдельный `FixCarStatsConfigEditor.cs` (`FlatOutLike/Setup/Fix CarStatsConfig NitroDrainRate`) чтобы патчить его через SerializedObject в рантайме/эдиторе.
Для этой задачи вместо editor-фикса ассет отредактирован НАПРЯМУЮ (YAML), т.к. заранее известны новые поля и их дефолты — так безопаснее и мгновенно, editor-скрипт не нужен когда меняешь текстовый YAML сам. `CreateDefaultCarStatsConfigEditor.cs` (`FlatOutLike/Configs/Create Default CarStatsConfig`) создаёт НОВЫЙ ассет через `CreateInstance` — там всегда корректные C#-дефолты, трогать не нужно.
Правило на будущее: **при добавлении полей в CarStatsConfig (или любой другой уже-заинстансированный SO) — всегда патчить существующие .asset файлы вручную (YAML) или через editor-скрипт**, иначе баланс/поведение тихо ломается нулями.

## EditMode-тесты
`Assets/Development/Tests/EditMode/CarMovementServiceTests.cs` — новый файл (репозиторий не имел тестов на CarMovementService до этого). Паттерн конфигов в тестах: `ScriptableObject.CreateInstance<CarStatsConfig>()` даёт корректные C#-дефолты (в отличие от старого ассета) — кастомные значения через `UnityEditor.SerializedObject` только если нужно отклониться от дефолта (см. `TargetSelectionServiceTests.MakeCar`).
