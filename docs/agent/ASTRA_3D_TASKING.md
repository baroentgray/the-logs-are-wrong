# Astra 3D Tasking Guide — THE LOGS ARE WRONG

Статус: рабочая инструкция для **формулирования задач GPT-6 Astra в Codex** при создании 3D-ассетов и окружения TLAW.

Этот файл предназначен прежде всего для Control Center / 3D-мастерской, которая **готовит задания для Astra**. Он не заменяет `AGENTS.md`, asset cards, frozen gameplay contracts или TLAW art direction.

## 1. Главный принцип

Astra должна получать **цель, authoritative inputs, ограничения и критерии успеха**, а не пошаговый урок по Blender.

Хорошая задача описывает:

1. что нужно получить;
2. какие файлы/референсы являются источником истины;
3. что важнее всего визуально и функционально;
4. какие вещи нельзя менять или изобретать;
5. сколько итераций разрешено;
6. что должна проверить автоматика;
7. что Astra обязана вернуть в handoff.

Не расписывать ей без необходимости: какие primitives создавать, сколько раз нажать bevel, где поставить каждую вершину, какой конкретный boolean использовать. Пусть Astra сама выбирает эффективный Blender workflow.

---

## 2. Доступные reasoning levels

В текущем плане доступны только:

- **LOW**
- **MEDIUM**

Использовать их так.

### MEDIUM — default для настоящего 3D-решения

Использовать, когда требуется:

- построить новый hero/interactive prop;
- понять форму по нескольким reference views;
- восстановить пространственную конструкцию;
- придумать layout помещения внутри frozen gameplay blockout;
- выполнить structural/environment dressing pass;
- разобраться с механической иерархией и moving parts;
- сделать первую полноценную V1 модели.

### LOW — default для bounded follow-up

Использовать, когда решение уже принято и нужно:

- изменить конкретные пропорции;
- поправить pivot/origin;
- переименовать или перегруппировать объекты;
- уменьшить polycount;
- поправить материалы/UV;
- экспортировать GLB;
- сделать повторный render;
- исправить один локальный дефект;
- выполнить механический cleanup;
- обновить уже существующий ассет без нового spatial design.

**Не держать MEDIUM по инерции на всех последующих проходах.** После принятия пространственного решения переходить на LOW, если новая сложная reasoning-задача не появилась.

---

## 3. Когда Astra оправдана

Astra — дорогой ресурс. Использовать её там, где ценны visual/spatial reasoning и автономная DCC-работа.

Хорошие задачи:

- промышленное оборудование;
- интерактивные механические props;
- рабочие станции;
- пилорама и её технологические узлы;
- помещения и structural dressing;
- reconstruction по нескольким изображениям;
- превращение greybox/blockout в согласованное TLAW-окружение;
- сложный Blender pass, где обычный text-to-3D требует большого cleanup.

Обычно не стоит тратить Astra на:

- массовое переименование;
- копирование сотен одинаковых объектов;
- простую конвертацию форматов;
- deterministic validation;
- обычный git/file cleanup;
- отчёты, которые может создать скрипт;
- мелкий UV/naming/export handoff после того, как модель уже решена;
- простые props, которые быстрее сделать процедурно или более дешёвым агентом.

---

## 4. Source-of-truth package для одной задачи

Не повторять весь TLAW-контекст в prompt. По возможности подготовить небольшой набор файлов и сослаться на него.

Рекомендуемый пакет:

```text
AGENTS.md                  # общие repo/governance ограничения
TLAW_3D_STYLE.md           # art direction / chunky tactile industrial
ASSET_CARD.md              # конкретный объект, размеры, функция, moving parts
references/                # только релевантные утверждённые изображения
blockout/                  # если это environment task
```

В prompt писать, например:

```text
Authoritative inputs:
- ./ASSET_CARD.md
- ./TLAW_3D_STYLE.md
- ./references/
```

Не заставлять Astra перечитывать десятки старых документов, если они не нужны этой задаче.

Если в repo существуют старые версии карточки/референса, явно назвать **единственный current authoritative set**.

---

## 5. Обязательный preflight перед дорогим modelling pass

Перед началом полноценного MEDIUM build Astra должна быстро проверить входные данные на явные противоречия.

Особенно проверить:

- размеры против силуэта на reference views;
- moving-part contract против изображения;
- число/положение pivot'ов;
- raised/actuated state;
- orientation/worker side;
- frozen gameplay clearance против proposed geometry;
- противоречащие друг другу старые и новые документы.

