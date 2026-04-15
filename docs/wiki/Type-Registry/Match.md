# Match

Returned by `Regex.match()` and `Regex.matchAll()`.

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `value` | property | `→ string` | Matched text |
| `index` | property | `→ int` | Zero-based position |
| `length` | property | `→ int` | |
| `groups` | property | `→ string[]` | Index 0 = full match, 1+ = captures |
| `group(name: string)` | method | `→ string?` | Named capture group |

See also: [Regex](Regex.md)
