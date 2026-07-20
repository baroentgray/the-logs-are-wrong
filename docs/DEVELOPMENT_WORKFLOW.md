# DEVELOPMENT_WORKFLOW

## Источники истины

GitHub — код, документы, ADR, данные, тесты, handoff. Linear — очередь и статус. Чаты не являются памятью проекта.

## Состояния тикета

`BACKLOG → READY NOW → CLAIMED → IN PROGRESS → READY FOR REVIEW → ACCEPTED`

Альтернативы: `BLOCKED`, `NEEDS_STRONGER_PROFILE`, `CHANGES REQUESTED`.

## Ветки

- `task/NS-042-log-intake-timer`
- `fix/NS-057-idempotent-intent`
- `docs/NS-011-network-rules`

Один тикет — одна ветка/worktree. Ни один агент не мержит `main`.

## Handoff

Обязательны: task, profile/model/reasoning/quota, result, files, tests, manual verification, risks, not changed, next action.

## Gate 0 sequence

1. ChatGPT готовит пакет.
2. Codex feasibility review без игрового кода.
3. ChatGPT сводит правки.
4. Claude red-team всего пакета.
5. Сергей утверждает.
6. Gate 1 после checklist.

## Gate 1 order

1. IDs/value objects.
2. Host clock abstraction.
3. Manifest/seed.
4. Log state machine.
5. Quota.
6. Procedures/anomaly data.
7. Containment state machine.
8. Jam/repair.
9. Event journal.
10. Determinism tests.
