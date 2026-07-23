# int — Type Registry

All members known to the type checker at compile time. Calling an undefined
member is a compile error.

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `toString()` | `→ string` | |
| `toFloat()` | `→ float` | Always succeeds |
| `abs()` | `→ int` | Faults (`ArithmeticError`) on `long.MinValue` — its magnitude is not representable |
| `format(pattern: string)` | `→ string` | Format using .NET pattern string (e.g. `"N2"`, `"X8"`, `"P1"`) |

## Static Functions

| Member | Signature | Notes |
|--------|-----------|-------|
| `int.min(a, b)` | `(int, int) → int` | |
| `int.max(a, b)` | `(int, int) → int` | |
| `int.clamp(v, lo, hi)` | `(int, int, int) → int` | |

## Literals

```grob
42              // decimal
0xFF            // hexadecimal
0b1010          // binary
1_000_000       // underscore separator
```

Hex digits are case-insensitive. Underscores are ignored by the compiler —
readability only.

## Examples

```grob
x := -42
x.abs()                    // 42
x.toString()               // "-42"
x.toFloat()                // -42.0

y := 255
y.format("X8")             // "000000FF"

int.min(10, 20)            // 10
int.max(10, 20)            // 20
int.clamp(150, 0, 100)     // 100
```

## Integer Division

`int / int → int` (truncating). `int / float` and `float / int` promote to
`float`. See [Operators](../Language-Specification/Operators.md).
