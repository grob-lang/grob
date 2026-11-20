# regex — Regular Expressions

Pattern matching, replacement and extraction. Core module — auto-available, no
import required. Powered by the .NET regex engine.

## Regex Literals

```grob
pattern := /\d{3}-\d{4}/
email   := /^[\w.]+@[\w.]+$/i
```

Regex literals are delimited by `/`. Supported flags: `i` (case-insensitive),
`m` (multiline `^`/`$`). Other options go inline at the start of the pattern, as
`(?s)` or `(?x)`; there is no `g` flag, because `matchAll` and `replaceAll` are the
global forms. Matching is always culture-invariant.

A literal's pattern is checked when the script compiles: an invalid one is E0602,
with the caret where the .NET parser stopped. A literal is compiled once per site,
so a literal inside a loop is not rebuilt on each pass.

The `/` character is disambiguated by context: after an operator, assignment or
opening paren it starts a regex literal. After a value it is the division
operator.

## Building a Regex at Run Time

`regex.compile(pattern, flags, timeout)` builds a `Regex` from a string, the only way
to make one from a value known at run time. `flags` defaults to `""` and `timeout` to
five seconds; `0` means no limit.

```grob
prefix := "v1.2"
release := regex.compile("^" + regex.escape(prefix) + `-\d+$`)
print(release.isMatch("v1.2-7"))   // true

slow := regex.compile(`^(\w+\s?)+$`, "i", 30)
print(slow.timeout)                // 30
```

An invalid pattern, flag string or timeout throws `ParseError`. A string-literal
pattern is checked when the script compiles instead (E0602).

## Timeouts

Matching is bounded by default, so a pattern that backtracks catastrophically fails
fast instead of hanging. The limit is five seconds unless the `Regex` was built by
`regex.compile` with another `timeout`; a `timeout` of `0` removes the limit. Expiry
throws `RuntimeError`, naming the operation and the limit but never the pattern.

## Module Convenience Functions

One-shot functions that take string patterns. Compile on each call — for repeated
use, prefer regex literals. They always use the five-second limit.

| Function | Signature | Description |
|----------|-----------|-------------|
| `regex.compile(pattern, flags = "", timeout = 5)` | `→ Regex` | Build a `Regex` at run time |
| `regex.isMatch(pattern, input)` | `→ bool` | Test for match |
| `regex.match(pattern, input)` | `→ Match?` | First match or nil |
| `regex.matchAll(pattern, input)` | `→ Match[]` | All matches |
| `regex.replace(pattern, input, replacement)` | `→ string` | Replace first |
| `regex.replaceAll(pattern, input, replacement)` | `→ string` | Replace all |
| `regex.split(pattern, input)` | `→ string[]` | Split on pattern |
| `regex.escape(input: string)` | `→ string` | Escape special characters |

## Regex Type Methods

See [Regex type registry](../Type-Registry/Regex.md).

| Method | Signature | Description |
|--------|-----------|-------------|
| `isMatch(input)` | `→ bool` | Fast existence check |
| `match(input)` | `→ Match?` | First match or nil |
| `matchAll(input)` | `→ Match[]` | All non-overlapping matches |
| `replace(input, replacement)` | `→ string` | Replace first |
| `replace(input, fn)` | `→ string` | Replace first with `fn`'s result for the `Match` |
| `replaceAll(input, replacement)` | `→ string` | Replace all |
| `replaceAll(input, fn)` | `→ string` | Replace each with `fn`'s result for its `Match` |
| `split(input)` | `→ string[]` | Split on pattern |

## The Match Type

See [Match type registry](../Type-Registry/Match.md).

| Member | Type | Description |
|--------|------|-------------|
| `value` | `string` | Matched text |
| `index` | `int` | Zero-based position |
| `length` | `int` | Length of match |
| `groups` | `string[]` | Capture groups (`[0]` is full match) |
| `group(name)` | `→ string?` | Named capture group; nil if absent or it did not take part |

## Replacements

Replacement strings use .NET's syntax: `$1` for a numbered group, `${name}` for a named
one and `$$` for a literal `$`. Since `${` inside a double-quoted string is
interpolation, write replacements as backtick raw strings:

```grob
dated := /(?<y>\d{4})-(?<m>\d{2})/
print(dated.replaceAll("2026-04", `${m}/${y}`))   // 04/2026
```

As in .NET, unnamed groups are numbered before named ones, so prefer names when a
pattern has both.

When a replacement is computed, pass a function instead. It receives each `Match` and
returns the replacement string; its parameter is typed `Match`, so its members are checked:

```grob
levels := /\b(ERROR|WARN)\b/
print(levels.replaceAll("WARN then ERROR", m => m.value.lower()))   // warn then error
```

The function form is a `Regex` method only; the one-shot `regex.replace` and
`regex.replaceAll` take a string.

## Examples

### Filter log lines

```grob
pattern := /ERROR|WARN/i

for line in fs.readLines(`C:\Logs\app.log`) {
    if (pattern.isMatch(line)) {
        print(line)
    }
}
```

### Extract data with named groups

```grob
pattern := /(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})/

m := pattern.match("Created on 2026-04-05")
if (m != nil) {
    year  := m.group("year") ?? "?"
    month := m.group("month") ?? "?"
    print("Year: ${year}, Month: ${month}")
}
```

### Replace in file content

```grob
content := fs.readText(`C:\config.txt`)
updated := /version=\d+\.\d+/.replaceAll(content, "version=2.0")
fs.writeText(`C:\config.txt`, updated)
```

## Notes

String literals are never implicitly treated as regex patterns. The regex
literal syntax is a grammar addition — `/pattern/flags` is always explicit.
The full .NET regex feature set is available including named groups and
lookaheads.
