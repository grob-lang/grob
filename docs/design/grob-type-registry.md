# Grob — Built-in Type Method Registry

> Compiler-known methods and properties per built-in type.
> Decisions authorised in the decisions log — April 2026 design sessions.
> This document is the implementation reference for the type checker and
> compiler — it defines the complete set of members registered on each type.
> Calling anything not in this registry is a compile error.
> When this document and the decisions log conflict, the decisions log wins.

---

All method calls on primitives are rewritten to native function calls at compile
time — no boxing, no vtable, no heap allocation.

---

**Build-status note (D-363, updating D-362's doc-honesty correction).**
The `string` section below is now **built**, except `padLeft`/`padRight`/`truncate`
— those three carry default parameters and are pending D-358's default-argument
call-site synthesis; every other member ships. `ResolveMemberAccessCall`
(`TypeChecker.Expressions.cs`) gained a primitive-value-receiver arm keyed on
`PrimitiveMemberRegistry` (`Grob.Core`, parallel to the `NamedTypeRegistry` shape),
and `VisitCall`/`VisitMemberAccess` rewrite a resolved primitive-member access to a
qualified native call at compile time (D-066), receiver injected as arg[0] — no
`GetProperty`/`Bind` runtime dispatch, no new opcode. The `int`, `float` and `bool`
sections below still describe the **target** surface only, not current behaviour —
the same mechanism, unbuilt for those three primitives until their own follow-on
increments prove it out.

---

## `string`

| Member                                             | Kind     | Signature    | Notes                                                       |
| -------------------------------------------------- | -------- | ------------ | ----------------------------------------------------------- |
| `length`                                           | property | `→ int`      |                                                             |
| `isEmpty`                                          | property | `→ bool`     |                                                             |
| `toInt()`                                          | method   | `→ int?`     | Returns nil if not parseable                                |
| `toFloat()`                                        | method   | `→ float?`   | Returns nil if not parseable                                |
| `trim()`                                           | method   | `→ string`   | Removes leading and trailing whitespace                     |
| `trimStart()`                                      | method   | `→ string`   | Removes leading whitespace only                             |
| `trimEnd()`                                        | method   | `→ string`   | Removes trailing whitespace only                            |
| `upper()`                                          | method   | `→ string`   |                                                             |
| `lower()`                                          | method   | `→ string`   |                                                             |
| `split(sep: string)`                               | method   | `→ string[]` |                                                             |
| `contains(s: string)`                              | method   | `→ bool`     |                                                             |
| `startsWith(s: string)`                            | method   | `→ bool`     |                                                             |
| `endsWith(s: string)`                              | method   | `→ bool`     |                                                             |
| `replace(from: string, to: string)`                | method   | `→ string`   | Replaces all occurrences                                    |
| `indexOf(s: string)`                               | method   | `→ int`      | First occurrence; -1 if not found                           |
| `lastIndexOf(s: string)`                           | method   | `→ int`      | Last occurrence; -1 if not found                            |
| `substring(start: int, length: int)`               | method   | `→ string`   | Zero-based start; throws `IndexError` if out of range       |
| `padLeft(width: int, char: string = " ")`          | method   | `→ string`   | Pads to total width on the left                             |
| `padRight(width: int, char: string = " ")`         | method   | `→ string`   | Pads to total width on the right                            |
| `repeat(count: int)`                               | method   | `→ string`   | Repeats the string n times                                  |
| `truncate(maxLength: int, suffix: string = "...")` | method   | `→ string`   | Truncates to max length; appends suffix if truncated        |
| `left(n: int)`                                     | method   | `→ string`   | First `n` characters. Throws `IndexError` if n > length     |
| `right(n: int)`                                    | method   | `→ string`   | Last `n` characters. Throws `IndexError` if n > length      |
| `toString()`                                       | method   | `→ string`   | Returns the string unchanged — identity for type uniformity |

---

## `int`

| Member                    | Kind   | Signature               | Notes                                                          |
| ------------------------- | ------ | ----------------------- | -------------------------------------------------------------- |
| `toString()`              | method | `→ string`              |                                                                |
| `toFloat()`               | method | `→ float`               | Always succeeds                                                |
| `abs()`                   | method | `→ int`                 |                                                                |
| `format(pattern: string)` | method | `→ string`              | Format using .NET pattern string (e.g. `"N2"`, `"X8"`, `"P1"`) |
| `int.min(a, b)`           | static | `(int, int) → int`      |                                                                |
| `int.max(a, b)`           | static | `(int, int) → int`      |                                                                |
| `int.clamp(v, lo, hi)`    | static | `(int, int, int) → int` |                                                                |

---

## `float`

| Member                    | Kind   | Signature                       | Notes                                                                  |
| ------------------------- | ------ | ------------------------------- | ---------------------------------------------------------------------- |
| `toString()`              | method | `→ string`                      |                                                                        |
| `toInt()`                 | method | `→ int`                         | Truncates — does not round                                             |
| `round()`                 | method | `→ int`                         | Nearest integer                                                        |
| `round(decimals: int)`    | method | `→ float`                       | Round to N decimal places                                              |
| `floor()`                 | method | `→ int`                         |                                                                        |
| `ceil()`                  | method | `→ int`                         |                                                                        |
| `abs()`                   | method | `→ float`                       |                                                                        |
| `format(pattern: string)` | method | `→ string`                      | Format using .NET pattern string (e.g. `"N2"`, `"F4"`, `"P1"`, `"E3"`) |
| `float.min(a, b)`         | static | `(float, float) → float`        |                                                                        |
| `float.max(a, b)`         | static | `(float, float) → float`        |                                                                        |
| `float.clamp(v, lo, hi)`  | static | `(float, float, float) → float` |                                                                        |

---

## `bool`

| Member       | Kind   | Signature  | Notes                         |
| ------------ | ------ | ---------- | ----------------------------- |
| `toString()` | method | `→ string` | Returns `"true"` or `"false"` |

---

## `T[]` (All Arrays)

| Member                                                     | Kind     | Signature | Notes                                                                                                |
| ---------------------------------------------------------- | -------- | --------- | ---------------------------------------------------------------------------------------------------- |
| `length`                                                   | property | `→ int`   |                                                                                                      |
| `isEmpty`                                                  | property | `→ bool`  |                                                                                                      |
| `first()`                                                  | method   | `→ T?`    | nil if empty                                                                                         |
| `last()`                                                   | method   | `→ T?`    | nil if empty                                                                                         |
| `contains(v: T)`                                           | method   | `→ bool`  |                                                                                                      |
| `filter(fn: T → bool)`                                     | method   | `→ T[]`   | Returns new array                                                                                    |
| `select<U>(fn: T → U)`                                     | method   | `→ U[]`   | Transformation. Returns new array.                                                                   |
| `each(fn: T → void)`                                       | method   | `→ void`  |                                                                                                      |
| `sort<U: Comparable>(fn: T → U, descending: bool = false)` | method   | `→ T[]`   | Returns new sorted array. **Stable.** U must be `int`, `float`, `string`, `date`, `guid`, or `bool`. |
| `append(value: T)`                                         | method   | `→ void`  | Appends one element. Mutates in place. Binding must not be `const` or `readonly`.                    |
| `insert(index: int, value: T)`                             | method   | `→ void`  | Inserts before index. Throws `IndexError` if out of range. Mutates in place.                         |
| `remove(index: int)`                                       | method   | `→ void`  | Removes element at index. Throws `IndexError` if out of range. Mutates in place.                     |
| `clear()`                                                  | method   | `→ void`  | Removes all elements. Mutates in place. Binding must not be `const` or `readonly`.                   |

**Mutation rules:**

- `append`, `insert`, `remove`, `clear` mutate the array in place.
- Calling any mutation method on a `const`- or `readonly`-bound
  array is a compile error.
- `filter`, `select`, `sort` always return a new array — they never mutate.

---

## `map<K, V>`

A first-class built-in type. Maps are *intended* to be statically typed by key and
value — `map<string, string>` and `map<string, int>` as distinct types — but **this
typing is not yet implemented (D-351)**: `TypeRef.TypeArguments` is parsed and not yet
consulted, `map` resolves to the flat `GrobType.Map` tag everywhere, and a
`for k, v in m` loop binds `v` as `Unknown`. Value-type inference — mirroring arrays'
`ArrayTypeDescriptor` (D-351) as a `MapTypeDescriptor`, with only `V` inferred since
v1 keys are `string`-only — is scheduled before v1; until it lands, the per-key and
per-value typing described in this section is the **target** surface, not current
behaviour. Users consume and construct maps; they cannot declare generic map types of
their own (same constrained-generics model as arrays). In v1, keys must be `string` —
non-string keys are deferred post-MVP.

**Construction:**

```grob
// Empty map with explicit type annotation
headers: map<string, string> := map<string, string>{}

// Map literal with initial entries — newline-separated
headers := map<string, string>{
    "Content-Type":  "application/json"
    "X-Api-Version": "2024-01-01"
}

// Single-line form with commas
flags := map<string, bool>{ "verbose": true, "dryRun": false }

// Returned by stdlib — no construction needed
all   := env.all()           // map<string, string>
hdrs  := response.headers    // map<string, string>
```

**Map literal separator rules:**

- Entries are separated by newlines or commas. Both are valid.
- Trailing commas are permitted.
- Each entry is `key: value` — colon separates key from value (not `=`).
- Keys are string literals in v1 (non-string keys post-MVP).

**Iteration:**

```grob
for k, v in headers {
    print("${k}: ${v}")
}
```

**Members:**

| Member                  | Kind     | Signature | Notes                                        |
| ----------------------- | -------- | --------- | -------------------------------------------- |
| `length`                | property | `→ int`   | Number of entries                            |
| `isEmpty`               | property | `→ bool`  |                                              |
| `keys`                  | property | `→ K[]`   | All keys as array. Order is insertion order. |
| `values`                | property | `→ V[]`   | All values as array. Order matches `keys`.   |
| `get(key: K)`           | method   | `→ V?`    | Returns nil if key absent                    |
| `set(key: K, value: V)` | method   | `→ void`  | Insert or overwrite. Mutates in place.       |
| `contains(key: K)`      | method   | `→ bool`  | True if key present                          |
| `remove(key: K)`        | method   | `→ void`  | No-op if key absent. Mutates in place.       |
| `clear()`               | method   | `→ void`  | Removes all entries. Mutates in place.       |
| `[key: K]`              | indexer  | `→ V?`    | Sugar for `get(key)`                         |
| `[key: K] = value`      | indexer  | `→ void`  | Sugar for `set(key, value)`                  |

**Mutation rules:**

- `set`, `remove`, `clear`, and indexer assignment mutate the map in place.
- Calling any mutation method on a `const`- or `readonly`-bound map
  is a compile error.
- `get` and the read indexer never mutate.

---

## `File` (Returned by `fs.list()`)

| Member                                             | Kind     | Signature  | Notes                                                                                          |
| -------------------------------------------------- | -------- | ---------- | ---------------------------------------------------------------------------------------------- |
| `name`                                             | property | `→ string` | Filename with extension, no path                                                               |
| `path`                                             | property | `→ string` | Full absolute path                                                                             |
| `directory`                                        | property | `→ string` | Parent directory path                                                                          |
| `extension`                                        | property | `→ string` | Lowercased, includes dot — e.g. `.xlsx`                                                        |
| `size`                                             | property | `→ int`    | Size in bytes                                                                                  |
| `modified`                                         | property | `→ date`   | Last write time                                                                                |
| `created`                                          | property | `→ date`   | Creation time                                                                                  |
| `isDirectory`                                      | property | `→ bool`   | True if entry is a directory                                                                   |
| `rename(newName: string)`                          | method   | `→ void`   | Rename in place — new name only, not path                                                      |
| `moveTo(destDir: string, overwrite: bool = false)` | method   | `→ void`   | Move to destination directory. Throws `IoError` if destination exists and `overwrite` is false |
| `copyTo(destDir: string, overwrite: bool = false)` | method   | `→ void`   | Copy to destination directory. Throws `IoError` if destination exists and `overwrite` is false |
| `delete()`                                         | method   | `→ void`   | Delete the file                                                                                |

---

## `date` (Instance Methods — Additions)

The following are additions to the `date` type registry confirmed in this session.
The full construction, parsing, formatting, arithmetic, comparison, component, epoch,
and timezone members are specified in the confirmed decisions table (Apr 2026,
`date module — API`).

Like `guid` (see `## guid` above), `date`'s full instance-member set (properties —
`year`/`month`/`day`/`hour`/`minute`/`second`/`dayOfYear`/`dayOfWeek`/`utcOffset` —
and methods — the arithmetic, comparison, ISO/Unix-epoch, timezone and interval
members) is `NamedTypeRegistry`'s (D-356) `date` entry; the two `daysUntil`/
`daysSince` members below are that entry's data, not a separate mechanism. `date`'s
static constructors (`date.now()`, `date.of()`, `date.parse()`, ...) remain
`NamespaceRegistry` entries, untouched by D-356.

