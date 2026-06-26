# Sprint 5 — Increment 3: location-based upvalue closing (D-325)

Branch: `fix/s5-inc3-upvalue-closing` — one concern, off `main`, `main` stays protected.

## Gate

Plan mode first. Produce a plan against the sequence below, post it, wait for explicit approval. No source change before approval. Full files in the diff, never patches. CodeRabbit in-loop; GPT-5.3 Codex cold-read against the merged branch before close.

## Objective

A closure retrieved from a container and called faults the VM with a value-stack underflow. Reproduction:

```grob
fn makeAdders(): fn(int): int [] {
    inc := (n: int) => n + 1
    return [inc]
}

adders := makeAdders()
print(adders[0](41))     // expected 42 — currently faults: value-stack underflow
```

Fix the cause, not the symptom: make upvalue closing **location-based**, so a closure that escapes via any route is handled identically.

## The decision — D-325 (self-contained)

A closure closes its captured locals by **stack location**, not by inspecting the return value. The VM keeps an **open-upvalue list** keyed by stack slot. On frame exit, every open upvalue at or above the returning frame's base is closed — its value copied off the value stack into the heap upvalue cell — and removed from the list. Closing is driven by the stack boundary alone and is indifferent to which heap object now references the closure.

Root cause of the current fault: closing was **value-based** — the close pass reached closures referenced directly by the return value but missed closures wrapped in an array, map or struct. The missed closure kept an open upvalue pointing at a slot the frame's return truncated; the next read of that upvalue underflowed the stack.

## Verify first (do not rely on memory)

Confirm each against the live `grob-decisions-log.md` tail before coding:

- **D-325** — this increment. Confirm it is the entry you are implementing and nothing supersedes it.
- **D-296** — four-category variable resolution. This fix is **category 4 only** (enclosing-scope locals captured as upvalues).
- **D-321** — top-level `fn` hoisting. Top-level functions capture no upvalues; the prologue and init-state machine are out of scope and must not regress.
- **D-303** — `GrobValue` is a tagged union. A closure is a `GrobFunction` value; there is no separate closure value type. Do not touch the value representation.
- **D-306** — disassembler and `#if DEBUG` tracing. Use tracing to watch stack depth evolve while diagnosing; the release dispatch loop stays branch-free.

## Load-bearing rules (inline reference)

**Call convention (already correct — do not change its shape).**

```text
CallFrame { GrobFunction Function; int InstructionPointer; int StackBase; }
Call:    StackBase = stackTop - argCount;  callee sits at StackBase - 1
Return:  stackTop  = StackBase - 1;        then push result
```

The callee occupies `StackBase - 1` and is discarded by `Return`. This is uniform across call shapes; the bug is not in the call convention.

**Value model.** A closure is a `BytecodeFunction : GrobFunction` carrying upvalues. `Peek(argCount) as GrobFunction` succeeds for a closure. The fault is stack arithmetic in upvalue closing, not a failed cast.

**Open-upvalue list (the mechanism to implement).** A list of open upvalues keyed by stack slot, ordered by slot descending. `OP_CLOSURE` capturing a local either reuses the existing open upvalue for that slot or creates one and inserts it in order. Frame exit closes everything at or above the frame base. This is the clox `openUpvalues` shape — match it.

**Post-return invariant.** After any `OP_RETURN`, no open upvalue may reference a slot at or above the returned frame's base.

**Error policy.** This increment adds **no** error code. The corrected path produces correct behaviour and surfaces no diagnostic. Count stays at 109. If implementation appears to need a new code, **stop and surface** — do not invent one.

## Closed surface — do not touch

- `GrobValue` representation (D-303).
- Top-level `fn` hoisting, the prologue, the init-state machine (D-321).
- The type checker — this is a VM-only fix.
- The call-frame layout and `Return` arithmetic, beyond threading the close sweep into frame exit.
- The error catalog / error-code count (D-308, D-316). No additions.

## TDD sequence

1. **Tests first** in `Grob.Vm.Tests`. Author the **escape matrix** and confirm each currently faults or misbehaves on `main`:
   - closure stored in an **array**, indexed out, called — the reproduction above.
   - closure stored as a **map** value, looked up, called.
   - closure stored as a **struct field**, accessed, called.
   - closure **returned** then **immediately invoked** (`makeCounter()()`).
   - closure **passed as a parameter** to another function, then called.
   - control case: a counter factory where each invocation observes its own captured state across calls (independence and persistence).
   A single array case is insufficient — the bug class is "captured closure escaped indirectly", so every route is exercised.
2. **Implement** the open-upvalue list, `OP_CLOSURE` capture (reuse-or-create), and the frame-exit close sweep at `OP_RETURN` and at any compiler-emitted `OP_CLOSE_UPVALUE` for an early scope exit.
3. **Assertion.** Add the post-return invariant as a `#if DEBUG` check (open-upvalue list head, if present, sits strictly below `frame.StackBase` after `OP_RETURN`). This converts a future regression of this class from a late underflow into an immediate, located failure.
4. **Green.** Whole `Grob.Vm.Tests` suite passes; no other suite regresses.

## Acceptance criteria

- Every escape-matrix case observes the captured value rather than faulting.
- The reproduction script returns 42.
- The `#if DEBUG` post-return invariant holds across the suite.
- Error-code count unchanged at 109; the D-316 drift gate is green.
- `grob-vm-architecture.md` gains an **upvalue-lifecycle section**: open and closed states, the open-upvalue list, capture at `OP_CLOSURE`, the frame-exit close sweep, the post-return invariant. Fold in alongside the D-322 known-limitation note already flagged against this doc.
- CodeRabbit clean; Codex cold-read raises nothing load-bearing.

## Stop-and-surface

- Any apparent need for a new error code.
- Any case where the call convention itself (not the close sweep) looks wrong — surface it rather than reshaping the convention.
- Any interaction that would touch top-level hoisting (D-321) or the value representation (D-303).
- A captured-variable semantics question the four-category model (D-296) does not already answer.
