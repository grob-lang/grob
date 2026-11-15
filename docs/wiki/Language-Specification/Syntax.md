# Syntax

Grob uses C-style syntax with newline-terminated statements. It is designed to
be readable by any C# or Go developer without prior knowledge of Grob.

## Variables

`:=` declares and assigns. `=` reassigns. There is no `var` keyword.

```grob
x := 42                  // declare and assign — x is int (inferred)
x = 100                  // reassign — x must already exist
y: int := 42             // explicit type annotation — always valid, never required here

const MAX := 100         // compile-time constant — value must be a literal or compile-time expression
const API_VERSION: int := 2

readonly base_url := env.require("BASE_URL")  // runtime-once — set once, never reassigned

name: string? := nil     // annotation required — nil provides no type information
items: int[] := []       // annotation required — [] provides no element type
```

`const` is for compile-time-constant bindings only (D-288). The value must be
resolvable at compile time: a literal, a compile-time arithmetic expression, or
a reference to another `const`. It cannot depend on runtime input.

`readonly` is for runtime-once bindings (D-289). The value is computed at
runtime but assigned exactly once — the compiler prevents reassignment. Use
`readonly` for values derived from environment variables, config files, or
computed setup values that must not change after initialisation.

Every variable must be initialised at declaration. There are no uninitialised
variables in Grob. A declaration without a value is a compile error.

`:=` always declares in the current local scope. `=` reassigns by walking the
scope chain to find the name wherever it lives. This is the Go model — mandatory
`:=` declaration makes scope resolution unambiguous.

## Braces and Blocks

Opening braces go on the same line as the keyword. Bare `{ }` is always a block.

```grob
if (condition) {
    // block
}

fn add(a: int, b: int): int {
    return a + b
}
```

| Syntax | Meaning |
|--------|---------|
| `{ }` | Block — always |
| `#{ field: value }` | Anonymous struct literal |
| `TypeName { field: value }` | Named type construction |

## Statement Termination

Newlines terminate statements. There are no semicolons. A newline is suppressed
(the statement continues) in two cases:

**Trailing token** — the current line ends with a binary operator, assignment
operator, comma, open bracket or parenthesis, member access dot, or lambda arrow.

**Leading dot** — the next line begins with `.`. The lexer peeks one token across
the newline.

```grob
// Leading dot — recommended for method chains
result := files
    .filter(f => f.extension == ".log")
    .select(f => f.name)
    .sort()

// Trailing operator
total := price +
    tax +
    shipping

// Multi-line function call
output := someFunction(
    arg1,
    arg2
)
```

Both cases are resolved entirely in the lexer. The parser receives a clean
token stream with newlines already resolved.

## Comments

```grob
// Single-line comment

/* Block comment
   spans multiple lines */

/// Doc comment — reserved, no semantics in v1
```

Block comments do not nest. `///` is recognised and discarded by the lexer —
semantics will be attached when `grob doc` tooling exists post-MVP.

## String Literals

```grob
"hello"                 // double-quoted
"Hello ${name}"         // interpolation
`raw\nstring`           // raw string — backtick-delimited, no escape processing
`C:\Users\chris`        // raw strings for Windows paths
```

Single-quoted strings are not valid. Double-quoted strings support escape
sequences (`\n`, `\t`, `\\`, `\"`) and interpolation (`${expr}`). Raw strings
suppress all escape processing. Double-quoted strings do not span lines; raw
strings do.

## Naming Conventions

Grob recommends `snake_case` for variables, functions and parameters. The
compiler produces a warning (not an error) for other naming styles. Type names
use `PascalCase`.

See also: [Types](Types.md), [Operators](Operators.md),
[Control Flow](Control-Flow.md)
