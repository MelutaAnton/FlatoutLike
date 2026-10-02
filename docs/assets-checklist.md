# Assets Checklist — FlatOutLike, Idle-Build слой (BaseHub)

> Чеклист подготовки контента для шагов 11–13 Implementation Order (§19 `game-design.md`): карта базы, стадийная стройка, idle-доход.
> **Дополнение** к основным пакам проекта — здесь только то, что нужно idle-слою.
>
> **Сверено с проектом 2026-10-03** (по `Assets/ThirdParty/` и `Assets/Development/`). Пометка *(проверить в Unity)* — найдено по файлам, но визуально/в рантайме не подтверждено.
>
> **Стиль:** stylized low-poly, палитра-матч с Battle Stadium.
> **Бюджет на слой:** закрыт — все нужные паки уже в проекте, докупать ничего не требуется.

---

## 0. Что уже есть в проекте (паки для idle-слоя)

| Пак (`Assets/ThirdParty/…`) | Чем полезен idle-слою |
|---|---|
| `Synty/PolygonConstruction` (584 префаба) | Стадии стройки: бетонные каркасы, арматура, плиты, кран, мешки, ограждения, барьеры, дорожные модули |
| `Synty/PolygonCity` (335 префабов) | Готовые здания (Garage, TicketBooth), декор улиц |
| `Synty/PolygonGeneric` (490 префабов) | Общий декор, пропсы |
| `Battle_Stadium` | Трибуны (`Tribunes`), миниатюра арены (`DerbyArena`) |
| `Junkyard_1` | Декор периметра (бочки, хлам) |
| `Arcade Idle Engine` | Пачка купюр (`Models/Cash/Cash_Model.fbx`, `Prefabs/Cash_Items/`), анимации `Idle`/`Runnig`/`Walking` (humanoid-кривые), текстуры `circle.png`/`CircleEmpty.png`/`UI_circle_mask.png`/`MoneyIcon.png`, звуки `CashCollect`/`cha-ching`/`Stamp` |
| `ArcadeIdleEngine` (HyperCasual) | `Coin Item.prefab`, Kenney Survival Kit (заборы, указатели, камни) в `Demo/ExternalAssets` |
| `JMO Assets/Cartoon FX Remaster` | Сырьё для VFX сбора монет / стройки / конфетти |
| `Real Fire & Smoke` | Сырьё для VFX пыли/дыма стройки |
| `Racing Game Ui Pack`, `Simple Vector Icons` | Кнопки, панели, иконки Hub-UI и Welcome Back попапа |
| `Universal Sound FX` | `8BIT/Coin_Collect` (10 вариантов), `CONSTRUCTION` (лупы), `CARTOON` и др. |

⚠️ Скрипты `Arcade Idle Engine/Scripts/*.cs` (без asmdef и неймспейсов: `Stack`, `Unlock`, `CashMan`…) компилируются в Assembly-CSharp — **не использовать**, архитектура пака несовместима с нашей. Берём только модели, текстуры, анимации, звук.

---

## 1. Решения по стилю (фиксируется ДО шага 11)

- [ ] Хаб в той же стилистике, что арена: stylized low-poly, без PBR-тяжести (mobile-perf).
- [ ] Ракурс карты: top-down follow за персонажем, ~45–60° (влияет на компоновку зданий, размер зон и читаемость стадий стройки на телефоне).
- [ ] Скрин арены Battle Stadium как палитра-референс → `Assets/Development/Art/StyleReferences/` (исключить из билда).
- [ ] Читаемость стадий стройки с рабочего расстояния камеры: пустырь / фундамент / каркас / готовое здание различаются силуэтом, не деталями.
- [ ] **Валюта визуально:** пачки купюр (`Cash_Model.fbx`, классика arcade idle) или монеты (`Coin Item.prefab` / цилиндр-примитив) — определяет `Coin.prefab` (п.4a).

---

## 2. 3D — стадии стройки (КРИТИЧНО)

- [x] Construction-пак в проекте: **Synty POLYGON Construction** (`Assets/ThirdParty/Synty/PolygonConstruction/`).
- [x] Материалы паков не на встроенном Standard-шейдере (Synty City/Construction/Generic, Arcade Idle Engine, Battle_Stadium) *(розовые материалы — проверить в Unity глазами)*.
- [ ] Масштаб Synty-моделей проверен (частый нюанс ×100) *(проверить в Unity)*.
- [ ] Строительные леса: по именам префабов в Synty не найдены — каркас стадии собирать из `SM_Bld_ConcreteFrame_*` / `SM_Bld_ConcreteRebar_*` + `SM_Bld_Crane_01`.
- [ ] Собрать префабы стадий в `Assets/Development/Prefabs/Hub/<BuildingId>/` (папка создана, пустая; **рекомендуется editor-скриптом на шаге 12**, не руками):
  - [ ] `Stage_0_Vacant.prefab` — пустырь: пятно земли + табличка-вывеска.
  - [ ] `Stage_1_Foundation.prefab` — `SM_Bld_Concrete_Floor_*` / `Slab` + ограждение стройки.
  - [ ] `Stage_2_Frame.prefab` — `ConcreteFrame`/`ConcreteRebar` + кран.
  - [ ] `Stage_3_Complete.prefab` — готовое здание (см. п.3).