| Member                   | Kind   | Signature | Notes                                                            |
| ------------------------ | ------ | --------- | ---------------------------------------------------------------- |
| `daysUntil(other: date)` | method | `→ int`   | Positive if `other` is later than receiver; negative if reversed |
| `daysSince(other: date)` | method | `→ int`   | Positive if receiver is later than `other`; negative if reversed |

---

## `strings` Module

| Member                                                  | Signature  | Notes                                                                                        |
| ------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------------- |
| `strings.join(parts: string[], separator: string = "")` | `→ string` | Joins array with separator. Receiver is an array — cannot be an instance method on `string`. |

---

## `csv.Table` (Returned by `csv.read()`, `csv.parse()`, `csv.stdin()`)

| Member       | Kind     | Signature    | Notes                                                       |
| ------------ | -------- | ------------ | ----------------------------------------------------------- |
| `headers`    | property | `→ string[]` | Empty array if `hasHeaders: false`                          |
| `rowCount`   | property | `→ int`      | Number of data rows                                         |
| `rows`       | property | `→ CsvRow[]` | All rows as raw access                                      |
| `mapAs<T>()` | method   | `→ T[]`      | Typed deserialisation — string-to-type coercion at boundary |

---

## `CsvRow`

| Member              | Kind    | Signature  | Notes                                                                |
| ------------------- | ------- | ---------- | -------------------------------------------------------------------- |
| `get(name: string)` | method  | `→ string` | By header name. Throws `LookupError` if no headers or name not found |
| `get(index: int)`   | method  | `→ string` | By zero-based index. Throws `IndexError` if out of range             |
| `[name: string]`    | indexer | `→ string` | Sugar for `get(name)`                                                |
| `[index: int]`      | indexer | `→ string` | Sugar for `get(index)`                                               |

