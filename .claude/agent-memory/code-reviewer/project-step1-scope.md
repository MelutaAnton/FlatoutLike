---
name: project-step1-scope
description: Шаг 1 «Каркас» из docs/game-design.md §19 — что должно быть в первом коммите проекта
metadata:
  type: project
---

Шаг 1 «Каркас» проекта (см. docs/game-design.md §19): только базовая инфраструктура, никакого геймплея.

**Что должно быть:**
- Models (enums + const-классы): GameState, RewardSource, HazardType, StuntType, UpgradeCategory, CarId, AnimatorHashes (заглушка), Tags, SceneKeys.
- Core: EventBus + IEventBus + Events.Flow + Events.Meta; IInputProvider + InputContext + InputRouter + KeyboardInputProvider; IState + StateMachine<TEnum>.
- Services/Flow: IGameFlowService + GameFlowService (с приватным EmptyState-stub'ом для всех GameState значений, потому что реальных стейтов ещё нет).
- Services/Meta: IPlayerProfile, PlayerProfile, IProfileStorage, PlayerPrefsProfileStorage, IMetaProgressService, MetaProgressService.
- Installers/GameLifetimeScope.cs.
- GameBootstrap.cs (единственный MonoBehaviour).
- .gitkeep файлы для пустых директорий слоёв.

**Чего быть НЕ должно:** CarController, DamageService, AI, ragdoll, ScriptableObject-конфигов, фабрик, UI views, Editor-скриптов. Editor-скрипты — отдельный agent, отдельный коммит.

**Why:** Шаг 1 заявлен как «минимальный каркас», на котором будут собираться шаги 2+. Смешивание с Editor-автоматизацией или с фрагментами геймплея нарушает изоляцию шагов.

**How to apply:** При ревью первого коммита/PR scope-шага 1 — любой файл вне списка выше отметить как нарушение scope.
