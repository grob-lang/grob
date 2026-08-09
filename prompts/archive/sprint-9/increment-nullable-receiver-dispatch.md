# Correctness batch — Increment: nullable-receiver method dispatch and `??` descriptor symmetry

**Branch:** `fix/nullable-receiver-dispatch`
**One concern:** type-checker resolution on nullable and `??` paths — close the
nullable-receiver hole in `ResolveMemberAccessCall`, and restore the `ArrayDescriptorOf`
symmetry D-401 deliberately left half-done.

Runs against the fresh corpus zip carrying D-356 through D-401. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm.

---

## Authority — two findings from D-401, both reported and deliberately not fixed there

**Finding A — `ResolveMemberAccessCall` matches only exact non-nullable type tags.**
It compares `receiverType == GrobType.Array` (and the equivalents), so a `NullableArray` or
`NullableMap` receiver **skips that arm entirely** — and with it the collection's own
nullable-`E0101` guard — falling through to the generic permissive `return GrobType.Unknown`.
D-401 reported two symptoms, which are almost certainly the same defect:

- `xs: int[]? := [1,2,3]; print(xs.first())` **compiles and runs**, deferring the fault to a
  runtime `E5201` nil-dereference only when the receiver is actually nil. Meanwhile
  `print(xs.length)` correctly raises **`E0101`** at compile time, because `VisitMemberAccess`'s
  nullable guard is generic. **The property path guards; the method-call path does not.**
- `m?.get("k")` resolves as `Unknown` rather than `int?`. D-401 confirmed by reading the
  emission path that the **runtime** short-circuit is correct — `EmitOptionalChainCall` guards on
  the value's `IsNil`, never on `GrobType`, exactly as D-400 intended — so this is purely a
  type-checker gap: the arm that would give the call a real type is never reached.

**Treat A as one defect with two symptoms until the gate proves otherwise.** If fixing the tag
matching closes both and restores `?.` call typing, say so; if it does not, report why.

**Finding B — `ArrayDescriptorOf` has no `BinaryExpr` arm.** A `??` result carried no
element/value descriptor and typed `Unknown`, so
`xs: int[]? := [1,2,3]; first := (xs ?? [])[0]; y: int := first` fails
`E0001: Cannot assign value of type 'unknown'` — **and still does**. D-401 fixed the **map** side
only (`MapDescriptorOf` gained
`BinaryExpr { Operator: NilCoalesce } binary => MapDescriptorOf(binary.Left) ?? MapDescriptorOf(binary.Right)`)
because its own acceptance test could not pass otherwise, and explicitly left the array side for
this batch.

**Why these two belong in one increment.** Both are type-checker resolution gaps on
nullable/`??` paths; both surfaced from the same D-401 test; and B is currently **half-fixed**,
leaving an asymmetry in the tree between two mirror-image helpers. Splitting them would leave
that divergence standing longer for no benefit — the kind of thing that gets forgotten and then
rediscovered as a bug.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce all three cases empirically first** (the D-366/D-380/D-400/D-401 precedent —
   build the CLI and run them): `xs.first()` on a nullable array; `m?.get("k")`'s resolved type;
   and `(xs ?? [])[0]` assigned to an `int`. Report actual behaviour before designing.
2. **Read `ResolveMemberAccessCall`'s dispatch arms in full** and report every place an exact
   non-nullable tag is compared. The fix must cover **every** collection and primitive receiver
   kind with a nullable variant — array, map, string, int, float, bool, struct, anon-struct,
   function — not just the two D-401 happened to observe. Report the full list.
3. **Decide what a nullable receiver should do**, and be precise, because there are three cases:
   - **Non-optional `.` on a nullable receiver** → **`E0101`**, matching what
     `VisitMemberAccess` already does for property access. Confirm `E0101` is the right existing
     code and that its message reads correctly for a method call.
   - **`?.` on a nullable receiver** → resolve the member against the **underlying** type and
     return its **nullable** form (`m?.get("k")` → `int?`, `xs?.first()` → `T?`,
     `s?.upper()` → `string?`). Confirm how the existing nullable-widening machinery expresses
     this, and reuse it.
   - **`?.` on a *non-nullable* receiver** — report what happens today. It may warrant a
     diagnostic, but **report only**; do not add one in this increment.