---

## `Regex` (Regex Literal `/pattern/flags`)

| Member                                           | Kind     | Signature    | Notes                                        |
| ------------------------------------------------ | -------- | ------------ | -------------------------------------------- |
| `pattern`                                        | property | `→ string`   | The source pattern string                    |
| `flags`                                          | property | `→ string`   | Active flags, e.g. `"i"` or `"im"`           |
| `isMatch(input: string)`                         | method   | `→ bool`     | Fast existence check — no `Match` allocation |
| `match(input: string)`                           | method   | `→ Match?`   | First match or nil                           |
| `matchAll(input: string)`                        | method   | `→ Match[]`  | All non-overlapping matches                  |
| `replace(input: string, replacement: string)`    | method   | `→ string`   | Replace first match                          |
| `replaceAll(input: string, replacement: string)` | method   | `→ string`   | Replace all matches                          |
| `split(input: string)`                           | method   | `→ string[]` | Split on pattern                             |

---

## `Match` (Returned by `Regex.match()` and `Regex.matchAll()`)

| Member                | Kind     | Signature    | Notes                                                                      |
| --------------------- | -------- | ------------ | -------------------------------------------------------------------------- |
| `value`               | property | `→ string`   | The matched text                                                           |
| `index`               | property | `→ int`      | Zero-based position in input                                               |
| `length`              | property | `→ int`      | Length of match                                                            |
| `groups`              | property | `→ string[]` | Capture groups. `groups[0]` is full match; `groups[1]`+ are capture groups |
| `group(name: string)` | method   | `→ string?`  | Named capture group. Nil if not present                                    |

