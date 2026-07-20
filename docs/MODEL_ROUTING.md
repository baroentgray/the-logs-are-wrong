# MODEL_ROUTING

Тикет требует `execution_profile`, а не вечное название модели.

| Профиль | Назначение | Риск |
|---|---|---|
| `LOCAL_ROUTINE` | данные, docs, логи | LOW |
| `CODEX_FAST` | маленькие проверяемые изменения | LOW |
| `CODEX_STANDARD` | обычная реализация | LOW–MEDIUM |
| `CODEX_DEEP` | интеграция, сеть, сложные баги | MEDIUM–HIGH |
| `CLAUDE_REVIEW` | red-team | MEDIUM–HIGH |
| `CLAUDE_ARCHITECT` | рискованные ADR | HIGH |
| `CLAUDE_BLENDER` | сложный Blender | MEDIUM–HIGH |
| `CHATGPT_ARCH_DESK` | документы и согласование | любой, без merge |

## Поля тикета

```yaml
execution_profile: CODEX_STANDARD
minimum_reasoning: MEDIUM
required_tools: [repository]
quota_requirement: YELLOW_OR_BETTER
fallback_profile: CODEX_FAST
model_switch_policy: checkpoint_required
```

## Run context

Фактическая модель задаётся клиентом/launcher, а не самоописанием агента.

## Fallback

- Автоматически только равный или дешевле.
- Переход вверх — `NEEDS_STRONGER_PROFILE`, тесты, commit, handoff.
- Переключение в том же чате допустимо, но это новая рабочая смена.

## Quota

- GREEN: все задачи профиля.
- YELLOW: короткие/средние с checkpoint.
- RED: закончить checkpoint, новых задач нет.
- UNKNOWN: считать YELLOW.

Шаблон: `.ai/model-profiles.example.yaml`.
