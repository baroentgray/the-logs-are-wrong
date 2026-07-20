# FIRST_SHIFT_SPEC — P0_SHIFT_A

## Конфигурация

| Параметр | Learning | Pressure |
|---|---:|---:|
| Объектов | 12 | 12 |
| Квота | 9 | 9 |
| По породам | 5 сосна + 4 дуб | 5 сосна + 4 дуб |
| Intake timeout | 60 сек. | 45 сек. |
| Hard shift deadline | 14 мин. (840 сек.) | 10 мин. (600 сек.) |
| Свободный запас | 3 | 3 |
| Аномальных | 5 | 5 |
| Минимум корректно переработанных аномалий | 2 | 2 |

## Success predicate

Смена завершается, когда:

- все 12 объектов находятся в terminal state; либо
- наступил hard shift deadline.

Успех:

```text
pine_credit >= 5
AND oak_credit >= 4
AND correctly_processed_anomalies >= 2
```

Ошибки не запрещают успех автоматически, если predicate всё ещё выполнен.

`Learning` допускает максимально осторожное прохождение с полным intake timeout на каждом объекте и запасом на завершение последней обработки. `Pressure` сознательно требует ускорения: full-timeout run должен проваливаться по времени и проверяется отдельным сценарием.

## Состав

- Истинные породы: 6 сосен, 6 дубов.
- 7 обычных объектов.
- 2 × `PENITENT_TRUNK`.
- 2 × `RESIN_BLASPHEMER`.
- 1 × `FALSE_SPECIES`.

Семь обычных дают только 4 сосны и 3 дуба. Списать все пять подозрительных и выполнить план невозможно.

## Scheduler

Полный контракт: `docs/INTAKE_SCHEDULER.md`.

P0-параметры:

- first admission: 0 секунд;
- normal feed delay: 5 секунд;
- early feed delay: 2 секунды;
- saw cycle: 6 секунд;
- repair hold: 6 секунд;
- movement noise: 2 секунды;
- capacities: intake/procedure/saw queue/saw/feed gate — по 1.

## Line noise

Уровень шума — доменный derived state:

```text
LOUD, если:
- активна пила;
- активен механический шум перемещения;
- идёт ремонт.

QUIET — иначе.
```

При изменении испускается `LineNoiseChanged`.

Тест Кающегося ствола требует 4 непрерывных секунды `QUIET`. Возобновление `LOUD` сбрасывает прогресс теста. Intake timer при этом **не останавливается**.

## Инструменты

Расходники:

- holy water ×2;
- salt ×2;
- red tape ×2.

Каждое успешное применение consumable step с `consumes: true` списывает одну единицу в момент завершения действия. Ошибочное применение с `consumes: true` также списывает предмет. Многоразовые инструменты имеют `consumes: false`.
Многоразовые:

- sound meter;
- choir cassette;
- scale;
- caliper;
- relabel stamp;
- hamster statue.

## Processing outcome

### Обычный объект

Правильный распил:

- terminal state: `PROCESSED`;
- credit: 1 единица истинной породы;
- correct anomaly count: +0.

### Кающийся ствол

Required flag: `SANITIZED_PENITENT`.

С флагом:

- credit: истинная порода ×1;
- `correctly_processed_anomalies +1`.

Без флага:

- распил разрешён;
- terminal state: `PROCESSED`;
- output rejected, quota credit: 0;
- `FALSE_PA_ANNOUNCEMENT`;
- effect type: `time_penalty`;
- penalty: 8 секунд.

### Смоляной святотатец

Required flag: `SEALED_RESIN`.

С флагом:

- credit: истинная порода ×1;
- `correctly_processed_anomalies +1`.

Без флага:

- распил разрешён;
- terminal state: `PROCESSED`;
- output rejected, quota credit: 0;
- `RESIN_BUTTON_LOCK`;
- effect type: `lock`;
- duration: 10 секунд.

Применение holy water до распила также вызывает `RESIN_BUTTON_LOCK`, но объект остаётся в текущем состоянии и всё ещё может быть правильно обработан.

### Ложная порода

Required flag: `CORRECTLY_RELABELED`.

С флагом:

- credit: истинная порода ×1;
- `correctly_processed_anomalies +1`.

Без флага:

- распил разрешён;
- terminal state: `PROCESSED`;
- credit: заявленная порода ×1;
- `correctly_processed_anomalies +0`;
- effect type: `miscredit`.

## Отстойник

- Любой объект можно необратимо списать.
- Вместимость не ограничена.
- Обычный объект: danger weight 0.
- `PENITENT_TRUNK` и `RESIN_BLASPHEMER`: danger weight 1.
- `FALSE_SPECIES`: danger weight 0.

Интервалы:

- weight 1: 90 сек.;
- weight 2: 75 сек.;
- weight 3+: 60 сек.

Цикл:

- ритуал: удержание 4 секунды у станции;
1. `STABLE` запускает deadline по текущему суммарному весу.
2. `SERVICE_REQUESTED`: 20 сек.
3. `OVERDUE`: 10 сек.
4. `INCIDENT`.

Успешный ритуал в `SERVICE_REQUESTED`/`OVERDUE`:

- возвращает `STABLE`;
- не удаляет объекты;
- не меняет danger weight;
- запускает новый полный интервал по текущему весу.

Placeholder Gate 2 incident:

- применяется 8-секундная forced line pause один раз;
- состояние остаётся `INCIDENT`, пока не завершён ритуал;
- после ритуала возвращается `STABLE` и запускается новый интервал;
- повторный incident не создаётся до разрешения текущего.

## Критерии сценарного теста

- Correct path достигает success predicate.
- Write off all anomalies не достигает квоты.
- Wrong Penitent/Resin не дают quota credit.
- Wrong False Species даёт declared-species credit.
- Минимум 2 корректные аномалии обязательно для успеха.
- Одинаковый seed + intents дают одинаковый event journal.
- Хотя бы один containment request может пересечься с intake task.
- `Learning full-timeout run` завершается до 840 секунд.
- `Pressure full-timeout run` ожидаемо проваливается по hard deadline.
- Holy water, ошибочно потраченная на Смоляного святотатца, не делает смену математически непроходимой.
- Изменение частоты сигналов после необратимого списания считается допустимой постфактум-дедукцией, а не бесплатным pre-decision detector.
