# Grob — Built-in Type Method Registry

> Compiler-known methods and properties per built-in type.
> Decisions authorised in the decisions log — April 2026 design sessions.
> This document is the implementation reference for the type checker and
> compiler — it defines the complete set of members registered on each type.
> Calling anything not in this registry is a compile error.
> When this document and the decisions log conflict, the decisions log wins.

-----

All method calls on primitives are rewritten to native function calls at compile
time — no boxing, no vtable, no heap allocation.

-----

## `string`

|Member                                            |Kind    |Signature   |Notes                                                    |
|--------------------------------------------------|--------|------------|---------------------------------------------------------|
|`length`                                          |property|`→ int`     |                                                         |
|`isEmpty`                                         |property|`→ bool`    |                                                         |
|`toInt()`                                         |method  |`→ int?`    |Returns nil if not parseable                             |
|`toFloat()`                                       |method  |`→ float?`  |Returns nil if not parseable                             |
|`trim()`                                          |method  |`→ string`  |Removes leading and trailing whitespace                  |
|`trimStart()`                                     |method  |`→ string`  |Removes leading whitespace only                          |
|`trimEnd()`                                       |method  |`→ string`  |Removes trailing whitespace only                         |
|`upper()`                                         |method  |`→ string`  |                                                         |
|`lower()`                                         |method  |`→ string`  |                                                         |
|`split(sep: string)`                              |method  |`→ string[]`|                                                         |
|`contains(s: string)`                             |method  |`→ bool`    |                                                         |
|`startsWith(s: string)`                           |method  |`→ bool`    |                                                         |
|`endsWith(s: string)`                             |method  |`→ bool`    |                                                         |
|`replace(from: string, to: string)`               |method  |`→ string`  |Replaces all occurrences                                 |
|`indexOf(s: string)`                              |method  |`→ int`     |First occurrence; -1 if not found                        |
|`lastIndexOf(s: string)`                          |method  |`→ int`     |Last occurrence; -1 if not found                         |
|`substring(start: int, length: int)`              |method  |`→ string`  |Zero-based start; throws `RuntimeError` if out of range  |
|`padLeft(width: int, char: string = " ")`         |method  |`→ string`  |Pads to total width on the left                          |
|`padRight(width: int, char: string = " ")`        |method  |`→ string`  |Pads to total width on the right                         |
|`repeat(count: int)`                              |method  |`→ string`  |Repeats the string n times                               |
|`truncate(maxLength: int, suffix: string = "...")`|method  |`→ string`  |Truncates to max length; appends suffix if truncated     |
|`left(n: int)`                                    |method  |`→ string`  |First `n` characters. Throws `RuntimeError` if n > length|
|`right(n: int)`                                   |method  |`→ string`  |Last `n` characters. Throws `RuntimeError` if n > length |
|`toString()`                                      |method  |`→ string`  |Returns the string unchanged — identity for type uniformity|

-----

## `int`

|Member                |Kind  |Signature              |Notes          |
|----------------------|------|-----------------------|---------------|
|`toString()`          |method|`→ string`             |               |
|`toFloat()`           |method|`→ float`              |Always succeeds|
|`abs()`               |method|`→ int`                |               |
|`int.min(a, b)`       |static|`(int, int) → int`     |               |
|`int.max(a, b)`       |static|`(int, int) → int`     |               |
|`int.clamp(v, lo, hi)`|static|`(int, int, int) → int`|               |

-----

## `float`

|Member                  |Kind  |Signature                      |Notes                     |
|------------------------|------|-------------------------------|--------------------------|
|`toString()`            |method|`→ string`                     |                          |
|`toInt()`               |method|`→ int`                        |Truncates — does not round|
|`round()`               |method|`→ int`                        |Nearest integer           |
|`round(decimals: int)`  |method|`→ float`                      |Round to N decimal places |
|`floor()`               |method|`→ int`                        |                          |
|`ceil()`                |method|`→ int`                        |                          |
|`abs()`                 |method|`→ float`                      |                          |
|`float.min(a, b)`       |static|`(float, float) → float`       |                          |
|`float.max(a, b)`       |static|`(float, float) → float`       |                          |
|`float.clamp(v, lo, hi)`|static|`(float, float, float) → float`|                          |

