# Codex Feasibility Review Prompt

Проведи проверку исполнимости приложенного пакета Gate 0 для игры THE LOGS ARE WRONG.

## Режим работы

- Не начинай реализацию Gate 1.
- Не создавай Unity-проект и игровой код.
- Не расширяй игровой scope.
- Не переписывай документы целиком.
- Проверяй, можно ли построить систему именно так, как она описана.

## Обязательно прочитать

1. `README_GATE0.md`
2. `docs/GAME_PILLARS.md`
3. `docs/PROTOTYPE_SCOPE.md`
4. `docs/FIRST_SHIFT_SPEC.md`
5. `docs/LOG_STATE_MACHINE.md`
6. `docs/ANOMALY_MATRIX.md`
7. `docs/NETWORK_RULES.md`
8. `docs/MODEL_ROUTING.md`
9. `AGENTS.md`
10. `docs/adr/ADR-001_NETWORK_STACK.md`
11. YAML в `data/`

Исходный дизайн лежит в `source/the_logs_are_wrong_design_v5_2.md` и используется только для проверки, что Gate 0 не исказил утверждённое решение.

## Проверь

1. Достаточно ли контрактов для чистой C# domain simulation без `UnityEngine`.
2. Есть ли неоднозначные состояния, переходы или ownership.
3. Проходим ли манифест `P0_SHIFT_A` и действительно ли он требует переработать минимум две аномалии.
4. Не противоречат ли Markdown и YAML друг другу.
5. Можно ли выразить state machines без скрытых зависимостей от сцены.
6. Достаточен ли intent/event contract для дальнейшей сети.
7. Реалистичен ли предлагаемый FishNet + Steam transport stack на актуальной Unity.
8. Какие версии/пакеты надо pin-ить.
9. Есть ли причина предпочесть NGO/MPS или другой стек — только с конкретным техническим обоснованием.
10. Какие минимальные интерфейсы/модули должны существовать в Gate 1.
11. Не потребует ли какой-либо пункт раннего Unity-кода или преждевременной системы.

## Формат каждого замечания

```text
[SEVERITY: HIGH/MEDIUM/LOW]
File:
Section:
Problem:
Why it blocks or risks implementation:
Minimal correction:
```

Отдельно выдай:

- Proposed module/file map для Gate 1 — только структура, без кода.
- Dependency order.
- Package compatibility verdict по ADR-001.
- Список файлов, которые готовы без изменений.
- Итог: `FEASIBLE`, `FEASIBLE_WITH_CHANGES` или `NOT_FEASIBLE`.
