---
name: "Sprint 1 · Increment C — AST + Visitor Base"
description: "Sprint 1 AST hierarchy with first-class ErrorExpr/ErrorStmt/ErrorDecl and a visitor base that forces every consumer to handle them."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 1 · Increment C — AST + Visitor Base

You are picking up Sprint 1 of the Grob compiler at **Increment C** of a five-part
breakdown. Read `.github/prompts/sprint-1-kickoff.prompt.md`,
`docs/design/grob-v1-requirements.md` §4 (Sprint 1) and especially
`docs/design/grob-language-fundamentals.md` §29 (parser error recovery — the AST
shape lives or dies on getting the error nodes right) before touching code.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/compiler-ast-and-visitor-base`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- Solution scaffold, `Grob.Core` foundations.
- **Increment A:** `TokenKind`, `Token` (with `BracketDepth`).
- **Increment B:** Full lexer with diagnostics and line-continuation, all tests
  green.

## Deliverable for this increment

The AST hierarchy and visitor base in `Grob.Compiler`. **No parser yet** — this
increment lays down the shape the parser will produce in Increment D.

1. **AST node hierarchy** covering the Sprint 1 grammar enumerated in §4:
    - **Expressions**: literals (int, float, string with interpolation parts,
      raw string, regex, bool, nil), identifier, unary, binary, grouping,
      ternary, array literal, index, member access, call, lambda.
    - **Statements**: variable declaration (`:=`), assignment (`=`), compound
      assignment, `++`/`--`, `print()`, `exit()`, `if`/`else if`/`else`,
      `while`, `for…in` (collection and numeric range), `select`/`case`/
      `default`, `break`, `continue`, `return`, `try`/`catch`, block.
    - **Declarations**: `fn`, `type`, `param`, `import`, `const`, `readonly`.
    - **Error nodes — first-class, day one**: `ErrorExpr`, `ErrorStmt`,
      `ErrorDecl`. Each carries the `SourceRange` of the failed parse and the
      `Diagnostic` that produced it. These are siblings in the hierarchy, not
      special-cased.
    - Every node carries a `SourceRange`. Use sealed records / hierarchy as
      idiomatic in modern C#.

2. **Visitor base.** Provide `AstVisitor<T>` (returns a value) and `AstWalker`
   (void traversal). Both must require explicit overrides for the three error
   node kinds — make the visit-methods `abstract` so that **every future
   visitor in Sprints 2+ is mechanically forced to handle them**. This is the
   point: §29.2 says every traversal handles error nodes, and we enforce it
   with the type system rather than discipline.

3. **Tests** in `Grob.Compiler.Tests`:
    - A minimal pretty-printer test for a handful of constructed AST shapes
      (build the AST by hand, walk it, assert the output). This is purely a
      visitor-dispatch smoke test — no parsing involved.
    - Visitor dispatch tests including each of the three error node kinds.
    - A "missing override" compile-error scenario is optional but documented in
      the hand-off (we'll lean on the compiler error itself in future sprints).

## Out of scope

No lexer changes. No parser. No type checking. Do not implement a pretty
printer beyond what's needed for the smoke test. Do not start building error
recovery — that is Increment D's whole job.

## Acceptance

- `dotnet build` and `dotnet test` green.
- A consumer (the parser to come) can produce an AST that includes error
  nodes without any special escape hatch.
- A new `AstVisitor<T>` subclass cannot compile without implementing
  `VisitErrorExpr`, `VisitErrorStmt`, `VisitErrorDecl`.

## Model

Sonnet for the bulk; consider Opus only if the visitor-base contract gets
intricate.

## Hand-off

Summarise: file layout of the AST, the visitor-base contract, and any naming
choices that depart from the spec wording. Next chat starts Increment D
(error-recovering parser) — this is the Opus chat.
