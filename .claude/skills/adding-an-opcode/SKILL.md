---
name: 'Adding an OpCode'
description: 'The full procedure for adding a bytecode instruction to Grob — touch the enum, the compiler selection logic and the VM dispatch together, with stability rules.'
---

# Adding an OpCode

The `OpCode` enum is meant to be complete from Sprint 2 and not grown casually. Adding
one is a deliberate, decision-logged act governed by ADR-0013 (opcode stability). Reach
for this skill only when a genuinely new bytecode instruction is required by a feature
in scope — not to optimise an existing sequence without a decision behind it.

An opcode lives in three places that must change together. Changing one or two and not
the third is the classic miscompilation: the compiler emits something the VM cannot
decode, or the VM has a handler nothing emits.

## Step 1 — Confirm it is warranted

- Is there a `D-###` or an acceptance criterion that needs this instruction? If not,
  this is a design decision, not an implementation task. Stop and raise it.
- Can an existing opcode or a lowering already express it? FOR is lowered to WHILE; `&&`
  and `||` short-circuit via jumps; compound assignment desugars to load-operate-store.
  Do not add an opcode for something the compiler can already lower.
- Check ADR-0013 for the stability contract. Opcodes are part of the `.grobc` wire
  format (`grob-grobc-format.md`); adding one has versioning implications.

## Step 2 — The enum (`Grob.Core`)

Add the case to `OpCode : byte` in `Grob.Core`, in the correct category section
(Values, Arithmetic, Comparison, Logic, Variables, Upvalues, Properties, Array, Control
flow, Functions, Structs, I/O, Increment/decrement, Nil, String interpolation, Exception
handling, Module). Document the operand layout in a trailing comment exactly as the
existing cases do — operand byte count and meaning. The `OpCode` enum is the single
source of truth for the instruction set.

## Step 3 — Encoding (`Grob.Core`)

If the opcode carries operands, make sure the chunk's write and read helpers handle the
operand width. Keep the line-number array in step — every emitted instruction records
its source line in the parallel array, never inline.

## Step 4 — The compiler selection logic (`Grob.Compiler`)

In the compiler visitor pass, emit the opcode where the type-annotated AST calls for it.
If the opcode is one of a typed family (the `Int` / `Float` / `String` split), the
selection is driven by the `ResolvedType` the type checker set — there is no runtime
type check, so the compiler must pick correctly here. Add the selection to the right
partial-class file by concern.

## Step 5 — The VM dispatch (`Grob.Vm`)

Add the handler to the fetch/decode/execute switch in `VirtualMachine`. Decode operands
with the same width the compiler wrote. Implement the stack effect precisely: how many
values it pops, how many it pushes. Runtime errors raised here carry the source line
from the chunk's line array and are subtypes of `GrobError`. The VM stops on the first
runtime error.

## Step 6 — Tests (both sides)

- `Grob.Compiler.Tests`: given source that should produce the opcode, assert the exact
  emitted bytecode including operands.
- `Grob.Vm.Tests`: hand-construct a `Chunk` using the opcode and assert the execution
  result and stack effect.
- If the feature is user-visible, add a `Grob.Integration.Tests` case proving it
  end-to-end.

## Step 7 — Record it

If this opcode changes the instruction set in a way that affects the `.grobc` format or
the stability contract, add a decisions-log entry (`D-###`) and update ADR-0013's
coverage if needed. Reference the `D-###` in the commit.

## Checklist

- [ ] Warranted by a decision or acceptance criterion; not expressible by lowering
- [ ] Enum case added in the right category with operand-layout comment (`Grob.Core`)
- [ ] Chunk encode/decode handles operand width; line array kept in step
- [ ] Compiler emits it via type-checker annotations (`Grob.Compiler`)
- [ ] VM decodes and executes it with the correct stack effect (`Grob.Vm`)
- [ ] Compiler-output test and VM-execution test both present and passing
- [ ] Decision logged if the wire format or stability contract is affected
- [ ] `dotnet build` and `dotnet test` green
