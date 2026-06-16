---
name: "Sprint 1 Kickoff"
description: "Scaffold the Grob solution and begin Sprint 1 — the project skeleton, lexer and error-recovering parser — against the locked architecture."
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

# Sprint 1 kickoff

Begin Sprint 1 — Foundation. The full scope and acceptance criteria are in
`docs/design/grob-v1-requirements.md` §4 (Sprint 1). Read it first; it is the build
guide. The solution layout is in `docs/design/grob-solution-architecture.md`. Do not
deviate from either.

Sprint 1 delivers: the C# solution skeleton, the lexer, the diagnostic infrastructure
and the **error-recovering parser**. Work in increments, each building and testing
green before the next.

## Order of work

1. **Solution scaffold.** Create the solution with six `src/` projects (`Grob.Core`,
   `Grob.Runtime`, `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Cli`), three
   `plugins/` projects, five `tests/` projects, and the `bench/` and `tooling/`
   siblings, exactly as the architecture doc specifies. .NET 10, self-contained
   deployment target. **Wire the project references so the DAG holds — `Grob.Compiler`
   and `Grob.Vm` must not reference each other.** Confirm the graph builds empty before
   adding code.
2. **`Grob.Core` foundations.** `SourceLocation` first — it threads through everything.
3. **`TokenKind` enum — complete** from §3.4. Written out in full, not grown.
4. **Diagnostics.** `Diagnostic` record (severity, message, source location, optional
   suggestion) and `DiagnosticBag` that accumulates without stopping. Errors to stderr.
   No diagnostic cap.
5. **Lexer — full.** Every keyword, operator, literal form (int with hex/binary/
   underscores, float, double-quoted with `${...}` interpolation detection, raw backtick
   strings, regex literals), every comment form. `Token` carries `(Kind, Lexeme, Line,
Column, File)`. The lexer tracks bracket nesting depth — the parser's recovery needs
   it. Reports all errors, never stops at first.
6. **Parser — error-recovering and stateless (D-300).** This is the defining Sprint 1
   deliverable and must be built in from the first parse method, not retrofitted. Follow
   `grob-language-fundamentals.md` §29 precisely: the synchronisation set, the three
   error-node kinds (`ErrorExpr`, `ErrorStmt`, `ErrorDecl`) as first-class AST citizens
   carrying source range and diagnostic, cascade suppression via the `Error` type. Every
   AST node carries a `SourceLocation`.
7. **Tests — comprehensive.** Lexer tests (source → token stream), parser tests (tokens
   → AST shape), and the recovery cases that are part of the acceptance criterion: a
   malformed expression mid-function still yields a complete AST for the rest of the
   file with an `ErrorExpr` at the failure site; a malformed declaration still lets
   subsequent declarations parse; a multi-error file yields one diagnostic per root
   cause with no cascade.

## Acceptance to hit

Lex and parse any valid Grob v1 source into a correct AST; report clear line/column
errors for invalid source; **error recovery works** as described in the criterion. Build
and test green.

## Notes

- For .NET 10 project setup, self-contained deployment configuration and any BCL
  specifics, ground against the Microsoft Learn docs rather than recalling.
- This is a large piece of work. Propose the increment breakdown first and confirm it
  before generating the full scaffold, so the maintainer can steer the sequencing.
- **Model suggestion:** the scaffold and lexer are mechanical (Sonnet is efficient); the
  error-recovering parser turns on judgement and is worth a stronger model.
