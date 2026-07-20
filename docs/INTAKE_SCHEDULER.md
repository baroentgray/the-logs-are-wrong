# INTAKE_SCHEDULER

## Назначение

`IntakeScheduler` является частью чистого домена. Сцена не решает, когда появляется следующее бревно и почему возник затор.

## Узлы и ёмкости

| Узел | Ёмкость | Назначение |
|---|---:|---|
| `SUPPLY_QUEUE` | manifest | ещё не поданные объекты |
| `FEED_GATE` | 1 | объект, поданный воротами, но не принятый входом |
| `INTAKE` | 1 | осмотр и intake timer |
| `PROCEDURE` | 1 | обратимый боковой сегмент |
| `SAW_QUEUE` | 1 | ожидание пилы |
| `SAW` | 1 | активный цикл распила |
| `CONTAINMENT` | без лимита | необратимое списание |

## Конфигурация P0

- Первый объект допускается в `INTAKE` при `ShiftStarted`.
- Нормальная подача после освобождения `INTAKE`: 5 секунд.
- Ранняя подача после `EarlyFeedRequested`: 2 секунды.
- Активен не более одного pending feed.
- Порядок manifest никогда не меняется кнопкой ранней подачи.
- Intake timer начинается только событием `LogAdmittedToIntake`.
- Saw cycle: 6 секунд.
- Repair hold: 6 секунд.
- Механический шум перемещения после принятого перехода: 2 секунды.

## Нормальная подача

Когда `INTAKE` становится пустым и одновременно:

- `FEED_GATE` пуст;
- pending feed отсутствует;
- в `SUPPLY_QUEUE` есть объект;

планируется `FeedDue` через 5 секунд.

При наступлении срока:

- если `INTAKE` пуст — объект переводится в `AT_INTAKE`, испускается `LogAdmittedToIntake`, запускается intake deadline;
- если `INTAKE` занят — объект занимает `FEED_GATE`, создаётся `FEED_GATE_BLOCKED` и `LINE_JAMMED`.

## Ранний запрос

`EarlyFeedRequested` разрешён, только когда:

- есть следующий объект;
- `FEED_GATE` пуст;
- pending feed отсутствует;
- линия находится в `LINE_CLEAR`.

Запрос планирует `FeedDue` через 2 секунды **даже при занятом INTAKE**. Это сознательный риск.

Отклонения:

- `NO_MORE_LOGS`;
- `FEED_ALREADY_PENDING`;
- `FEED_GATE_OCCUPIED`;
- `LINE_NOT_CLEAR`.

## Intake deadline

При `LogAdmittedToIntake`:

```text
intake_deadline = host_now + profile.intake_timeout_seconds
```

Если до deadline не принят другой маршрут, scheduler пытается выполнить default auto-route в `SAW_QUEUE`.

- Свободен `SAW_QUEUE` — объект переходит в `QUEUED_FOR_SAW`.
- Занят `SAW_QUEUE` — объект остаётся в `AT_INTAKE`, создаётся `INTAKE_AUTOFEED_BLOCKED` и `LINE_JAMMED`.
- После устранения затора auto-route повторяется.

## Процедурная позиция

- `RouteToProcedure` допустим только при свободном `PROCEDURE`.
- Переход освобождает `INTAKE` и запускает normal feed.
- `ReturnFromProcedure` допустим только при свободном `INTAKE`.
- Если `INTAKE` занят, intent отклоняется `TARGET_OCCUPIED`; затор не создаётся.
- Из `PROCEDURE` можно необратимо списать объект.
- Штатного прямого перехода `PROCEDURE → SAW` нет.

## Пила

- Когда `SAW` свободна и `SAW_QUEUE` занят, цикл начинается автоматически.
- На старте создаётся `SawCycleStarted`.
- Через 6 секунд применяется processing outcome и объект становится `PROCESSED`.
- Затем следующий объект может начать цикл.

## Затор и ремонт

Jam имеет единственную активную причину:

- `FEED_GATE_BLOCKED`;
- `INTAKE_AUTOFEED_BLOCKED`.

Новый feed-intent в `LINE_JAMMED` или `REPAIRING` всегда отклоняется `LINE_NOT_CLEAR`, поэтому второй затор поверх первого недостижим.

В состояниях `LINE_JAMMED` и `REPAIRING` разрешены intents, необходимые для устранения блокера:

- маршрутизация объекта с `INTAKE`;
- перевод на процедурную позицию;
- возврат с процедурной позиции при свободном `INTAKE`;
- применение процедур;
- необратимое списание;
- начало/завершение ремонта.

Блокируются только normal/early feed-intents и действия, требующие движения уже заблокированного механизма.

Перед завершением ремонта блокирующее условие должно быть устранено:

- освобождён `INTAKE` для feed gate;
- освобождён `SAW_QUEUE` для auto-route.

`RepairCompleted` при сохранённом блокере отклоняется `BLOCKING_CONDITION_REMAINS`.

После успешного ремонта scheduler выполняет ожидающий переход.

## Порядок одного host tick

1. Завершения удержаний и процедур с deadline этого tick; завершение цикла пилы сюда не входит.
2. Intents, принятые сервером до cutoff, по `server_receive_sequence`.
3. Истечения intake/shift/containment deadlines.
4. Saw completion и автоматический старт следующего saw cycle.
5. Feed admission и автоматические маршруты.
6. Вычисление jam, line noise и containment transitions.
7. Domain events получают последовательные `event_sequence`.

**Правило границы:** intent, принятый сервером не позже точного deadline, обрабатывается до expiration этого же tick.

## События

- `FeedScheduled`
- `EarlyFeedRequested`
- `FeedRequestRejected`
- `LogPlacedAtFeedGate`
- `LogAdmittedToIntake`
- `IntakeDeadlineStarted`
- `IntakeDeadlineExpired`
- `AutoRouteAttempted`
- `LineJammed`
- `RepairStarted`
- `RepairCompleted`
- `SawCycleStarted`
- `SawCycleCompleted`