---

## `guid`

A first-class primitive type known to the type checker at compile time. Registered
by `GuidPlugin` in `Grob.Stdlib` at startup. Distinct from `string` — `guid == string`
is a compile error.

**Registry mechanism (D-356).** The **instance members** table below (properties and
methods on a `guid` *value*) is `Grob.Core.NamedTypes.NamedTypeRegistry`'s `guid` entry
— a single declarative table consulted by the type checker's annotation resolvers and
method/property validators, the VM's instance dispatch and `ValueDisplay`'s registered
`toString()` lookup, replacing what were six separate hand-rolled, string-matched
dispatch arms per type. The **static members** table (the constructors below) is
unaffected — those stay `NamespaceRegistry` entries (D-342), a receiver-qualified call
(`guid.newV4()`), not an instance surface; the two registries compose. `date`'s
instance-member table further down is the registry's second entry, migrated
behaviour-preserving alongside `guid`'s.

**Generation:**

```grob
id   := guid.newV4()                                          // random
id   := guid.newV7()                                          // time-ordered (RFC 9562)
id   := guid.newV5(guid.namespaces.url, rgId, "storage", env) // deterministic, variadic names
```

`guid.newV5()` takes variadic `name: string...` segments — concatenated before hashing.
Same inputs always produce the same GUID. Idempotent resource creation pattern.

