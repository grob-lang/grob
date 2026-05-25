---
name: "Sprint 1 · Increment A — Tokens"
description: "Define the complete TokenKind enum and Token record in Grob.Core."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 1 · Increment A — Tokens

You are picking up Sprint 1 of the Grob compiler at **Increment A** of a five-part
breakdown agreed with the maintainer. The Sprint 1 scope is in
`docs/design/grob-v1-requirements.md` §4. The increment plan is summarised in
`docs/design/grob-decisions-log.md` (look for the most recent Sprint 1 entry) and
in the original kickoff at `.github/prompts/sprint-1-kickoff.prompt.md`. Read both
before touching code.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/core-token-kind-and-token`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- Solution scaffold with the seven `src/`, three `plugins/` and five `tests/`
  projects, .NET 10, DAG clean (`Grob.Compiler` ↔ `Grob.Vm` do not reference each
  other).
- `Grob.Core`: `SourceLocation`, `SourceRange`, `Severity`, `Diagnostic`,
  `DiagnosticBag` + xUnit tests.

## Deliverable for this increment

1. **`TokenKind` enum, complete from the start**, per
   `docs/design/grob-v1-requirements.md` §3.4. Every keyword, every operator,
   every punctuation, every literal kind, every structure token (`Newline`,
   `EOF`, `Error`). Built-ins (`print`, `exit`, `input`) are **identifiers, not
   keywords** — do not add token kinds for them.
2. **`Token` record** in `Grob.Core` with fields:
   `Kind`, `Lexeme`, `Location` (`SourceLocation`), `BracketDepth` (int).
   `BracketDepth` is the lexer's depth count at this token; the parser uses it
   to identify statement-boundary newlines without re-tracking. This was a
   deliberate design call — see the kickoff confirmation thread.
3. **Tests in `Grob.Core.Tests`**: enum surface (every requirements-listed kind
   present), `Token` equality, `Token.ToString` shape.

## Out of scope for this increment

No lexer. No AST. No parser. Do not touch `Grob.Compiler` source beyond what is
already there. If you spot something to improve elsewhere, leave it alone.

## Acceptance

- `dotnet build` green at the solution root.
- `dotnet test` green.
- A maintainer reading the enum can map every entry to a line in §3.4.

## Model

Sonnet — this is mechanical. Save the Opus budget for Increment D.

## Hand-off

When done, summarise: enum count, any §3.4 ambiguities you resolved and how, and
list the test files added. The next chat will start Increment B (Lexer).
