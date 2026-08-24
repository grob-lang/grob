---
description: "Sprint 9 · param declaration grammar — implement D-410. Replace ParseParamBlockDecl's `param { … }` block form with the braceless per-line declaration: one `param` keyword per parameter, decorators stacked above, the parameter group delimited by contiguity. Add the top-level decorator-stack dispatch arm and settle the recovery-anchoring interaction it creates with §29. Wire E4201 to its first throw site for malformed param declarations. Release-gate blocker: all eleven validation scripts fail to parse at their first `param` line today. Grammar only — no decorator semantics, no parameter binding, no CLI passing."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · `param` declaration grammar (D-410)

D-409 found that `grob-language-fundamentals.md` §19 and all eleven validation
scripts write `param name: type = value` one per line with no braces, while the
live parser requires a single `param { … }` block. It recorded the divergence
as the sixth advertised-but-unbuilt instance and deliberately left the
language-design question open. **D-410 has now settled it: the braceless
per-line form is canonical and the block form is retired.** This increment
implements that decision.

It is a release-gate blocker in its own right. Every one of the eleven
validation scripts fails to parse at its very first `param` line, independent
of any missing stdlib module — so no amount of Increment C–G work makes any of
them runnable until this lands. Fixing it now, while no script parses and no
user code exists, is a parser correction. Fixing it after C–G would be a
breaking change to passing code.

**This is a grammar increment. It is not the parameter feature.** Sprint 10
(Script Parameters and Decorators) owns decorator semantics, parameter binding,
`.grobparams` and CLI passing. Do not build any of it here.

Read, in order:

1. `docs/design/grob-language-fundamentals.md` **§19, "The `param`
   declaration"** — the normative grammar D-410 added. This is the
   specification for this increment. Also §19's ordering rules and its "Name
   uniqueness" paragraph, and **§29** (parser error recovery — the
   synchronisation set and its anchor list).
2. `docs/design/grob-decisions-log.md` — **D-410** in full (the decision, the
   eight corpus sites, the recovery-simplification argument, and the
   registry-consequence split). **D-411** for the decorator set this grammar
   carries, and specifically for what it defers to Sprint 10. **D-405** and
   **D-406** for the recovery machinery that moves — D-406's `type`-body path
   is explicitly *not* in scope and must stay working.
3. `docs/design/grob-formatter-specification.md` **§3.10** (parameter blocks)
   and **§3.3** (blank lines) — both are already written against the braceless
   form and describe the shape the parser must now accept. No formatter code
   exists yet; read them as a second specification of the same grammar.
4. `docs/design/grob-error-codes.md` — **E4201** (`param` declaration syntax
   error), **E4202** (retirement-pending, see below), **E2202**, **E2001**.
5. Confirm the **next free D-number** against the live tail before allocating.
   D-411 is the current tail as of this prompt; verify, do not assume.

---

## Read-only investigation gate

**Read-only means no source edits. Building and running the CLI is expected and
required** — reproduce the current behaviour empirically before designing
anything. Static reading has missed a premise error in nearly every increment
this run.

Write the full investigation report **to a scratch file outside the repository
working tree**. Do not stage, commit or archive it. In this chat: the file
path, one line per gate item, and **explicitly any STOP condition hit**.

State the expected result for each item before you measure it.

### Gate items

1. **Reproduce the current failure.** Build the CLI and run it against a
   minimal braceless `param` script and against a `param { … }` script.
   Capture the exact diagnostics. Confirm the braceless form fails at the
   `param` line and the braced form parses.

2. **Enumerate every test that touches `param` parsing.** Not just
   `ParserParamBlockRecoveryTests.cs` — grep the whole `tests/` tree.
   For each test, classify it: does it assert the braced form as **recovery
   mechanics** (a malformed entry resynchronises correctly), or as **language
   behaviour** (the block form is the grammar)? The first class gets rewritten
   against the new shape. **STOP and surface if any test asserts the block form
   as a language decision** — that would mean a decision exists somewhere the
   corpus sweep has not found, and D-410 would need revisiting before any edit.

3. **Do the eleven validation scripts exist as `.grob` files on disk?**
   `grob-sample-scripts.md` holds them as fenced blocks. There is a standing
   rule that every public code sample must exist as a corpus file compiled by
   the release gate, but whether it is implemented is unconfirmed. Search for
   a corpus/samples directory containing them. **STOP and surface if they are
   markdown-only** — the acceptance criterion below assumes a mechanical
   parse-through check, and if the files do not exist that check has to be
   built or the criterion restated. Do not silently substitute a hand-written
   approximation of a script.

4. **Confirm the throw-site state.** This prompt asserts that E2201, E2202,
   E4001, E4002, E4101, E4102, E4201 and E4202 are all defined in
   `ErrorCatalog` with **zero throw sites** in `src/`, and that E4001's only
   non-catalog reference is a doc comment on the category enum in
   `ErrorDescriptor.cs`. Verify. If any of them is thrown somewhere, the scope
   below changes and the finding is surfaced first. (This is the
   referenced-as-defined-is-not-thrown class D-407 put on record.)