**Well-known namespaces (RFC 4122):**

```grob
guid.namespaces.dns   // 6ba7b810-9dad-11d1-80b4-00c04fd430c8
guid.namespaces.url   // 6ba7b811-9dad-11d1-80b4-00c04fd430c8
guid.namespaces.oid   // 6ba7b812-9dad-11d1-80b4-00c04fd430c8
```

**Parsing and sentinel:**

| Member                           | Kind   | Signature | Notes                                                                                |
| -------------------------------- | ------ | --------- | ------------------------------------------------------------------------------------ |
| `guid.newV4()`                   | static | `→ guid`  | Random                                                                               |
| `guid.newV7()`                   | static | `→ guid`  | Time-ordered random (RFC 9562)                                                       |
| `guid.newV5(namespace, name...)` | static | `→ guid`  | Deterministic. Variadic `name: string...` segments.                                  |
| `guid.parse(value: string)`      | static | `→ guid`  | Throws `ParseError` if invalid. Compile-time validation on string literal arguments. |
| `guid.tryParse(value: string)`   | static | `→ guid?` | Returns nil if invalid                                                               |
| `guid.empty`                     | static | `→ guid`  | `00000000-0000-0000-0000-000000000000`                                               |
| `guid.namespaces.dns`            | static | `→ guid`  | RFC 4122 DNS namespace                                                               |
| `guid.namespaces.url`            | static | `→ guid`  | RFC 4122 URL namespace                                                               |
| `guid.namespaces.oid`            | static | `→ guid`  | RFC 4122 OID namespace                                                               |

**Instance members:**

