# D-393 Q2 — Per-receiver `NativeFunction` cache

**Branch:** `perf/receiver-method-cache`
**One concern:** stop `ArrayNatives.GetMethod` constructing a fresh `NativeFunction` on every
`GetProperty` dispatch, by caching the bound method on the receiver instance. Last of D-393's
three fixes, sequenced after Q3 (D-394) and Q1 (D-397).

Runs against the fresh corpus zip carrying D-356 through D-397. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — unchanged by this increment.

---

## Authority and the measured case

**D-393 Q2** ratified the design in full — read it before planning; the invalidation analysis in
particular is already done and verified against source, and this increment implements it rather
than re-deriving it.

**The cost.** `ArrayNatives.GetMethod` returns `new NativeFunction("append", 1, (args, _) =>
Append(args, receiver))` — **freshly, on every call**. The lambda captures `receiver`, so Roslyn
cannot cache it as a static delegate: a display-class instance, a delegate bound to it, and the
`NativeFunction` wrapper are allocated **per dispatch**, before the native body runs.

**What the two prior fixes leave.** After D-394 (Q3) and D-397 (Q1), the per-native-call floor on
`attr-native` is **74.1 B/call**, down from 186.1. `Run_ArrayForIn` is **259,488 B**, down from
531,616. The known remaining components are the per-call `callArgs` array (common to all natives)
and — on the array path specifically — this per-call rebinding triple. **`attr-array-dispatch`
minus `attr-native` isolates it**: that difference is what this increment targets.

---

## The design, as ratified — implement, do not redesign

A lazily created `Dictionary<string, NativeFunction>?` **field on `GrobArray`/`GrobMap`**,
populated on first bind per method name, consulted before constructing a fresh `NativeFunction`.

**The invalidation story is "none needed", for two independent reasons** — D-393's analysis,
which the gate below re-verifies rather than re-argues:

- **(a) Receiver mutation is safe.** Every bound native closes over `receiver` **by reference**
  and reads its live state per invocation — `append`/`filter`/etc. never snapshot at bind time —
  so a cached `NativeFunction` observes mutations exactly as a fresh one would.
- **(b) No per-access VM context is captured — the load-bearing reason.** A cached
  `NativeFunction` would be unsound if it held the caller's `line`, `column`, cancellation token
  or `FinallyContext`, because the cache outlives the access that created it. It holds none: the
  bound delegate closes over `receiver` **and nothing else**. The four higher-order arms take
  their `VmInvoker` from the parameter the caller supplies **at invocation time**, not at bind
  time — which is exactly what D-394 proved when it deleted `GetMethod`'s bind-time `invoker`
  parameter as dead.

Lifetime is tied to the receiver by being a field on it: it cannot outlive the receiver (no
leak) and cannot predate it (nothing to invalidate against).

**Rejected by D-393, do not revisit:** a name-keyed cache held externally (cannot carry which
receiver a bound method closes over); a `ConditionalWeakTable` at VM or static scope (its own
per-entry allocation plus a global lookup, for no invalidation benefit over the direct field).

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Re-verify D-393's reason (b) against the post-D-397 tree.** D-397 replaced the `VmInvoker`
   delegate with a `readonly struct` carrying `IVmCallHost`, `line`, `column`, the cancellation
   token and a `VmFinallyWindow`. Confirm the bound delegates in `ArrayNatives` still capture
   **only** `receiver` and take their `VmInvoker` at invocation time — the soundness argument
   rests on this, and the type it rests on has changed since D-393 verified it. **If D-397
   altered the capture set in any way, STOP and report** rather than proceeding on a stale
   analysis.
2. **`GrobArray` and `GrobMap`'s current shape** — where the field goes, and whether either type
   has equality, serialisation, cloning or `ValueDisplay` behaviour that a new field could
   disturb. The cache is an implementation detail and must not become observable.
3. **`MapNatives.GetMethod`** — D-393 scoped the cache to it "if it ever grows a higher-order
   member". Report whether map members are bound the same per-call way today, and therefore
   whether the map path benefits now or only later. **Decide by evidence, not symmetry**: if map
   binding already allocates per call, cache it; if not, say so and leave it.
