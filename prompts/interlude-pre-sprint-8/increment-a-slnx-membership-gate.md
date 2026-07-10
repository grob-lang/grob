# Pre-Sprint-8 Interlude — Increment A: Solution-membership gate

**Branch:** `fix/slnx-membership-gate`
**Authorises:** D-335
**Model:** Sonnet (Haiku viable — mechanical, self-contained)
**Depends on:** none

## Why

PR #94 (`28eb753`, 2026-06-26) dropped `tests/Grob.Integration.Tests` from `Grob.slnx`
incidental to an unrelated change. For two sprints neither `dotnet test Grob.slnx` nor
CI's bare `dotnet test` executed it. Commit `b1e1f32` restored the line. The line being
back is not the fix — the fix is a mechanical gate that makes any future orphaning a
build failure, in the same shape as D-316's error-code count agreement. An unreferenced
test project must not be able to go green by being invisible.

## Plan gate

Produce a numbered plan and stop for approval. The plan must report:

1. Confirm `tests/Grob.Integration.Tests` is present in `Grob.slnx` as of the branch
   base (it should be, post-`b1e1f32`).
2. Confirm `tests/Grob.Consistency.Tests` exists and its current membership-check
   pattern (D-316 count assertion, D-308 catalog agreement) — the new check joins that
   file and should match its idiom.
3. Confirm the `.slnx` XML shape for project entries (element and attribute names) by
   reading the actual file, not from memory.
4. State how test projects are identified on disk — the rule is every
   `**/*.Tests.csproj` under `tests/`. Confirm no test project legitimately lives
   outside `tests/`; if one does, the plan must widen the enumeration root and say so.

Do not edit until approved.

## Scope

**In:** `tests/Grob.Consistency.Tests/` (one new test class or method plus any small
helper).

**Out:** `Grob.slnx` (already correct — do not re-edit), any source project, any other
test project, `ci.yml` (the gate runs inside the existing consistency suite, which CI
already executes).

## TDD

Structure the check as a **pure, testable function** over inputs, plus one live
assertion over the real repo — so it is properly red/green rather than a tautology that
passes because `b1e1f32` already fixed the tree.

1. **Red.** Write unit tests for a `SolutionMembership` helper:
   - Given a synthetic `.slnx` string missing one of a set of discovered csproj paths,
     the helper returns that path as orphaned. Assert non-empty — **red** (helper does
     not exist yet).
   - Given a synthetic `.slnx` referencing all discovered paths, the helper returns
     empty.
   - Path comparison is case-insensitive and separator-normalised (Windows-native
     target; the runner may be `windows-latest`).
2. **Green.** Implement `SolutionMembership`: parse the `.slnx` for project paths,
   enumerate `**/*.Tests.csproj` under `tests/`, return the set difference.
3. **Live guard.** Add one test that runs the helper against the real repo root and
   asserts zero orphans. This passes now and fails the build the moment a test project
   is dropped.

## Acceptance

- [ ] Unit tests cover the missing-project and all-present cases; both drive red→green.
- [ ] Live guard asserts every `**/*.Tests.csproj` under `tests/` is referenced by
      `Grob.slnx`; passes against the current tree.
- [ ] Removing any test project's line from `Grob.slnx` fails the build (demonstrate
      locally, then restore — do not commit the removal).
- [ ] `dotnet test` green at the solution root.
- [ ] 90% line coverage on the new helper.
- [ ] Error-code count unchanged at 116. No error code touched.

## Commit

One commit. Message names the finding and the durable fix, e.g. `test(consistency): gate
test-project membership in Grob.slnx (D-335)`. Body notes the two-sprint blind spot this
closes and that it generalises D-316.

## Guardrails

British English, no Oxford comma, never "simply". One concern. No source-project changes.
The gate is self-relative — it asserts on-disk projects against the solution, never
against a hard-coded expected count, so it carries no frozen baseline.