| Member              | Kind     | Signature  | Notes                                                                                        |
| ------------------- | -------- | ---------- | -------------------------------------------------------------------------------------------- |
| `version`           | property | `→ int`    | 4, 5, or 7                                                                                   |
| `isEmpty`           | property | `→ bool`   | True if all zeros                                                                            |
| `toString()`        | method   | `→ string` | Canonical lowercase with hyphens: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`                     |
| `toUpperString()`   | method   | `→ string` | Uppercase variant — some Azure ARM endpoints require this                                    |
| `toCompactString()` | method   | `→ string` | 32 lowercase hex chars, no hyphens: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx` — storage names, keys |

**Operators:** `==`, `!=`

**String interpolation:** `"Resource ID: ${myGuid}"` calls `guid.toString()` implicitly.

**Map key (v1):** `map<guid, string>` is not supported in v1. Keys must be `string`.
Use `myGuid.toString()` as the key. Non-string map keys are post-MVP.

See D-149 for the full guid module decision, including the rationale for
excluding version 1 and version 3.

---

## `json.Node` (Returned by `json.read()`, `json.parse()`, `json.stdin()`, and node indexer)

| Member          | Kind     | Signature       | Notes                                                                                 |
| --------------- | -------- | --------------- | ------------------------------------------------------------------------------------- |
| `isNull`        | property | `→ bool`        | True if node is a JSON null                                                           |
| `isString`      | property | `→ bool`        |                                                                                       |
| `isInt`         | property | `→ bool`        |                                                                                       |
| `isFloat`       | property | `→ bool`        |                                                                                       |
| `isBool`        | property | `→ bool`        |                                                                                       |
| `isArray`       | property | `→ bool`        |                                                                                       |
| `isObject`      | property | `→ bool`        |                                                                                       |
| `asString()`    | method   | `→ string`      | Throws `JsonError` if node is not a string                                            |
| `asInt()`       | method   | `→ int`         | Throws `JsonError` if node is not numeric                                             |
| `asFloat()`     | method   | `→ float`       | Throws `JsonError` if node is not numeric                                             |
| `asBool()`      | method   | `→ bool`        | Throws `JsonError` if node is not boolean                                             |
| `asArray()`     | method   | `→ json.Node[]` | Throws `JsonError` if node is not an array                                            |
| `mapAs<T>()`    | method   | `→ T`           | Constrained generic. Throws `JsonError` on shape mismatch (missing field, wrong type) |
| `[key: string]` | indexer  | `→ json.Node?`  | Returns nil for missing keys — never throws                                           |
| `toString()`    | method   | `→ string`      | Raw JSON text of this node                                                            |

Check predicates (`isArray`, `isObject` etc.) before calling accessor methods when the
node shape is uncertain. `asArray()` returns `json.Node[]` — each element is itself a
`json.Node`, so `.select(i => i.asString())` works naturally. `mapAs<T>()` is the
preferred boundary mechanism for mapping a node to a known user-defined type.

---

## `ProcessResult` (Returned by all `process.*` functions)

| Member       | Kind     | Signature  | Notes                                                              |
| ------------ | -------- | ---------- | ------------------------------------------------------------------ |
| `stdout`     | property | `→ string` | Captured stdout. Empty string if the process produced no output    |
| `stderr`     | property | `→ string` | Captured stderr. Empty string if the process produced no output    |
| `exitCode`   | property | `→ int`    | Process exit code                                                  |
| `toString()` | method   | `→ string` | Returns `stdout` — most useful default for print and interpolation |

---

## `Response` (Returned by `http.get()`, `http.post()`, `http.put()`, `http.patch()`, `http.delete()`)

Defined in `Grob.Http`. Available after `import Grob.Http`.

