# string — Type Registry

All members known to the type checker at compile time. Calling an undefined
member is a compile error. Method calls are rewritten to native function calls —
no boxing, no vtable.

## Properties

| Member | Signature | Notes |
|--------|-----------|-------|
| `length` | `→ int` | |
| `isEmpty` | `→ bool` | |

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `toInt()` | `→ int?` | Returns nil if not parseable |
| `toFloat()` | `→ float?` | Returns nil if not parseable |
| `trim()` | `→ string` | Leading and trailing whitespace |
| `trimStart()` | `→ string` | Leading whitespace only |
| `trimEnd()` | `→ string` | Trailing whitespace only |
| `upper()` | `→ string` | |
| `lower()` | `→ string` | |
| `split(sep: string)` | `→ string[]` | |
| `contains(s: string)` | `→ bool` | |
| `startsWith(s: string)` | `→ bool` | |
| `endsWith(s: string)` | `→ bool` | |
| `replace(from: string, to: string)` | `→ string` | Replaces all occurrences; an empty `from` is a `RuntimeError` |
| `indexOf(s: string)` | `→ int` | First occurrence; -1 if not found |
| `lastIndexOf(s: string)` | `→ int` | Last occurrence; -1 if not found |
| `substring(start: int, length: int)` | `→ string` | Zero-based. Throws `RuntimeError` if out of range |
| `padLeft(width: int, char: string = " ")` | `→ string` | Pads to total width |
| `padRight(width: int, char: string = " ")` | `→ string` | Pads to total width |
| `repeat(count: int)` | `→ string` | Repeats n times |
| `truncate(maxLength: int, suffix: string = "...")` | `→ string` | Truncates and appends suffix if needed |
| `left(n: int)` | `→ string` | First n characters. Throws if n > length |
| `right(n: int)` | `→ string` | Last n characters. Throws if n > length |

## Examples

```grob
name := "  Hello World  "
name.trim()                    // "Hello World"
name.length                    // 15
name.contains("World")         // true

"42".toInt()                   // 42 (as int?)
"not a number".toInt()         // nil

"hello".padLeft(10)            // "     hello"
"hello".repeat(3)              // "hellohellohello"
"hello world".left(5)          // "hello"
"hello world".right(5)         // "world"
"a very long string".truncate(10)  // "a very ..."
```

See also: [strings module](../Standard-Library/strings.md)
