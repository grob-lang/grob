# Regex

Created by regex literals `/pattern/flags`. Compiled once at declaration.

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `pattern` | property | `→ string` | |
| `flags` | property | `→ string` | |
| `isMatch(input: string)` | method | `→ bool` | |
| `match(input: string)` | method | `→ Match?` | |
| `matchAll(input: string)` | method | `→ Match[]` | |
| `replace(input, replacement: string)` | method | `→ string` | First match |
| `replaceAll(input, replacement: string)` | method | `→ string` | All matches |
| `split(input: string)` | method | `→ string[]` | |

See also: [Match](Match.md), [regex module](../Standard-Library/regex.md)
