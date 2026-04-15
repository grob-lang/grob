# Expressions

## Ternary

```grob
label := isActive ? "on" : "off"
prefix := count == 1 ? "item" : "items"
```

Two-way inline value selection. Both arms must return the same type.

## Switch Expression

```grob
status := used_pct switch {
    >= crit_percent => "CRITICAL",
    >= warn_percent => "WARNING",
    _               => "OK"
}

message := http_code switch {
    200 => "OK",
    404 => "Not found",
    _   => "Unknown"
}
```

Multi-branch value selection. Each arm is `pattern => value`. `_` is the
catch-all. The type checker enforces exhaustiveness — missing `_` when not all
cases are covered is a compile error. All arms must return the same type.

## Anonymous Struct Literals

```grob
result := #{ name: "grob", version: "1.0.0" }
```

`#{ }` is always an anonymous struct literal. `{ }` is always a block. No
ambiguity.

## Expressions vs Statements

Assignment is not an expression. `if (x := foo())` is a compile error.

Statements: `:=`, `=`, `+=`, `if/else`, `while`, `select/case`, `for...in`,
`try/catch`, `break`, `continue`, `return`, `i++`, `i--`.

Expressions: literals, identifiers, arithmetic, function calls, ternary `? :`,
switch expression, `#{ }`, `TypeName { }`, method calls, member access.

## Try-Parse Pattern

Grob uses nullable return types for fallible conversions:

```grob
count := "42".toInt()           // int? → 42
bad   := "hello".toInt()        // int? → nil
port  := input.toInt() ?? 8080  // nil coalescing for defaults
```

See also: [Operators](Operators.md), [Functions](Functions.md)