5. **The decorator-stack dispatch question.** `ParseTopLevelItem` switches on
   `Current.Kind` and `@` is not `TokenKind.Param`, so a top-level decorator
   stack currently falls through to `ParseStatement()`. Establish what that
   produces today and design the arm. Two shapes to weigh, with a
   recommendation and a reason:
   - a `TokenKind.At` arm that parses the decorator stack and then **requires**
     a `param` declaration (giving E4002 a natural home in Sprint 10 when a
     decorator stack is followed by something else);
   - lookahead from the dispatch that skips the stack to find the keyword.

6. **The recovery-anchoring interaction — the one real design question here.**
   `IsTopLevelKeyword` is both the declaration-dispatch predicate and the tail
   of `IsSyncAnchor`. It does not include `At`. If it stays that way, recovery
   after an error resynchronises onto the `param` keyword and **swallows the
   decorator stack above it**. If `At` is added, a resync could stop at a stray
   `@` in any position. Establish empirically which failure actually occurs,
   then decide. **§29's anchor list in the spec names six keywords and not
   `@`** — if the chosen answer needs `@` in the synchronisation set, that is a
   **spec amendment and therefore a decision**: surface it at the gate and take
   the approved path. Do not edit §29 inside the implementation phase.

7. **Enumerate breaking changes before editing.** Every test, gold master and
   fixture the change affects. A test may be **updated to assert new correct
   behaviour; never weakened or deleted** to accommodate the change. Check
   `docs/errors/examples/` specifically for any expected output asserting a
   diagnostic on a `param` construct — the E4201 wiring below changes the code
   emitted there.

Stop at the end of the gate for approval before any edit.

---

## What you're building

1. **The braceless `param` declaration.** `ParseParamBlockDecl` is replaced by
   a per-declaration parse: `{ decorator newline } "param" identifier ":" type
   [ "=" expression ] newline`, per §19's normative production. One `param`
   keyword per parameter. No `{`, no `}`. The type annotation is mandatory.
   A default uses `=`.

2. **The parameter group by contiguity.** Consecutive `param` declarations
   form a group the way consecutive `import` declarations do — delimited by the
   first line that is neither a `param` declaration nor one of its decorators,
   not by a brace. Decide and record whether the AST keeps `ParamBlockDecl` as
   a group node the parser assembles, or emits one declaration node per `param`
   and lets a later pass group them. **Recommendation: one node per `param`
   declaration**, since that is what the grammar now says and it makes each
   declaration an ordinary top-level item for `SourceLocation`, recovery and
   the eventual LSP. If you diverge, say why.

3. **The top-level decorator-stack dispatch arm**, to the shape approved at the
   gate. Decorators continue to be **parsed and skipped** exactly as
   `SkipParameterDecorators` does today — capturing them into the AST is
   Sprint 10's front door, not this increment's.

4. **The recovery answer**, to the shape approved at the gate. Note that D-410
   records this change as a *net simplification*: a braceless `param` is an
   ordinary top-level declaration, so §29's existing keyword-anchored
   `Synchronise()` covers it and `ParseDeclaredParameterOrError` /
   `SkipToNextLiteralElementBoundary`'s newline mode are no longer needed **for
   `param`**. **`ParseTypeFieldOrError` and the `type`-body path stay** —
   D-406 built that mode for both constructs and only the `param` half retires.
   Confirm the `type` path is untouched by running its tests unmodified.

5. **E4201's first throw site, and its retitle.** A malformed `param`
   declaration — a missing type annotation, `:=` where `=` belongs —
   currently raises the generic E2001. §19 as written names E4201 for this,
   and E4201 has existed with no throw site since it was defined. Wire it, on
   exactly the D-407 pattern. `ErrorCatalog.E4201` referenced through its
   descriptor, never a literal (D-308).

   **E4201 is also retitled** from "`param` block syntax error" to "`param`
   declaration syntax error" (D-414). The title names a form D-410 retired,
   and unlike a description it lives in `ErrorCatalog.cs` and is diffed by the
   D-316 agreement gate — so the registry edit and the source edit must land
   in the **same commit**, which is why it could not be done in the D-414
   documentation branch and belongs here. No code is added or removed and the
   count stays **121**; only the title string moves, in both places at once.

6. **The decision entry** at the real next-free D-number, in three-location
   lockstep, recording: the implementation, the AST shape chosen, the
   dispatch-arm and recovery answers, whether §29 needed amending, and the
   parse-through result for the eleven scripts.

---

## Out of scope

Everything Sprint 10 owns: decorator **semantics** (`@secure` handling,
`@allowed`/`@minLength`/`@maxLength`/`@minValue`/`@maxValue` validation),
capturing decorators into the AST, parameter binding, `.grobparams` files, CLI
parameter passing.

The six new decorators of D-411 and `@pattern` — none of them is built here.

