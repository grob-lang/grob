# Attributing the `for...in` snapshot allocation residual

Measurement-only increment (`bench/snapshot-residual-attribution`), a blocking
prerequisite for phase 3b per D-388: a `Run_ArrayForIn` allocation ceiling must not be
derived until the ≈75 KB pure-snapshot residual D-387/D-388 left unattributed is
actually attributed. No `src/` change. No opcode change. No new error code; count
unchanged at **121**.

**Result in one line: the three-copy hypothesis is refuted. There is exactly one array
copy — the 24,080 B `$snapshot` allocation D-383 requires, and the only term in the
differential that scales with element count. The rest, ≈48 KB at 1,000 iterations, is a
C#-compiler closure-capture allocation paid on every `GetProperty` dispatch against an
array receiver — including the `length` read the array `for...in` condition issues every
iteration. That component scales with iteration count and is completely independent of
the snapshotted array's size; the copy is the only size-dependent term. Any allocation
ceiling derived from this must carry both.**

---

## 1. The disassembled minimal case

`xs := [1, 2, 3]` followed by `for x in xs { }`, compiled and disassembled
(`Disassembler.DisassembleChunk`):

```text
0000    1 Constant                0 '1'
0002    | Constant                1 '2'
0004    | Constant                2 '3'
0006    | NewArray                3
0008    | DefineGlobal            3
0010    2 GetGlobal               3
0012    | GetProperty             4        <- constructs the snapshot ($snapshot)
0014    | Constant                5 '0'
0016    | GetLocal                1
0018    | GetLocal                0
0020    | GetProperty             6        <- condition: reads snapshot.length, every iteration
0022    | LessInt
0023    | JumpIfFalse            12
0026    | GetLocal                0
0028    | GetLocal                1
0030    | GetIndex
0031    | PopN                    1
0033    | IncrementInt            1
0035    | Loop                   22
0038    | PopN                    2
0040    3 Return
```

The **snapshot-constructing op is the single `GetProperty` at offset 0012** (constant
pool slot 4, name `"$snapshot"`), matching `EmitArrayForIn`'s doc comment
(`Compiler.ControlFlow.cs:206`–241) and `VirtualMachine.cs:887`–890's
`new GrobArray(array.Elements)`. It executes **exactly once** per `for...in` statement,
regardless of loop length — confirmed by this disassembly and unchanged by loop size,
since the loop body (offsets 0016–0038) references the already-built snapshot via
`GetLocal 0`, never rebuilding it.

The **second `GetProperty`, at offset 0020 (constant pool slot 6, name `"length"`), is
inside the loop condition and therefore executes once per iteration** — 1,000 times for
a 1,000-element array. This is the site the rest of this note attributes the residual
to.

## 2. Every allocation on the snapshot path, by site (read from the handlers)

| Site | `VirtualMachine.cs` | Allocates? | Per-call cost (measured) |
|---|---|---|---|
| `GetProperty "$snapshot"` | :887–890, `new GrobArray(array.Elements)` | **Yes — the one intentional D-383 copy** | 24,080 B (1,000-element source) |
| `GrobArray.Elements` accessor | `GrobArray.cs:29`, `=> _elements` | No — returns the live `List<GrobValue>` directly, no defensive copy | — |
| `GrobMap.Keys`/`Values`/`InsertionOrderKeys`/`InsertionOrderValues` | not on this path (array `for...in` only) | n/a for this path | — |
| `GetProperty "length"` (array) | :873–876, `GrobValue.FromInt(array.Count)` | The struct push itself: **no**. But see §3 — the surrounding `GetProperty` case body allocates a closure display-class regardless of which branch is taken | **≈48 B/call** |
| `GetProperty "isEmpty"` (array) | :878–881 | Same as `length` — same case body, same display-class cost | ≈48 B/call (measured, see §4) |
| `GetIndex` | :791–822 | No — struct read/push only | ≈0.1 B/call (noise-level) |
| `PopN`, `IncrementInt`, `LessInt`, `JumpIfFalse`, `Loop`, `GetLocal` | various | No — struct-only stack/slot operations | ≈0.3 B/call combined (noise-level) |
| `GetProperty` closure-binding path (`ArrayNatives.GetMethod`, e.g. `.append`) | :894–899, `FinallyContext` + lambda + `NativeFunction` | Yes — several objects, not reached by `for...in`'s own emission | ≈248 B/call (for comparison only; not on the `for...in` path) |

