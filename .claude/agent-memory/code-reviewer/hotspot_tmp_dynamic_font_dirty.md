---
name: hotspot-tmp-dynamic-font-dirty
description: TMP SDF-ассеты в Art/Fonts/TMP грязнеют сами (динамический атлас 1x1 → 1024x1024) после Play Mode — в diff шага это мусор вне скоупа
metadata:
  type: project
---

`Assets/Development/Art/Fonts/TMP/*SDF.asset` (Bungee, RussoOne) — Dynamic-атлас: после Play Mode Unity дописывает в них текстуру 1024x1024 и глифы (+1000 строк YAML). В git status появляются в шагах, которые шрифты не трогали.

**Why:** замечено 2026-10-03 на ревью «Фиксы по аудиту»; это побочный эффект редактора, а не правка шага.

**How to apply:** не считать Critical по скоупу; Warning «не коммитить в шаг — `git checkout --` или Clear Dynamic Data». Если повторяется — предложить `m_ClearDynamicDataOnBuild`/статический атлас отдельной задачей.
