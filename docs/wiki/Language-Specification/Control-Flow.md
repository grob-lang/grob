# Control Flow

## `if / else`

```grob fragment
if (condition) {
    // then block
}

if (condition) {
    // then block
} else {
    // else block
}

if (condition) {
    // then block
} else if (condition) {
    // else if block
} else {
    // fallback block
}
```

Parentheses around the condition are required. Opening brace on the same line as
the keyword. `else if` is two keywords — not `elif` or `elsif`. `else {` must be
on the same line as the closing brace of the preceding block.

`if/else` is a statement, not an expression. For expression-position conditionals
use the ternary `? :` or the switch expression. See [Expressions](Expressions.md).

## `while`

```grob fragment
while (condition) {
    // body
}
```

Parentheses around the condition are required. `do...while` is deferred post-MVP.
For collection iteration prefer `for...in` or functional methods.

## `select / case`

Multi-branch statement. First matching case executes. No fall-through.

```grob fragment
select (value) {
    case 0 {
        print("Zero")
    }
    case 1, 2 {
        print("One or two")
    }
    case "error" {
        log.error("Error state")
    }
    default {
        print("Something else: ${value}")
    }
}
```

Multiple values per case arm: `case 1, 2 { }` matches either value. The
`default` arm is optional — if omitted and no case matches, execution continues
past the `select` block. Exhaustiveness is not enforced on `select` statements
(the switch *expression* enforces exhaustiveness because a missing case means a
missing value).

`break` does not apply inside `select` — there is no fall-through so it is never
needed. `select` works on any comparable type: `int`, `string`, `bool`.

`select` is always a statement. The switch expression (`value switch { }`) is
always an expression. The two forms are syntactically unambiguous. See
[ADR-0009](../ADR/0009-select-statement-vs-switch-expression.md).

## `for...in` — Collection Iteration

```grob fragment
// Value only
for file in files {
    print(file.name)
}

// Index and value
for i, file in files {
    print("${i}: ${file.name}")
}
```

The single-identifier form binds the value, not the index. Both `i` and `file`
are declared by the `for` statement — no `:=` in the loop header. Both are
scoped to the loop body and immutable within it — reassigning either is a
compile error.

Arrays are iterable in v1. Whether additional types are iterable depends on the
iterable protocol (OQ-007, open).

## `for...in` — Numeric Range

```grob
for i in 0..10 { }              // 0, 1, 2 ... 10  (inclusive)
for i in 0..100 step 5 { }      // 0, 5, 10 ... 100
for i in 10..0 step -1 { }      // 10, 9, 8 ... 0
```

The `..` operator means both bounds are inclusive. `step` is optional and
defaults to `1`. A descending range without an explicit negative step is a
compile error — `for i in 10..0 { }` without `step -1` is rejected at compile
time.

The loop variable is `int`, declared by the `for` statement, immutable within
the body. The compiler lowers range loops to `while` — the VM never sees range
opcodes.

## `break` and `continue`

```grob fragment
for file in files {
    if (file.name == "skip.log") {
        continue
    }
    if (file.size > maxSize) {
        break
    }
    process_file(file)
}
```

`break` exits the innermost enclosing loop. `continue` skips the remainder of
the current iteration. Both work in `for...in` and `while` loops. Both are
statements — using either outside a loop is a compile error.

Labelled break is deferred post-MVP. The v1 alternative is to extract the inner
loop into a function and use `return`.

See also: [Expressions](Expressions.md), [Operators](Operators.md)
