# Claude Gate 0 Red-Team Prompt — Draft 0.2

Проведи финальное red-team review всего пакета после Codex feasibility review.

## Не делать

- Не начинать реализацию.
- Не расширять gameplay scope.
- Не переписывать файлы целиком.
- Не возвращать сеть в Gate 2.

## Обязательно проверить

1. `docs/INTAKE_SCHEDULER.md`:
   - admission;
   - capacity;
   - early feed;
   - same-tick order;
   - воспроизводимость jam.
2. `docs/FIRST_SHIFT_SPEC.md` и оба YAML:
   - success predicate;
   - correct/incorrect processing;
   - проходимость.
3. `docs/LOG_STATE_MACHINE.md`:
   - event sequence;
   - rejection;
   - snapshot/replay;
   - terminal states.
4. Containment:
   - повторные cycles;
   - incident resolution;
   - отсутствие удаления списанных объектов.
5. Gate boundary:
   - Gate 2 local only;
   - Gate 3 two-player Steam;
   - Gate 4 four slots.
6. `NETWORK_RULES.md` и ADR-001:
   - authority;
   - package risk;
   - smoke-test before pins.
7. Model routing и AGENTS.
8. Соответствие v5.2.

## Формат замечания

```text
[SEVERITY: HIGH/MEDIUM/LOW]
File:
Section:
Problem:
Why it matters:
Minimal correction:
```

В конце:

- список файлов ready as-is;
- список решений, требующих Сергея;
- `APPROVE`, `APPROVE_WITH_CHANGES` или `REJECT`.
