# Correctness batch — Increment: complete the side-channel helper sweep

**Branch:** `fix/side-channel-sweep-completion`
**One concern:** close the seven side-channel gaps D-403's survey found and left open —
`TernaryExpr`/`SwitchExprNode` arms across the four pinned helpers, and the three helpers the
sweep discovered but never pinned — **and** decide whether those two new merge points need the
identity guard `??` just acquired.

Runs against the fresh corpus zip carrying D-356 through D-403. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm.

---

## Authority — D-403's survey, reported and deliberately left open

D-403 closed the `?.` property-access asymmetry and the two `NilCoalesce` arms D-402 deferred,
then its own wider sweep found **seven further gaps**, none fixed on that branch:

1. **`TernaryExpr`/`SwitchExprNode` arms are missing across all four pinned helpers** —
   `ArrayDescriptorOf`, `MapDescriptorOf`, `ExpressionDescriptor`, `GetStructTypeName`.
2. **Three more helpers share the identical missing-arm shape**, found beyond the two D-402
   named: `GetFieldValueStructTypeName`, `SilentMapDescriptorOf`, `TryGetAnonStructLiteral`.

So the class is **seven helpers, not four**, and the two most common structural merge forms after
`??` — the ternary and the switch expression — are unhandled everywhere.

**The pinning region will not catch these.** `SideChannelHelperExhaustivenessTests.cs` pins each
helper's **current** handled-node-kind set, and the current set is the gap. It guards against
regression, not against the omission itself.

---

## The load-bearing question, which is not "add six arms"

**PR #189 established that these structural arms are only sound with an identity guard.**
D-403's Finding 4: the arms make a side-channel identity **survive** a merge, and the surviving
identity is always **one operand's** — so the arm is sound only if the merge first proves the
operands agree on that identity. `??` did not, and three mismatches were silently accepted
(`(f ?? g)()`, `(a ?? b)[0]`, `(d ?? g).toString()` — the last consulting the wrong nominal
method table entirely). That is now closed by `NilCoalesceIdentityMismatch` /
`StructIdentitiesAgree`, covering five kinds: array element, map value, function, named struct,
anonymous struct.

**A ternary and a switch expression are the same shape of merge.** `cond ? a : b` and a switch
expression both yield one value from several branches, and a side-channel arm would have to pick
one branch's identity to propagate.

**So adding the arms without an equivalent guard would reintroduce exactly the unsoundness #189
just closed, in two new places.** This increment must therefore decide, and record:

- **Do the new merge points need the identity check?** Almost certainly yes, by the same
  argument — but confirm it rather than assume, and note that a ternary/switch has **N branches**
  where `??` has two, so the predicate may need generalising from a pair to a set.
- **Can `NilCoalesceIdentityMismatch`/`StructIdentitiesAgree` be reused?** They were deliberately
  factored into one place so kinds are decided together rather than as sequential guards. Reuse,
  extend, or explain why not — do not write a second copy.
- **Or is the honest answer to add no arms at all** for these forms? A structural arm that cannot
  be made sound is worse than a missing one — the missing arm degrades to `Unknown` (permissive,
  which D-362 catalogues as a known state), whereas an unsound arm propagates a **wrong** identity
  and consults the wrong method table. **Deciding not to add them, with reasoning, is a legitimate
  and possibly correct outcome.**

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce empirically first** (the standing precedent): what do
   `(cond ? arrA : arrB)[0]`, a switch expression yielding arrays indexed immediately, and the
   equivalents for function/map/struct identity resolve to today? Confirm they degrade to
   `Unknown` rather than misbehaving, so the starting position is known.
