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

Chris ruled on the gate report's §0 on 23 November. Each ruling took the report's recommendation. All five belong in the landing entry (prompt §5), as divergences from the prompt:

- **Q1.** A VM-phase run past its bound is the outcome *bounded*. It is counted, but it is not a finding, because the VM honoured the cancellation. A timeout in the lexer, parser, checker or compiler stays a finding. The prompt's §1.1 and §4 list every run past the budget as a finding.
- **Q2.** E9001 prints a `GrobInternalException`'s message. For any other exception it prints only the type name, and the message and stack trace appear only under `--verbose`. This follows D-077 and requirements §1. The prompt said to print "the exception's message".
- **Q3.** In the REPL, `exit(n)` ends the session and `ReplCommand.Run` returns `n`, as D-110 has `exit(n)` end a script. This closes F-5 in group 3.
- **Q4.** The REPL's E9001 handler wraps each entry: one loop iteration, from `ReadEntry` through `ProcessEntry`. The session continues after an internal error. This is the sanctioned reading of `src/CLAUDE.md`'s outermost-catch rule for the REPL.
- **Q5.** F-1 to F-7 are filed as register rows in group 2's commit, so the known-signature list can name them.

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

Run the prompt's session-start check for session B (change groups 1 and 2, Opus, high).

## Open for the maintainer

- **Seven pre-campaign findings, F-1 to F-7** (report §1), to be filed in group 2 under Q5. Two are hit by the unmutated corpus: F-2 (`toZone("abc")` escapes the CLI as an unhandled host exception) and F-4 (E0206 leaves an identifier without a `Declaration`). F-1 (`const x := 1 / 0` crashes the compile phase) and F-3 (the parser overflows the stack below 500 nesting levels) are P0s under the strategy.
- **The prompt's fake-capability test** ("a seed that writes, deletes and runs a process") cannot be written yet: no native writes a file or runs a process before 9C. The report §4.2 proposes the test that can be written now.
- Carried from the previous hand-off: the benchmark workflow for #261 is still to dispatch; `.grob-session/handoffs/feat-stdlib-regex-replace-fn.md` on `main` is still unretired.
