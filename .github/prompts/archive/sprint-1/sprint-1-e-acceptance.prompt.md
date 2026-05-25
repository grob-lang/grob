---
name: "Sprint 1 · Increment E — Recovery Acceptance Tests"
description: "Close Sprint 1: the named recovery acceptance scenarios plus an end-to-end smoke test in Grob.Integration.Tests."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 1 · Increment E — Recovery Acceptance Tests

You are closing out Sprint 1 at **Increment E** of the five-part breakdown.
Read `.github/prompts/sprint-1-kickoff.prompt.md`,
`docs/design/grob-v1-requirements.md` §4 (Sprint 1 acceptance criteria) and
`docs/design/grob-language-fundamentals.md` §29 — especially §29.6 (worked
example) — before writing tests.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `test/compiler-sprint-1-acceptance`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Increments A–D**: complete `TokenKind` + `Token`, full lexer, AST with
  first-class error nodes, error-recovering recursive-descent parser. All
  prior tests green.

## Deliverable for this increment

Tests, fixtures and a smoke-test end-to-end run. **No production-code changes
unless a genuine defect surfaces** — if it does, call it out explicitly in the
hand-off rather than silently patching.

1. In `Grob.Compiler.Tests`, the three named Sprint 1 acceptance scenarios:
    - **Malformed expression mid-function.** Given a function with a broken
      expression somewhere in its body, assert: rest of the file parses into a
      complete AST, `ErrorExpr` placeholder sits at the failure site with the
      correct `SourceRange`, exactly one diagnostic is emitted for this failure.
    - **Malformed declaration.** Given a file where one top-level declaration
      fails to parse, assert: subsequent declarations parse cleanly into the
      AST, the broken declaration is an `ErrorDecl`, exactly one diagnostic.
    - **Multi-error file with no cascade.** Given a file with three or more
      independent failures, assert: one diagnostic per root cause, no extras
      attributable to cascade in the parser's own output, AST shape matches
      expectations between the errors.

2. The **§29.6 worked example** as a verbatim fixture. Lex + parse, assert:
   exactly two diagnostics with the expected codes/messages, AST structure
   matches the spec's description, ranges match.

3. In `Grob.Integration.Tests`, a smoke test: load one or more representative
   `.grob` sources, run them through lexer + parser, assert no exceptions and
   that the diagnostic count matches the fixture's expectation. This is _not_
   an execution test — there is no VM yet. The intent is to prove the
   end-to-end pipeline boundary holds.

4. Fixtures land in a dedicated folder (`tests/fixtures/sprint-1/` or the
   convention already established in the repo — check before inventing one).
   Use `.grob` source files plus paired `.expected.txt`-style assertions where
   that pattern already exists in `grob-project-knowledge/`.

## Out of scope

No type checker. No `Error`-type cascade suppression (it lands with the type
checker in Sprint 2 — your cascade-suppression assertions are about the
parser's own output, not anything downstream). No new grammar coverage —
that was Increment D's job.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- All three Sprint 1 acceptance criteria from §4 are covered by a named test.
- The §29.6 worked example is a passing test.
- Smoke integration test passes.
- Sprint 1 is closeable: lexer, parser, diagnostics and error recovery all
  demonstrably meet the spec.

## Model

Sonnet — fixture authoring and assertion writing. Save the Opus budget for
Sprint 2's type-checker design.

## Hand-off

Summarise: tests added, fixtures added, any spec ambiguities surfaced while
writing the assertions, and a one-paragraph "Sprint 1 closed" statement
suitable for the decisions log.
