# Grob — Language Design Brainstorm

> Captured from initial design session, February 2026.  
> Early sketch notes. Nothing here is a firm decision.  
> Harden after SharpBASIC retrospective and clox.

-----

## Identity Statement

> *Grob is a statically typed scripting language with C-style syntax, type inference,
> and first-class file system operations. Nullable types are explicit. Immutability
> is opt-in via const. It’s designed to be readable by any C# or Go developer
> without prior knowledge of Grob.*

-----

## Target Audience

- Developers and system administrators
- Hobbyists who want something learnable
- North star: **Arduino** — made hardware accessible without dumbing it down
- Core philosophy: *“A statically typed scripting language that a hobbyist can
  learn and a developer can trust.”*

-----

## Day One Use Case

Bulk file operations — the thing people reach for PowerShell and bash for:

- Bulk rename / copy / move / delete with patterns
- Recursive directory traversal
- Find files matching criteria
- Pipe / redirect output to a text file
- Run an external command and capture output

If someone can write a bulk file rename script on day one, Grob has earned its place.

-----

## Syntax Decisions

|Concern             |Decision     |Rationale                                                                            |
|--------------------|-------------|-------------------------------------------------------------------------------------|
|Brace style         |Same-line `{`|C#/Go familiar, avoids newline terminator ambiguity                                  |
|Statement terminator|Newline      |No semicolons. Parser infers continuation when line ends mid-expression              |
|Line continuation   |Implicit     |Trailing operator, comma, or open bracket signals continuation — no `_` or `\` needed|
|Comments            |`//`         |Universal C-style                                                                    |

### Line continuation examples

> **Note (April 2026):** These examples originally used `let` — corrected to `:=`
> to match the confirmed syntax.

```grob
// Single line — obvious
x := 42

// Multi-line — parser sees trailing comma, continues
result := someFunction(
    arg1,
    arg2
)

// Multi-line expression — parser sees trailing operator
total := price
    + tax
    + shipping
```

-----

## Type System

### Declaration and Assignment

```grob
x := 42              // inferred, mutable
x: int := 42         // explicit type annotation, mutable
const MAX := 100     // immutable binding, inferred
const MAX: int := 100  // immutable binding, explicit
```

- `:=` declares and assigns — first use of a name
- `=` reassigns — name must already exist
- Explicit type annotations are optional but always permitted
- This lets the compiler give useful errors:
  - `'x' already declared. Use = to reassign.`
  - `'y' is not declared. Use := to declare.`

### No `var` keyword

Dropped — adds ceremony without buying safety. `:=` is sufficient.

### Mutability

- Mutable by default
- `const` for immutable bindings
- Immutable binding means the binding cannot be reassigned
- Content mutability for collections — defer to later design phase

### No Uninitialized Variables

Every declaration must have a value or be explicitly optional.

```grob
x: int           // compiler error — no value
x: int? := nil   // fine — explicitly optional
x := 42          // fine — has a value
```

Zero-value sugar deliberately excluded — explicit is better.

-----

## Nullable / Optional Types

`?` suffix on any type means the value might be nil. C# familiar — free
onboarding for the target audience.

```grob
name: string? := nil   // explicitly nullable
name = "Alice"         // fine — mutable

print(name)            // compiler error — might be nil
if name != nil {
    print(name)        // safe — compiler knows
}
```

Non-optional types are **guaranteed non-nil**. The type system enforces this at
compile time. No null reference exceptions on non-optional types.

### Nil Coalescing

```grob
display := name ?? "Anonymous"    // use default if nil
```

### Optional Chaining

```grob
length := name?.length ?? 0       // safe navigation, default if nil
```

Both operators are C# familiar — `??` and `?.` — zero learning curve for
the target audience.

-----

## Functions

```grob
fn add(a: int, b: int): int {
    return a + b
}

// Return type inferred when unambiguous
fn greet(name: string) {
    print("Hello " + name)
}
```

-----

## File Operations — Day One Example

