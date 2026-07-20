# LOG_STATE_MACHINE

## Состояния бревна

```text
SCHEDULED
  → AT_FEED_GATE
      → AT_INTAKE
          → AT_PROCEDURE
              → AT_INTAKE
              → HELD_WRITTEN_OFF
          → QUEUED_FOR_SAW
              → IN_SAW
                  → PROCESSED
          → HELD_WRITTEN_OFF
```

`QUEUED_FOR_SAW` — намеренная точка невозврата: штатное списание и возврат на процедурную позицию после этого перехода недоступны. Переход обязан иметь явную светозвуковую телеграфию.

Terminal:

- `PROCESSED`;
- `HELD_WRITTEN_OFF`.

Уникальная аномалия может нарушить необратимость только отдельным событием/ADR.

## Line state

```text
LINE_CLEAR
→ LINE_JAMMED
→ REPAIRING
→ LINE_CLEAR
```

Jam reason является обязательным полем.

## Line noise

```text
QUIET
↔ LOUD
```

Derived из saw/movement/repair activity. Не является ручным переключателем.

## Containment

```text
STABLE
→ SERVICE_REQUESTED
→ OVERDUE
→ INCIDENT
→ STABLE
```

После каждого успешного ritual/incident resolution запускается новый interval по текущему danger weight. Списанные объекты не удаляются.

## Processing result

```text
ProcessingOutcome {
  terminal_state
  credited_species: species | none
  credited_units
  correct_anomaly_delta
  effects[]
}
```

Каждая аномалия задаёт:

- `required_flags`;
- `route_without_flags`;
- `on_correct`;
- `on_incorrect`.

## Intent contract

```text
Intent {
  shift_id
  intent_id
  actor_id_hint
  target_id
  action
  expected_state_version
  client_observed_tick
  parameters
}
```

`actor_id_hint` не является доверенным сетевым полем. Trusted local adapter либо network adapter сопоставляет actor самостоятельно.

## Accepted event

```text
DomainEvent {
  shift_id
  event_id
  event_sequence
  caused_by_intent_id?
  server_tick
  state_version_after
  event_type
  payload
}
```

- `event_sequence` строго возрастает внутри shift.
- `state_version_after` увеличивается с каждым state-changing event.
- Одинаковый `intent_id` не выполняется повторно.

## Rejection

```text
IntentRejected {
  shift_id
  intent_id
  server_tick
  current_state_version
  reason
}
```

Минимальные reasons:

- `SHIFT_MISMATCH`
- `ACTOR_NOT_BOUND`
- `STALE_STATE_VERSION`
- `TARGET_NOT_FOUND`
- `TARGET_NOT_IN_STATE`
- `TARGET_OCCUPIED`
- `MISSING_ITEM`
- `HOLD_NOT_COMPLETE`
- `FEED_ALREADY_PENDING`
- `LINE_NOT_CLEAR`
- `BLOCKING_CONDITION_REMAINS`
- `NO_ACTIVE_REQUEST`
- `NO_MORE_LOGS`

Rejection не увеличивает state version.

## Конкурирующие intents

- Adapter присваивает `server_receive_sequence`.
- В одном host tick intents обрабатываются по этой последовательности.
- Первый валидный intent меняет state.
- Следующий конфликтующий intent отклоняется с актуальным reason/version.
- Intent, принятый сервером не позже deadline, обрабатывается до expiration этого tick.

## Snapshot/replay

```text
ShiftSnapshot {
  shift_id
  server_tick
  state_version
  last_event_sequence
  scheduler_state
  logs[]
  line_state
  containment_state
  inventory
  quota
  objectives
}
```

- Gate 1 умеет восстановить state из snapshot + events после `last_event_sequence`.
- Полный replay от начального manifest должен давать тот же итоговый snapshot.
- Network join/resync использует этот контракт только начиная с Gate 3.

## Обязательные тесты

- Terminal state необратим.
- Processing credit не начисляется дважды.
- Wrong processing соответствует YAML.
- Ritual до сигнала отклоняется.
- Повторные containment cycles детерминированы.
- Same-tick route конфликт разрешается sequence.
- Intake intent на границе deadline имеет установленный приоритет.
- Snapshot + replay совпадает с live state.
- Seed + intents → идентичный journal.
