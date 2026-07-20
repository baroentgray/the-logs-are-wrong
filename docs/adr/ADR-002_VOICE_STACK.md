# ADR-002 — Voice Stack and Mimic Routing

**Status:** PROPOSED; implementation after vertical slice.

## Need

Proximity voice, короткий rolling buffer, Mimic playback всем кроме владельца голоса, Steam-only.

## Proposed

Steam Voice capture/compression/decompression; передача пакетов через game network. Получатели выбираются игровым кодом.

## Constraints

- Без постоянного хранения по умолчанию.
- Минимальный in-memory rolling buffer.
- Очистка после использования/смены/disconnect.
- Явная настройка и уведомление.
- Privacy/legal review до Gate 5.

## Spike

Capture; buffer 5–10 сек.; выбор реплики; отправка 2 из 3 клиентов; пространственное playback от бревна; нагрузка; feedback/echo; PTT/mute.

## Vivox alternative

Vivox даёт positional channels, audio taps и injection, но дешёвое исключение одного слушателя для Mimic пока не подтверждено. Остаётся fallback.
