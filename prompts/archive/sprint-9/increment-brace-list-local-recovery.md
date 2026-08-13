# Correctness batch — Increment: local recovery for the four remaining `{}`-delimited constructs

**Branch:** `fix/brace-list-local-recovery`
**One concern:** extend D-405's local element-boundary recovery to the four `{}`-delimited
element lists that still lack it, closing the open finding D-405 recorded.

Runs against the fresh corpus zip carrying D-356 through D-405. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm.

---

## Authority

**D-405** fixed map literals and anon-struct literals, surveyed the general shape, and recorded
the rest as an explicit open finding:

> named-struct construction (`TypeName { … }` in `ParsePostfix`), switch-expression arms
> (`ParseSwitchArms`), `type`-declaration field bodies (`ParseTypeDecl`) and `param`-block bodies
> (`ParseParamBlockDecl`) all empirically reproduce the identical double-diagnostic shape — each
> parses a `{}`-delimited element list with no local recovery wrapper of its own.

**Array literals and call-argument lists are correctly excluded** — `Synchronise()` never anchors
on `)`/`]`, so it skips them as ordinary tokens using the lexer's per-token bracket depth. That
exclusion is incidental rather than designed, but it is real; do not extend the wrapper to them.

**The defect is one bug in six places**, four still live. D-405 established it is not a
`Synchronise()` defect — `Synchronise()` matches D-300 §29 exactly, and genuinely cannot
distinguish a literal's own closing `}` from an enclosing block's. The fix is a **local wrapper
alongside it**, never a change to it.

**Two of the four are on paths users hit constantly.** `param` blocks open most real Grob scripts
and `type` declarations are how users define structs. D-405's own two-mistakes finding showed the
pre-fix behaviour was not merely a phantom diagnostic but a **lost** one — the whole element list
was abandoned after the first failure, so later genuine mistakes were never reported. That is
worse here than in the map-literal case, because these constructs are more common.

---

## The pattern to apply — and the subtlety that is not copy-paste

D-405's helper is `SkipToNextLiteralElementBoundary`, and PR #191's review established the part
that matters: the scan **must not start at nesting depth zero.** An element can fail with
delimiters already open (`map<string, int>{ "a": foo(1 2), "b": 3 }` fails inside an argument
list with `(` consumed), and a from-zero counter meets the `)` first, drives negative, and then
runs to EOF swallowing the rest of the file — the exact over-swallowing D-405 exists to remove.

The shipped design replays the abandoned element's own tokens via `OpenDelimitersBetween` into a
**delimiter stack**, so the scan resumes at the nesting actually open at the failure point. A
closing token that cannot close whatever is innermost-open is **not** counted: a `}` is taken to
be the enclosing construct's own brace and stops the scan **unconsumed**, handing it to
`Expect(TokenKind.RightBrace)`; any other stray closer is skipped as an ordinary token.

**Each of the four constructs must be checked against that design, not assumed to fit it** — see
gate step 2. Element separators, terminators and enclosing context differ between them.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce all four empirically first** (the standing precedent), and for each report the
   exact diagnostics with codes, messages and positions: a malformed field in
   `TypeName { … }` construction; a malformed switch-expression arm; a malformed field in a `type`
   declaration body; a malformed entry in a `param` block. Also run the **two-mistakes** case for
   each — two malformed elements in the same list — since D-405 showed the interesting failure is
   a *lost* diagnostic, not a duplicated one.
2. **Check each construct against the wrapper's assumptions.** D-405's helper was built for
   comma-separated `{}`-delimited lists. Report per construct: what separates elements (comma?
   newline? neither?), what terminates the list, and whether the element parse can leave
   delimiters open. **A construct whose shape differs may need a variant or may not fit at all** —
   report that rather than forcing it.