Если конфликт **материально меняет форму или механику**, не расходовать длинный modelling loop молча.

Правило:

```text
If authoritative dimensions/function materially conflict with the approved visual references,
report the conflict before expensive refinement. Do not solve it by silently violating either source.
```

Косметические неоднозначности Astra может решать сама в стиле TLAW и коротко перечислять в handoff.

### Урок benchmark TLAW_DISPOSAL_LEVER_01

Первый Astra benchmark успешно построил технически качественный prop, но письменные размеры задали заметно более длинную рукоять, чем visual reference. Astra корректно соблюла contract и выставила WARN.

Следовательно: **согласованность размеров и референса проверять до дорогого polish pass**.

---

## 6. Предпочтительный Blender workflow

По умолчанию разрешать Astra самостоятельно выбирать workflow, но направлять к детерминированной работе.

Рекомендуемая формулировка:

```text
Choose the most efficient Blender workflow yourself.
Prefer Blender Python / bpy / headless CLI for deterministic construction,
transforms, repetition, validation and export.
Use interactive Blender operations when visual inspection or a GUI-only operation materially helps.
```

Преимущества для TLAW:

- реальные размеры легче контролировать;
- повторяющиеся детали дешевле создавать процедурно;
- pivot/hierarchy легче сделать детерминированно;
- экспорт и проверки воспроизводимы;
- меньше длинных UI/tool loops.

Не заставлять Astra управлять каждой вершиной отдельными tool calls, если тот же результат проще получить через `bpy`.

---

## 7. Делить работу на смысловые проходы

Не просить одним prompt'ом одновременно:

`layout + hero geometry + clutter + UV polish + render farm + Unity + exhaustive QA`.

Это резко увеличивает reasoning/tool iterations и расход allowance.

### Для отдельного ассета

Оптимальный порядок:

```text
0. preflight / contract consistency
1. V1 geometry + proportions + hierarchy
2. one visual comparison render
3. at most 1–2 targeted correction passes
4. material grouping / basic UV
5. automated validation + export
6. engine import check separately
```

### Для помещения

```text
0. frozen gameplay blockout check
1. shell / architecture / structural pass
2. fixed gameplay anchors and clearances validation
3. major industrial dressing
4. reuse/instance approved prop library
5. clutter / lived-in pass
6. optimization / validation
7. Unity scene check separately
```

После каждого крупного pass следующий prompt должен быть **дельтой**, а не повтором всей исходной спецификации.

Пример:

```text
Keep the existing accepted V1.
Increase the body width by 8% and shorten the lever by 10%.
Preserve pivot, hierarchy, materials and all unaffected geometry.
Render one new 3/4 validation view only.
```

---

## 8. Жёстко ограничивать self-correction loop

Не писать:

```text
keep improving until perfect
iterate until there are no issues
exhaustively verify everything
```

Такие формулировки фактически дают Astra неограниченный бюджет итераций.

Вместо этого:

```text
Build one complete V1 before polishing.
Make at most 2 additional visual correction passes.
Stop earlier when silhouette, proportions, hierarchy and material grouping satisfy the contract.
Do not spend extra passes on imperceptible polish.
```

Для большого environment pass можно разрешить 3 прохода, но только если каждый имеет отдельную цель.

---

## 9. Visual comparison должен быть коротким и полезным

Для props обычно достаточно:

- одного 3/4 render после V1;
- одного side/front validation render при необходимости;
- close-up только если механика/pivot действительно спорны.

Не генерировать 7–10 ракурсов для каждого production prop по умолчанию.

Большой набор orthographic/debug renders оправдан для benchmark, acceptance audit или проблемного ассета, но не для каждой итерации.

Для environment использовать несколько фиксированных validation cameras и сравнивать одни и те же views между проходами.

---

## 10. Не заставлять Astra выполнять дешёвый QA вручную

То, что можно детерминированно проверить скриптом, должно проверяться скриптом.

Целевая общая автоматическая проверка TLAW 3D должна покрывать, где применимо:

- dimensions/bounds;
- transforms/scale;
- naming;
- hierarchy;
- object/material counts;
- triangle/vertex counts;
- non-manifold geometry;
- loose/duplicate geometry;
- UV presence;
- pivot coordinates;
- GLB roundtrip;
- forbidden object types;
- optional mechanical-range samples.

