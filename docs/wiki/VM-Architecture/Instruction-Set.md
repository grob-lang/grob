# Instruction Set

Grob defines a custom bytecode instruction set. Each opcode is a single byte.

## Core Opcodes

| Category | Opcodes |
|----------|---------|
| Values | `Constant`, `Nil`, `True`, `False`, `Pop` |
| Arithmetic | `Add`, `Subtract`, `Multiply`, `Divide`, `Negate`, `Modulo` |
| Comparison | `Equal`, `NotEqual`, `Less`, `Greater`, `LessEqual`, `GreaterEqual` |
| Logic | `Not`, `And`, `Or` |
| Variables | `GetLocal`, `SetLocal`, `GetGlobal`, `SetGlobal`, `DefineGlobal` |
| Control flow | `Jump`, `JumpIfFalse`, `Loop` |
| Functions | `Call`, `Return` |
| I/O | `Print` |

The instruction set grows as the language needs it. Opcodes are defined in
`Grob.Core` as an enum.

## Constant Pool

Literals live in a separate constant pool array. Bytecode references them by
index. `CONSTANT 0` pushes `constants[0]` onto the value stack.

## The `.grobc` Format

Magic number: `GROB` (0x47 0x52 0x4F 0x42). Little-endian. Includes a format
version field for stale-file detection.

See also: [Overview](Overview.md), [Call Frames](Call-Frames.md)