**E4202's removal and E2202's title widening**, both of which D-410 defers to
"the implementing increment". *Read this carefully:* D-410 means the increment
that implements the **ordering enforcement**, which is Sprint 10 — not this
one. Removing E4202 requires the `ErrorCatalog` edit and the registry edit in
the same commit, and E2202 has no throw site to widen against yet. Confirm
that reading at the gate; if you conclude D-410 meant this increment, surface
it rather than acting on either interpretation.

Any stdlib module. The `OpCode` enum. Any `GrobValueKind` variant. The
formatter (`grob fmt` has no implementation yet).

---

## Tests

- **Parser tests:** the canonical form parses — a bare `param`, a `param` with
  a default, a decorated `param`, a stack of several decorators, a mixed group
  of decorated and undecorated declarations separated by a blank line (the
  formatter's §3.10 shape). `param {` is no longer accepted and produces a
  clear diagnostic. Every parameter node carries a non-null `SourceLocation`
  (D-137); §3.1.1 holds on the nodes that carry annotations.
- **Diagnostics:** `param foo := 1` is E4201, not E2001. A missing type
  annotation is E4201. Each assertion pins the full contract — code, message,
  `Range.Start.Line`/`Column` — following D-405's precedent, not just a count.
- **Recovery:** a malformed `param` declaration is reported once, not twice,
  and a following well-formed `param` is still parsed and reported
  independently — the two-distinct-mistakes proof D-405 established as
  load-bearing. A decorator stack above a malformed `param` is not swallowed.
  An adversarial case: a `param` whose default expression leaves a bracket
  permanently open.
- **Mutation-verify the recovery guard.** Delete the resync behaviour the new
  tests guard, confirm they fail for the right reason, restore. A PR review
  previously found a test that stayed green when its subject was deleted.
- **Regression:** `ParseTypeFieldOrError` and every `type`-body recovery test
  pass **unmodified**. Function parameter lists (`ParseParameterList`, which
  shares `ParseDeclaredParameter`) are unaffected — confirm, since that shared
  call path is the obvious place for an accidental regression.
- **The eleven validation scripts parse.** Subject to gate item 3: each script
  lexes and parses to completion with zero diagnostics. They will still fail
  **type-checking** on missing modules (`fs`, `json`, `csv`, `process`,
  `Grob.Http`, `Grob.Crypto`) — that is expected and is not this increment's
  problem. Assert parse-clean, not run-clean.

---

## Acceptance

- The braceless `param` declaration parses per §19's normative grammar; the
  `param { … }` form no longer parses.
- All eleven validation scripts parse to completion with zero diagnostics —
  the D-409 release-gate blocker is cleared. (Or, if gate item 3 stopped, the
  restated criterion approved at the gate is met.)
- E4201 has a throw site; a malformed `param` declaration no longer reports the
  generic E2001.
- The decorator-stack dispatch and the recovery-anchoring answer are both
  implemented as approved, and any §29 amendment was surfaced as a decision
  before the edit, not folded in.
- `type`-body recovery (D-406) and function parameter lists are untouched,
  proven by their existing tests passing unmodified.
- No test weakened or deleted; every breaking change enumerated at the gate.
- E4201 is retitled in `ErrorCatalog.cs` and `grob-error-codes.md` in the
  same commit (D-414), and D-316 is green after it.
- No new error code; count stays **121**. No opcode change. No
  `GrobValueKind` change.
- Full solution `dotnet test` green; coverage at or above the floor on
  `Grob.Compiler`, with the new parse and recovery paths covered rather than
  excluded.
- The decision logged in three-location lockstep at the verified next-free
  D-number.

---

## Model

Sonnet. This is a grammar replacement over machinery D-405 and D-406 have
already exercised hard, plus one throw-site wiring on the D-407 pattern. The
only genuinely novel piece is the dispatch/anchor interaction in gate items 5
and 6, and that is a narrow, empirically settleable question rather than a
structural one — no Opus carve-out. The sprint's carve-out was spent on the
named-type registration table.

---

## Standing requirements

**Archive this prompt verbatim** to `prompts/archive/sprint-9/param-declaration-grammar.md`,
committed **with** the increment, **as issued and never retrofitted** to match
what was decided. The gap between what was asked and what landed is the
record's value.

**Report findings outside scope; do not fix them.** Nearly every increment this
run has surfaced something, and the batching is why they stayed reviewable.

**A negative result is a good outcome.** If the gate refutes a premise in this
prompt, that is the gate working as intended.

---

## Hand-off

Summarise: the AST shape chosen and why; the dispatch-arm and recovery-anchor
answers and whether §29 needed amending; the E4201 wiring; the parse-through
result for the eleven scripts; the decision and its lockstep entry; any finding
reported and not fixed.

Note for the next chat: the queue after this is the **corpus sweep** (last, so
it captures final state — the accumulated list is in the session handoff, with
A2's per-page wiki build-status convention as the anchor item, and OQ-005/OQ-006
already struck as resolved by D-303/D-304). Then **Increment C, `fs`** — and
per D-411 the Sprint 9 C-onward prompts are **rebuilt, not corrected**, at the
head of each run; `sprint-9-c.md` predates the entire consolidation phase and
knows nothing of D-356 through D-411.