Astra должна **запустить validator и прочитать summary**, а не тратить reasoning на ручное повторение тех же проверок.

Рекомендуемая инструкция:

```text
Run the existing TLAW 3D validator after export.
Do not manually duplicate checks already covered by the validator.
Write machine-readable details to validation.json.
```

Benchmark может требовать exhaustive QA; production task — только проверки, полезные для конкретного риска.

---

## 11. Экономия токенов/allowance

Главные способы экономии — не сокращение каждого предложения, а сокращение **reasoning/tool cycles**.

### Делать

- MEDIUM только на spatial/design problem;
- LOW на bounded corrections;
- ссылаться на файлы вместо повторения контрактов;
- давать 2–4 информативных reference views, а не десятки почти одинаковых;
- использовать один accepted V1 и редактировать его;
- задавать max correction passes;
- использовать headless/bpy для повторяемых операций;
- валидировать скриптом;
- писать validation details в файл, не в chat;
- ограничивать финальный ответ;
- разделять Blender build и Unity integration;
- reuse/instance готовые assets в environment tasks;
- останавливать задачу сразу после достижения acceptance criteria.

### Не делать

- повторно вставлять полный art bible в каждый follow-up;
- заставлять Astra исследовать весь repo;
- просить web research для выдуманного TLAW prop, если references уже утверждены;
- запускать image generation, если хороший visual reference уже существует;
- перестраивать всю модель из-за локального дефекта;
- просить exhaustive QA после каждой мелкой правки;
- просить длинный narrative report;
- одновременно решать modelling, gameplay code и Unity scene integration без необходимости.

---

## 12. Reference policy

### Для выдуманного TLAW asset

Обычно достаточно:

- front/side или front/3q;
- дополнительный ракурс для скрытой механики;
- asset card.

Добавлять:

```text
These references are the approved visual target.
Do not search the web for alternate designs unless a required functional fact is missing.
```

Это предотвращает дрейф в сторону реальных промышленных аналогов вместо TLAW art direction.

### Для реконструкции реального объекта/архитектуры

Можно разрешить Astra самой искать дополнительные views и реальные размеры, если задача прямо требует reconstruction accuracy.

### Для нового помещения без утверждённого concept

Допустим отдельный concept phase. Не смешивать его автоматически с production modelling pass.

---

## 13. Environment-specific правила

Astra не должна сама менять gameplay layout под красоту.

Перед environment dressing заморозить:

- gameplay blockout;
- walkable corridors;
- player clearances;
- machine footprints;
- interaction points;
- anchor points;
- hazard zones;
- sightline requirements, если они уже gameplay-significant.

Формулировка:

```text
The gameplay blockout and marked anchors are frozen.
Do not move, resize or obstruct them.
Dress around them.
```

Astra может владеть:

- architectural shell;
- beams/supports;
- cable trays;
- lights;
- wall furniture;
- storage;
- brackets/fasteners;
- justified pipes;
- secondary machinery shells;
- approved prop placement;
- industrial clutter;
- lived-in detail.

Не разрешать procedural clutter перекрывать gameplay volumes.

Использовать **instances/reuse** существующей библиотеки вместо сотен уникальных почти одинаковых meshes.

---

## 14. Asset-specific правила

При создании mechanical/interactive prop обязательно явно указывать только реально важные механические факты:

- function;
- real dimensions/ranges;
- moving parts;
- true pivot location/axis;
- raised/rest state;
- actuated state или направление движения;
- physical stops/guards;
- required separate objects;
- forbidden invented controls.

Не надо заранее диктовать topology каждого bolt, если это не acceptance requirement.

Приоритет для TLAW props:

```text
silhouette
> proportions
> mechanical readability
> moving-part separation / pivots
> game-usable geometry
> material grouping
> visual polish
> microdetail
```

---

## 15. Final response budget

Финальный chat output Astra должен быть коротким.

Рекомендуемая формулировка:

```text
Write full technical evidence to validation.json / report.md.
In chat return no more than 10 lines:
- PASS / WARN / FAIL
- output paths
- dimensions
- tris
- materials
- unresolved warnings only
Do not narrate completed work.
```

Если нужен подробный acceptance report, его читает Control Center из файла отдельно.

---

## 16. Базовый prompt template — новый prop

