---
name: allocating-an-error-code
description: >
  The single authoritative procedure for allocating a Grob error code — the
  pre-declared-budget model with an allocate-and-surface fallback for the unanticipated
  case. Use whenever an increment needs a diagnostic code: to confirm the budget covers
  it, to make the fold-into-existing versus register-new judgement, or to register a new
  code in lockstep. Codes are immutable once shipped (ADR-0017), so this keeps the
  design judgement explicit while making the mechanics deterministic. writing-an-error-test
  defers to this skill for the code itself.
---

# Allocating an error code

Error codes are a **stability contract** — immutable once shipped (ADR-0017),
surfaced to users through `--explain Exxxx`. So allocation is deliberate. But "never
invent a code" is not the same as "stop dead": the judgement of *whether* a new code is
warranted stays explicit and surfaced, while the mechanics of *which* number and *how*
to register are deterministic. This skill is the one home for both, so the rules stop
disagreeing across documents.

## The model — budget first, ladder second

- **Pre-declared budget.** Each increment prompt declares the error-code budget — the
  codes it expects to touch or add. Those are pre-authorised; registering a budgeted code
  needs no further decision, only correct mechanics (Steps 3–4).
- **The fallback ladder.** When a diagnostic needs a code **outside** the budget — the
  unanticipated case, like the sibling-reference code that surfaced mid-increment — do
  not invent and do not stall. Walk the ladder: confirm nothing existing fits (Step 1),
  surface the fold-versus-new judgement for a decision (Step 2), then register the
  approved code with the deterministic mechanics (Steps 3–4).

The count is **never** asserted from a prompt's arithmetic. The live registry is the
source; the D-316 gate ratifies the real total on the commit (Step 5).

## Step 1 — Confirm no existing code fits

Read `docs/design/grob-error-codes.md` against the live registry — not a snapshot, not
memory. Find the nearest existing code and ask whether the failure is genuinely an
instance of it. The distinction that matters is **semantic, not surface**: E1002
(undefined member) is member *access on a value*; E0012 (unknown field at construction)
is a *named construction site* — different surfaces, different codes, even though both
are "a name that is not there". If an existing code is the right instance, reuse it and
stop here.

## Step 2 — Surface the fold-versus-new judgement

If nothing fits cleanly, the call is a design decision, not an implementation choice.
**Surface it** with the nearest code, why it does or does not fit, and the proposed new
code's category. Do not fold a genuinely distinct failure into an ill-fitting code to
avoid the conversation, and do not register a new code silently. D-330 (E0012 dedicated
rather than folded into E1002) and D-318 (E0008–E0011 dedicated rather than folded into
E0003) are the precedents — dedicated codes where the surface differs. Wait for the
decision, then log it as a `D-###` via `logging-a-decision` (the decision authorises the
code; the registration implements it).

## Step 3 — Allocate the number

- **Category range** (ADR-0014 Category Map): `E0xxx` Type, `E1xxx` Name resolution,
  `E2xxx` Syntax, `E3xxx` Module, `E4xxx` Param/decorator, `E5xxx` Runtime, `E9xxx`
  Internal. Place the code in the range its category demands — a construction-site type
  error is `E00xx`, a parser error is `E2xxx`.
- **Next free in that range, from the live registry tail.** Read the actual highest used
  number in the sub-block and take the next one. Never guess, never take a number from a
  prompt that may be stale — that is exactly what caused the D-306 and D-329 collisions.

## Step 4 — Register in three-location lockstep

In the same edit, in `docs/design/grob-error-codes.md`:

1. **Summary index row** — code, title, category, status (`pre-release`).
2. **Full entry** — code, title, category, status, description, source decision (`D-###`).
3. **Standing total** — increment the canonical total line. This is the single source
   for the present count; the dated footer lines are history, not the live total.

Register the descriptor in the `ErrorCatalog` (D-308) at the same number, so the
`"Exxxx"` string appears exactly once in the solution. No literal at a call site.

## Step 5 — Reconcile the count and let the gate ratify

The count moves against the **live** total, not a prompt's `N → N+1`. After registering,
the D-316 consistency gate (`Grob.Consistency.Tests`) asserts
`ErrorCatalog.All.Count == ` the registry total on the commit. A green gate is the proof
the count is right. If an increment adds more codes than its budget anticipated (the
budget said one, reality needed two), the budget arithmetic in the prompt is the stale
part — reconcile to the live total and note the divergence; do not bend the registry to
match the prompt.

## Step 6 — The gold-master pair

A new code needs its negative-test coverage. Hand off to `writing-an-error-test` for the
`*_grob.txt` / `*_expected.txt` pair — that skill owns the gold master and defers to this
one for the code itself.

## Checklist

- [ ] Budget checked — is this code pre-authorised, or a ladder case?
- [ ] No existing code fits (semantic match, not surface) — confirmed against the live registry
- [ ] Fold-versus-new judgement surfaced and decided; `D-###` logged for a new code
- [ ] Correct category range; next free number taken from the live tail, never guessed
- [ ] Three-location lockstep — summary row, full entry, standing total — plus the `ErrorCatalog` descriptor
- [ ] Count reconciled against the live total; D-316 gate green on the commit
- [ ] Gold-master pair authored via `writing-an-error-test`
