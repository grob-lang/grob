# float — Type Registry

All members known to the type checker at compile time. Calling an undefined
member is a compile error.

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `toString()` | `→ string` | |
| `toInt()` | `→ int` | Truncates — does not round; faults (`ArithmeticError`) out of range, `NaN` or `Infinity` |
| `round()` | `→ int` | Nearest integer; `MidpointRounding.AwayFromZero` on a `.5` boundary |
| `roundTo(decimals: int)` | `→ float` | Round to N decimal places; same away-from-zero midpoint rule (renamed from the overloaded `round(decimals)`, D-368) |
| `floor()` | `→ int` | |
| `ceil()` | `→ int` | |
| `abs()` | `→ float` | |
| `format(pattern: string)` | `→ string` | Format using .NET pattern string (e.g. `"N2"`, `"F4"`, `"P1"`, `"E3"`) |

## Static Functions

Namespace-receiver calls (D-370), not instance methods — the same call shape as
`math.sqrt(x)`, registered on `NamespaceRegistry` rather than the instance-member
table above.

| Member | Signature | Notes |
|--------|-----------|-------|
| `float.min(a, b)` | `(float, float) → float` | Defers to .NET's `Math.Min` — `NaN` in either position propagates; `-0.0` sorts below `+0.0` |
| `float.max(a, b)` | `(float, float) → float` | Defers to .NET's `Math.Max` — `NaN` in either position propagates; `+0.0` sorts above `-0.0` |
| `float.clamp(v, lo, hi)` | `(float, float, float) → float` | Faults (`ArithmeticError`) if `lo > hi` — an inverted range is a caller bug, not silently clamped |

## Literals

```grob
3.14            // standard
0.5             // leading zero required — .5 is not valid
1.5e10          // scientific notation
2.3E-4          // E case-insensitive
```

## Examples

```grob
x := 3.7
x.toInt()                  // 3 (truncates)
x.round()                  // 4
x.floor()                  // 3
x.ceil()                   // 4

ratio := 2.0 / 3.0
ratio.roundTo(2)            // 0.67

pi := 3.14159
pi.format("F2")             // "3.14"

float.clamp(1.5, 0.0, 1.0)  // 1.0
```
