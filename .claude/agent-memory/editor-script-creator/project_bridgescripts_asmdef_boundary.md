---
name: project_bridgescripts_asmdef_boundary
description: Editor-скрипты для ThirdParty-паков без asmdef (RealisticEngineSound) живут в Assets/Development/BridgeScripts/Editor/, не в обычном Scripts/Editor/
metadata:
  type: project
---

Паттерн для ThirdParty-паков, поставленных БЕЗ .asmdef (их код падает в Assembly-CSharp,
а не в изолированную сборку): любой мост (`*Bridge`) между таким паком и FlatOutLike-типами
тоже кладётся в Assembly-CSharp — в `Assets/Development/BridgeScripts/` (без .asmdef рядом,
runtime-мост) и `Assets/Development/BridgeScripts/Editor/` (без .asmdef, editor-автоматизация
моста).

Причина: обычный `Assets/Development/Scripts/Editor/` лежит под `FlatOutLike.Editor.asmdef`,
который НЕ видит Assembly-CSharp — типы пака (например `RealisticEngineSound`) и моста
(`CarEngineSoundBridge`) там недоступны (CS0246). `BridgeScripts/Editor/` без asmdef
компилируется в Assembly-CSharp-Editor, который видит и пак, и мост, и все обычные
FlatOutLike-типы (FlatOutLike.asmdef auto-referenced).

Пример: `SetupEngineSoundsEditor.cs` (`Assets/Development/BridgeScripts/Editor/`, меню
`FlatOutLike/Setup/Setup Engine Sounds`) встраивает RealisticEngineSound-двигатели в 4
префаба машин (Car_Sport_05→v8_italian, Car_Sport_09→V8_american_modern,
Car_MT_02→Truck_old, Car_Enemy→i6_german), создаёт `EngineSoundConfig_Default.asset`
в `Assets/Development/Data/Configs/Audio/`, добавляет анкор "EngineSound" (localPos
0,0.5,0.8) и `CarEngineSoundBridge` на корень каждого префаба.

**How to apply:** при следующем ThirdParty-паке без asmdef, который нужно связать
с рантайм-кодом проекта — заводить runtime-мост и его editor-автоматизацию в
`Assets/Development/BridgeScripts/` (и `/Editor/` подпапке), а не в обычные
`Scripts/` / `Scripts/Editor/`. Смотри также [[project_step3_damage_vfx_scripts]]
для похожих ThirdParty-VFX-интеграций (там паки были с asmdef или nested-ссылками,
без этой проблемы).