4. **`VisitMemberAccess`'s generic guard is the precedent to mirror** — read it and report why it
   is generic where the call path is not. Reuse the same mechanism rather than writing a parallel
   nullable check.
5. **`ArrayDescriptorOf`'s missing arm** — confirm the exact shape D-401 added to
   `MapDescriptorOf` and mirror it. Report whether any *other* `*DescriptorOf` helper has the
   same gap (a function-type descriptor exists, per `FunctionTypeDescriptor.cs`).
6. **THE BREAKING-CHANGE ENUMERATION — required before any edit.** Turning `xs.first()` on a
   nullable receiver into a compile error rejects programs that currently compile. Enumerate
   **every** test, fixture, gold master and validation script affected, and report the list. A
   test may be **updated to assert the new correct behaviour**; it may **never** be weakened or
   deleted. If the fallout is wider than a handful of sites, **STOP and report**.
7. **Confirm no runtime change is needed.** D-401 established `EmitOptionalChainCall` already
   guards correctly. If the gate finds a VM change is required, that is a surprise worth stopping
   on.

Report the empirical reproduction, the full arm list, the three-case decision, the mirrored
guard, the descriptor gaps, and the breaking-change list. Then STOP.

---

## Scope boundaries — do NOT

- **Do not change `?.`'s runtime emission** (D-400) — it is correct. This is type-checker only,
  unless the gate proves otherwise.
- **Do not add a diagnostic for `?.` on a non-nullable receiver** — report only.
- **Do not weaken or delete a test** to absorb the breaking change.
- **Do not fix the remaining correctness-batch findings** — `Synchronise()`'s double diagnostic
  (D-376), `E5102`'s missing throw site (D-382), D-380's diagnostic-quality gaps. Report anything
  new; fix none of them here.
- **No new error code** — `E0101` for the nullable-receiver call, `E0001` and the existing
  nullable diagnostics elsewhere. Count stays **121**. If the gate finds a genuinely new
  condition, STOP and escalate via `allocating-an-error-code`.
- **No new opcode.**

---

## Tests — TDD, red first

- **Finding A, the guard:** a non-optional `.` method call on a nullable receiver raises
  **`E0101`** at compile time — for **every** receiver kind the gate found affected, not just
  array and map. Property access (`xs.length`) still raises `E0101` as before.
- **Finding A, the typing — load-bearing:** `m?.get("k")` types as `int?`, `xs?.first()` as
  `T?`, `s?.upper()` as `string?`. This is the half that makes `?.` genuinely usable rather than
  merely non-crashing, and it is what D-400 could not deliver alone.
- **`?.` runtime behaviour unchanged**: short-circuits to nil on a nil receiver, dispatches
  normally otherwise (D-400's tests stay green).
- **Finding B, symmetry:** `xs: int[]? := [1,2,3]; y: int := (xs ?? [])[0]` compiles and runs.
  The map equivalent (D-401's) stays green. Assert both in one place so the symmetry is visible.
- **Nested `??`** (`(a ?? b ?? c)[0]`) resolves its descriptor, since the arm recurses on both
  operands.
- **Non-nullable receivers unaffected**: every existing array, map, string and numeric member
  call resolves exactly as before.
- Each updated test carries its new assertion with the change visible in the diff.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-402**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: whether A's two symptoms
  were **one defect or two**, and the evidence; the full list of receiver kinds fixed, not just
  the two observed; the three-case decision for nullable receivers (`.` → `E0101`, `?.` →
  nullable-widened member type, non-nullable `?.` reported only); the guard mechanism reused from
  `VisitMemberAccess` rather than duplicated; `ArrayDescriptorOf`'s arm restoring D-401's
  deliberate asymmetry, and any further `*DescriptorOf` gap found; **the full breaking-change
  list** with what each updated test now asserts; and confirmation that no runtime or opcode
  change was needed. No new error code, count 121. Cite D-401 (both findings), D-400, D-374,
  D-377, D-378, and the language fundamentals' nullable rules.
- **Update `grob-language-fundamentals.md`** only if its nullable or `?.` section is silent on
  method calls against nullable receivers. If the spec was already correct and only the
  implementation lagged, say so and change nothing.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
