---
applyTo: "**"
---

# Commit message conventions for Grob

When you generate a commit message — for staged changes, for a proposal,
for a branch — follow conventional commits.

## Pre-stage checklist (MANDATORY before `git add`)

The pre-commit hooks `trailing-whitespace`, `end-of-file-fixer`, and
`dotnet format — verify no changes` mutate the working tree on failure
but do **not** restage. Skipping the checklist below means at least one
aborted commit cycle every time.

1. **Run `dotnet format whitespace Grob.slnx`** on the working tree.
   This rewrites indentation, trailing whitespace, and final newlines
   for every `.cs` file across the solution. It is fast and idempotent.
2. **Verify namespaces match folder paths.** For every new or moved
   `.cs` file, the namespace on line 1 must equal the folder path under
   `src/` or `tests/` (see `project-layout.instructions.md`).
3. **Build and test:** `dotnet build Grob.slnx` then
   `dotnet test Grob.slnx --nologo`. Zero warnings, zero failures.
4. **Then** `git add -A` and commit.

Do not rely on the pre-commit hook to surface these issues. Hooks are
the safety net, not the workflow.

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
  Includes the tests that cover it. Strict TDD means the test and the
  implementation arrive together; a `feat` commit without tests is not a
  `feat` commit.
- **`fix`** — bug fix in existing behaviour. Includes the regression test
  that fails before the fix and passes after.
- **`refactor`** — change to internal structure that does not affect
  observable behaviour. No new tests should be required for a pure refactor;
  if you need new tests, it isn't a refactor.
- **`test`** — test-only changes that don't accompany a production-code
  change. Use for: adding regression rows from property-test discoveries,
  filling coverage gaps in already-shipped code, refactoring test
  infrastructure, adding gold-master coverage for previously-uncovered
  diagnostics. Do not use `test` to commit the tests for a feature
  separately from the feature itself — that's a `feat`.
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
- Explain _why_, not _what_ — the diff already shows what.
- One blank line between paragraphs.
- Reference the decision log entry if the change implements a settled
  decision: `Implements D-300 (error-recovering parser).`
- **Note coverage changes.** If the commit materially affects coverage —
  adding `[ExcludeFromCodeCoverage]`, removing tested code, lowering a
  project's percentage — say so in the body with the justification.
  Silent coverage drops are how 90% becomes 85% becomes 70%.

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

Includes happy-path, failure-path, and edge-case tests covering
empty input, EOF mid-string, escape-at-buffer-boundary, and nested
interpolation.

Implements D-185 (string literal forms).
```

```
fix(vm): handle stack underflow on Pop opcode before runtime values

Pop against an empty stack now throws InternalVmError with the
instruction offset rather than crashing the host process.

Regression: see issue #142.
```

```
test(compiler): add property-test discovery as regression row

FsCheck found that lexer scanning of triple-backtick strings
crashed on input containing exactly three backticks and no
content. Adds the shrunk input as an InlineData row alongside
the property test.
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
`test(compiler)` for the regression rows from the property-test
discovery. Should I propose them separately?"

If staged changes contain a feature _without_ tests, do not commit them
as `feat`. Surface the gap: "These changes add behaviour but I don't
see tests covering it. TDD discipline expects tests with the feature.
Shall we add them before committing?"