-----

## `bool`

|Member      |Kind  |Signature |Notes                        |
|------------|------|----------|-----------------------------|
|`toString()`|method|`→ string`|Returns `"true"` or `"false"`|

-----

## `T[]` (All Arrays)

|Member                                     |Kind    |Signature|Notes                                                                             |
|-------------------------------------------|--------|---------|----------------------------------------------------------------------------------|
|`length`                                   |property|`→ int`  |                                                                                  |
|`isEmpty`                                  |property|`→ bool` |                                                                                  |
|`first()`                                  |method  |`→ T?`   |nil if empty                                                                      |
|`last()`                                   |method  |`→ T?`   |nil if empty                                                                      |
|`contains(v: T)`                           |method  |`→ bool` |                                                                                  |
|`filter(fn: T → bool)`                     |method  |`→ T[]`  |Returns new array                                                                 |
|`map(fn: T → U)`                           |method  |`→ U[]`  |Returns new array                                                                 |
|`each(fn: T → void)`                       |method  |`→ void` |                                                                                  |
|`sort(fn: T → U, descending: bool = false)`|method  |`→ T[]`  |Returns new sorted array                                                          |
|`select(fn: T → U)`                        |method  |`→ U[]`  |Projection — alias for map, reads better in data pipelines                        |
|`append(value: T)`                         |method  |`→ void` |Appends one element. Mutates in place. Binding must not be `const`.               |
|`insert(index: int, value: T)`             |method  |`→ void` |Inserts before index. Throws `RuntimeError` if out of range. Mutates in place.    |
|`remove(index: int)`                       |method  |`→ void` |Removes element at index. Throws `RuntimeError` if out of range. Mutates in place.|
|`clear()`                                  |method  |`→ void` |Removes all elements. Mutates in place. Binding must not be `const`.              |

**Mutation rules:**

- `append`, `insert`, `remove`, `clear` mutate the array in place.
- Calling any mutation method on a `const`-bound array is a compile error.
- `filter`, `map`, `sort`, `select` always return a new array — they never mutate.

-----

## `map<K, V>`

A first-class built-in type. The type checker knows `map<string, string>` and
`map<string, int>` as distinct types. Users consume and construct maps; they
cannot declare generic map types of their own (same constrained-generics model
as arrays). In v1, keys must be `string` — non-string keys are deferred post-MVP.

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

|Member                 |Kind    |Signature|Notes                                       |
|-----------------------|--------|---------|--------------------------------------------|
|`length`               |property|`→ int`  |Number of entries                           |
|`isEmpty`              |property|`→ bool` |                                            |
|`keys`                 |property|`→ K[]`  |All keys as array. Order is insertion order.|
|`values`               |property|`→ V[]`  |All values as array. Order matches `keys`.  |
|`get(key: K)`          |method  |`→ V?`   |Returns nil if key absent                   |
|`set(key: K, value: V)`|method  |`→ void` |Insert or overwrite. Mutates in place.      |
|`contains(key: K)`     |method  |`→ bool` |True if key present                         |
|`remove(key: K)`       |method  |`→ void` |No-op if key absent. Mutates in place.      |
|`clear()`              |method  |`→ void` |Removes all entries. Mutates in place.      |
|`[key: K]`             |indexer |`→ V?`   |Sugar for `get(key)`                        |
|`[key: K] = value`     |indexer |`→ void` |Sugar for `set(key, value)`                 |

**Mutation rules:**

- `set`, `remove`, `clear`, and indexer assignment mutate the map in place.
- Calling any mutation method on a `const`-bound map is a compile error.
- `get` and the read indexer never mutate.

-----

## `File` (Returned by `fs.list()`)

