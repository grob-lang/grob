# string

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `length` | property | `→ int` | |
| `isEmpty` | property | `→ bool` | |
| `toInt()` | method | `→ int?` | Nil if not parseable |
| `toFloat()` | method | `→ float?` | Nil if not parseable |
| `trim()` | method | `→ string` | |
| `trimStart()` | method | `→ string` | |
| `trimEnd()` | method | `→ string` | |
| `upper()` | method | `→ string` | |
| `lower()` | method | `→ string` | |
| `split(sep: string)` | method | `→ string[]` | |
| `contains(s: string)` | method | `→ bool` | |
| `startsWith(s: string)` | method | `→ bool` | |
| `endsWith(s: string)` | method | `→ bool` | |
| `replace(from, to: string)` | method | `→ string` | All occurrences |
| `indexOf(s: string)` | method | `→ int` | -1 if not found |
| `lastIndexOf(s: string)` | method | `→ int` | -1 if not found |
| `substring(start, length: int)` | method | `→ string` | Throws if out of range |
| `padLeft(width: int, char: string = " ")` | method | `→ string` | |
| `padRight(width: int, char: string = " ")` | method | `→ string` | |
| `repeat(count: int)` | method | `→ string` | |
| `truncate(maxLength: int, suffix: string = "...")` | method | `→ string` | |
| `left(n: int)` | method | `→ string` | Throws if n > length |
| `right(n: int)` | method | `→ string` | Throws if n > length |
| `toString()` | method | `→ string` | Identity |

See also: [strings module](../Standard-Library/strings.md)