`Compiler.cs:362`'s `GetOrCreateGlobalNameIndex` and the `Constant`/`DefineGlobal`/
`GetGlobal` opcodes were also checked: all compile-time or struct-only, no per-call
heap allocation.

## 3. The actual source of the residual — not a copy at all

Reading `VirtualMachine.cs:860`–904 shows the `"length"` and `"isEmpty"` branches
`break` out of the `GetProperty` case **before** the closure-declaring statements at
:894–899 (`CancellationToken ct = _cancellationToken; var finallyContext = new
FinallyContext(...); NativeFunction? method = ArrayNatives.GetMethod(..., (callable,
args) => InvokeCallable(callable, args, line, column, ct, finallyContext));`). By
inspection alone, `"length"` should cost nothing beyond the struct push.

Direct measurement disagrees with that reading, and the disagreement is itself the
finding. Ablating the hand-assembled loop (§4) isolates the cost to the `"length"`
`GetProperty` call specifically, and it is **constant regardless of the snapshotted
array's size** (§4, Part 7) and **identical for `"length"` and `"isEmpty"`** (§4,
Part 8) — both early-return branches, both preceding the closure statements. A
synthetic repro entirely independent of Grob (§5) reproduces the same effect from a
minimal C# `switch`/`while(true)` shape carrying an early-return `if` as a sibling, in
the same block, of a later branch that declares a lambda capturing an outer,
per-iteration-reassigned local.

**Conclusion: this is a Roslyn closure-capture display-class allocation.** The lambda
at :899 (`(callable, args) => InvokeCallable(...)`) captures `line` and `column`
(locals of the outer `RunDispatch` method, reassigned every dispatch loop iteration)
together with `ct`/`finallyContext` (declared inside the `GetProperty` case body). The
compiler-generated display class covering that capture is allocated once per entry into
the shared lexical scope containing **both** the early-return branches and the
closure-declaring statement — not gated by which branch actually executes at runtime.
The result: every `GetProperty` dispatch against an array receiver pays this cost,
including a bare `.length` or `.isEmpty` read that user code — or the `for...in`
condition — issues, whether or not the call ever reaches the native-method-binding
code. This is a general `GetProperty`-on-array dispatch tax, not specific to
`for...in` and not a copy of anything.

## 4. Measurements

All measurements use `GC.GetAllocatedBytesForCurrentThread` directly (no
BenchmarkDotNet), around a tightly scoped window, with a fresh `VirtualMachine` per
trial and a JIT warm-up call excluded from the window — D-388's own technique, applied
more narrowly. Every figure below was byte-identical across 3–8 repeated trials (no
run-to-run variance at this precision), so single figures are reported. Reproduced via
a throwaway console harness outside the repository (project references only, no
`src/` edit); discarded at the end of this increment, per the scope boundary on
permanent fixtures.

**Part 1 — isolated `$snapshot` copy, replicating D-388.** Raw C#
`new GrobArray(existingArray.Elements)`, 1,000-element `List<GrobValue>` source:
**24,080 B** — identical to D-388's figure.

**Part 2 — the same call through the real VM dispatch path** (hand-assembled chunk:
`Constant(array); GetProperty($snapshot); Pop; Return`, executed via `vm.Run`):
**24,256 B** — 176 B above the raw constructor call (VM/stack setup overhead for a
single dispatch), confirming the snapshot copy itself is **1.0×** the isolated
baseline. No doubling at the copy site.

**Part 3 — the whole `for...in` statement, hand-assembled to mirror `EmitArrayForIn`
exactly** (no build loop, no globals — a fresh 1,000-element `GrobArray` loaded from a
constant, then the identical op sequence disassembled in §1): **72,592 B**, split by
ablation:

| Variant | Bytes | Marginal vs. bare loop |
|---|---:|---:|
| Bare counting loop (no `length` read, no `GetIndex`) | 24,424 | — (baseline: one copy + loop arithmetic) |
| `+ GetIndex`/`PopN` (item binding) | 24,520 | +96 B / 1,000 iterations |
| `+ length` `GetProperty` (condition), no `GetIndex` | 72,496 | +48,072 B / 1,000 iterations |
| Full loop (`length` + `GetIndex`) | 72,592 | +48,072 B (length) + 96 B (index) |

