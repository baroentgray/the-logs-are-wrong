# PROTOTYPE_SCOPE

## Вопросы прототипа

1. Интересно ли сомневаться между похожими аномалиями?
2. Возникают ли споры «проверять или рисковать»?
3. Работает ли ограниченная поставка как давление?
4. Создаёт ли intake scheduler понятный каскад?
5. Выдёргивает ли сигнал отстойника игрока из ритма?
6. Понимают ли игроки причинность ошибки?

## Gate 1 — чистая C# domain simulation

### Входит

- Typed YAML configuration.
- Host clock abstraction.
- Event journal, state version, snapshots и replay.
- Детерминированный `IntakeScheduler`.
- Ёмкости узлов и порядок одного host tick.
- Манифест `P0_SHIFT_A`.
- State machines брёвен, линии и отстойника.
- Уровень шума линии как доменный predicate.
- Предметы, процедуры, последствия ошибок.
- Квота и `min_correctly_processed_anomalies`.
- Unit/scenario/replay tests.

### Ограничение

`TheLogsAreWrong.Domain` не зависит от:

- `UnityEngine`;
- FishNet/NGO;
- сцен, prefab и MonoBehaviour;
- аудио и графики;
- YAML-библиотеки.

YAML — внешний adapter.

## Gate 2 — локальный цифровой технический прототип

### Входит

- Один Unity-процесс и один локальный authoritative simulation host.
- Unity-представление domain events.
- Один цех из примитивов.
- Scripted test driver и ручное управление одним человеком.
- Возможность локально эмулировать несколько actor intents без сети.
- `ADAPTIVE`, `FULL_LINE`, `CUSTOM`.
- Вход, процедурная позиция, пила, отстойник.
- 3 аномалии, 2 породы, P0 manifest.
- Intake scheduler, ранний запрос, один затор и ремонт.
- Автовыгрузка.
- Сигнал/ритуал/placeholder incident отстойника.
- Диегетическая телеграфия.

### Не входит

- FishNet, Steam lobby или transport.
- Реальный host/client.
- Второй физический компьютер.
- Подражатель и voice.
- Разные инструкции.
- Смерть, толчки и рэгдолл.
- Финальный арт.

## Gate 3 — первый сетевой тест

### Входит

- Отдельный smoke-test pin-матрицы Unity/FishNet/FishySteamworks/Steamworks.NET.
- Steam-сессия на 2 реальных игроков.
- Listen server.
- Domain intents/events через network adapter.
- Синхронизация P0_SHIFT_A.
- Disconnect handling и понятное завершение при потере host.
- Проверка таймеров под latency.

### Не входит

- Полный баланс на 4 игроков.
- Voice.
- Host migration.
- Reconnect внутри смены.

## Gate 4 — сетевой вертикальный срез

- 4 слота.
- Полный сетевой first playable.
- Адаптивные обязанности по числу игроков.
- Арт-направление и выбранный персонаж.
- Реальные метрики плейтеста.

## Stop condition

Новая аномалия или поздняя система не входит, пока предыдущий gate не ответил на свои вопросы.
