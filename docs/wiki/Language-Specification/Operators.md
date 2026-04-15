# Operators

## Arithmetic

| Operator | Operation | Notes |
|----------|-----------|-------|
| `+` | Addition / string concatenation | `string + int` is a compile error |
| `-` | Subtraction | |
| `*` | Multiplication | |
| `/` | Division | `int / int` truncates; `float / float` is float |
| `%` | Modulo | |

## Comparison

| Operator | Operation |
|----------|-----------|
| `==` | Equal |
| `!=` | Not equal |
| `<` | Less than |
| `>` | Greater than |
| `<=` | Less than or equal |
| `>=` | Greater than or equal |

## Logical

| Operator | Operation | Notes |
|----------|-----------|-------|
| `&&` | Logical AND | Short-circuit |
| `\|\|` | Logical OR | Short-circuit |
| `!` | Logical NOT | |

## Assignment

| Operator | Operation |
|----------|-----------|
| `:=` | Declare and assign |
| `=` | Reassign |
| `+=` `-=` `*=` `/=` `%=` | Compound assignment |

All compound assignment operators are statements — they do not produce a value.

## Increment and Decrement

`i++` and `i--` — postfix only. Both are statements, not expressions. Apply to
`int` only. The compiler lowers `i++` to `i = i + 1`.

## Other

| Operator | Operation |
|----------|-----------|
| `??` | Nil coalescing |
| `?.` | Optional chaining |
| `..` | Range (numeric `for` loops only in v1) |

Bitwise operators are not in scope for v1.

## Precedence (Highest to Lowest)

| Level | Operators | Notes |
|-------|-----------|-------|
| 1 | `()`, `[]`, `.`, `?.` | Postfix |
| 2 | `-` (unary), `!` | Prefix |
| 3 | `*`, `/`, `%` | Multiplicative |
| 4 | `+`, `-` | Additive |
| 5 | `..` | Range |
| 6 | `<`, `>`, `<=`, `>=` | Comparison |
| 7 | `==`, `!=` | Equality |
| 8 | `&&` | Logical AND |
| 9 | `\|\|` | Logical OR |
| 10 | `? :` | Ternary |
| 11 | `??` | Nil coalescing |
| 12 | `:=`, `=`, `+=`, `-=`, `*=`, `/=`, `%=` | Assignment |
| 13 | `=>` | Lambda arrow |

See also: [Expressions](Expressions.md), [Types](Types.md)