2. **Read `NilCoalesceIdentityMismatch` and `StructIdentitiesAgree` in full** — the five kinds,
   the message shape (D-401's `"...: <what> do not match."`), and crucially their **permissive**
   behaviour when either side's descriptor or nominal name is missing or `Unknown`, which is what
   keeps `xs ?? []` working. Any new guard must inherit that permissiveness or it will break
   unannotated code.
3. **Survey the three unpinned helpers** — `GetFieldValueStructTypeName`, `SilentMapDescriptorOf`,
   `TryGetAnonStructLiteral`. For each: what side channel it recovers, which node kinds it
   handles, which it lacks, and **whether it is reachable by user code through a merge form at
   all**. A helper only reachable through paths that cannot carry a merge needs no arm, and that
   is worth establishing rather than adding arms uniformly.
4. **The N-branch generalisation.** If the guard extends to ternary/switch, report how: all
   branches must agree pairwise, or all must agree with the first, and what happens when one
   branch is `Unknown` (permissive, per step 2) or is an error node.
5. **Switch-expression specifics.** It is exhaustive (D-277/D-301) and may have an `Error`-typed
   arm under D-300's recovery. Report how those interact with an identity check, and confirm
   `Error`'s universal assignability keeps a mismatch from cascading, as it does for nested `??`.
6. **Extending the pinning region.** `SideChannelHelperExhaustivenessTests.cs` currently pins
   four helpers. Report how to extend it to all seven and to the new node kinds, and note D-403's
   **scope-lifetime constraint**: three helpers resolve `IdentifierExpr` via `LookupSymbol`, which
   returns `null` after `Check` pops its scopes, so those arms are pinned through real post-`Check`
   consumers (`CallExpr.ResolvedReturnType`, `IndexExpr.ElementType`) rather than direct
   reflection. Any new pin must respect that.
7. **Breaking-change enumeration.** If a new identity guard rejects merges that compile today,
   enumerate every affected test, fixture, gold master and validation script before editing.
   D-403's own guard broke nothing, which is a good sign but not a guarantee here.

Report the empirical baseline, the guard's reusability, the three-helper survey, the N-branch
design, the pinning-region extension, and the breaking-change list. Then STOP.

---

## Scope boundaries — do NOT

- **Do not add a structural arm you cannot make sound.** If the identity check cannot be extended
  to a merge form, leaving that form resolving `Unknown` is the correct outcome — record it.
- **Do not write a second identity predicate.** Reuse or extend the existing one; D-403 factored
  it deliberately to keep `ResolveNilCoalesce`'s complexity flat as kinds grew from one to five.
- **Do not weaken the permissive-on-`Unknown` behaviour** — it is what keeps `xs ?? []` and other
  unannotated fallbacks compiling.
- **Do not change `??`'s existing behaviour** (D-403 Finding 4) or the `?.` paths (D-400, D-402,
  D-403).
- **Do not fix the remaining correctness-batch findings** — `Synchronise()`'s double diagnostic
  (D-376), `E5102`'s missing throw site (D-382), D-380's diagnostic-quality gaps. Report anything
  new; fix none of them.
- **No new error code** — reuse `E0002` and the existing message shape. Count stays **121**. **No
  new opcode.**

---

## Tests — TDD, red first

- **Each arm added**: the side channel survives a ternary and a switch expression —
  `(cond ? a : b)[0]` types as the element type, `(cond ? f : g)()` resolves the return type, and
  the struct-identity equivalents resolve the correct nominal or anonymous name.
- **Each guard added — load-bearing**: a mismatched ternary or switch raises `E0002` with the
  established message shape, for **every** kind the guard covers. The `date`/`guid` case is the
  one worth mirroring explicitly, since #189 showed it consulted the wrong method table rather
  than merely mislabelling.
- **Permissiveness preserved**: a merge where one branch is `Unknown` or unannotated still
  compiles, exactly as `xs ?? []` does.
- **N-branch behaviour**: a three-branch switch with one mismatched arm reports once, and nested
  merges report at the innermost point without cascading.
- **The pinning region extended to all seven helpers**, and **mutation-verified**: delete one arm
  from one helper, confirm the pin fails for the expected reason, restore, confirm green. D-403
  set this standard by doing exactly that for `ArrayDescriptorOf`.
- Every existing test unchanged unless enumerated in the breaking-change list, each with its new
  assertion visible in the diff.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-404**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: which of the seven gaps
  were closed and which were **deliberately left** with reasoning; **the soundness decision for
  ternary and switch merges** — guard extended, or arms not added, and why; the N-branch design if
  one was built; the three previously-unpinned helpers' survey results, including any found
  unreachable through merge forms; the pinning region extended to seven helpers and the mutation
  that verified it; the breaking-change list if any; and that the side-channel class is now
  **fully surveyed** — or, if further helpers turned up, that it is not, with the new ones named.
  No new opcode, no new error code, count 121. Cite D-403 (the survey and Finding 4), D-402,
  D-401, D-400, D-362, D-277/D-301 (switch-expression exhaustiveness), D-300 (error recovery).
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