|Member                   |Kind    |Signature |Notes                                    |
|-------------------------|--------|----------|-----------------------------------------|
|`name`                   |property|`→ string`|Filename with extension, no path         |
|`path`                   |property|`→ string`|Full absolute path                       |
|`directory`              |property|`→ string`|Parent directory path                    |
|`extension`              |property|`→ string`|Lowercased, includes dot — e.g. `.xlsx`  |
|`size`                   |property|`→ int`   |Size in bytes                            |
|`modified`               |property|`→ date`  |Last write time                          |
|`created`                |property|`→ date`  |Creation time                            |
|`isDirectory`            |property|`→ bool`  |True if entry is a directory             |
|`rename(newName: string)`|method  |`→ void`  |Rename in place — new name only, not path|
|`moveTo(destDir: string, overwrite: bool = false)`|method  |`→ void`  |Move to destination directory. Throws `IoError` if destination exists and `overwrite` is false|
|`copyTo(destDir: string, overwrite: bool = false)`|method  |`→ void`  |Copy to destination directory. Throws `IoError` if destination exists and `overwrite` is false|
|`delete()`               |method  |`→ void`  |Delete the file                          |

-----

## `date` (Instance Methods — Additions)

The following are additions to the `date` type registry confirmed in this session.
The full construction, parsing, formatting, arithmetic, comparison, component, epoch,
and timezone members are specified in the confirmed decisions table (Apr 2026,
`date module — API`).

|Member                  |Kind  |Signature|Notes                                                           |
|------------------------|------|---------|----------------------------------------------------------------|
|`daysUntil(other: date)`|method|`→ int`  |Positive if `other` is later than receiver; negative if reversed|
|`daysSince(other: date)`|method|`→ int`  |Positive if receiver is later than `other`; negative if reversed|

-----

## `strings` Module

|Member                                                 |Signature |Notes                                                                                       |
|-------------------------------------------------------|----------|--------------------------------------------------------------------------------------------|
|`strings.join(parts: string[], separator: string = "")`|`→ string`|Joins array with separator. Receiver is an array — cannot be an instance method on `string`.|

-----

## `csv.Table` (Returned by `csv.read()`, `csv.parse()`, `csv.stdin()`)

|Member      |Kind    |Signature   |Notes                                                      |
|------------|--------|------------|-----------------------------------------------------------|
|`headers`   |property|`→ string[]`|Empty array if `hasHeaders: false`                         |
|`rowCount`  |property|`→ int`     |Number of data rows                                        |
|`rows`      |property|`→ CsvRow[]`|All rows as raw access                                     |
|`mapAs<T>()`|method  |`→ T[]`     |Typed deserialisation — string-to-type coercion at boundary|

-----

## `CsvRow`

|Member             |Kind   |Signature |Notes                                                                |
|-------------------|-------|----------|---------------------------------------------------------------------|
|`get(name: string)`|method |`→ string`|By header name. Throws `RuntimeError` if no headers or name not found|
|`get(index: int)`  |method |`→ string`|By zero-based index. Throws `RuntimeError` if out of range           |
|`[name: string]`   |indexer|`→ string`|Sugar for `get(name)`                                                |
|`[index: int]`     |indexer|`→ string`|Sugar for `get(index)`                                               |

-----

## `Regex` (Regex Literal `/pattern/flags`)

|Member                                          |Kind    |Signature   |Notes                                       |
|------------------------------------------------|--------|------------|--------------------------------------------|
|`pattern`                                       |property|`→ string`  |The source pattern string                   |
|`flags`                                         |property|`→ string`  |Active flags, e.g. `"i"` or `"im"`          |
|`isMatch(input: string)`                        |method  |`→ bool`    |Fast existence check — no `Match` allocation|
|`match(input: string)`                          |method  |`→ Match?`  |First match or nil                          |
|`matchAll(input: string)`                       |method  |`→ Match[]` |All non-overlapping matches                 |
|`replace(input: string, replacement: string)`   |method  |`→ string`  |Replace first match                         |
|`replaceAll(input: string, replacement: string)`|method  |`→ string`  |Replace all matches                         |
|`split(input: string)`                          |method  |`→ string[]`|Split on pattern                            |

-----

## `Match` (Returned by `Regex.match()` and `Regex.matchAll()`)

|Member               |Kind    |Signature   |Notes                                                                     |
|---------------------|--------|------------|--------------------------------------------------------------------------|
|`value`              |property|`→ string`  |The matched text                                                          |
|`index`              |property|`→ int`     |Zero-based position in input                                              |
|`length`             |property|`→ int`     |Length of match                                                           |
|`groups`             |property|`→ string[]`|Capture groups. `groups[0]` is full match; `groups[1]`+ are capture groups|
|`group(name: string)`|method  |`→ string?` |Named capture group. Nil if not present                                   |

