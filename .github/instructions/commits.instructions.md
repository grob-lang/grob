---
applyTo: "**"
---

# Commit message conventions for Grob

When you generate a commit message — for staged changes, for a proposal,
for a branch — follow conventional commits.

## Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

The subject line is mandatory. The body and footer are optional but
recommended for anything non-trivial.

## Type

Exactly one of:

- **`feat`** — new functionality visible to users of the library or CLI.
- **`fix`** — bug fix in existing behaviour.
- **`refactor`** — change to internal structure that does not affect
  observable behaviour. No new tests should be required for a pure refactor;
  if you need new tests, it isn't a refactor.
- **`test`** — adding or modifying tests without changing production code.
- **`docs`** — changes to docs, comments, README, ADRs, spec files.
- **`build`** — changes to csproj, .slnx, NuGet packages, build scripts.
- **`ci`** — changes to GitHub Actions workflows or CI configuration.
- **`chore`** — maintenance that doesn't fit elsewhere. Use sparingly.

Do not invent new types. If the change doesn't fit one of the above, the
change is probably doing more than one thing — split it.

## Scope

The project or component touched. For Grob:

- `core` — `Grob.Core`
- `compiler` — `Grob.Compiler`
- `vm` — `Grob.Vm`
- `runtime` — `Grob.Runtime`
- `stdlib` — `Grob.Stdlib`
- `cli` — `Grob.Cli`
- `lsp` — `Grob.Lsp`
- `tests` — when the change spans test projects
- `docs` — when the change spans `docs/`
- `build` — when the change spans build configuration

If a change genuinely touches more than one scope, leave the scope off
rather than listing them. Multi-scope changes are usually doing too much
and should be split.

## Subject

- Imperative mood: "add lexer for string literals" not "added" or "adds".
- Lowercase first letter. No trailing full stop.
- 50 characters or fewer where possible. Hard limit 72.
- British English. No Oxford comma. Never "simply".

## Body

- Wrap at 72 characters.
- Explain *why*, not *what* — the diff already shows what.
- One blank line between paragraphs.
- Reference the decision log entry if the change implements a settled
  decision: `Implements D-300 (error-recovering parser).`

## Footer

- `Refs:` or `Closes:` for issue references.
- `BREAKING CHANGE:` followed by a description if the change breaks public API.
  This is rare in Sprint 1 because there is no shipped public API yet.

## Examples

Good:

```
feat(compiler): add lexer for double-quoted strings

Supports escape sequences (\n \r \t \\ \" \$) and ${name}
interpolation segments. Unterminated strings produce E0042
with source location pointing at the opening quote.

Implements D-185 (string literal forms).
```

```
test(compiler): cover lexer error recovery on unterminated strings

Adds gold-master tests for E0042 across three scenarios:
unterminated single-line, unterminated with newline before EOF,
and unterminated inside an interpolation segment.
```

```
refactor(core): rename TokenKind.String to TokenKind.StringLiteral

Aligns with IntLiteral and FloatLiteral naming. No behaviour change.
```

Bad:

```
Added stuff to the lexer

I changed the lexer to do strings. Also fixed a thing
in the parser while I was there.
```

Why bad: no type prefix, wrong tense, vague subject, body explains
what not why, mixes two unrelated changes that should be separate
commits.

## When you generate a message

If staged changes touch multiple concerns, **do not** invent a single
commit that papers over the split. Surface it: "These changes look like
two commits to me — one `feat(compiler)` for the lexer change, one
`test(compiler)` for the new tests. Should I propose them separately?"