| Member       | Kind     | Signature               | Notes                                                                                  |
| ------------ | -------- | ----------------------- | -------------------------------------------------------------------------------------- |
| `statusCode` | property | `→ int`                 | HTTP status code — 200, 404, 500 etc.                                                  |
| `isSuccess`  | property | `→ bool`                | True if `statusCode` is 200–299                                                        |
| `headers`    | property | `→ map<string, string>` | Response headers. Keys normalised to lowercase — `"content-type"` not `"Content-Type"` |
| `asText()`   | method   | `→ string`              | Response body as string                                                                |
| `asJson()`   | method   | `→ json.Node`           | Parses body as JSON. Throws `JsonError` if body is not valid JSON                      |
| `toString()` | method   | `→ string`              | Returns a status summary — never exposes the response body                             |

Header keys are normalised to lowercase on all responses. HTTP/2 mandates lowercase;
HTTP/1.1 is case-insensitive. Normalising eliminates the class of bug where
`headers["Content-Type"]` and `headers["content-type"]` produce different results.

---

## `AuthHeader` (Constructed by `auth.bearer()`, `auth.basic()`, `auth.apiKey()`)

Defined in `Grob.Http`. Available after `import Grob.Http`. Opaque type — not
constructable directly. Only `http.*` functions accept it as a parameter.

| Member       | Kind   | Signature  | Notes                                                                                           |
| ------------ | ------ | ---------- | ----------------------------------------------------------------------------------------------- |
| `toString()` | method | `→ string` | Returns `"[AuthHeader]"` — never exposes the underlying credential, including under `--verbose` |

---

## `ZipEntry` (Returned by `zip.list()`)

Defined in `Grob.Zip`. Available after `import Grob.Zip`.

| Member           | Kind     | Signature  | Notes                                |
| ---------------- | -------- | ---------- | ------------------------------------ |
| `name`           | property | `→ string` | Entry path within the archive        |
| `size`           | property | `→ int`    | Uncompressed size in bytes           |
| `compressedSize` | property | `→ int`    | Compressed size in bytes             |
| `modified`       | property | `→ date`   | Last modified timestamp of the entry |
| `toString()`     | method   | `→ string` | Returns `name`                       |

---

## `Error` (Compiler-Internal — Parser Error Recovery)

`Error` is the type assigned to AST error nodes (`ErrorExpr`, `ErrorStmt`,
`ErrorDecl`) produced by the error-recovering parser. It is part of the
compile-time type system and never appears in user code, runtime values,
plugin signatures, stdlib APIs, or the bytecode. It is documented here
for completeness of the type registry.

**Properties.** No members. The `Error` type has no methods, no
properties, no constructors, no literal form, and no factory functions.
There is no way to construct or reference an `Error` value from Grob
source.

**Assignability.** `Error` is uniquely assignable to and from every
other type. `Error + int`, `Error.foo`, `f(Error)`, `let x: string :=
Error` are all silent — the type checker emits no diagnostic against
an expression whose type is `Error`. This is the cascade-suppression
mechanism: a single parse error does not produce a downstream wave of
type-mismatch diagnostics, because the broken expression's type is
compatible with whatever context it appears in.

**Lifecycle.** Produced only by the parser when it builds an error
node during recovery (per D-300 and `grob-language-fundamentals.md`
§29). Consumed only by the type checker, which propagates it
silently. The compiler never emits bytecode for a node typed `Error` —
a script that contains any `Error`-typed node has type errors and
fails the type-check pass before compilation begins.

**Why it lives in the registry.** The `Error` type is internal to the
compiler, but it is part of the type system's surface. The type checker
must know about it; downstream visitors (LSP, formatter) must handle it
correctly; engineers reading the registry to understand "what types can
exist" need to know it exists. Listing it here prevents the surprise
discovery during implementation and makes the cascade-suppression
contract explicit.

For full semantics see `grob-language-fundamentals.md` §29 and D-300 in
the decisions log.

---

## `fn(T…): R` (Function Type — Compile-Time Only)

A function type describes a callable value: its parameter arity, positional
parameter types, and return type. Authorised by D-326; cross-references D-296
(closure categories) and the `→` internal notation used in stdlib signatures.

