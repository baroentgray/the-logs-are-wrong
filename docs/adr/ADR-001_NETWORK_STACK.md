# ADR-001 — Network Stack

**Status:** PROPOSED / VERSION_UNVERIFIED  
**Implementation gate:** Gate 3

## Proposed

- Unity `6000.3.x` LTS line.
- FishNet high-level layer.
- FishySteamworks transport.
- Steamworks.NET.
- Domain assembly independent from all four.

## Candidate smoke-test matrix

These are candidates from Codex feasibility review, not accepted production pins:

| Component | Candidate |
|---|---|
| Unity | `6000.3.13f1` |
| FishNet | `4.7.2R` |
| FishySteamworks | `4.1.1` / commit `21e8582` |
| Steamworks.NET | `2025.163.0` / commit `98d6584` |

## Known compatibility risk

FishySteamworks `4.1.1` is older than the candidate FishNet release, and its package metadata does not declare a FishNet version dependency. Therefore semantic version matching cannot prove compatibility.

## Decision rule

Do not add these packages during Gate 1/2.

After Gate 1:

1. Create isolated empty Unity smoke-test project.
2. Install exact candidate refs, never floating branches.
3. Validate editor compile and Windows build.
4. Start local host/client with non-Steam transport.
5. Start Steam lobby/session using two Steam accounts.
6. Connect four empty slots if technically possible.
7. Record package manifest, commits and known warnings.
8. Accept pins only after successful result.

If matrix fails, test nearest compatible FishNet tag or evaluate NGO/MPS with a concrete comparison.

## Current verdict

Architecture is feasible. Package compatibility is conditional until smoke-test.