- [ ] Минимум для шага 12: полные наборы стадий для `HighJumpTower`, `TicketBooth`, `Tribunes`. Заглушки трюков — общий `Stage_0_Vacant` + вывеска "Coming soon".

---

## 3. 3D — готовые здания (Stage_3)

- [ ] `Tribunes` — модуль трибун из **Battle Stadium**.
- [ ] `DerbyArena` (миниатюра на карте) — секция арены из **Battle Stadium**.
- [ ] `HighJumpTower` — girder-конструкция + measurement backdrop (переиспользование элементов сцены `Stunts_HighJump`, editor `Build HighJump Scene`).
- [ ] `Garage`, `TicketBooth` — здания **Synty POLYGON City** (уже в проекте; Kenney/ProBuilder-болванки больше не нужны).
- [ ] Заглушки трюков (`LongJump`, `FireRings`, `Bowling`, `FieldGoal`, `HighCard`) — `Stage_0_Vacant` + вывеска "Coming soon" (TextMeshPro), отдельные модели НЕ нужны.

---

## 4. 3D — земля и декор карты

- [x] Дорожки — дорожные модули **Synty Construction** (`SM_Env_Road_*`, разметка, бордюры).
- [ ] Ground-плоскость: stylized grass/dirt tileable. ⚠️ `ThirdParty/Asphalt stuff` — файлы `.blend`: Unity импортирует их только при установленном Blender. Альтернатива — материалы Synty / Arcade Idle Engine (`Grass.mat`, `Floor*.mat`) или Polyhaven/ambientCG (CC0) в формате текстур.
- [x] Периметр карты: забор/барьеры/бочки — Synty Construction (`SM_Env_Fence_*`, `SM_Prop_Barrier_*`), **Junkyard_1**, Battle Stadium.
- [ ] Skybox — тот же HDRI, что в остальных сценах.

---

## 4a. 3D — персонаж хаба и валюта (КРИТИЧНО для arcade idle)

- [x] **Персонаж** — модель водителя (`Prefabs/Characters/Driver_Default.prefab`, Mixamo Humanoid). Отдельная модель не нужна.
- [ ] **Анимации Idle / Run** — нужны к Unity-checkpoint'у шага 11. Два пути:
  - [ ] **(сначала проверить)** клипы `Arcade Idle Engine/Animation/Idle.anim`, `Runnig.anim` (`Walking.anim`) — содержат humanoid-кривые, должны ретаргетиться на водителя *(проверить в Unity)*;
  - [ ] если ретаргет не годится — Mixamo `Hub_Idle.fbx`, `Hub_Run.fbx` (бег с руками у груди под стопку) → `Assets/Development/Art/Animations/` (сейчас там только `SitDriver.anim`).
  - [ ] (опц.) `Hub_Victory` — реакция на завершение стройки.
- [ ] **`Coin.prefab`** в `Assets/Development/Prefabs/Hub/` (по решению п.1): `Cash_Model.fbx` / `Coin Item.prefab` / цилиндр-примитив, low-poly — стопка из 20–30 штук не должна стоить дорого. Используется `CoinStackController` и `CoinBubbleController`; спавн через фабрику (правило CLAUDE.md). Нужно к шагу 13.
- [x] Радиальный таймер-круг (`LaunchConfirmView`) — `Arcade Idle Engine/Textures/circle.png` / `UI_circle_mask.png` (копия в `Development/Art`) или процедурный `Image.fillAmount`.

---

## 5. VFX

Сырьё в проекте: **JMO Cartoon FX Remaster**, **Real Fire & Smoke**.

- [ ] Создать наши префабы в `Assets/Development/Prefabs/VFX/`, типы — в `VfxEffectType` + `VfxConfig` (skill `add-vfx-effect`), шаги 12–13:
  - [ ] `VFX_CoinCollect.prefab` — разлёт монет при сборе проходом.
  - [ ] `VFX_BuildInvest.prefab` — пыль/удары при вложении.
  - [ ] `VFX_BuildStageComplete.prefab` — завершение этапа.
  - [ ] `VFX_BuildingComplete.prefab` — конфетти/фейерверк финала стройки.
- [ ] Спавн только через `VfxFactory`.

---

## 6. UI