4. **Thread-safety.** Report whether a `VirtualMachine` and its values can be touched from more
   than one thread in any current or planned path (the playground architecture's capability model
   is worth a glance). If single-threaded by construction, say so explicitly and use a plain
   `Dictionary`; if not, that changes the design and is a STOP-and-report.
5. **Memory shape.** The cache adds a `Dictionary` per receiver *that has a method called on it*.
   For a program holding many small arrays and calling one method on each, that could **increase**
   total allocation. Report the crossover: how many calls on one receiver before the cache pays
   for itself, and whether the lazy-creation-on-first-bind design makes the common case safe.
   **This is the one way this fix could make things worse**, and it deserves a number, not a
   reassurance.
6. **Plan the measurement.** `attr-array-dispatch` (1,000 `xs.contains(i)` on **one** receiver —
   the cache's best case) is the primary fixture; `Run_ArrayForIn` (1,000 `.append()` on one
   receiver, then a `for...in`) is the second. State expected post-fix figures **before**
   measuring. Note that `attr-native` should be **unchanged** — it calls no array method — and if
   it moves, something unintended happened.

Report the re-verified capture set, the field placement, the map decision, the thread-safety
finding, the crossover analysis, and the measurement plan. Then STOP.

---

## Scope boundaries — do NOT

- **Do not redesign the cache.** D-393 ratified receiver-field-with-lazy-dictionary and recorded
  why the alternatives lose. Implement it.
- **Do not implement option C** (split registration shapes) — rejected by D-393 with explicit
  revisit triggers, none of which have fired.
- **Do not change any native's behaviour**, arity, or the `NativeFunction` shape. This changes
  **when** a bound method is constructed, nothing about what it does.
- **Do not make the cache observable** — not through equality, `ValueDisplay`, `toString`,
  serialisation or any user-visible surface.
- **Do not update a benchmark baseline** — a reduction needs none. Ceiling re-derivation follows
  this increment as its own piece of work, once, against final numbers.
- **No new opcode. No new error code** — count stays **121**.

---

## Tests — TDD, red first

- **Cache correctness under mutation — the load-bearing test:** call `xs.length` on an array,
  mutate it (`append`, `remove`, `clear`), call `xs.length` again, and assert the second call
  reflects the mutation. Repeat for a higher-order member (`filter` before and after mutation) to
  prove reason (a) holds in practice, not only in argument.
- **Per-access VM context stays per-access — proving reason (b):** invoke the *same* method on the
  *same* receiver from two different call sites and assert a fault raised inside each reports its
  **own** `line`/`column`, not the site that first populated the cache. This is the failure the
  invalidation analysis says cannot happen; test it rather than trust it.
- **Nested and cross-native invocation** still work (D-397 added
  `CrossNativeNestedInvocation_...`; keep it green), including a `filter` lambda invoking
  `select` on a **different** receiver — two caches live at once.
- **Cache isolation:** two arrays each get their own bound methods; a method bound on one is never
  returned for the other.
- **Unrecognised member** still raises `E1002` (D-377/D-380's tightening) — the cache must not
  turn a miss into a hit or vice versa.
- Every existing array, map, VM, string and numeric test passes unchanged.
- **The measurement**, before and after, same machine, one sitting (`git stash`, matching D-394's
  and D-397's technique): `attr-array-dispatch`, `Run_ArrayForIn` and `attr-native` `Allocated`
  figures both ways, with the derived per-call delta. **`attr-native` unchanged is part of the
  expected result.**

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-398**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: D-393 Q2 implemented and
  the field's placement; **the re-verification of the capture set against the post-D-397
  `VmInvoker` struct**, since the soundness argument depends on it; the map decision and its
  evidence; the thread-safety finding; **the crossover analysis** — how many calls per receiver
  before the cache pays, and the risk case of many receivers each called once; **the before and
  after measurements with the derived per-call delta**, including `attr-native` confirmed
  unchanged; and that this **completes D-393's three fixes**, with the ceiling re-derivation now
  due against final numbers. No opcode change, no new error code, count 121. Cite D-393 (Q2, Q4),
  D-397, D-394, D-391, D-372, D-313.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
