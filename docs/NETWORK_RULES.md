# NETWORK_RULES — контракт Gate 3/4

## Scope boundary

Этот документ определяет будущую сетевую границу, но **FishNet не является зависимостью Gate 1 или Gate 2**.

- Gate 1: pure C#.
- Gate 2: один процесс, local authoritative adapter.
- Gate 3: Steam/listen-server на 2 игроков.
- Gate 4: 4 слота и полный сетевой срез.

## Proposed stack

FishNet + FishySteamworks + Steamworks.NET. Статус `PROPOSED` до smoke-test.

## Authority

| Данные | Авторитет |
|---|---|
| Manifest, seed, deadlines | Host |
| Scheduler и node occupancy | Host |
| Log/line/containment state | Host |
| Procedures, inventory, quota | Host |
| Actor movement | locally responsive; host validates interactions |
| Voice | separate stream, never domain authority |

## Connection binding

- Server создаёт `connection_id → actor_id`.
- `actor_id_hint` из client payload игнорируется либо сверяется.
- Intent без binding отклоняется `ACTOR_NOT_BOUND`.
- Actor не может действовать от имени другого connection.

## Ordering

- Network adapter присваивает `server_receive_sequence`.
- Domain обрабатывает intents по нему.
- State version обязателен.
- Typed rejection возвращается инициатору.
- Accepted domain events реплицируются по `event_sequence`.

## Snapshot/resync

- Host хранит/создаёт `ShiftSnapshot`.
- Join/reconnect не входит в Gate 3, но manual resync для теста допустим.
- Gate 3 клиент может запросить snapshot при обнаружении gap.
- Reconnect gameplay остаётся out of scope.

## Тесты по gates

### Gate 1

- Intent ordering.
- Rejections.
- Snapshot/replay.
- No networking package.

### Gate 2

- Local adapter использует тот же intent API.
- Multiple actor IDs могут эмулироваться в одном процессе.
- No FishNet.

### Gate 3

1. Host/client видят один event sequence.
2. Lag не продлевает deadline.
3. Duplicate intent idempotent.
4. Simultaneous routes resolve deterministically.
5. Containment signal is one host event.
6. Client disconnect does not mutate logs.
7. Host loss ends shift.
8. Steam join works on 2 accounts.

### Gate 4

- 4 simultaneous slots.
- Load/latency test.
- Adaptive configuration.