**Part 4 — does the `length`-property cost scale with array size?** Same 1,000-call
loop, array size varied, loop bound held fixed at 1,000 so call count does not change:

| Array size | With `length` calls | Without | Delta |
|---:|---:|---:|---:|
| 3 | 48,568 | 496 | 48,072 |
| 100 | 50,896 | 2,824 | 48,072 |
| 1,000 | 72,656 | 24,424 | 48,232 |
| 5,000 | 168,496 | 120,424 | 48,072 |

Constant to within measurement noise (≤160 B) across a 1,667× range of array size.
**The residual does not scale with the snapshot's element count.** This alone refutes
the "copies the array again" hypothesis — a second or third data copy would scale
linearly with size, as the one genuine copy does (Part 1).

**Part 5 — is the cost specific to `"length"`, or any early-return `GetProperty`
branch?** 1,000 calls each, array size 1,000:

| Property | Bytes / 1,000 calls | Per call |
|---|---:|---:|
| `length` | 48,344 | ≈48.0 B |
| `isEmpty` | 48,344 | ≈48.0 B (identical) |
| `append` (reaches the closure-binding path) | 248,344 | ≈248.3 B |
| `NegateInt` ×1,000 (control — no `GetProperty` at all) | 320 | ≈0.3 B |

`length` and `isEmpty` — both early-return branches — cost identically. The control
(a repeated non-`GetProperty` opcode) costs almost nothing, ruling out a generic
"any repeated switch-case" artifact. `append`, which does reach the closure-binding
code, costs more (the same base tax plus its own `FinallyContext`/delegate/
`NativeFunction` allocations) — consistent with §3's account.

**Part 6 — independent synthetic repro, zero Grob code.** A minimal C# method
mirroring the shape (outer per-iteration-reassigned local, an early-return `if`
sibling to a later lambda-declaring branch in the same block), called 1,000 times:
the early-return branch costs **32,000 B** (32 B/call) even though the lambda and its
captured object are **never constructed** on that path; the branch that reaches the
lambda costs 120,000 B. This reproduces the phenomenon from C#/Roslyn semantics alone,
independent of any Grob-specific code, confirming §3's account of the mechanism (the
real VM's per-call figure, ≈48 B, is larger because its closure captures more fields —
`line`, `column`, `ct`, `finallyContext`'s three constructor arguments — than this
repro's two-field closure).

## 5. Reconciliation

Summing the counted sites from §2/§4 for the isolated 1,000-element, 1,000-iteration
loop:

```text
  24,080   one $snapshot copy (Part 1)
+ 48,072   1,000 × GetProperty("length") closure-capture tax (Part 3/4)
+     96   1,000 × GetIndex/PopN (item binding)
+    344   1,000 × bare loop arithmetic (LessInt/JumpIfFalse/IncrementInt/Loop)
---------
  72,592   predicted total
  72,592   measured (Part 3, full loop)          <- exact match, zero residual
```

Against the real compiled fixtures (`attr-build` vs. `attr-build` + 1/2/3 trailing
`for x in xs { }` loops, identical array built via 1,000 `.append()` calls, measured
the same way — direct `GC.GetAllocatedBytesForCurrentThread` around `vm.Run`, no
BenchmarkDotNet): each additional loop costs **72,536–72,596 B**, matching the
isolated hand-assembled figure to within measurement noise (≤60 B) and confirming the
hand-assembled repro faithfully represents the real compiled `for...in` lowering.

Against **D-387's committed figure of record (75,416 B)** and **D-388's own local
re-run (74,560 B)**: this entry's direct-GC-counter figure (72,536–72,596 B) sits
≈1,960–2,880 B below both. That gap is **not attributed to any counted allocation
site** above — the counted-site sum already closes the gap against this entry's own
measurement exactly. It is a **cross-harness** difference: every figure this note
produces comes from direct GC counters, every figure it is compared against comes from
BenchmarkDotNet. Harness/operation-batching overhead folded into a per-op average is the
plausible account, and D-388 documented an ≈850 B gap of that kind between a short local
job and a canonical one — but **no matched-harness comparison was run here**, so that
account is not established. The remainder is therefore left **unresolved**: this note
neither claims it is harness overhead nor claims it is a Grob-VM allocation site it
failed to find. Settling it needs the same fixture measured under both harnesses, which
is outside this increment's scope and is named here for whoever takes it on.