```grob
fn rename_files(dir: string, from: string, to: string) {
    for file in fs.list(dir) {
        if file.name.contains(from) {
            file.rename(file.name.replace(from, to))
        }
    }
}

rename_files("C:\\Reports", "2024", "2025")
```

Readable to any C#, Go, Swift, or Kotlin developer without prior Grob knowledge.

-----

## Fluent Syntax

Yes — but not day one. Fluent chaining requires a collections API with
consistent return types. Design the collections stdlib first and fluent
falls out naturally.

```grob
// Target fluent style for collections
files := fs.list("C:\\Reports")
    .filter(f => f.name.contains("2024"))
    .map(f => f.rename(f.name.replace("2024", "2025")))
```

Reference: C# LINQ is the design north star for the collections API.

-----

## Reference Languages

|Language  |What to steal                                                         |
|----------|----------------------------------------------------------------------|
|**Go**    |Error handling as values, simple formatting, low ceremony, fast feel  |
|**Kotlin**|Type inference, null safety, concise syntax, optional chaining        |
|**Swift** |Optionals done right, readable and writable in equal measure          |
|**C#**    |`?` nullable syntax, `??` and `?.` operators, LINQ as fluent reference|
|**Rust**  |Pattern matching, making illegal states unrepresentable               |

-----

## What Grob Is NOT

- Not Python — dynamically typed, whitespace-significant, clunky for scripting
- Not PowerShell — powerful but syntactically hostile
- Not bash — cryptic, inconsistent
- Not Rust — too steep a learning curve for hobbyists
- Not a general-purpose application language — it is a scripting language first

-----

## The Gap Grob Fills

Nobody has nailed **statically typed, low ceremony, genuinely readable scripting**.

- Go comes closest but was designed for services not scripts
- PowerShell and bash own the sysadmin space through ubiquity not quality
- Python owns education but is dynamically typed and feels clunky at scale

Grob targets that gap.

-----

## Implementation Notes

- Bytecode VM as centrepiece — informed by clox (Crafting Interpreters Part III)
- Do not design in detail until SharpBASIC is complete and retrospective is written
- Do not design in detail until clox is worked through
- MVP success criterion: a working console-based calculator
- Open source — release when core is solid, not before

-----

## Deferred — Design Later

> **Note (April 2026):** Most items in this table have been resolved in the
> decisions log. This table is preserved for historical context. See
> `Grob___Decisions___Context_Log.md` for current status.

|Feature               |Notes                                             |Status (April 2026)                         |
|----------------------|--------------------------------------------------|--------------------------------------------|
|Standard library shape|fs, strings, process — needs detailed API design  |✅ Resolved — all 13 core modules specified  |
|Collections API       |Required before fluent syntax is useful           |✅ Resolved — array methods, filter/map/sort |
|Content mutability    |Mutable binding vs mutable value distinction      |⬜ Deferred to post-MVP                      |
|Error handling model  |Go-style values vs exceptions — not decided       |✅ Resolved — exceptions with try/catch      |
|Module / import system|`import Grob.Maths` — late phase, not early driver|✅ Resolved — `math` is core (no import). Plugins use `import`.|
|String interpolation  |`"Hello ${name}"` — likely yes, detail later      |✅ Resolved — confirmed load-bearing         |
|Pattern matching      |`match` expression — likely yes, detail later     |✅ Resolved — switch expression (C# style)   |
|VS Code extension     |TextMate grammar, LSP — post-MVP                  |⬜ Deferred to post-MVP                      |

-----

## Current Status

- [x] SharpBASIC complete
- [x] SharpBASIC retrospective written
- [ ] clox worked through — in progress
- [x] Grob design session — formal spec begun
- [x] Grob Claude Project created with dedicated system prompt
- [ ] MVP defined and scoped

-----

*This document is an early sketch from February 2026. The authoritative decisions
record is `Grob___Decisions___Context_Log.md`. This document is preserved for
historical context.*