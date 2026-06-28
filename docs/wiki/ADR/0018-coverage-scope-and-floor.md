# ADR-0018 — Test-coverage scope and floor

- **Status:** Accepted — June 2026
- **Decision record:** D-328 (`grob-decisions-log.md`)
- **Supersedes:** none
- **Superseded by:** none
- **Relates to:** ADR-0014 (numbering scheme — mechanical-gate precedent), D-313 (two-axis
  benchmark gate), D-316 (CI-enforced corpus drift regime), D-308 (catalog-not-literal discipline)

---

## Context

Overall SonarCloud line coverage drifted to **88.9%**, below the project's standing 90%
expectation. Investigation showed the gap was two unrelated problems wearing one number:

1. **Genuine under-tested logic.** `Compiler.Statements.cs` at 49.5% with 43 uncovered
   conditions — half-covered statement bytecode emission, the worst failure class because it
   compiles green and emits incorrect runtime behaviour. The type-checker statement surface
   sat similarly exposed.
2. **Structurally-untestable or out-of-scope code counted as if it were testable.** CLI
   read-print IO loops (`ReplCommand`), `tooling/` `Main` entrypoint wrappers, and single
   defensive branches on record types — all dragging the denominator down with no risk behind
   them.

A bare percentage with no defined scope is undefined. It drifts because nobody can say what it
is supposed to *mean*, and the obvious remedy — "top up to 90%" — invites assertion-free tests
that exercise lines without pinning behaviour. That is quality theatre: it inflates the
maintenance surface and manufactures false confidence, which is worse than the gap it closes.

A confounding case sat inside the same data: `RunCommand` and `DiagnosticFormatter` read as 0%
despite being exercised end-to-end by the validation suite and the gold-master error-examples
library — an instrumentation gap, not an absence of tests. Excluding them to make the number
move would have hidden exactly the user-facing run-and-diagnostic surface that Grob's identity
("a developer can trust") most depends on.

## Decision

Coverage gets a **defined scope** and a **mechanical floor**.

### 1. Scope — a committed exclusion set

What is deliberately *out* of the coverage denominator is declared as committed configuration
(`sonar.coverage.exclusions`, with matching `[ExcludeFromCodeCoverage]` / `.runsettings` for
local parity). **Every exclusion carries a reason.** The set comprises:

- CLI process and IO shells — specifically the REPL read-print loop *after* its eval core is
  extracted into a testable unit. The eval core is covered; the loop is excluded.
- `tooling/` `Main` entrypoint wrappers.
- Explicitly-annotated defensive or structurally-unreachable branches, including value variants
  the compiler does not yet emit.

`RunCommand` and `DiagnosticFormatter` are **not** excluded. They are covered by fixing the
validation-suite and gold-master instrumentation so the collector sees the paths those suites
already exercise.

What remains in the denominator is the language implementation proper — lexer, parser, type
checker, compiler, VM, runtime, stdlib — the surface where a coverage number carries meaning.

### 2. Floor — a CI-enforced gate

The in-scope denominator carries a **90% line+branch** coverage floor, enforced in CI, red on
breach. The gate is mechanical, not eyeballed, in the shape of the D-313 benchmark gate, and
self-relative in the shape of the D-316 drift regime — no frozen baseline. It reads blended
coverage including conditions, because branch coverage on the parser and type checker is the
number that actually protects against silent regressions.

The floor is a **tripwire that triggers triage, not a target to fill.** Reaching it by writing
assertion-free tests is a violation of the spirit of this decision, not compliance with it.

## Consequences

- The coverage number becomes meaningful and stable. Future drift is caught at the pull request
  that causes it, not discovered a sprint later.
- The exclusion set is reviewable in the diff — the same property that makes committed benchmark
  baselines auditable. An unexplained exclusion fails review.
- The incentive to write filler tests for untestable shells is removed: those shells are out of
  scope by declaration, so there is nothing to game.
- User-facing run and diagnostic surface is held *in* scope and covered, reinforcing the
  trust identity rather than quietly eroding it for a cleaner percentage.
- The cognitive-complexity refactor of the type-checker statement-visit method
  (`csharpsquid:S3776`) is sequenced with this work: cover to pin behaviour, decompose under
  green, confirm coverage holds. Refactor and coverage are complementary, not two chores.

## Notes

This decision adds no error codes and changes no language semantics. It is implemented as
Sprint 5 Increment 6, before the Codex cold-read pass, so the adversarial QA begins from a clean
board.