-----

## `guid`

A first-class primitive type known to the type checker at compile time. Registered
by `GuidPlugin` in `Grob.Stdlib` at startup. Distinct from `string` — `guid == string`
is a compile error.

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

|Member                              |Kind  |Signature |Notes                                                                                               |
|------------------------------------|------|----------|----------------------------------------------------------------------------------------------------|
|`guid.newV4()`                      |static|`→ guid`  |Random                                                                                              |
|`guid.newV7()`                      |static|`→ guid`  |Time-ordered random (RFC 9562)                                                                      |
|`guid.newV5(namespace, name...)`    |static|`→ guid`  |Deterministic. Variadic `name: string...` segments.                                                 |
|`guid.parse(value: string)`         |static|`→ guid`  |Throws `RuntimeError` if invalid. Compile-time validation on string literal arguments.              |
|`guid.tryParse(value: string)`      |static|`→ guid?` |Returns nil if invalid                                                                              |
|`guid.empty`                        |static|`→ guid`  |`00000000-0000-0000-0000-000000000000`                                                              |
|`guid.namespaces.dns`               |static|`→ guid`  |RFC 4122 DNS namespace                                                                              |
|`guid.namespaces.url`               |static|`→ guid`  |RFC 4122 URL namespace                                                                              |
|`guid.namespaces.oid`               |static|`→ guid`  |RFC 4122 OID namespace                                                                              |

**Instance members:**

|Member            |Kind    |Signature |Notes                                                                           |
|------------------|--------|----------|--------------------------------------------------------------------------------|
|`version`         |property|`→ int`   |4, 5, or 7                                                                      |
|`isEmpty`         |property|`→ bool`  |True if all zeros                                                               |
|`toString()`      |method  |`→ string`|Canonical lowercase with hyphens: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`        |
|`toUpperString()` |method  |`→ string`|Uppercase variant — some Azure ARM endpoints require this                       |
|`toCompactString()`|method |`→ string`|32 lowercase hex chars, no hyphens: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx` — storage names, keys|

**Operators:** `==`, `!=`

**String interpolation:** `"Resource ID: ${myGuid}"` calls `guid.toString()` implicitly.

**Map key (v1):** `map<guid, string>` is not supported in v1. Keys must be `string`.
Use `myGuid.toString()` as the key. Non-string map keys are post-MVP.

**Excluded versions:** Version 1 (time-based with MAC address — leaks machine identity)
and Version 3 (name-based MD5 — superseded by v5) are not exposed.

-----

## `json.Node` (Returned by `json.read()`, `json.parse()`, `json.stdin()`, and node indexer)

|Member               |Kind    |Signature       |Notes                                                                           |
|---------------------|--------|----------------|--------------------------------------------------------------------------------|
|`isNull`             |property|`→ bool`        |True if node is a JSON null                                                     |
|`isString`           |property|`→ bool`        |                                                                                |
|`isInt`              |property|`→ bool`        |                                                                                |
|`isFloat`            |property|`→ bool`        |                                                                                |
|`isBool`             |property|`→ bool`        |                                                                                |
|`isArray`            |property|`→ bool`        |                                                                                |
|`isObject`           |property|`→ bool`        |                                                                                |
|`asString()`         |method  |`→ string`      |Throws `JsonError` if node is not a string                                      |
|`asInt()`            |method  |`→ int`         |Throws `JsonError` if node is not numeric                                       |
|`asFloat()`          |method  |`→ float`       |Throws `JsonError` if node is not numeric                                       |
|`asBool()`           |method  |`→ bool`        |Throws `JsonError` if node is not boolean                                       |
|`asArray()`          |method  |`→ json.Node[]` |Throws `JsonError` if node is not an array                                      |
|`mapAs<T>()`         |method  |`→ T`           |Constrained generic. Throws `JsonError` on shape mismatch (missing field, wrong type)|
|`[key: string]`      |indexer |`→ json.Node?`  |Returns nil for missing keys — never throws                                     |
|`toString()`         |method  |`→ string`      |Raw JSON text of this node                                                      |

