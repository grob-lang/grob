# Syntax

Grob uses C-style syntax with newline-terminated statements. It is designed to
be readable by any C# or Go developer without prior knowledge of Grob.

## Variables

`:=` declares and assigns. `=` reassigns. There is no `var` keyword.

```grob
x := 42                  // declare and assign — x is int (inferred)
x = 100                  // reassign — x must already exist
x: int := 42             // explicit type annotation — always valid, never required here

const MAX := 100         // immutable binding
const MAX: int := 100    // immutable binding, explicit type

name: string? := nil     // annotation required — nil provides no type information
items: int[] := []       // annotation required — [] provides no element type
```

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
    .map(f => f.name)
    .sort()

// Trailing operator
total := price
    + tax
    + shipping

// Multi-line function call
result := someFunction(
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

Triple backtick strings are available for multiline verbatim content (SQL, JSON
templates, multiline command strings):

```grob
query := ```
SELECT *
FROM users
WHERE active = 1
```
```

Double-quoted strings support escape sequences (`\n`, `\r`, `\t`, `\\`, `\"`,
`\$`) and interpolation (`${expr}`). Any unknown `\x` sequence is a compile
error. Single backtick strings suppress all escape processing. Triple backtick
strings preserve content verbatim across multiple lines.

Single-quoted strings (`'value'`) are not valid in Grob.

## Naming Conventions

Grob recommends `snake_case` for variables, functions and parameters. The
compiler produces a warning (not an error) for other naming styles. Type names
use `PascalCase`.

## Trailing Commas

Trailing commas are permitted in all comma-separated lists — array literals,
struct construction, map literals, function parameters and function arguments.
`grob fmt` normalises trailing comma usage.

## Shadowing

A local variable may shadow a variable from an enclosing scope. The compiler
emits a warning, not an error. Preventing shadowing entirely is annoying in
real scripts where short variable names are naturally reused.

See also: [Types](Types.md), [Operators](Operators.md),
[Control Flow](Control-Flow.md)