3. **THE SWITCH-ARM INTERACTION — check this explicitly.** D-404 wired `CheckMergeIdentity` to run
   **once** after `VisitSwitchExpr`'s arm-folding loop, guarding side-channel identity across arms.
   An abandoned arm now produces an error node. Report: does that node reach the fold, what type
   does it carry, and does `Error`'s universal assignability (D-300) keep the guard from firing
   spuriously on a recovered arm? **If recovery and the merge guard interact badly, that is a
   finding to report before designing**, not something to discover from a failing test.
4. **`param` blocks and `type` declarations are declaration contexts, not expression contexts.**
   D-300 §29 lists top-level declaration keywords as synchronisation anchors. Report whether a
   malformed element inside these bodies interacts with that anchor set differently from a literal
   interior, and whether the wrapper is still the right tool.
5. **Shared helper or per-site variants?** Report whether one wrapper serves all four, or whether
   the differences in step 2 warrant parameterisation. **Prefer one shared helper** — D-401→D-404
   repeatedly showed divergent copies of the same logic drifting — but say so with reasons rather
   than by default.
6. **Gold masters.** D-405 searched all 57 files in `docs/errors/examples/` and found none
   encoding a double diagnostic. Re-run that search for these four constructs specifically; report
   any that will need regenerating, and classify each as a legitimate behaviour change rather than
   a test-passing edit.

Report the four reproductions, the per-construct shape analysis, the switch-arm interaction
verdict, the declaration-context finding, the shared-versus-variant recommendation, and the gold
master list. Then STOP.

---

## Scope boundaries — do NOT

- **Do not modify `Synchronise()`** or D-300 §29's anchor set. D-405 established the spec is
  accurate for the contexts it covers; this is a local wrapper alongside it.
- **Do not extend the wrapper to array literals or call-argument lists** — they do not share the
  gap.
- **Do not change any construct's grammar or semantics.** This changes recovery only.
- **Do not force a construct into the wrapper if step 2 shows it does not fit** — report and
  leave it, exactly as D-405 left these four.
- **Do not fix the remaining correctness-batch findings** — `E5102`'s missing throw site (D-382),
  D-380's three diagnostic-quality gaps.
- **No new error code** — count stays **121**. **No new opcode.**

---

## Tests — TDD, red first

- **One malformed element produces exactly one diagnostic**, for each of the four constructs —
  full diagnostic contract per the project's convention: code, message, and
  `Range.Start.Line`/`Range.Start.Column`.
- **Two malformed elements produce two genuine diagnostics — load-bearing.** This is D-405's own
  proof shape, and the one that showed the pre-fix behaviour was losing a real error rather than
  duplicating one. Assert it for all four.
- **The delimiter-stack cases**, mirroring D-405's three regression tests, for each construct that
  can nest: an element failing with a bracket already open does not swallow the rest of the file;
  an element leaving a bracket permanently open does not fabricate a spurious later element.
- **EOF safety**: a malformed element with no closing `}` at all terminates without an infinite
  loop, and the missing brace is reported as its own genuine diagnostic.
- **Switch arms specifically**: a recovered arm does not cause `CheckMergeIdentity` (D-404) to
  fire spuriously, and a *genuine* merge mismatch in a well-formed switch still raises `E0002`.
- **Recovery still works**: after a malformed element, a subsequent well-formed statement is
  still parsed and checked (D-300's purpose).
- Every existing parser, type-checker and gold-master test green unless enumerated.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-406**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the four reproductions
  including the two-mistakes shape; which constructs took the shared wrapper and which needed a
  variant or **did not fit at all**, with reasons; **the switch-arm/`CheckMergeIdentity`
  interaction verdict**; whether declaration contexts behaved differently from expression
  contexts; the delimiter-stack handling per construct; any gold masters regenerated; and that
  D-405's open finding is now **closed — or, if any construct was left unfixed, that it remains
  open with the reason named**. No new opcode, no new error code, count 121. Cite D-405 (the
  survey and the wrapper), D-404 (the switch-arm merge guard), D-300 (§29 and cascade
  suppression), D-376.
- **Deliverable:** repo-pathed zip (source, tests, gold masters if any, updated design docs).
  Archive this prompt under `prompts/archive/sprint-9/`.