Check predicates (`isArray`, `isObject` etc.) before calling accessor methods when the
node shape is uncertain. `asArray()` returns `json.Node[]` — each element is itself a
`json.Node`, so `.map(i => i.asString())` works naturally. `mapAs<T>()` is the
preferred boundary mechanism for mapping a node to a known user-defined type.

-----

## `ProcessResult` (Returned by all `process.*` functions)

|Member      |Kind    |Signature |Notes                                                           |
|------------|--------|----------|----------------------------------------------------------------|
|`stdout`    |property|`→ string`|Captured stdout. Empty string if the process produced no output |
|`stderr`    |property|`→ string`|Captured stderr. Empty string if the process produced no output |
|`exitCode`  |property|`→ int`   |Process exit code                                               |
|`toString()`|method  |`→ string`|Returns `stdout` — most useful default for print and interpolation|

-----

## `Response` (Returned by `http.get()`, `http.post()`, `http.put()`, `http.patch()`, `http.delete()`)

Defined in `Grob.Http`. Available after `import Grob.Http`.

|Member        |Kind    |Signature              |Notes                                                                         |
|--------------|--------|-----------------------|------------------------------------------------------------------------------|
|`statusCode`  |property|`→ int`                |HTTP status code — 200, 404, 500 etc.                                         |
|`isSuccess`   |property|`→ bool`               |True if `statusCode` is 200–299                                               |
|`headers`     |property|`→ map<string, string>`|Response headers. Keys normalised to lowercase — `"content-type"` not `"Content-Type"`|
|`asText()`    |method  |`→ string`             |Response body as string                                                       |
|`asJson()`    |method  |`→ json.Node`          |Parses body as JSON. Throws `JsonError` if body is not valid JSON             |
|`toString()`  |method  |`→ string`             |Returns a status summary — never exposes the response body                    |

Header keys are normalised to lowercase on all responses. HTTP/2 mandates lowercase;
HTTP/1.1 is case-insensitive. Normalising eliminates the class of bug where
`headers["Content-Type"]` and `headers["content-type"]` produce different results.

-----

## `AuthHeader` (Constructed by `auth.bearer()`, `auth.basic()`, `auth.apiKey()`)

Defined in `Grob.Http`. Available after `import Grob.Http`. Opaque type — not
constructable directly. Only `http.*` functions accept it as a parameter.

|Member      |Kind  |Signature |Notes                                                                                  |
|------------|------|----------|---------------------------------------------------------------------------------------|
|`toString()`|method|`→ string`|Returns `"[AuthHeader]"` — never exposes the underlying credential, including under `--verbose`|

-----

## `ZipEntry` (Returned by `zip.list()`)

Defined in `Grob.Zip`. Available after `import Grob.Zip`.

|Member          |Kind    |Signature |Notes                                 |
|----------------|--------|----------|--------------------------------------|
|`name`          |property|`→ string`|Entry path within the archive         |
|`size`          |property|`→ int`   |Uncompressed size in bytes            |
|`compressedSize`|property|`→ int`   |Compressed size in bytes              |
|`modified`      |property|`→ date`  |Last modified timestamp of the entry  |
|`toString()`    |method  |`→ string`|Returns `name`                        |

-----

*This registry will grow as stdlib design progresses. Add entries here when locked.*

-----

*Document updated April 2026 — pre-implementation review: `string.toString()` added*
*(identity method for type uniformity — every type now has `toString()`).*
*Previous: `json.Node`, `ProcessResult`, `Response`, `AuthHeader`, `ZipEntry` types added.*
*Previous: `guid` type added as first-class built-in;*
*`File.copyTo` and `File.moveTo` updated with `overwrite: bool = false` parameter;*
*array mutation methods added (`append`, `insert`, `remove`, `clear`);*
*`map<K, V>` type added as first-class built-in.*
*Document created April 2026 — extracted from Grob___Decisions___Context_Log.md.*
*Authorised decisions recorded in Grob___Decisions___Context_Log.md.*
*This document is the implementation reference — the decisions log is the authority.*