```text
Build <ASSET_ID> directly in Blender.

Reasoning mode: MEDIUM for the first build.

Authoritative inputs:
- ./ASSET_CARD.md
- ./TLAW_3D_STYLE.md
- ./references/

Goal:
Create a game-ready stylized TLAW industrial prop matching the approved references
and satisfying the functional dimensions and moving-part contract.

Before modelling:
Perform a short consistency check between dimensions/function and visual references.
If a material conflict exists, report it before expensive refinement.

Priority:
1. silhouette and proportions
2. mechanical readability
3. correct separate moving parts and pivots
4. clean game-usable geometry
5. material grouping
6. visual polish

Workflow:
- Choose the most efficient Blender workflow yourself.
- Prefer bpy/headless Blender for deterministic construction and repetition.
- Build one complete V1 before polishing.
- Render one 3/4 comparison view.
- Make at most 2 targeted visual correction passes.
- Modify existing geometry; do not rebuild unaffected parts.

Do not:
- invent controls or functionality
- alter gameplay-significant dimensions silently
- search for alternate designs unless required information is missing
- create gameplay code, colliders or Unity behaviour
- add sci-fi/cyberpunk/steampunk/post-apocalyptic decoration

Deliver:
- .blend
- .glb
- 1 primary 3/4 render
- 1 validation view if needed
- validation.json

Run the existing TLAW 3D validator after export.
Do not duplicate validator checks manually.

Final chat response: <=10 lines, PASS/WARN/FAIL + output paths + key metrics + unresolved warnings.
```

---

## 17. Базовый prompt template — bounded correction

Запускать обычно на LOW.

```text
Continue from the existing accepted Blender asset. Do not rebuild it.

Change only:
- <specific correction 1>
- <specific correction 2>

Preserve:
- accepted silhouette except for the requested delta
- hierarchy
- pivots
- unaffected geometry
- materials
- scale/orientation

Make one correction pass and one validation render.
Run the validator only for checks affected by this change.

Return <=8 lines with changed items, PASS/WARN/FAIL and unresolved warnings.
```

---

## 18. Базовый prompt template — помещение / dressing

```text
Dress <AREA_ID> in Blender using the supplied frozen gameplay blockout.

Reasoning mode: MEDIUM for spatial composition.

Authoritative inputs:
- ./TLAW_3D_STYLE.md
- ./AREA_CONTRACT.md
- ./blockout/
- ./references/
- ./approved_asset_library/

The gameplay blockout, anchor points, machine footprints, walkable clearances
and hazard volumes are frozen. Do not move or obstruct them.

Pass goal:
<STRUCTURAL / MAJOR_DRESSING / CLUTTER — choose exactly one>

Reuse and instance approved assets wherever possible.
Create new secondary geometry only where required to make the area coherent.

Preserve TLAW chunky tactile industrial style.
Avoid random pipes, excessive clutter and decorative complexity without function.

Use the fixed validation cameras.
Make one complete pass, compare screenshots, then at most 2 targeted corrections.

Do not proceed into the next environment pass automatically.
Stop after this pass and return the scene plus concise warnings.
```

---

## 19. Acceptance ownership

Astra не принимает собственную модель в production окончательно.

Её `PASS/WARN/FAIL` — self-report.

Финальное TLAW acceptance выполняется отдельно по:

- approved asset/environment contract;
- visual review;
- automated validator evidence;
- Blender/GLB inspection;
- Unity import/runtime check, если требуется.

Не считать красивый render достаточным production evidence.

---

## 20. Практический baseline после первого benchmark

`TLAW_DISPOSAL_LEVER_01` показал, что Astra уже способна в одном автономном Blender workflow получить:

- согласованную многовидовую геометрию;
- отдельную механическую иерархию;
- настоящий axle-centred pivot;
- moving assembly;
- game-usable polycount;
- небольшое число материалов;
- UV;
- чистый GLB roundtrip;
- deterministic bpy build/validation scripts.

Поэтому для TLAW industrial props рабочая гипотеза теперь:

```text
asset card + approved references
-> Astra MEDIUM: spatial/model solution
-> Astra LOW or cheaper agent: bounded cleanup
-> automated validator
-> Unity import check
```

Для environments:

```text
frozen gameplay blockout
-> Astra MEDIUM: one semantic environment pass at a time
-> LOW/cheaper agent: bounded cleanup/repetition
-> validator
-> Unity visual/gameplay clearance check
```

Не расходовать Astra на работу, которую можно детерминированно выполнить после того, как она уже решила сложную 3D-задачу.
