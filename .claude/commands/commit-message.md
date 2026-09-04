---
description: Generate a conventional-commits message for the currently staged changes. Inspect the staged diff, choose the right type and scope and produce a complete message body.
allowed-tools: Bash(git status:*), Bash(git diff:*), Bash(git branch:*), Bash(git log:*)
---

# Generate commit message

Inspect the currently staged changes (`git diff --cached`) and produce a commit message
that follows the conventions below.

## Pre-stage checklist (run before `git add`)

The pre-push hooks `trailing-whitespace`, `end-of-file-fixer` and
`dotnet format — verify no changes` mutate the working tree on failure but do **not**
restage. Skipping this means at least one aborted commit cycle every time.

1. **Run `dotnet format whitespace Grob.slnx`** on the working tree — rewrites
   indentation, trailing whitespace and final newlines across every `.cs` file. Fast
   and idempotent.
2. **Verify namespaces match folder paths.** For every new or moved `.cs` file, the
   namespace on line 1 must equal the folder path under `src/` or `tests/` (see
   `src/CLAUDE.md`).
3. **Build and test:** `dotnet build Grob.slnx` then `dotnet test Grob.slnx --nologo`.
   Zero warnings, zero failures.
4. **Then** `git add -A`. Hooks are the safety net, not the workflow.

## Procedure

1. Run `git status` to confirm there are staged changes and which files are touched.
2. Run `git diff --cached` to see the actual changes.
3. Run `git branch --show-current` to confirm you are **not** on `main`. If you are on
   `main`, stop. Say "I'm on main — this commit must go on a branch. Should I create
   one?" and wait.
4. Identify the conventional-commits type (see below).
5. Identify the scope from the files touched.
6. Write the subject: imperative mood, lowercase first letter, no trailing full stop,
   ≤50 characters where possible (hard limit 72).
7. Write the body: wrapped at 72 characters, explaining *why*. Reference the
   decision-log entry if applicable (`Implements D-###.`).
8. Add a footer only if there is an issue to close or a breaking change to flag.

## Type

Exactly one of:

- **`feat`** — new functionality visible to users of the library or CLI. Includes the
  tests that cover it. Strict TDD means the test and the implementation arrive
  together; a `feat` commit without tests is not a `feat` commit.
- **`fix`** — bug fix in existing behaviour. Includes the regression test that fails
  before the fix and passes after.
- **`refactor`** — change to internal structure that does not affect observable
  behaviour. No new tests should be required; if you need new tests, it is not a
  refactor.
- **`test`** — test-only changes that do not accompany a production-code change:
  regression rows from property-test discoveries, coverage-gap filling, test
  infrastructure, gold-master coverage for previously-uncovered diagnostics. Do not use
  `test` to commit a feature's tests separately from the feature — that is a `feat`.
- **`docs`** — docs, comments, README, ADRs, spec files.
- **`build`** — csproj, .slnx, NuGet packages, build scripts.
- **`ci`** — GitHub Actions workflows or CI configuration.
- **`chore`** — maintenance that does not fit elsewhere. Use sparingly.

Do not invent new types. If the change does not fit one, it is probably doing more than
one thing — split it.

## Scope

The project or component touched: `core`, `compiler`, `vm`, `runtime`, `stdlib`,
`cli`, `lsp`; `tests` when the change spans test projects; `docs` when it spans
`docs/`; `build` for build configuration. If a change genuinely touches more than one
scope, leave the scope off — multi-scope changes are usually doing too much and should
be split.

## Subject, body, footer

- **Subject:** imperative mood ("add lexer for string literals", not "added"/"adds").
  Lowercase first letter, no trailing full stop. Prose follows the `house-style`
  skill.
- **Body:** wrap at 72. Explain *why*, not *what* — the diff shows what. Reference the
  decision (`Implements D-300 (error-recovering parser).`). **Note coverage changes**:
  adding `[ExcludeFromCodeCoverage]`, removing tested code or lowering a project's
  percentage must be stated with the justification. Silent coverage drops are how 90%
  becomes 85% becomes 70%.
- **Footer:** `Refs:` or `Closes:` for issues; `BREAKING CHANGE:` with a description if
  it breaks public API.

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
refactor(core): rename TokenKind.String to TokenKind.StringLiteral

Aligns with IntLiteral and FloatLiteral naming. No behaviour change.
```

Bad — no type prefix, wrong tense, vague subject, body explains what not why, mixes two
unrelated changes:

```
Added stuff to the lexer

I changed the lexer to do strings. Also fixed a thing in the parser.
```

## Split detection

After drafting, look again with fresh eyes. Do all the changes serve one purpose?
Could a reviewer revert this commit cleanly? Are the type and scope honest? If any
answer is "no", surface it before committing:

> "These staged changes look like two commits to me — one `feat(compiler)` for the
> lexer change, one `test(compiler)` for the regression rows from the property-test
> discovery. Should I propose them separately?"

If staged changes contain a feature *without* tests, do not commit them as `feat`.
Surface the gap.

## Prose pass

If the staged diff touches `docs/`, any `*.md` file, or a changed diagnostic message
string, run the `house-style` checklist over the added and changed lines before
proposing the message — not over the whole file, only over what this commit adds.

Gate on what the diff touches, not on how big it is. Large code commits are already
the best-covered surface in this project: TDD, `dotnet format`, the coverage gate,
Sonar and CodeRabbit all see them. A two-line Oxford comma in a spec document is seen
by none of those, which is exactly why it lands.

If `tooling/prose-check.ps1` flagged candidates at pre-commit, clear or dismiss each
one here rather than committing over the warning. It is a high-recall detector and
some hits are legitimate two-clause sentences — dismissing a false positive is a
judgement you make and state, not one you skip.

## Output

Present the proposed message in a fenced block, ready to paste into `git commit -F -`,
then ask: "Commit this, or revise?" Wait for the maintainer's response. Do not run
`git commit`, stage, push or merge autonomously.