**Attribution: 100% of this entry's own measured 72,536–72,596 B differential is
accounted for by name and site. Of D-387's committed 75,416 B figure of record,
≈96–97% is accounted for by name and site; the remaining ≈3–4% (≈1,960–2,880 B) is an
unresolved cross-harness measurement difference — neither attributed to a named
allocation site nor established as harness overhead.**

## 6. Three-copy hypothesis: refuted

D-388 predicted `3 × 24,080 + 1,977 ≈ 74,217 B`, close to the measured 74,560 B, and
flagged this as arithmetic, not evidence. It is refuted:

- There is **exactly one** `new GrobArray(...)` call on the array `for...in` path
  (§1's disassembly, §2's handler read) — the `$snapshot` `GetProperty` at bytecode
  offset 0012 in §1's disassembly, executed exactly once regardless of array size or
  loop length.
- The ≈48 KB residual does not scale with array size (§4, Part 4) — a second or third
  _copy_ of the array's data would. It scales with **iteration count** (§4, Parts 3
  and 5) and is identical for a property call that never touches the array's element
  data at all (`isEmpty`).
- The ≈3.1× multiple D-388 measured is coincidental: it falls out of combining one real
  24,080 B copy with a per-call tax (≈48 B) that happens, at the specific N=1,000
  chosen for these fixtures, to sum to a figure close to 2× the copy size. A fixture
  built with N=200 elements/iterations would show a _very different_ ratio to a single
  copy (a ≈4,816 B copy against a ≈9,614 B iteration tax — see Part 4's scaling table)
  while remaining fully attributed by the same two named sites.

## 7. What is load-bearing, what is not

- **Load-bearing:** the one `$snapshot` copy (D-383's contents-snapshot guarantee).
  Untouched by this entry.
- **Not load-bearing, and not a copy of anything:** the ≈48 B/call closure-capture
  allocation on every `GetProperty` dispatch against an array receiver. It is a
  C#-compiler artefact of how `VirtualMachine.cs`'s `OpCode.GetProperty` case is
  currently structured (the closure-declaring statements at :894–899 share a lexical
  block with the early-return branches at :873–890), not a semantic requirement of
  `for...in`, `length` or `isEmpty`. A future, separately measured and separately
  decided increment could restructure that case body (for instance, moving the
  closure-declaring statements into their own nested block or a local function so their
  display class is scoped only to the branch that needs it) — enough context for that
  increment to act on. **No proposal, no fix, no design is made here**, per this
  increment's scope boundary.
- Also worth noting for that future increment: this tax is paid by **any** user
  `.length`/`.isEmpty` array-property read, not only by `for...in`'s internal
  `$snapshot`/`length` emission — a fix would benefit ordinary array-property access
  generally, not just the loop path this note was scoped to.

## 8. Is phase 3b unblocked?

**Yes.** D-388's blocking condition was that the residual be attributed before a
`Run_ArrayForIn` allocation ceiling is derived, so a future fix does not force lowering
a published threshold. That condition is met: the ≈75 KB residual is now named, sited
and measured as (one D-383 copy) + (N × a per-`GetProperty`-call closure tax unrelated
to array size) + (negligible loop arithmetic), reconciling exactly against this note's
own measurement and to within an unresolved ≈3–4% cross-harness difference of the
committed figure of record (§5). Phase 3b may derive a ceiling knowing precisely what it
is committing to, and knowing that the per-call tax component would shrink a ceiling
(not require raising one) if a future increment removes it — the correct direction for
D-313's ratchet rule.

---

Cites D-388 (the refuted hypothesis and the blocking condition this closes), D-387 (the
committed differential figure of record and the isolating technique this refines),
D-386 (the original doubling hypothesis), D-383 (the contents-snapshot guarantee the
one genuine copy implements), D-313 (the measurement-before-optimisation gate this
satisfies) and `docs/design/bench-allocation-attribution.md` (§5, phase 1's original
framing of the residual).
