---
name: "Sprint 1 · Increment D — Error-Recovering Parser"
description: "The defining Sprint 1 deliverable: a stateless recursive-descent parser with error recovery wired in from the first parse method, per D-300 / §29."
agent: "Grob Compiler Engineer"
tools:
    [
        "search",
        "read",
        "edit",
        "execute",
        "microsoft-docs/microsoft_docs_search",
    ]
---

# Sprint 1 · Increment D — Error-Recovering Parser

This is the **defining Sprint 1 deliverable** and the increment the maintainer
flagged as worth a stronger model. Read, in order:

1. `.github/prompts/sprint-1-kickoff.prompt.md`
2. `docs/design/grob-v1-requirements.md` §4 (Sprint 1)
3. `docs/design/grob-language-fundamentals.md` **§29 in full** — synchronisation
   set, error nodes, cascade suppression, statelessness, worked example. This
   is the spec for what you build.
4. Decision **D-300** in `docs/design/grob-decisions-log.md`.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/compiler-error-recovering-parser`.
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
- **Increment B:** Full lexer.
- **Increment C:** AST hierarchy with first-class `ErrorExpr`/`ErrorStmt`/
  `ErrorDecl`, and a visitor base whose three abstract `VisitError*` methods
  force every traversal to handle error nodes.

## Deliverable for this increment

A recursive-descent **`Parser`** in `Grob.Compiler`. Input:
`IReadOnlyList<Token>` + `DiagnosticBag`. Output: AST root.

Non-negotiable properties — these are why we set this up as a fresh chat:

- **Stateless** across files and invocations. No fields beyond the immediate
  parse state (cursor, the token list, the diagnostic bag, current AST being
  built). No learned anchors, no cross-file memory. §29.5.
- **Error recovery wired into every parse method from the first one written,
  not retrofitted.** Retrofitting touches every method and produces subtly
  wrong recovery; the kickoff is explicit about this.
- **One `Synchronise()` routine** implementing §29.1 anchors:
    - Statement-boundary newline at `BracketDepth == 0`.
    - Closing `}` of an enclosing block.
    - Top-level keyword: `fn`, `type`, `param`, `import`, `const`, `readonly`.
    - EOF terminates unconditionally.
    - Bracket-nesting is tracked through the skipped region: a `(`/`[`/`{` opened
      inside the skip must close before a subsequent `Newline` re-arms.
- **No exceptions cross statement boundaries.** A parse failure produces one
  `Diagnostic`, one error node spanning failure-token → (exclusive) anchor,
  advances to the anchor, and returns. The outer parse method continues.
- **No diagnostic cap** — §29.4.
- Every AST node carries a `SourceRange`. The error nodes carry the
  `Diagnostic` that produced them.

## Grammar to cover (from §4 Sprint 1)

Expressions: arithmetic, comparison, logical, unary, grouping, literals,
identifiers, string interpolation, ternary, array literals, index, member
access, calls, lambdas. Standard precedence ladder; lambdas right-associative
via `=>`.

Statements: `:=` declarations, `=` assignment, compound assignments,
`++`/`--`, `print()`, `exit()`, `if`/`else if`/`else`, `while`,
`for…in` (collection and numeric range with optional `step`),
`select`/`case`/`default`, `break`, `continue`, `return`, `try`/`catch`/
`finally`, blocks.

Declarations: `fn`, `type`, `param`, `import`, `const`, `readonly`.

## Tests for this increment

Happy-path parser tests (token stream → AST shape) for a representative
sample of each grammar production above. Make these focused — full grammar
coverage is shared with Increment E. The dedicated recovery-acceptance suite
lands in Increment E; here, include enough recovery tests to prove the design
works end-to-end:

- A malformed expression terminated by `}` produces `ErrorExpr`, parse
  continues after the brace (§29.6 worked example).
- A malformed declaration with a missing brace recovers at the next top-level
  keyword.
- Multi-error file: at least three independent failures, three diagnostics,
  no cascade in the parser's own output.

## Out of scope

No type checking. No `Error`-type cascade suppression (that lands when the type
checker arrives in Sprint 2 — but the parser must produce nodes that the type
checker can mark `Error` without any special escape hatch). No bytecode.

## Acceptance

- `dotnet build` and `dotnet test` green.
- Parser is stateless, recovery is built-in to every method, and the §29.6
  worked example produces exactly the two diagnostics the spec calls for.
- Recovery never deadlocks: every call to `Synchronise()` is guaranteed to
  advance the cursor (write a test that constructs a degenerate token stream
  and asserts termination).

## Model

**Opus.** This is the increment the kickoff explicitly singled out as worth the
stronger model. Resist the urge to descend to mechanical pattern-matching —
the design judgement around recovery and statelessness is what we are paying
for here.

## Hand-off

Summarise: parser entry point, the `Synchronise()` contract, the shape of how
each parse method handles failure, and any spots where you had to interpret
§29 rather than follow it literally. Next chat starts Increment E — the full
recovery acceptance suite.
