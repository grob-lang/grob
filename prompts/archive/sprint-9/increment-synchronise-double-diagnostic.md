# Correctness batch — Increment: `Synchronise()` emits two diagnostics for one malformed key

**Branch:** `fix/synchronise-double-diagnostic`
**One concern:** one syntax mistake should produce one diagnostic. A malformed map-literal or
anon-struct-literal key currently produces two.

Runs against the fresh corpus zip carrying D-356 through D-404. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm.

---

## Authority — and a caveat about it

**This finding has no `D-###`.** It was reported during the map-literal increment's PR review
(the D-376 branch) and recorded only in conversation: a malformed **non-string** map-literal key
produces **two diagnostics rather than one**, traced to a pre-existing `Synchronise()` limitation
**shared identically by anon-struct literals** today. It was deliberately left unfixed as
out-of-scope for a grammar-scoped branch, and has been carried in the correctness batch since.

**Treat the description as a lead, not as established fact.** Everything below the empirical
reproduction in the gate must be built on what this increment observes, not on that summary. D-376
itself has landed, and several increments have touched the type checker since.

**Why it matters.** D-300 ratified error-recovering parsing with **cascade suppression** as an
explicit goal — error nodes typed `Error`, with `Error`'s universal assignability stopping one
mistake from producing a run of downstream complaints. That machinery has been relied on
repeatedly since: D-404 cites it for the no-cascade behaviour of nested merges. A construct
emitting two diagnostics for one mistake is a deviation from that ratified design, and it is
exactly the kind of noise that makes a diagnostic surface feel unreliable.

**Related, already correct — do not disturb.** D-376 records the key rule: a map-literal key must
be a plain double-quoted string, parsed via `ParseInterpolatedString()` then required to have
every part be a `StringTextPart`, mirroring `EvalConstantExpr`'s existing "is this string literal
actually plain" check. Anything else — an identifier, a raw/backtick string, a genuinely
interpolated string — is `E2001`. **That rule is correct.** This increment changes how many
diagnostics a violation produces, not what is or is not a valid key.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce empirically first** (the standing precedent — build the CLI and run it). Report the
   **exact** diagnostics, in order, with codes, messages and positions, for each of:
   - a map literal with an identifier key (`map<string,int>{ foo: 1 }`);
   - a map literal with an interpolated key (`map<string,int>{ "k${i}": 1 }`);
   - a map literal with a raw/backtick key;
   - the anon-struct equivalents (`#{ ... }` with a malformed field name);
   - a **well-formed** literal, to confirm the clean path is unaffected.
   **The reported "two diagnostics" may be one, two or more, and may differ per case.** Establish
   the real shape before designing anything.
2. **Trace where each diagnostic originates.** For every duplicate, report which code path emits
   it and why the second is not suppressed — is `Synchronise()` re-reporting at the recovery
   point, is the key parse emitting before the caller does, or does the error node fail to
   suppress a downstream complaint? **The answer determines whether this is a `Synchronise()`
   fix, a call-site fix, or an `Error`-node-typing fix**, and the entry must say which.
3. **Read `Synchronise()` and D-300's §29 synchronisation set in full.** D-300 specifies the
   set: statement-boundary newlines outside any open bracket, the closing `}` of an enclosing
   block, and top-level declaration keywords. Report whether the current implementation matches
   that specification, and whether a literal's interior — inside braces, mid-expression — is a
   context the specified set was designed for. **If the implementation matches the spec and the
   spec does not cover this context, that is the finding**, and it may warrant a spec amendment
   rather than only a code change.
4. **Establish the general shape.** Map literals and anon-struct literals share the behaviour.
   Report whether **array literals**, **call argument lists**, **switch arms** or any other
   comma-separated construct behave the same way on a malformed element. If they do, this is a
   general recovery gap, not a two-construct one — **report the full list; fix only what the
   approved scope covers.**
5. **The gold-master surface.** `docs/errors/examples/` holds gold-master diagnostic pairs, and
   D-376 added one for `duplicate-key-map-literal`. Report every example whose expected output
   contains a duplicated diagnostic — those files encode the current wrong behaviour and will need
   regenerating. **Regenerating a gold master to match a fix is legitimate; regenerating one to
   make a test pass without understanding why the output changed is not.** State which each is.
6. **Confirm no diagnostic is lost.** The fix must remove a **duplicate**, never the genuine
   first report, and must not suppress a *second distinct* mistake later in the same literal.
   Report how the design distinguishes them.

Report the empirical reproduction, the origin trace, the `Synchronise()`-versus-spec verdict, the
general-shape survey, the gold-master list, and the test plan. Then STOP.

---

## Scope boundaries — do NOT

- **Do not change what is a valid key.** D-376's plain-string rule stands; only the diagnostic
  count changes.
- **Do not change `E2001`** or introduce a new code. Count stays **121**. If the gate finds a
  genuinely new condition, STOP and escalate via `allocating-an-error-code`.
- **Do not widen the fix beyond the approved scope.** If step 4 finds array literals or argument
  lists share the shape, **report them**; extending to them is a scope decision, not an
  assumption.
- **Do not suppress diagnostics broadly.** The failure mode of a cascade-suppression fix is
  swallowing a genuine second error. D-300's whole design is one-mistake-one-diagnostic, **not**
  fewer diagnostics.
- **Do not fix the remaining correctness-batch findings** — `E5102`'s missing throw site (D-382),
  D-380's three diagnostic-quality gaps.
- **No new opcode.**

---

## Tests — TDD, red first

- **Each malformed-key form produces exactly one diagnostic**, for both map literals and
  anon-struct literals — asserted with the full diagnostic contract per the project's convention:
  code, message, and `Range.Start.Line`/`Range.Start.Column`, not code alone.
- **Two distinct mistakes still produce two diagnostics — load-bearing.** A literal with a
  malformed key *and* a separate genuine error later must report both. This is the test that
  proves the fix removed a duplicate rather than suppressing recovery.
- **Recovery still works**: after a malformed key, parsing continues and a subsequent
  well-formed statement is still checked (D-300's whole purpose).
- **The well-formed path is untouched** — every existing map-literal and anon-struct test green.
- **Gold masters regenerated only where the diagnostic count legitimately changed**, each
  reviewed and named in the landing entry.
- If step 4's survey found other constructs sharing the shape, a **characterisation test**
  recording their current behaviour, so the known gap is pinned rather than merely written down.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-405**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: **the empirical
  reproduction**, since this finding never had a `D-###` and this entry becomes its first
  authoritative record; where each duplicate originated and **whether the fix was in
  `Synchronise()`, at the call site, or in error-node typing**; whether the implementation matched
  D-300's §29 specification and whether the spec needs amending for literal-interior contexts;
  **the general-shape survey** — every other construct sharing the behaviour, fixed or named as
  open; the gold masters regenerated, each classified as a legitimate behaviour change; and the
  test proving two distinct mistakes still report twice. No new opcode, no new error code, count
  121. Cite D-300 (§29 error recovery and cascade suppression), D-376 (the map-literal key rule
  and the PR review that found this), D-404 (the most recent reliance on `Error`'s
  no-cascade behaviour).
- **Update `grob-language-fundamentals.md` §29** only if the gate found the specification silent
  or wrong about literal-interior recovery. If the spec was right and the implementation lagged,
  say so and change nothing.
- **Deliverable:** repo-pathed zip (source, tests, gold masters, updated design docs). Archive
  this prompt under `prompts/archive/sprint-9/`.
