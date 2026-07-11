# Assets Checklist — FlatOutLike, Idle-Build слой (BaseHub)

> Чеклист подготовки контента для шагов 11–13 Implementation Order (§19 `game-design.md`): карта базы, стадийная стройка, idle-доход.
> **Дополнение** к основным закупкам проекта (1Poly Destructible Cars, Battle Stadium, Real Fire & Smoke, Mega GUI Pack 2, MEGA CAR EXPLOSION, Mixamo) — здесь только то, чего для idle-слоя не хватает.
>
> **Стиль:** stylized low-poly, палитра-матч с Battle Stadium.
> **Бюджет на слой:** $0–60 (большая часть закрывается переиспользованием и CC0).

---

## 1. Решения по стилю (фиксируется ДО закупки)

- [ ] Хаб в той же стилистике, что арена: stylized low-poly, без PBR-тяжести (сохраняем mobile-perf подход основного проекта).
- [ ] Зафиксировать ракурс карты: изометрия ~45–60° (влияет на компоновку зданий, размер тап-зон и читаемость стадий стройки на телефоне).
- [ ] Скрин арены Battle Stadium как палитра-референс → `Assets/Development/Art/StyleReferences/` (исключить из билда).
- [ ] Проверить читаемость стадии стройки с рабочего расстояния камеры: пустырь / фундамент / каркас / готовое здание должны различаться силуэтом, не деталями.

---

## 2. 3D — стадии стройки (КРИТИЧНО)

Ядро визуала idle-слоя: игрок должен видеть прогресс каждой площадки с одного взгляда.

- [ ] **Synty POLYGON Construction Pack** (~$30) — Asset Store:
  - Леса, краны, бетонные плиты, фундаменты, ограждения стройки, мешки, инструменты, контейнеры.
  - Закрывает стадии «фундамент» и «каркас» для ЛЮБОГО здания карты.
  - Импорт → `Assets/ThirdParty/Synty_PolygonConstruction/`, содержимое не редактировать (правило CLAUDE.md).
- [ ] Бесплатная альтернатива: **Kenney Construction Kit** (CC0) — визуально проще, но $0.
- [ ] Собрать префабы стадий в `Assets/Development/Prefabs/Hub/<BuildingId>/`:
  - [ ] `Stage_0_Vacant.prefab` — пустырь: пятно земли + табличка-вывеска.
  - [ ] `Stage_1_Foundation.prefab` — фундамент + забор стройки.
  - [ ] `Stage_2_Frame.prefab` — каркас + леса + кран.
  - [ ] `Stage_3_Complete.prefab` — готовое здание (см. п.3).
- [ ] Минимум для шага 12: полные наборы стадий для `HighJumpTower`, `TicketBooth`, `Tribunes`. Заглушки трюков используют общий `Stage_0_Vacant` + вывеску "Coming soon".

---

## 3. 3D — готовые здания (Stage_3)

Максимум переиспользования уже купленного.

- [ ] `Tribunes` — модуль трибун из **Battle Stadium** (уже куплен).
- [ ] `DerbyArena` (миниатюра на карте) — стадион-модуль / секция арены из **Battle Stadium**.
- [ ] `HighJumpTower` — girder-конструкция + measurement backdrop, переиспользование сборки из Stunts-сцены (editor-скрипт `Build HighJump Ramp` уже собирает эти элементы).
- [ ] `Garage`, `TicketBooth` — варианты:
  - **Kenney City Kit / Suburb Kit** (CC0, $0) — простые низкополигональные здания.
  - **Synty POLYGON City** (~$30, опционально) — если хочется богаче и в одном стиле с Construction.
  - ProBuilder-болванки в палитре проекта — приемлемый fallback для первого билда.
- [ ] Заглушки трюков (`LongJump`, `FireRings`, `Bowling`, `FieldGoal`, `HighCard`) — `Stage_0_Vacant` + вывеска "Coming soon" (TextMeshPro), отдельные модели НЕ нужны.

---

## 4. 3D — земля и декор карты

- [ ] Ground-плоскость: stylized grass/dirt/asphalt tileable-текстура — **Polyhaven** / **ambientCG** (CC0).
- [ ] Дорожки между зданиями — модули из **Kenney Racing Kit** (CC0) или плоские decal-полосы.
- [ ] Периметр карты: забор/кусты/бочки — переиспользование **Battle Stadium** и (если куплен) **Junkyard Kit**.
- [ ] Skybox — тот же HDRI, что в остальных сценах (консистентность освещения).

---

## 4a. 3D — персонаж хаба и монеты (КРИТИЧНО для arcade idle)

Персонаж и стопка монет — то, на что игрок смотрит в хабе постоянно.

- [ ] **Персонаж** — переиспользуется модель водителя (Mixamo, уже в проекте). Отдельная модель НЕ нужна.
- [ ] **Mixamo-анимации** (free) докачать в `Assets/Development/Art/Animations/`:
  - [ ] `Hub_Idle.fbx` (расслабленный idle, не боевой)
  - [ ] `Hub_Run.fbx` (бег; вариант с руками у груди — имитация переноски — смотрится лучше со стопкой)
  - [ ] (опц.) `Hub_Victory.fbx` — реакция на завершение стройки здания
- [ ] **3D-монета** для стопки и пузырей: цилиндр-примитив с материалом gold в палитре проекта, ИЛИ монета из **Kenney Casino/Coin ассетов** (CC0). Один меш, low-poly (12-16 сегментов) — стопка из 20-30 штук не должна стоить дорого.
- [ ] Префаб `Coin.prefab` в `Assets/Development/Prefabs/Hub/` — используется `CoinStackController` (стопка) и `CoinBubbleController` (пузырь у income-здания). Спавн через пул (`VfxFactory`/отдельная фабрика — решит CLI по CLAUDE.md).
- [ ] Радиальный таймер-круг запуска (`LaunchConfirmView`) — filled-circle спрайт из **Mega GUI Pack 2** (уже куплен) или процедурный `Image.fillAmount`.

