# Consolidation — Increment C0a-2: array mutating member surface

**Branch:** `feat/array-mutating-members`
**One concern:** deliver the four in-place mutating array members — `append(value: T)`,
`insert(index: int, value: T)`, `remove(index: int)`, `clear()` — with compile-time
`const`/`readonly` rejection. Completes the `T[]` surface.

Runs against the fresh corpus zip carrying D-356 through D-371. Corpus-first discipline
throughout; read the live decisions log, error-code and opcode registry tails, do not trust
this prompt or memory for D-### numbers or error codes.

---

## Authority and context

- **The gap.** `grob-type-registry.md`'s `T[]` section documents thirteen members. D-371
  delivered nine (four pre-existing higher-order, five non-mutating query members); these four
  are the remainder. Verify the count against the live registry section before building — the
  surface is the registry's, not this prompt's.
- **The rejection rule is already settled — do not redesign it.** D-291 §4 (deep immutability)
  names this case *explicitly*: "Any method call or operation that would mutate the bound value
  is a compile error — `X.append(...)` on `readonly T[]`, `X["k"] = v` on `readonly map<...>`,
  field assignment on a `readonly` struct, `++X`, `X += 1`." The machinery exists:
  `FindReadonlyRoot` (`TypeChecker.Statements.cs`, D-350) walks index and member chains to a
  `readonly` root, and **E0204** is the code it raises. `const`-bound arrays are unreachable —
  D-289 bars collection literals as `const` RHS, as D-350 established — so add no `const` path.
- **Method-call fall-through is currently permissive.** D-371 records that `arr.append(1)` and
  `arr.garbage()` still resolve permissively to `Unknown` at compile time, because only the nine
  landed members are recognised by name. Registering these four closes most of that gap. Report
  what remains permissive afterwards (an unrecognised method name on an array receiver) and
  whether that should now raise E1002 like the bare-property case D-371 tightened — **report and
  recommend; do not change it unilaterally**, since it is a behaviour change beyond this
  increment's four members.

---

## BLOCKING PREREQUISITE — aliasing semantics must be decided before this increment ships

**This increment makes array aliasing observable for the first time, and the corpus does not
document what should happen.**

`GrobArray` is a `sealed class` (`Grob.Core/GrobArray.cs`), so today's de facto behaviour is
**reference semantics**:

```
a := [1, 2]
b := a
b.append(3)
print(a.length)   // 3 under reference semantics, 2 under value semantics
```

Until now this was unobservable — no array operation mutated in place, so no program could tell
the two apart. `append` makes it observable in every script that assigns an array to a second
binding or passes one to a function.

A corpus search found **no decision on aliasing, reference semantics or copy-on-assignment
anywhere** — not in the decisions log, not in `grob-type-registry.md`, not in
`grob-language-fundamentals.md`. D-291 settles *immutability*; it does not settle *aliasing*.

**Do not settle this by shipping.** Silently landing reference semantics would decide a
user-visible language question by implementation accident — precisely the failure mode the
advertised-vs-built audit exists to prevent, and the same class as the `float.round` overload
that needed D-368 before A1a could proceed.

**Required before source edits:** confirm a ratified decision exists covering array (and, by
extension, map and struct) assignment and argument-passing semantics — reference or value — and
cite its D-### in the plan. If no such decision exists, **STOP and escalate**: this needs a
decision authored in a planning session, not a call made inside an implementation increment.

For that decision's benefit, report from the source: whether function arguments pass the same
`GrobArray` instance (reference) or a copy; whether `GrobStruct` and the map representation are
likewise classes; and whether any existing test asserts either behaviour. Report findings; do
not change semantics.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **The aliasing prerequisite above** — cite the ratified D-### or escalate.
2. **`ValidateArrayMethodCall` / `IsArrayMethod`** (renamed by D-371) — how the nine recognised
   members are validated. Confirm adding four names is additive, and how `ArrayDescriptorOf`
   supplies the element type for `append`/`insert`'s `T`-typed value argument (the D-371
   `contains(v: T)` precedent — E0004 when a descriptor is available, permissive when not).
3. **`FindReadonlyRoot`'s reach.** It is used today for assignment targets (`arr[i] = v`,
   `obj.field = v`). Confirm it can be applied at a *method-call* site — `readonlyArr.append(1)`
   — and report where that check belongs so E0204's message remains accurate for a mutating
   method rather than an assignment. Confirm it walks chains (`ro.field.append(1)`,
   `roArr[0].append(1)` where the element is itself an array).
4. **The runtime natives.** `ArrayNatives.cs` holds the receiver-bound native table
   (`filter`/`select`/`sort`/`each`/`first`/`last`/`contains`). Report where the four mutating
   natives go and confirm they mutate the receiver `GrobArray` in place rather than returning a
   new one — all four are documented `→ void`.
