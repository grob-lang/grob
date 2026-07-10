# Pre-Sprint-8 Interlude — Value Display and Solution Integrity

**State:** post-Sprint-7 Increment E, pre-QA cold-read. Predecessor commit `b1e1f32`
on `feat/errors-close` (local).

**Authorises:** D-335, D-336, D-337.

This interlude clears the findings surfaced at the 7E hand-off so the Sprint 7 QA
cold-read runs against an honest repository, and so Sprint 8 (`formatAs`) builds on a
reconciled `print` path rather than an undecided one. It is remediation, not feature
work — everything here is owed by a decision already in the log.

## The problem in one paragraph

`Grob.Integration.Tests` was silently dropped from `Grob.slnx` in PR #94 and unrun by
CI for two sprints (D-335). That blind spot let `Sprint6IncrementBTests` be merged
asserting behaviour the VM never had: `print()` on a struct emits `[TypeName]`, not
field values. The `[TypeName]` fall-through was never a decision — it is a debug
formatter's fall-through arm that resembles D-159's deliberate credential opacity by
coincidence, and because all plugin types and user `type`s share the `Struct`
discriminator (D-297), that same arm is currently the *only* thing keeping
`AuthHeader` from printing its bearer token. D-336 replaces the whole mess with a
`ValueDisplay` service under a numbered, security-ordered dispatch precedence.

## Increments

Run one per chat session, fresh corpus zip each time, plan-gate before any edit.

| # | Branch | Authorises | Model | Depends on |
|---|--------|-----------|-------|-----------|
| A | `fix/slnx-membership-gate` | D-335 | Sonnet (Haiku viable) | none |
| B | `feat/value-display-service` | D-336 | **Opus** — the one load-bearing sub-problem | none |
| C | `feat/value-display-wiring` | D-336 | Sonnet | B |
| D | `chore/7e-archive-hygiene` | D-335, D-336 | Haiku | none |

**Sequence.** A and B are independent and can run in either order or in parallel
sessions. C depends on B. D is independent and can run any time. The natural order is
A → B → C → D, with the QA cold-read after C lands.

**Branch base.** Confirm at each increment's plan gate whether the branch bases off
`main`, off `feat/errors-close` (`b1e1f32`), or off a merge of it. This interlude runs
before Sprint 7 closes, so the base is not yet `main`. Do not assume.

## Standing guardrails (all increments)

- Plan-mode approval gate before any edit. Surface a numbered plan, flag genuine
  decisions, stop for approval.
- TDD red/green/refactor. Tests written and confirmed failing before implementation.
- 90% minimum line coverage on new code. FsCheck for properties where stated.
- No error code added or changed. Count stays at **116** (D-334).
- `.slnx` solution format. British English throughout, no Oxford comma, never "simply".
- One concern per branch. `/start-branch` and `/commit-message` refuse on `main`.
- Read live files before claiming their contents. Where a brief says "confirm", that is
  a plan-gate verification, not an assumption to carry.