---

## 5. VFX

База — **JMO Cartoon FX Remaster Free** (бесплатно) + уже купленный **Real Fire & Smoke**.

- [ ] Создать наши префабы в `Assets/Development/Prefabs/VFX/`, зарегистрировать типы в `VfxEffectType` enum + `VfxConfig` (один источник правды, правило CLAUDE.md):
  - [ ] `VFX_CoinCollect.prefab` — разлёт монет при тап-сборе.
  - [ ] `VFX_BuildInvest.prefab` — пыль/удары молотка при вложении.
  - [ ] `VFX_BuildStageComplete.prefab` — завершение этапа.
  - [ ] `VFX_BuildingComplete.prefab` — конфетти/фейерверк финала стройки.
- [ ] Спавн только через `VfxFactory` (доступ к пулу — правило CLAUDE.md).

---

## 6. UI

- [ ] **Mega GUI Pack 2** (уже куплен) закрывает: монеты, прогресс-бары, попапы, кнопки — включая daily-rewards элементы, пригодные для Welcome Back попапа.
- [ ] Дополнительно (free): **Kenney Game Icons** (CC0) — иконки зданий, молоток/стройка, часы (offline-доход).
- [ ] Спрайты в `Assets/Development/Art/Sprites/Hub/`:
  - [ ] `Icon_Coin.png` (если из Mega GUI Pack — скопировать используемое, не ссылаться в ThirdParty вслепую).
  - [ ] `Icon_Hammer.png` — индикатор «доступно вложение».
  - [ ] `Icon_Clock.png` — offline-доход в Welcome Back.
  - [ ] `Label_ComingSoon.png` — или TextMeshPro-надпись без спрайта.
- [ ] Шрифты — уже настроенные TMP-ассеты проекта, ничего нового.

---

## 7. Audio

- [ ] SFX (источники: **Kenney Audio** CC0 / **Sonniss GDC Bundle** / **Pixabay**) в `Assets/Development/Audio/SFX/`:
  - [ ] `SFX_CoinCollect.ogg`, `SFX_CoinPile.ogg`
  - [ ] `SFX_CoinStream_Loop.ogg` — поток монет при вложении стоя в зоне (loop, стартует/стопается с потоком)
  - [ ] `SFX_BuildInvest.ogg` (молоток/стройка)
  - [ ] `SFX_BuildStageComplete.ogg`
  - [ ] `SFX_BuildingComplete_Fanfare.ogg`
  - [ ] `SFX_WelcomeBack_Popup.ogg`
- [ ] `Music_Hub.ogg` — спокойный casual-loop (**Pixabay Music** / **FreePD**) в `Assets/Development/Audio/Music/`. Контраст с интенсивным звуком Derby — хаб должен «отдыхать».

---

## 8. Подготовка Unity ДО шага 11

- [ ] Папки: `Assets/Development/Prefabs/Hub/`, `Assets/Development/Art/Sprites/Hub/`.
- [ ] Construction-пак импортирован, материалы конвертированы в URP (Edit → Rendering → Materials → Convert...).
- [ ] Проверен масштаб Synty-моделей (частый нюанс x100).
- [ ] Пустая сцена `BaseHub.unity` создана в `Assets/Development/Scenes/` — наполнит editor-скрипт `Build Base Hub Layout`, руками ничего не расставлять.
- [ ] ❌ НЕ настраивать `BuildingCatalogConfig` руками — инстансы с дефолтами создаст editor-скрипт `Create Building SO Set` после генерации классов CLI.

---

## 9. Чек-лист «готов к шагу 11»

- [ ] Mixamo-анимации Hub_Idle / Hub_Run скачаны, Humanoid-риг проверен на модели водителя.
- [ ] `Coin.prefab` (low-poly монета) собран.
- [ ] Construction-пак (Synty или Kenney) импортирован, URP-конверсия сделана.
- [ ] Префабы 4 стадий собраны минимум для `HighJumpTower`, `TicketBooth`, `Tribunes`.
- [ ] Готовые здания `DerbyArena`, `Garage` собраны из имеющихся паков (или ProBuilder-болванки).
- [ ] Общий `Stage_0_Vacant` + вывеска "Coming soon" для заглушек готовы.
- [ ] Ground-текстура и дорожки на месте.
- [ ] 4 VFX-префаба созданы, типы зарегистрированы в `VfxEffectType`.
- [ ] SFX-набор (6 файлов) и `Music_Hub` на месте.
- [ ] Спрайты Hub-UI в `Art/Sprites/Hub/`.
- [ ] Пустая сцена `BaseHub.unity` существует.

✅ Все галочки — запускай сессию 11 по `cli-workflow.md`.

---

## 10. Бюджет (итог по слою)

| Уровень | Состав | Цена |
|---|---|---|
| **Минимум** | Kenney Construction + City Kit (CC0) + переиспользование Battle Stadium / Junkyard | **$0** |
| **Рекомендуемый** | + Synty POLYGON Construction (стадии стройки заметно богаче) | **~$30** |
| **Расширенный** | + Synty POLYGON City (разнообразие готовых зданий) | **~$60** |

Рекомендация: **$30-уровень**. Стадии стройки — то, на что игрок смотрит чаще всего в idle-слое; Synty Construction окупается визуально. Готовые здания на старте закрываются переиспользованием и Kenney.
