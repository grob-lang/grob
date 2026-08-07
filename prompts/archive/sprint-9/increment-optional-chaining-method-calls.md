# Correctness batch — Increment: `?.` on a method call crashes at runtime

**Branch:** `fix/optional-chaining-method-calls`
**One concern:** make optional chaining short-circuit a **method call** on a nil receiver, as it
already does for property access. Today `xs?.first()` on a nil `int[]?` crashes the VM.

Runs against the fresh corpus zip carrying D-356 through D-399. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm; D-380's own text says 119, which was
correct when written and was superseded by D-382's `E5905`/`E5906`.

---

## Authority and the defect

**D-380 found and characterised this**, and deliberately left it for the correctness batch as
orthogonal to its own type-checker-only change:

> the guard exists for property access but **not** for method calls — `xs?.first()` on a nil
> `int[]?` crashes with "Call target is not a function (kind: Nil)" even though `first` is a
> recognised method, proving the gap is a runtime dispatch omission… (confirmed by testing a
> *recognised* method name via `?.`, which crashes identically).

**Why this matters more than the message suggests.** Optional chaining is foundational Grob
surface: `?.` **short-circuits the entire chain** and yields nil, which is one of the language's
stated nullable-safety guarantees. A user writing the documented idiom against a nil receiver
gets a VM crash with an internal-sounding diagnostic — a direct breach of the "fails well"
contract D-353 is built around, and the kind of thing Pillar 1 fuzzing finds immediately.

**Not currently gate-blocking**, only because no validation script happens to use `?.` — which
is luck, not safety.

**The property path is the precedent to mirror**, not a pattern to reinvent. D-380 records that
`?.` on a nullable receiver already stays permissive and correct for property access, and D-377
records the property-path guard's shape. Find that guard and apply the same treatment to the
call path.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce it empirically first**, per the D-366/D-380 precedent — build the CLI and run it,
   do not reason from source alone. Report the actual behaviour of: `xs?.first()` on a nil
   `int[]?`; `xs?.length` on the same (the working property path, for contrast); `m?.get("k")`
   on a nil map; `s?.upper()` on a nil `string?`; and a nominal receiver (`d?.addDays(1)` on a
   nil `date?`). **The receiver-kind coverage matters** — the fix must not be array-only if the
   omission is not.
2. **Locate the property-path guard** that makes `xs?.length` work, and the call path that lacks
   it. Report both, and whether the call path can reuse the same mechanism rather than adding a
   parallel one.
3. **Where the short-circuit belongs — compiler or VM.** `?.` short-circuits the *whole chain*,
   so the jump is emitted by the compiler. Report how `OptionalChain`/`QuestionDot` lowering
   emits its nil check today for property access, and why a method call skips it — is the check
   absent, emitted in the wrong place, or emitted but bypassed by the call path? **The answer
   determines whether this is a compiler fix or a VM one**, and the entry must say which.
4. **Argument evaluation on the short-circuited path.** `xs?.contains(sideEffect())` — when `xs`
   is nil, does the argument evaluate? It **must not**: short-circuiting means the call does not
   happen, so its arguments must not either. Report the current behaviour and the intended one.
   This is the subtle correctness question of the increment and the one most likely to be got
   wrong quietly.
5. **The result type.** A short-circuited `xs?.first()` yields nil. `first()` already returns
   `T?`, so the type is unchanged — but confirm for a **non-nullable-returning** method:
   `xs?.length` (an `int` property) must type as `int?`, and `s?.upper()` (returning `string`)
   must type as `string?`. Report whether the type checker already widens the result of a `?.`
   call, or whether that is part of this fix.
6. **Chained cases.** `a?.first()?.upper()`, and a `?.` call whose result feeds a further
   member access. Confirm the whole chain short-circuits from the first nil, per the language
   guarantee, rather than short-circuiting only one link.

Report the empirical reproduction across all five receiver kinds, the guard's location, the
compiler-versus-VM verdict, the argument-evaluation finding, the result-type finding, and the
test list. Then STOP.

---

## Scope boundaries — do NOT

- **Do not change `?.`'s semantics.** It short-circuits the entire chain and yields nil; that is
  settled language design. This makes the implementation match it.
- **Do not change `??`, nullable type checking, or the nullable diagnostics.** Only the
  `?.`-call dispatch path.
- **Do not fix the other correctness-batch findings** — `Synchronise()`'s double diagnostic
  (D-376), `E5102`'s missing throw site (D-382), or D-380's remaining diagnostic-quality gaps.
  Report anything new; fix none of them here.
- **Do not add a new error code.** A correctly short-circuiting `?.` raises **nothing** — it
  yields nil. If plan-mode finds a genuinely new *diagnostic* condition (e.g. `?.` on a
  non-nullable receiver being a compile error), STOP and report rather than minting inline.
- **No new opcode** unless plan-mode shows the short-circuit genuinely cannot be expressed with
  existing jumps — in which case STOP and escalate via `adding-an-opcode`, do not grow the enum.
- Count stays **121**.

---

## Tests — TDD, red first

- **The reported case:** `xs?.first()` on a nil `int[]?` yields nil and does **not** crash.
- **Every receiver kind the gate found affected** — array, map, string, numeric, nominal —
  each with a recognised method, each yielding nil.
- **The property path still works** (`xs?.length` on nil) — the regression this fix could
  plausibly break.
- **Non-nil receivers still dispatch normally**: `xs?.first()` on a real array returns the real
  element, indistinguishable from `xs.first()`.
- **Argument evaluation — load-bearing:** `xs?.contains(sideEffect())` on a nil `xs` does **not**
  evaluate `sideEffect()`. Assert via a counter or log; this is the test that proves
  short-circuiting rather than nil-tolerating.
- **Chained short-circuit:** `a?.first()?.upper()` on a nil `a` yields nil with no crash, and
  the *whole* chain is skipped rather than one link.
- **Result typing:** a `?.` call on a non-nullable-returning method types as nullable, and using
  it unguarded in a non-nullable context raises the existing nullable diagnostic.
- **Interaction with `??`:** `xs?.first() ?? 0` yields `0` for a nil receiver.
- Every existing nullable, optional-chaining, array, map, string and numeric test unchanged.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-400**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the empirical
  reproduction across every receiver kind and which were affected; **whether the fix was a
  compiler or a VM change**, and why; the guard reused from the property path rather than a
  parallel mechanism; the argument-evaluation semantics on the short-circuited path, with the
  test that proves it; the result-type widening and whether it already existed; the chained-case
  behaviour; and that this closes D-380's fourth finding. No new error code, count 121; opcode
  status stated explicitly. Cite D-380 (the finding), D-377, D-353 (the "fails well" contract),
  and the language fundamentals' `?.` section.
- **Update `grob-language-fundamentals.md`** only if its `?.` section is silent on
  short-circuited method calls or on argument evaluation — if the spec was already correct and
  only the implementation lagged, say so and change nothing.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