5. **`insert`/`remove` bounds.** Both throw `IndexError` out of range per the registry. Confirm
   the existing `IndexError` leaf and its code (the same the array indexer and `substring` use)
   and reuse it. Pin the boundary rule: `insert` at `index == length` (append position) — valid
   or out of range? Report the least-surprising reading and test it either way.
6. **`for...in` interaction.** `Compiler.ControlFlow.cs` emits a synthetic `length` read for loop
   bounds (D-371). Report what happens when a loop body mutates the array it is iterating —
   whether bounds are re-read per iteration or captured once. **Report only**; if the behaviour
   is surprising, name it as a finding for scheduling rather than fixing it here.
7. **`CallExpr.ResolvedReturnType`** — all four return `void`. Confirm how D-362's void-returning
   `CallExpr` case (the permissive-`Unknown` source it enumerated) applies, and that a void
   member call used as an expression behaves as the existing `each()` does.

Report the validation additions, the E0204 call-site check, the natives, the pinned bounds rule,
the `for...in` finding, and the test list. Then STOP.

---

## Surface to build

| `append(value: T)` | method | `→ void` | Appends one element. Mutates in place |
| `insert(index: int, value: T)` | method | `→ void` | Inserts before index. Throws `IndexError` if out of range. Mutates in place |
| `remove(index: int)` | method | `→ void` | Removes element at index. Throws `IndexError` if out of range. Mutates in place |
| `clear()` | method | `→ void` | Removes all elements. Mutates in place |

Semantics per `grob-type-registry.md`'s `T[]` section (authoritative).

---

## Scope boundaries — do NOT

- **Do not decide aliasing semantics.** Cite the ratified decision or escalate. This is the one
  hard gate on the increment.
- **Do not build map members** — C0b.
- **Do not change any of the nine landed array members** (D-371) or `sort`'s comparer.
- **Do not tighten unrecognised-method-name fall-through to E1002** — report and recommend only.
- **Do not fix the `E0004` sort-comparator taxonomy finding** (D-371) or the `for...in` mutation
  finding if one emerges — both belong to the pending correctness batch.
- **No new opcode. No new error code** — E0204 for immutability, the existing `IndexError` for
  bounds, E0003/E0004 for arity and argument type. Count stays 118. If a genuinely new condition
  has no home, STOP and escalate via `allocating-an-error-code`.

---

## Tests — TDD, red first, same commit

- Each of the four members: type-checker resolution, compiler bytecode-shape, and end-to-end
  value tests proving in-place mutation (`a.append(3)` then `a.length` is 3).
- **Element-type checking**: `intArray.append("x")` and `intArray.insert(0, "x")` raise E0004;
  correct types pass; a receiver without a descriptor stays permissive (the D-371 rule).
- **Arity**: wrong argument counts raise E0003.
- **`readonly` rejection — the load-bearing test:** `readonly xs := [1, 2]` then `xs.append(3)`
  raises **E0204** at compile time, for all four members. Include a chained case
  (`ro.field.append(1)` or the nested-array equivalent) proving `FindReadonlyRoot`'s walk.
- **Bounds**: `insert`/`remove` out of range raise a **catchable** `IndexError` — assert
  catchable from Grob source via `try`/`catch`, not a host exception. Include negative indices,
  `index == length` per the pinned rule, and `remove` on an empty array.
- **`clear`** empties the array; `length` is 0 and `isEmpty` is true afterwards (the D-371
  members observing the mutation).
- **Aliasing behaviour asserted explicitly**, matching whatever the ratified decision states —
  a test that pins it either way, so the semantics cannot drift silently. This is the test the
  prerequisite exists to make possible.
- Every existing array, `string`, numeric and `math` test unchanged.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-372**; confirm, do not assume. The
  entry records: the four mutating members delivered, completing the `T[]` surface; the ratified
  aliasing decision this increment implements against, cited by D-###, and the test that pins it;
  the E0204 compile-time rejection at a method-call site and `FindReadonlyRoot`'s reach; the
  `IndexError` reuse and the pinned `insert` boundary rule; what remains permissive in
  method-name fall-through and the recommendation made about it; and the `for...in` mutation
  finding if one emerged. No new opcode, no new error code, count 118. Cite D-371, D-291, D-350,
  D-289, D-351, D-362, and the aliasing decision.
- **Update `grob-type-registry.md`** — the `T[]` build-status note records the surface as fully
  built, citing this D-###. Update `wiki/Type-Registry/array.md` to match, including the
  mutation rules and the aliasing semantics now that they are documented.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages).
  Archive this prompt under `prompts/archive/sprint-9/`.
