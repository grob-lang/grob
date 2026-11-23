# Session hand-off — 23 November 2026

## Where we are

- Branch: `feat/crash-oracle`, HEAD `acd3bec` before this commit
- Working tree: clean (scratch in gitignored `.grob-session/probes/`)
- Commits on this branch: the hand-offs only (`git log --oneline origin/main..HEAD`)
- Increment prompt: `docs/prompts/crash-oracle.md` (D-451)
- Picked up from: this branch's own previous hand-off, which this file overwrites. No other path is pending retirement
- Phase reached: **session A complete.** The §1 investigation gate is done and reported, and no source was edited. The report is at `D:\Code\grob-lang\gate-reports\crash-oracle-gate-report.md`, outside the working tree

## Green state

- Tests: not run this session (the gate is read-only; the tree is unchanged since the previous hand-off's 5,776 passed)
- Coverage: not run this session
- Pre-push gate: runs on this hand-off's push

## Decisions taken, not yet logged

None taken. The gate report's §0 holds five proposals that need Chris's ruling before session B: Q1 (a VM-phase timeout is *bounded*, not a finding), Q2 (D-077 against printing a host exception's message), Q3 (the REPL's `exit(n)`), Q4 (the per-iteration REPL handler against `src/CLAUDE.md`'s outermost-catch rule) and Q5 (file F-1 to F-7 as register rows in group 2). Once ruled, they belong in the landing entry, or in their own entry if Chris prefers.

## Error codes and registry state

None allocated. Count 122. E9001 is registered and raised nowhere, as expected.

## Tried and rejected

- **The `ResolvedType` half of §3.1.1 as a direct check.** `IdentifierExpr.ResolvedType` is an enum that defaults to `Unknown`, a legal type, so an unset field cannot be told from a set one. The oracle uses a null `Declaration` as the "never visited" sign instead (report §1.1).
- **Trusting `Range.End` for mutation edits.** The parser's `RangeFrom` ends 41 node kinds at the next token's start, and single-parameter lambdas end at their last token's start (F-7). The mutator anchors at `Range.Start` and scans tokens (report §1.6).
- **A bulk black-box campaign mode.** It costs 198 ms per `grob.exe` process. Rejected for campaigns; a black-box re-check of each finding is kept (report §4.3).
- **Running the oracle on a thread with a larger stack.** It would hide F-3, the parser's stack overflow. Rejected.
- **Roslyn for extracting raw-literal seeds.** It is not in the package set, and a regex extracted 1,041 unique literals with none unterminated.
- **The gate's probe** is a throwaway console project in the session scratchpad, not in the repository. Do not look for it; group 1 builds the real oracle.

## Quarantined or skipped tests

None.

## Why this session stopped

Session A's work is the gate only (prompt §1, session table). It is finished, and the prompt ends the session with this hand-off.

## Next session, first action

Get Chris's rulings on Q1 to Q5 in the gate report's §0, then run the prompt's session-start check for session B.

## Open for the maintainer

- **Q1 to Q5** in the gate report's §0.
- **Seven pre-campaign findings, F-1 to F-7** (report §1). Two are hit by the unmutated corpus, so the CI slice needs their rows in group 2: F-2 (`toZone("abc")` escapes the CLI as an unhandled host exception) and F-4 (E0206 leaves an identifier without a `Declaration`). F-1 (`const x := 1 / 0` crashes the compile phase) and F-3 (the parser overflows the stack below 500 nesting levels) are P0s under the strategy.
- **The prompt's fake-capability test** ("a seed that writes, deletes and runs a process") cannot be written yet: no native writes a file or runs a process before 9C. The report §4.2 proposes the test that can be written now.
- Carried from the previous hand-off: the benchmark workflow for #261 is still to dispatch; `.grob-session/handoffs/feat-stdlib-regex-replace-fn.md` on `main` is still unretired.