- [x] Кнопки/панели/прогресс-бары — **Racing Game Ui Pack** (уже используется в мете). *Mega GUI Pack 2 в проекте нет — прежняя версия чеклиста устарела.*
- [x] Иконки — **Simple Vector Icons** + `Arcade Idle Engine/Textures/MoneyIcon.png`.
- [ ] Спрайты в `Assets/Development/Art/Sprites/Hub/` (папка создана, пустая; копии из паков — правило: спрайты пака не трогать даже import-настройками, `UIBuildHelpers.LoadSprite` копирует сам):
  - [ ] `Icon_Coin` — `MoneyIcon.png` или иконка Simple Vector Icons.
  - [ ] `Icon_Hammer` — индикатор «доступно вложение».
  - [ ] `Icon_Clock` — offline-доход в Welcome Back.
  - [ ] "Coming soon" — TextMeshPro-надпись без спрайта.
- [x] Шрифты — TMP-ассеты проекта (Bungee / RussoOne), ничего нового.

---

## 7. Audio

Источники в проекте: **Universal Sound FX**, звуки **Arcade Idle Engine**.

- [ ] Выбрать и назначить через `AudioConfig` (паттерн аудио-слоя, `SfxId`):
  - [x] сбор монет — `8BIT/Coin_Collect/*` или `Arcade Idle Engine/Audio/CashCollect.wav`, `cha-ching.wav` *(выбрать на слух)*;
  - [ ] поток монет при вложении (loop) — подобрать (`8BIT/Coin_Collect` в лупе или `CARTOON`);
  - [x] стройка — `CONSTRUCTION/CONSTRUCTION_Bricks_*_loop`, `Stamp.wav` *(выбрать на слух)*;
  - [ ] завершение этапа / фанфара финала стройки — искать в Universal Sound FX (по именам не проверено);
  - [ ] звук Welcome Back попапа — искать в Universal Sound FX (`BUTTONS`/`CARTOON`).
- [ ] ❌ **`Music_Hub` — спокойный casual-loop: НЕТ.** В `Assets/Development/Audio/Music/` только панк/рок под Derby. Нужен контрастный спокойный трек (**Pixabay Music** / **FreePD**). Не блокер кода.

---

## 8. Подготовка Unity ДО шага 11

- [x] Папки `Assets/Development/Prefabs/Hub/`, `Assets/Development/Art/Sprites/Hub/` (коммит 7f463200).
- [x] Construction-пак импортирован; материалы не на встроенном Standard *(визуально — проверить)*.
- [ ] Проверен масштаб Synty-моделей.
- [x] Пустая сцена `Assets/Development/Scenes/BaseHub.unity` (наполнит editor-скрипт `Build Base Hub Layout`; в Build Settings её добавит он же).
- [ ] ❌ НЕ настраивать `BuildingCatalogConfig` руками — инстансы создаст editor-скрипт `Create Building SO Set`.

---

## 9. Чек-лист «готов к шагу 11»

Для шага 11 (персонаж, камера, зоны запуска, миграция с MainMenu/Stunts_Hub) обязательны только первые два пункта; остальное — к шагам 12–13.

- [ ] **[шаг 11]** Анимации Idle / Run на водителе работают (клипы Arcade Idle Engine или Mixamo).
- [ ] **[шаг 11]** Масштаб/материалы Synty проверены в Unity.
- [x] Construction-пак импортирован.
- [ ] **[шаг 12]** Префабы 4 стадий для `HighJumpTower`, `TicketBooth`, `Tribunes` (editor-скриптом).
- [ ] **[шаг 12]** Готовые здания `DerbyArena`, `Garage`, `Tribunes`, `TicketBooth` (Battle Stadium + Synty City).
- [ ] **[шаг 12]** Общий `Stage_0_Vacant` + вывеска "Coming soon".
- [ ] **[шаг 11–12]** Ground-плоскость; дорожки ✓ (Synty).
- [ ] **[шаги 12–13]** 4 VFX-префаба, типы в `VfxEffectType`.
- [ ] **[шаги 12–13]** SFX выбраны; `Music_Hub` найден.
- [ ] **[шаг 13]** `Coin.prefab` (по решению п.1).
- [ ] **[шаги 11–13]** Спрайты Hub-UI в `Art/Sprites/Hub/`.
- [x] Пустая сцена `BaseHub.unity`.

---

## 10. Бюджет

Закупки для слоя не нужны: Synty Construction + City + Generic, два idle-пака, Battle Stadium, Junkyard, Universal Sound FX уже в проекте (это выше прежнего «расширенного» уровня ~$60).

Единственный недостающий контент — спокойный музыкальный луп для хаба (бесплатно: Pixabay Music / FreePD) и, при неудачном ретаргете, Mixamo-анимации (бесплатно).
