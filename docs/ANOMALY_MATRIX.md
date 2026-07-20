# ANOMALY_MATRIX — Gate 0–2

## Общий processing contract

| Аномалия | Required flags | Route без flags | Correct credit | Incorrect credit | Effect |
|---|---|---|---|---|---|
| Кающийся | `SANITIZED_PENITENT` | allowed | true species ×1 | none | time penalty |
| Святотатец | `SEALED_RESIN` | allowed | true species ×1 | none | button lock |
| Ложная порода | `CORRECTLY_RELABELED` | allowed | true species ×1 | declared species ×1 | miscredit |

Корректная обработка любой из трёх увеличивает `correctly_processed_anomalies` на 1.

## PENITENT_TRUNK

- Instant: dark resin, cold bark.
- Observed: whisper under line noise.
- Confirm: sound meter, 4 continuous seconds while `LineNoise == QUIET`.
- If noise becomes `LOUD`, test progress resets.
- Procedure: holy water, 3 seconds.
- Required flag: `SANITIZED_PENITENT`.
- Incorrect saw: output rejected, no quota credit, `FALSE_PA_ANNOUNCEMENT`, 8-second time penalty.
- Effect class: `time_penalty`.

## RESIN_BLASPHEMER

- Instant: amber resin, incense odor.
- Observed: faint choir.
- Confirm: choir cassette, 4 seconds.
- Procedure: salt + red tape.
- Required flag: `SEALED_RESIN`.
- Incorrect saw: output rejected, no quota credit, `RESIN_BUTTON_LOCK` for 10 seconds.
- Wrong holy water before saw: same lock, object remains processable.
- Effect class: `lock`.

## FALSE_SPECIES

- Instant: texture/document mismatch.
- Observed: suspicious mass.
- Confirm: scale + caliper, 6 seconds.
- Procedure: relabel stamp.
- Required flag: `CORRECTLY_RELABELED`.
- Incorrect saw: credit goes to declared species.
- Effect class: `miscredit`.

## Причинность

Каждый effect хранит:

- `effect_type`;
- `trigger`;
- `duration_seconds`, если применимо;
- `target`, если применимо;
- `quota_credit`;
- `terminal_state`;
- `correct_anomaly_delta`.

Сложные каскады не входят в Gate 1.


## Item consumption contract

- `holy_water`, `salt`, `red_tape`: consumable, `consumes: true`.
- `sound_meter`, `choir_cassette`, `scale`, `caliper`, `relabel_stamp`, `hamster_statue`: reusable, `consumes: false`.
- Consumable списывается после завершения действия независимо от того, было применение правильным или ошибочным.
- Interrupted/invalid action до completion предмет не списывает.
- Wrong action обязан явно объявлять `consumes`.
