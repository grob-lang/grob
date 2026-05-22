---
name: "Sprint 1 · Increment B — Lexer"
description: "Full Grob lexer in Grob.Compiler, every literal form, with bracket-depth tracking."
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

# Sprint 1 · Increment B — Lexer

You are picking up Sprint 1 of the Grob compiler at **Increment B** of a five-part
breakdown. Read `.github/prompts/sprint-1-kickoff.prompt.md` and
`docs/design/grob-v1-requirements.md` §4 (Sprint 1) first.
`docs/design/grob-language-fundamentals.md` §8 (strings, raw blocks, regex) is the
authoritative reference for literal forms.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/compiler-lexer`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- Solution scaffold, `Grob.Core` foundations.
- **Increment A:** `TokenKind` (complete from §3.4) and `Token` record carrying
  `Kind`, `Lexeme`, `Location`, `BracketDepth`. Tests green.

## Deliverable for this increment

A single-pass `Lexer` in `Grob.Compiler` producing
`IReadOnlyList<Token>` and writing diagnostics into a `DiagnosticBag`. It must:

- **Report every error**, never stop at the first.
- Handle:
    - Identifiers + keyword recognition.
    - Int literals: decimal, `0x...` hex, `0b...` binary, `_` digit separators.
    - Float literals.
    - **Double-quoted strings with `${...}` interpolation**, segmented into
      `StringStart` / `StringPart` / `InterpStart` / `InterpEnd` / `StringEnd`.
    - **Raw backtick strings**: single-backtick single-line and triple-backtick
      block forms (per language fundamentals §8 — confirmed in scope).
    - Regex literals — disambiguated from `/` division by preceding-token class.
    - Comments: `//`, `/* */`, `///` recognised and discarded (we recognise `///`
      so it lexes cleanly even though doc-comment semantics arrive in Sprint 10).
- **Bracket-depth counter**: `(`, `[`, `{` increment; `)`, `]`, `}` decrement.
  Every emitted `Token` carries the depth in effect when it was produced.
- **Newline handling**:
    - Always emit `Newline` tokens at source line breaks.
    - Apply the **line-continuation heuristic at the lexer boundary** before
      emitting the `Newline`: suppress `Newline` if the previous non-whitespace
      token ends with a continuation-eligible token (operator, comma, opening
      bracket, opening brace, `=>`) or if the next non-whitespace token begins a
      leading-dot chain. This means the parser only sees the surviving `Newline`s.
- End the stream with a single `EOF` token at depth zero.

## Tests (in `Grob.Compiler.Tests`)

- Table-driven suite per token family (keywords, operators, int literals with
  all radixes + separators, float, identifiers, comments).
- Interpolation segmentation tests: `"hi ${name}!"` → expected token sequence.
- Regex-vs-divide disambiguation: at least two contrasting cases.
- Raw backtick single-line and triple-backtick block forms.
- Line-continuation: trailing operator suppresses `Newline`; leading dot on the
  next line suppresses `Newline`; otherwise `Newline` is emitted.
- Bracket-depth assertions on a small snippet.
- A handful of realistic "snippet → token stream" tests covering a few lines
  of Grob each.
- Error cases: unterminated string, invalid escape, stray character — verify
  the lexer continues and emits a diagnostic, does not throw.

## Out of scope

No AST. No parser. No type checking. Do not modify `Grob.Core` beyond fixing a
genuine defect blocked by this increment (if so, call it out explicitly in the
hand-off). Do not invent token kinds — Increment A already locked the set.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- The lexer never throws on malformed input — it diagnoses and continues.

## Model

Sonnet — mechanical, large volume. Save Opus for Increment D.

## Hand-off

Summarise: lexer entry point and shape, how interpolation segmentation is
structured, how the regex/divide disambiguation rule is implemented, how
line-continuation is wired, and any spec ambiguities you had to resolve.
Next chat starts Increment C (AST + visitor base).
