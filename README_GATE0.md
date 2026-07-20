# THE LOGS ARE WRONG — Gate 0 v1.2

**Источник дизайна:** `source/the_logs_are_wrong_design_v5_2.md`  
**Статус:** УТВЕРЖДЕНО. Gate 0 закрыт; пакет является контрактом для Gate 1.  
**Вердикты:** Codex — `FEASIBLE_WITH_CHANGES`; Claude — `APPROVE_WITH_CHANGES`, все замечания Draft 0.2 закрыты.  
**Разрешение:** Gate 1 может начаться после фиксации этого пакета в репозитории.

## Решения Сергея, зафиксированные в Draft 0.3

1. **Learning deadline = 840 секунд (14 минут).**  
   Это оставляет запас после максимально осторожной последовательной обработки. Pressure остаётся 600 секунд и сознательно не гарантирует прохождение при использовании полного intake timeout на каждом объекте.

2. **Интервалы отстойника — допустимая постфактум-дедукция.**  
   Seeded jitter пока не вводится. Игрок не видит точный countdown; после необратимого списания внимательная команда может косвенно оценивать накопленную опасность по частоте запросов. Это считается легальной информацией в духе игры.

## Что закрыто после Claude review

- Ранний feed разрешён только при `LINE_CLEAR`.
- В `LINE_JAMMED`/`REPAIRING` разрешены действия, необходимые для устранения блокера; feed-intents запрещены.
- Расходование предметов формализовано через `consumes`.
- Confirm tests нормализованы до `tools: []`.
- `min_correctly_processed_anomalies` унифицировано.
- Same-tick stage переименован в `hold_and_procedure_completions`.
- `QUEUED_FOR_SAW` объявлен точкой невозврата.
- Длительность ритуала 4 секунды внесена в текст.
- Добавлен сценарный тест с потраченной святой водой.
- Полный Claude review сохранён в `reviews/CLAUDE_RED_TEAM_REVIEW.md`.

## Следующий порядок

1. Сергей подтверждает Draft 0.3.
2. Пакет переносится в GitHub.
3. Заполняется реальный `.ai/model-profiles.yaml`.
4. Создаётся первый Gate 1 implementation ticket.
5. Codex начинает pure C# domain implementation.

## Final approval

Сергей утвердил пакет формулировкой:

> Утверждаю Gate 0 Draft 0.3 как контракт для Gate 1.

Дата фиксации: 2026-07-19.

## v1.1 operational addition

Added:
- `SOFTWARE_REQUIREMENTS.md`;
- `scripts/check-environment.ps1`.

This does not change gameplay scope or domain contracts.


## v1.2 branding decision

Approved on 2026-07-20:

- public title: `THE LOGS ARE WRONG`;
- tagline: `Quota still applies.`;
- repository: `the-logs-are-wrong`;
- root namespace: `TheLogsAreWrong`;
- internal code: `TLAW`;
- `NONCONFORMING TIMBER` reserved for in-world corporate terminology.

See `PROJECT_IDENTITY.md`.

This is a branding-only update. Gate 0 gameplay, domain, scope and architecture contracts remain unchanged and approved.
