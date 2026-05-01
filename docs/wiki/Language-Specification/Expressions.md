# Expressions

## Statements vs Expressions

Grob distinguishes statements (execute, produce no value) from expressions
(produce a value).

**Statements:** variable declaration (`:=`), reassignment (`=`), compound
assignment (`+=` etc), increment/decrement, `if/else`, `while`, `select/case`,
`for...in`, `try/catch`, `break`, `continue`, `return`.

**Expressions:** literals, identifiers, arithmetic/comparison/logical operations,
function calls, ternary `? :`, switch expression, anonymous struct `#{ }`, named
type construction, method calls, member access.

Assignment is not an expression. `if (x := foo())` is a compile error.

## Literals

```grob
42                  // int
0xFF                // int (hex)
0b1010              // int (binary)
1_000_000           // int (underscores for readability)
3.14                // float (leading zero required — .5 is not valid)
1.5e10              // float (scientific notation)
"hello"             // string
"Hello ${name}"     // string with interpolation
`raw\nstring`       // raw string (backtick, no escape processing)
true                // bool
false               // bool
nil                 // nil
[1, 2, 3]           // array
```

## Ternary Expression

Two-way inline value selection.

```grob
label  := isActive ? "on" : "off"
prefix := count == 1 ? "item" : "items"
```

## Switch Expression

Multi-branch value selection. C# 8 style.

```grob
status := used_pct switch {
    >= crit_percent => "CRITICAL",
    >= warn_percent => "WARNING",
    _               => "OK"
}

message := http_code switch {
    200 => "OK",
    404 => "Not found",
    500 => "Server error",
    _   => "Unknown"
}
```

Each arm is `pattern => value`. `_` is the catch-all. The type checker enforces
exhaustiveness: missing `_` when not all cases are covered is a compile error.
All arms must return the same type — mismatch is a type error.

Switch expression results can be assigned or passed directly:

```grob
log.info(http_code switch {
    200 => "Request succeeded",
    _   => "Request failed with code ${http_code}"
})
```

## Anonymous Struct Literals

```grob
item := #{ name: "report.xlsx", size: 2048 }

// Common use — projection in data pipelines
results
    .select(r => #{ repo: r.name, stale: r.staleCount })
    .formatAs.table()
```

The type checker creates an internal structural type for each anonymous struct.
Field access is type-safe. Accessing undefined fields is a compile error.

## Named Type Construction

```grob
r := Repo {
    name:    "grob",
    ssh_url: "git@github.com/grob-lang/grob"
}
```

`TypeName { }` is an expression that produces a value of that type. It can
appear anywhere that type is expected.

## Array Indexing

```grob
first := items[0]
matrix_cell := matrix[r][c]
```

Zero-based. `[]` is indexing, `()` is function calls — no overlap. The type
checker validates that the subject of `[]` is an indexable type. Indexing a
function or calling an array is a compile error.

## Method Calls and Member Access

```grob
name.length          // property — no ()
name.upper()         // method call
"42".toInt()         // method on literal
42.toString()        // method on primitive
```

Method-call syntax on all types is syntactic sugar — the compiler rewrites to
native function calls at compile time. Primitives are never boxed. The type
checker distinguishes properties (no `()`) from methods based on the type
registry.

## `void`

`void` is not a first-class type. It exists only as the return type of functions
that produce no value. The return value of a `void` function cannot be assigned
or used in expression position.

See also: [Types](Types.md), [Operators](Operators.md), [Functions](Functions.md)
