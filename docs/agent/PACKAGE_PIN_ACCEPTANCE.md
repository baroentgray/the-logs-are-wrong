# Package pin acceptance record

Schema: `tlaw.package-pin-acceptance/v1`.

Date: `2026-08-12`.

This record is immutable. A later version change is recorded as a new decision and a new record, never by rewriting
this file (D-012).

## Identity

| Field | Value |
| --- | --- |
| GitHub contract | [Issue #121](https://github.com/baroentgray/the-logs-are-wrong/issues/121) |
| Accepted exact main baseline | `08d617bcb5506de251faf38bfa07e4e19b5a5494` |
| Decision record | `D-017` in `docs/agent/DECISIONS.md` |
| Prerequisite evidence | TLAW-049 / [Issue #118](https://github.com/baroentgray/the-logs-are-wrong/issues/118), verdict `PACKAGE_MATRIX_SMOKE_PASS` |
| Smoke evidence document | `docs/agent/PACKAGE_SMOKE_TEST.md` |

## Accepted pins

| Component | Accepted version | Resolved identity |
| --- | --- | --- |
| Unity Editor | `6000.3.21f1` | changeset `c02631ffc030` |
| FishNet | `4.7.2` | upstream tag → commit `de19b5d66459f60400ffd0edc443c4da173a01e7` |
| Steamworks.NET | `2025.164.1` | annotated tag object `d6930827976de076964a97f713fea0b557783a54` → peeled commit `c21a8f0e31c56ae8707130967faf491f7dd7c0d8` |
| FishySteamworks | `4.1.1` | upstream tag → commit `21e858249249e2c322365fe9fefbe865f290b0d9` |

FishySteamworks is installed from its official release asset, not from a package registry:

| Artifact | Size | SHA-256 |
| --- | --- | --- |
| `FishySteamworks.4.1.1.unitypackage` | `17,188` bytes | `5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57` |

The Steamworks.NET tag is annotated, so it has two distinct hashes. Both are recorded above so the tag object is never
mistaken for the commit: `git ls-remote` reports the tag object `d693082…`, while the Unity lockfile records the peeled
commit `c21a8f0…`.

## Accepted Steam transport configuration

`_peerToPeer=true` is part of this acceptance and must be carried explicitly into Gate-3 setup and handoff.

The shipped default `_peerToPeer=false` is **not** the accepted Steam P2P configuration. In that path FishySteamworks
calls `SteamNetworkingSockets.CreateListenSocketIP`, which is not the supported Steam client-app path; during TLAW-049
it made `ServerManager.StartConnection()` return `false` with **no diagnostic output whatsoever**, because
`ServerSocket.StartConnection` catches its exception in a bare `catch` and FishNet's `LogError` path emitted nothing.

Treat this as a known trap for Gate 3: a misconfigured FishySteamworks server fails silently. Setting the flag is an
ordinary serialized Inspector value — this acceptance patches, forks, or vendors no package source.

## TLAW-049 smoke evidence

| Field | Value |
| --- | --- |
| Candidate head (PR #120) | `c69838d776b32406bf4d18f25ac7470ea03c040c` |
| PR exact-head CI run | `31609602249` |
| Merged exact main | `08d617bcb5506de251faf38bfa07e4e19b5a5494` |
| Exact-main push run | `31624345300` |
| Exact-main job | `94206759645` — `Deterministic verification`, success |
| Exact-main artifact | `9152530856` — `verification-31624345300` |
| Artifact digest | `sha256:cfe483464a26459ba5b164b31f88f8c25650b55ac8a125001cd57c4deaa5e3d6` |
| Verifier | PASS |
| Tests | `1631` passed / `0` failed / `0` skipped |
| Gate 0 | PASS; Git object reader `52/52` PASS |

The run, job, artifact identity and digest above were re-confirmed against the GitHub API while writing this record.

## Frozen Gate-0 boundary

`docs/NETWORK_RULES.md` and `GATE_0_EXIT_CHECKLIST.md` remain **byte-for-byte unchanged**. Both are protected by exact
SHA-256 in `tools/Tlaw.Verify/Gate0/gate0-baseline.json`:

| Frozen path | Baseline SHA-256 |
| --- | --- |
| `docs/NETWORK_RULES.md` | `2077f8f345277e953e6dfadb1b2ae43c5cecd20038498a68fc8b524eeac36687` |
| `GATE_0_EXIT_CHECKLIST.md` | `0fa908d0c0f71aca4d9263fea4badacf8ff65cd06d3ab8d6f284e1a30945afa0` |

Consequently two pieces of frozen text now read as historically earlier than reality, and that is intended:

- `GATE_0_EXIT_CHECKLIST.md` § Network still shows `After Gate 1: isolated package smoke-test` and
  `After smoke-test: exact pins accepted` as unchecked;
- `docs/NETWORK_RULES.md` § "Proposed stack" still marks FishNet + FishySteamworks + Steamworks.NET as `PROPOSED`
  until smoke-test.

**Neither is a reversal of this acceptance.** They are the frozen Gate-0 baseline as approved at Gate-0 exit. The
smoke-test has since been performed (TLAW-049, `PACKAGE_MATRIX_SMOKE_PASS`) and the pins have since been accepted by
the owner (D-017 and this record). The later authoritative state is recorded append-only under `docs/agent/**`, which
D-002 places outside frozen Gate 0. Reading order is therefore: frozen Gate-0 documents give the historical baseline;
`docs/agent/DECISIONS.md` and this record give the current accepted state.

## Scope limits

- Accepting these pins **does not start Gate 2 or Gate 3.** Neither has been started.
- Gate 2 remains a single local Unity process with one local authoritative host and **no** FishNet, Steamworks, or
  other networking dependency.
- FishNet, FishySteamworks and Steamworks.NET are for Gate 3+ networking work only.
- No Unity, FishNet, FishySteamworks, or Steamworks.NET production dependency exists in this repository, and none was
  added by this task.
- The TLAW-049 package smoke was **not** re-run; this task records an already-made owner decision.
- Branch, worktree, and temporary smoke-evidence cleanup is **not authorized** and was not performed.
- Any future change to an accepted version or resolved identity requires a new explicit owner decision and a new
  record, not an edit to D-017 or to this file.
