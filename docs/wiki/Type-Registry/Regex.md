# Regex — Type Registry

Created from regex literals (`/pattern/flags`). Compiled once at declaration.

## Properties

| Member | Signature | Notes |
|--------|-----------|-------|
| `pattern` | `→ string` | The source pattern string |
| `flags` | `→ string` | Active flags, e.g. `"i"` or `"im"` |

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `isMatch(input: string)` | `→ bool` | Fast existence check — no Match allocation |
| `match(input: string)` | `→ Match?` | First match or nil |
| `matchAll(input: string)` | `→ Match[]` | All non-overlapping matches |
| `replace(input: string, replacement: string)` | `→ string` | Replace first match |
| `replaceAll(input: string, replacement: string)` | `→ string` | Replace all matches |
| `split(input: string)` | `→ string[]` | Split on pattern |

## Examples

```grob fragment
email_pattern := /^[\w.]+@[\w.]+$/i

if (email_pattern.isMatch(user_input)) {
    print("Valid email format")
}

// Extract all numbers from a string
numbers := /\d+/.matchAll("Order 42, Item 7, Qty 3")
for m in numbers {
    print(m.value)   // "42", "7", "3"
}
```

See also: [Match type](Match.md), [regex module](../Standard-Library/regex.md)
