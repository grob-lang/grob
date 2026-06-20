# Grob — Sprint 4 → 5 Interlude

Two self-contained increment prompts that sit in the gap between Sprint 4
(Control Flow) and Sprint 5 (Functions and Closures). Neither adds language
surface. Both are infrastructure increments and run as separate sessions, each
against a fresh repo zip, in order.

| Increment | File | Authorises | Purpose |
|---|---|---|---|
| A — Correctness & Drift | `interlude-increment-A-correctness-drift.md` | D-316 | Reconcile the corpus to a green floor, then land a permanent CI-enforced drift gate so it cannot silently reappear |
| B — Hardening | `interlude-increment-B-hardening.md` | D-317 (+ OQ-018) | Supply-chain, build, workflow, secret and provenance hardening of the Grob project itself |

## Run order — A before B

A first, for two reasons. The drift gate must be landed and green on a
reconciled corpus before anything else moves, so its baseline is clean. And B
edits build and CI files; doing that on top of an already-passing consistency
floor means any regression B introduces is caught by A's gate immediately rather
than discovered later.

## Positioning

The 12-sprint plan in `grob-v1-requirements.md` has no hardening sprint and no
interlude. These two increments are the interlude. They are additive
infrastructure — each carries a closed-surface rule that protects `src/`
language code, the spec documents' language content, and the error catalog and
registry from modification.

## Number allocation — verify before relying

At time of authoring the live decisions log tops out at D-315 and the open
questions at OQ-017. The prompts allocate:

- **D-316** — Increment A authorising entry
- **D-317** — Increment B authorising entry
- **OQ-018** — code signing and release provenance (deferred; see Increment B)

Number collisions have caused errors before (the D-286 collision is on record in
the decisions-log changelog). Each prompt instructs the executor to confirm
these are still the first free numbers against the live log before writing. The
canonical on-disk files outrank any number stated here.

## State corrected at authoring time

For the record, the authoring pass found and is built against the following live
state, which diverged from prior session framing:

- Error-code registry is at **99 codes** across 7 categories (Sprint 4
  Increment C added E0501–E0504, Increment E added E0505). Prior framing said 86
  and 94. Increment A's gate is designed to make this count self-checking.
- clox is complete (May 2026). The Project Status milestone row reading
  "Sprint 1" is stale — flagged for Increment A reconciliation.
- §11 Security in `grob-v1-requirements.md` covers language and runtime safety
  only. Increment B's supply-chain hardening is net-new and does not collide
  with it.
