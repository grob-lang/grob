---
name: 'Emitting Closures and Upvalues'
description: 'The procedure for compiling and executing closures in Grob — the four-category variable resolution, upvalue capture of enclosing-function locals, the open/closed upvalue lifecycle, the close-on-return hand-off and per-call capture independence. Use when implementing or reviewing lambda capture (Sprint 5 Increment D), not for top-level-only lambdas.'
---

# Emitting closures and upvalues

A lambda that references an **enclosing-function local** captures it as an
**upvalue**. This skill is the procedure for getting capture right — the resolution
that decides what is captured, the open/closed lifecycle on the value stack, the
close-on-return hand-off, and the per-call independence that makes two closures from
two calls hold separate state. The `Closure`, `GetUpvalue`, `SetUpvalue` and
`CloseUpvalue` opcodes already exist in the closed enum (Sprint 2 A) — this is about
wiring their compiler emission and VM dispatch, not adding an opcode. The design
follows clox; the authority is `grob-language-fundamentals.md` §12.1, D-296 and
`grob-vm-architecture.md`.

## Step 0 — Decide whether capture is even involved

Resolve each free identifier in the lambda body into one of the four D-296
categories **before** reaching for any upvalue machinery:

1. **Top-level `const`** — inlined as a constant-pool load. No runtime slot, no
   capture.
2. **Top-level `readonly`** — global read. No capture.
3. **Top-level mutable** — global read, and global write from the body. No capture.
4. **Enclosing-function local** — **this is capture.** Upvalue.

If every free reference is categories 1–3, the lambda is **not a closure** — it
compiles to a `BytecodeFunction` and a plain `Call`, and you emit **no**
`Closure`/upvalue opcode. Most top-level lambda usage is categories 1–3. Only when a
reference resolves to an enclosing-function local do the rest of these steps apply.
The word "capture" means category 4 and nothing else.

## Step 1 — Resolve the upvalue (compiler)

When a body references a category-4 local, record an upvalue on the lambda's
function. For each upvalue, the resolver decides:

- **Local of the immediately enclosing function** — captures a slot in the parent's
  frame; record the slot index and `isLocal = true`.
- **Upvalue of the enclosing function** — transitive capture, where the variable
  belongs to a function further out and the immediately enclosing lambda already
  captured it; record the parent upvalue index and `isLocal = false`.

The resolver walks the enclosing chain, adding an upvalue at each level so a
deeply-nested reference is threaded out to where the variable actually lives. Get the
`isLocal` flag right at every level — it tells the VM whether to capture a stack slot
or copy a parent upvalue when the `Closure` is created.

## Step 2 — Emit `Closure` (compiler)

After compiling the lambda's `BytecodeFunction`, emit `Closure` with the upvalue
descriptors (the `isLocal`/index pairs from Step 1). At runtime this creates a
`Closure` object — the `BytecodeFunction` plus a populated upvalue array. Emit
`GetUpvalue`/`SetUpvalue` for reads/writes of the captured variable in the body
(never `GetLocal`/`SetLocal` — the variable is not a local of the lambda).

## Step 3 — Create the closure and capture (VM)

On the `Closure` opcode, build the upvalue array: for each descriptor, if `isLocal`,
capture the enclosing frame's stack slot (creating or reusing an **open upvalue** for
that slot); if not, copy the enclosing closure's upvalue at the given index. Key the
open-upvalue list by the **stack slot of the current frame**, not by variable name —
this is what gives each enclosing-function call its own captured slot.

## Step 4 — The open/closed lifecycle (VM)

- **Open.** While the enclosing function is on the stack, the upvalue references the
  live stack slot. `GetUpvalue`/`SetUpvalue` read/write through to that slot, so the
  closure and the enclosing function see the same variable, and two closures
  capturing the same open slot see each other's writes.
- **Closed.** When the enclosing function **returns**, every open upvalue pointing at
  one of its slots is **closed**: the value copies off the stack into a heap cell and
  the upvalue thereafter references the heap cell. Do this in the `Return` path,
  before the slots are discarded. After closing, the closure keeps the variable alive
  independent of the (now gone) frame.
- `GetUpvalue`/`SetUpvalue` must be **transparent** across the transition — the same
  opcode reads/writes whether the upvalue is open (stack) or closed (heap). The
  indirection lives in the `Upvalue` object, not in the opcode.

## Step 5 — Per-call independence

Each call to the enclosing function allocates its own slots, so capturing in two
calls yields two closures with **separate** captured state. `makeCounter()` called
twice gives two counters that do not share. If they share, the open-upvalue list is
keyed wrongly (by name rather than by the current frame's slot) or the close-on-return
is not isolating per-frame slots. This is the single most important property to prove
empirically.

## Step 6 — Tests (both sides)

- `Grob.Compiler.Tests`: a capturing lambda disassembles to `Closure` +
  `GetUpvalue`/`SetUpvalue` with the right upvalue descriptors; a transitive capture
  resolves to an upvalue-of-enclosing chain; a **top-level-only** lambda emits **no**
  upvalue opcode (the guard that categories 1–3 are not routed through capture).
- `Grob.Vm.Tests`: close-on-return (a closure outlives its enclosing frame and
  reads/writes the captured variable correctly); **per-call independence** (two
  closures from two calls count separately); a write through an open upvalue is seen
  by another closure capturing the same open slot.
- `Grob.Integration.Tests`: a closure factory producing independent counters and a
  capturing lambda passed to `filter`/`select` runs end-to-end.

Verify the lifecycle by disassembly and VM result, not by inspection — the open→closed
hand-off and per-call independence only submit to empirical proof.

## Checklist

- [ ] Every free reference classified into a D-296 category before any upvalue work
- [ ] Categories 1–3 emit no upvalue opcode; only category 4 captures
- [ ] Upvalue descriptors record local-vs-enclosing-upvalue correctly at each level
- [ ] `Closure` emitted with descriptors; `GetUpvalue`/`SetUpvalue` for captured reads/writes
- [ ] Open-upvalue list keyed by the current frame's stack slot
- [ ] Upvalues close to the heap in the `Return` path, before slots are discarded
- [ ] `GetUpvalue`/`SetUpvalue` transparent across open→closed
- [ ] Per-call independence proven (two closures, separate state)
- [ ] Compiler-output and VM-execution tests both present; top-level-only guard test present
- [ ] `dotnet build` and `dotnet test` green; coverage at or above 90%