**Surface syntax.** `fn(T1, T2): R` — written wherever a `TypeRef` is accepted
(variable annotations, function return types, parameter types). A trailing `?` or
`[]` suffix binds to the **return type** by default; a nullable function, or an
array of functions, itself requires grouping parens — `(fn(): int)?`,
`(fn(): int)[]` (D-327). See `grob-language-fundamentals.md` §9 for the full
type-reference grammar.

**Identity.** Structural. Two function types are equal when they have the same
parameter arity, the same positional parameter types in order, and the same
return type. There are no named function types; the shape alone determines
equality.

**Assignability.** Invariant in v1. `fn(int): int` is assignable only to
`fn(int): int` — no covariance on the return type, no contravariance on
parameter types. Nullable widening still applies: a non-nullable `fn(): T` is
assignable to `(fn(): T)?`.

**Runtime.** Erased. A `fn(T…): R`-typed binding holds an ordinary
`GrobFunction` at runtime. The function type is a compile-time constraint only;
no opcode, no `.grobc` change, no `GrobValue` variant. See D-303.

**Internal notation.** The stdlib registry uses `→` to describe function shapes
(e.g. `filter(fn: T → bool)`) — this is internal generic notation for built-in
methods only. User-declared function types use the `fn(T…): R` surface form.

**Members.** None. A function type has no properties, no methods and no
constructor syntax. A function-typed value is called with `f()` syntax.

---

_This registry will grow as stdlib design progresses. Add entries here when locked._

---

_Document updated June 2026 — D-326: `fn(T…): R` function type section added._
_Function types are compile-time only (erased at runtime), structurally identified_
_and invariantly assignable. Cross-references D-296 and D-303._
_Document updated May 2026 — D-284 throw-site reconciliation: eight_
_registry throw sites updated from the residual `RuntimeError` to the_
_typed leaf in the ten-leaf exception hierarchy (D-284,_
_`grob-language-fundamentals.md` §27) assigns them. `string.substring`,_
_`string.left`, `string.right`, `T[].insert`, `T[].remove` and_
_`CsvRow.get(index)` now throw `IndexError` (bounds violations);_
_`guid.parse` now throws `ParseError` (matching §27 and the corrected_
_`grob-stdlib-reference.md`); `CsvRow.get(name)` now throws `LookupError`_
_(a header-name miss is a name-lookup failure, not a bounds violation —_
_the natural domain a script author catches). This document is named the_
_authoritative throw-site reference by `grob-v1-requirements.md` §6; the_
_split had previously landed in fundamentals, requirements and the stdlib_
_reference but not here. No decision changed — this aligns the registry_
_with D-284 as already recorded._
_Document updated May 2026 — Session 4: `Error` (compiler-internal) type_
_entry added (S-3.2). Compiler-internal type produced by error-recovering_
_parser per D-300 and `grob-language-fundamentals.md` §29; assignable to_
_and from every other type to suppress diagnostic cascades; never visible_
_to user code or the runtime. Listed for registry completeness._
_Previous: April 2026 — pre-implementation review: `string.toString()` added_
_(identity method for type uniformity — every type now has `toString()`)._
_April 2026 (Session B Interlude) — array and map mutation rules updated to cover_
_`const`- and `readonly`-bound containers equivalently following the_
_`const`/`readonly` keyword split (D-288, D-291)._
_Previous: `json.Node`, `ProcessResult`, `Response`, `AuthHeader`, `ZipEntry` types added._
_Previous: `guid` type added as first-class built-in;_
_`File.copyTo` and `File.moveTo` updated with `overwrite: bool = false` parameter;_
_array mutation methods added (`append`, `insert`, `remove`, `clear`);_
_`map<K, V>` type added as first-class built-in._
_Document created April 2026 — extracted from grob-decisions-log.md._
_Authorised decisions recorded in grob-decisions-log.md._
_This document is the implementation reference — the decisions log is the authority._
