# Grob — Standard Library Reference

> Reference document for the standard library module shapes, built-in functions,
> conditional expressions, and lambda/closure semantics.
> Decisions authorised in the decisions log — April 2026 design sessions.
> This document is the implementation reference for stdlib module APIs and
> language-level expression forms confirmed during stdlib design.
> When this document and the decisions log conflict, the decisions log wins.

-----

## Core Modules — Auto-Available

These modules are always in scope. No `import` statement needed.
If a reasonable developer expects it in any scripting language, it's core.

|Module   |Purpose                                                                                                     |Status   |
|---------|------------------------------------------------------------------------------------------------------------|---------|
|`fs`     |File system — list, read, write, rename, copy, move, delete, traverse                                       |Specified|
|`strings`|String operations — `join()` module function; all other ops are instance methods on the `string` type       |Specified|
|`process`|External command execution — `run()`, `runOrFail()`, `runShell()`, `runShellOrFail()`, capture stdout/stderr|Specified|
|`env`    |Environment variables — `get(key): string?`, `require(key): string`                                         |Specified|
|`json`   |JSON parse/serialise — `read()`, `write()`, `parse()`, `stdin()`, `stdout()`, `Node` type                   |Specified|
|`date`   |Date/time — single type, full arithmetic, timezone/UTC, Unix epoch, parse/format                            |Specified|
|`csv`    |CSV parse/serialise — `read()`, `write()`, `parse()`, `stdin()`, `stdout()`, `Table` and `CsvRow` types     |Specified|
|`log`    |Structured logging — four levels, all to stderr, `setLevel()`, `--verbose` for debug                        |Specified|
|`regex`  |Regular expressions — `/pattern/flags` literal type, `Match` type, module convenience functions             |Specified|
|`path`   |Path manipulation — `join()`, `joinAll()`, decompose, normalise, OS separator constant                      |Specified|
|`math`   |Maths — sqrt, pow, log, trig, random, constants `pi`/`e`/`tau`                                              |Specified|
|`format` |Output formatting — `table()`, `list()`, `csv()`, numeric and date patterns                                 |Specified|
|`guid`   |GUID generation — v4 random, v7 time-ordered, v5 deterministic; parsing, well-known namespaces               |Specified|

-----

## First-Party Plugins — Explicit Import Required

```grob
import Grob.Http   // http.* and auth.* both available — auth is a sub-namespace, not a separate plugin
```

Install once:

```bash
grob install Grob.Http
```

|Module       |Purpose                                                                                                               |
|-------------|----------------------------------------------------------------------------------------------------------------------|
|`Grob.Http`  |HTTP client — `get()`, `post()`, `put()`, `patch()`, `delete()`, `download()`, `auth.*` sub-namespace, `Response` type|
|`Grob.Crypto`|Checksums and hashing — MD5, SHA256, file integrity                                                                   |
|`Grob.Zip`   |Archive — compress and expand zip archives                                                                            |
|`Grob.Git`   |Git operations — local repo (LibGit2Sharp), AzureDevOps client, GitHub client. Shared types across hosts. **Post-MVP.**|

-----

### `Grob.Http`

```grob
import Grob.Http
// http.* and auth.* both available — auth is a sub-namespace, not a separate install

// GET
response := http.get(url)
response := http.get(url, auth.bearer(token))
response := http.get(url, auth.bearer(token), headers: map<string,string>{ "X-Api-Version": "2024-01-01" })
response := http.get(url, auth.bearer(token), timeoutSeconds: 60)

// POST / PUT / PATCH — body is string; serialise structs with json.encode() first
body     := json.encode(myStruct)
response := http.post(url, body)
response := http.post(url, body, auth.bearer(token))
response := http.put(url, body, auth.bearer(token))
response := http.patch(url, body, auth.bearer(token))

// DELETE — no body
response := http.delete(url, auth.bearer(token))

// File download — streaming to disk, throws NetworkError on non-2xx
http.download(url, "C:\\downloads\\file.zip")
http.download(url, "C:\\downloads\\file.zip", auth.bearer(token))
http.download(url, "C:\\downloads\\file.zip", auth.bearer(token), timeoutSeconds: 120)

// Response
response.statusCode    // int — 200, 404, 500 etc.
response.isSuccess     // bool — true if 200–299
response.headers       // map<string, string> — keys normalised to lowercase
response.asText()      // → string
response.asJson()      // → json.Node  (throws JsonError if not valid JSON)

// Auth helpers
auth.bearer(token: string): AuthHeader
auth.basic(username: string, password: string): AuthHeader
auth.apiKey(key: string, headerName: string = "X-Api-Key"): AuthHeader
```

Full signatures:

```
http.get(url: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response
http.post(url: string, body: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response
http.put(url: string, body: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response
http.patch(url: string, body: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response
http.delete(url: string, auth: AuthHeader? = nil, headers: map<string,string>? = nil, timeoutSeconds: int = 30): Response
http.download(url: string, dest: string, auth: AuthHeader? = nil, timeoutSeconds: int = 30): void
```

`body` is `string`. To send a struct as JSON: `json.encode(myStruct)` first. This keeps
`Grob.Http` independent of the `json` module — it handles bytes over the wire, not
type serialisation.

`headers` is `map<string, string>`. Constructable dynamically — unlike anonymous structs,
maps can be built at runtime.

Response header keys are normalised to lowercase. HTTP/2 mandates lowercase; HTTP/1.1
is case-insensitive. Normalising eliminates the class of bug where `headers["Content-Type"]`
and `headers["content-type"]` return different results.

`http.download()` throws `NetworkError` on non-2xx. A download that gets a 404 has not
downloaded anything useful — treating it as a successful-but-inspect response would require
the caller to check `isSuccess` and then delete a garbage file. All other functions return
`Response` and leave `isSuccess` inspection to the caller — 404 is a legitimate expected
result in many API workflows.

`AuthHeader` is an opaque type. The type checker knows it as `Grob.Http.AuthHeader`
and only `http.*` functions accept it. Not constructable directly. `toString()` returns
`"[AuthHeader]"` — the credential is never exposed, including under `--verbose`.

-----

### `Grob.Crypto` — Shape

```grob
import Grob.Crypto

// File hashing — streams internally, never loads full file into memory
hash := crypto.sha256File("C:\\releases\\tool.zip")   // lowercase hex string
hash := crypto.md5File("C:\\releases\\tool.zip")       // lowercase hex string

// String hashing (UTF-8 encoded)
hash := crypto.sha256("some-value")   // lowercase hex string
hash := crypto.md5("some-value")      // lowercase hex string

// Verification — constant-time comparison, names the intent
ok := crypto.verifySha256("C:\\releases\\tool.zip", expected_hash)   // bool
ok := crypto.verifyMd5("C:\\releases\\tool.zip", expected_hash)      // bool
```

All hex output is **lowercase**. File functions stream internally — never load the
full file into memory. Verify functions compare in constant time — useful for
published hash verification (downloaded files, release artifacts).

SHA-1, SHA-512, HMAC, byte array output — all post-MVP.

-----

### `Grob.Zip`

```grob
import Grob.Zip

// Create archive from a directory path
zip.create("archive.zip", "C:\\Reports")
zip.create("archive.zip", "C:\\Reports", overwrite: true)

// Create archive from a File object (file or directory)
folder := fs.list("C:\\Projects").filter(f => f.isDirectory).first()
zip.create("archive.zip", folder)

// Create archive from an explicit list of file paths
zip.create("bundle.zip", ["config.json", "data.csv", "readme.txt"])

// Create from a File[] — convert to string[] first
files := fs.list("C:\\Reports").filter(f => f.extension == ".csv")
zip.create("csvs.zip", files.map(f => f.path))

// Extract to a directory
zip.extract("archive.zip", "C:\\Output")
zip.extract("archive.zip", "C:\\Output", overwrite: true)

// List entries without extracting
entries := zip.list("archive.zip")
for entry in entries {
    print("${entry.name}  ${entry.size} bytes")
}
```

Full signatures:

```
zip.create(dest: string, source: string, overwrite: bool = false): void
zip.create(dest: string, source: File, overwrite: bool = false): void
zip.create(dest: string, source: string[], overwrite: bool = false): void
zip.extract(archive: string, dest: string, overwrite: bool = false): void
zip.list(archive: string): ZipEntry[]
```

`overwrite: false` (default) throws `IoError` if the destination archive or any
extracted file already exists. `overwrite: true` replaces silently — consistent with
`fs.copy()` and `fs.move()`.

When source is a `File` object, `file.path` is extracted internally. If `file.isDirectory`
is true the directory and its contents are archived; if false the single file is archived.
There is no `File[]` overload — convert with `.map(f => f.path)` to get `string[]`.

`zip.list()` reads the zip central directory only — it does not extract or load entry
data into memory. Safe on large archives.

Password-protected zips are post-MVP. All failures throw `IoError`.

-----

### Community Plugins — Explicit Import Required

Any C# class library implementing `IGrobPlugin`. Published to NuGet tagged `grob-plugin`.
Loaded via `import` after `grob install`. The `import` lines are the dependency manifest.

```grob
import AcmeCorp.Xml           // alias: xml.*
import AcmeCorp.Xml as parser // explicit alias — collision resolution only
```

-----

### `env` Module

```grob
// Hard fail with clear error message if missing or empty
PAT := env.require("ADO_PAT")

// Nil-safe with default
days := env.get("STALE_DAYS")?.toInt() ?? 30

// Check without retrieving
if (env.has("CI")) {
    log.info("Running in CI mode")
}

// Set for current process only — does not persist
env.set("MY_VAR", "value")

// Inspect the full environment
all := env.all()
for k, v in all {
    print("${k}=${v}")
}
```

Full signatures:

```
env.get(key: string): string?
env.require(key: string): string
env.set(key: string, value: string): void
env.has(key: string): bool
env.all(): map<string, string>
```

`env.get()` returns `string?` — nil if not set. `env.require()` returns `string` —
never nil; throws `RuntimeError` if the variable is absent or empty. Error message
names the missing variable. `env.has()` returns false for both absent and empty
variables — an empty env var is functionally absent for scripting purposes.
`env.set()` is process-scoped only: modifies the current process environment block,
does not persist across script invocations, does not write to the registry or user
profile. `env.all()` returns a `map<string, string>` of all current process
environment variables.

-----

### `process` Module — Shape (Sketch)

```grob
// Safe form — use when any argument comes from data (the default choice)
// Arguments are never shell-interpolated — prevents command injection
result := process.run("az", ["group", "show", "--name", groupName])
print(result.stdout)
print(result.exitCode)

// Shell form — use when command is a known trusted literal
// Name makes shell involvement explicit
result := process.runShell("az group list")

// Fail fast if command fails
process.runOrFail("git", ["commit", "-m", message])
process.runShellOrFail("az bicep build --file main.bicep")

// Timeout — 0 (default) means infinite. Throws ProcessError on expiry.
result := process.run("az", ["deployment", "wait"], timeout: 300)
result := process.runShell("long-running-command", timeout: 60)
```

Full signatures:

```
process.run(cmd: string, args: string[], timeout: int = 0): ProcessResult
process.runShell(cmd: string, timeout: int = 0): ProcessResult
process.runOrFail(cmd: string, args: string[], timeout: int = 0): ProcessResult
process.runShellOrFail(cmd: string, timeout: int = 0): ProcessResult
```

Returns a `ProcessResult` type with `stdout: string`, `stderr: string`, `exitCode: int`.

`timeout: int = 0` — seconds. `0` means infinite (runs until the process completes
or the OS kills it). On timeout expiry, throws
`ProcessError("Process timed out after {n} seconds: {cmd}")`.

`process.run()` is the primary form — the safe path has the shorter name. Use it
whenever any argument comes from data (user input, API responses, file content,
environment variables). `process.runShell()` is available for full command strings
where shell interpretation is intentional. The distinction is documented prominently.

-----

### `json` Module

```grob
// File input — primary pattern
repos := json.read("repos.json").mapAs<Repo>()

// File output
json.write("results.json", results)

// Parse a string (e.g. process output, API response body)
node := json.parse(someString)

// Serialise a value to JSON string — inverse of parse()
body := json.encode(myStruct)
response := http.post(url, body, auth.bearer(token))

// Pipeline entry/exit — stdin/stdout for genuine pipeline composition
repos := json.stdin().mapAs<Repo>()
json.stdout(results)

// Navigate a node
name  := node["name"].asString()
items := node["items"].asArray()
count := node["missing"]?.asInt() ?? 0   // nil-safe — missing key returns nil

// Type predicates before accessing
if (node["value"].isArray) {
    items := node["value"].asArray()
}
```

Full module signatures:

```
json.read(path: string): json.Node
json.write(path: string, value: T, compact: bool = false): void
json.parse(content: string): json.Node
json.encode(value: T, compact: bool = false): string
json.stdin(): json.Node
json.stdout(value: T, compact: bool = false): void
```

`json.write()` and `json.encode()` default to pretty-printed output (indented,
human-readable). Pass `compact: true` for single-line output with no whitespace —
suited for pipeline output and machine-to-machine communication. `json.stdout()`
follows the same convention.

`json.read()` and `json.write()` are the primary forms — file-based, no shell piping
required. `json.stdin()` and `json.stdout()` exist for genuine pipeline composition
(agent hooks, CI stages). `json.encode()` is the inverse of `json.parse()` — use it
to serialise typed values and anonymous structs to JSON strings before passing to
`http.post()` or writing to a template. Examples lead with file-read; stdin shown as
the pipeline variant.

`json.Node` indexer `node["key"]` returns `json.Node?` — nil for missing keys, never
throws. Use `?.` chaining and `??` defaults for nil-safe traversal. Accessor methods
(`asString()`, `asInt()`, `asFloat()`, `asBool()`, `asArray()`) throw `JsonError` if
the node is the wrong type — check `isString`, `isArray` etc. first when the shape is
uncertain. `mapAs<T>()` throws `JsonError` on shape mismatch.

-----

### `date` Module — Shape

```grob
// Current moment
now   := date.now()           // date and time
today := date.today()         // date only, time zeroed

// Construction
d := date.of(2026, 4, 5)
t := date.ofTime(2026, 4, 5, 14, 30, 0)

// Parsing
d := date.parse("2026-04-05")               // ISO 8601 default
d := date.parse("05/04/2026", "dd/MM/yyyy") // explicit pattern

// Formatting
d.toIso()                     // "2026-04-05"
d.toIsoDateTime()             // "2026-04-05T14:30:00Z"
d.format("dd MMM yyyy")       // "05 Apr 2026"
d.format("dd/MM/yyyy HH:mm")  // "05/04/2026 14:30"

// Arithmetic
d.addDays(7)
d.minusDays(30)
d.addMonths(3)
d.addHours(2)
d.addMinutes(30)

// Comparison — operators and methods
d1 < d2
d1 > d2
d1 == d2
d.isBefore(other)
d.isAfter(other)

// Components — all properties, no ()
d.year
d.month
d.day
d.hour
d.minute
d.second
d.dayOfWeek    // "Monday" etc
d.dayOfYear

// Unix epoch
d.toUnixSeconds()
d.toUnixMillis()
date.fromUnixSeconds(epoch)
date.fromUnixMillis(epoch)

// Timezone and UTC
d.toUtc()
d.toLocal()
d.toZone("Europe/London")
d.utcOffset    // minutes as int

// Interval computation
created.daysUntil(date.today())   // positive if today is later
date.today().daysSince(created)   // same result, different direction
```

**Timezone conventions:**

- `date.now()` and `date.today()` return local time.
- `date.of()` and `date.ofTime()` construct local time values.
- `date.parse()` with an ISO 8601 string preserves the timezone from the string.
  A string with no timezone offset is interpreted as local time.
- Use `toUtc()`, `toLocal()`, `toZone()` to convert between timezones.
- `date.fromUnixSeconds()` and `date.fromUnixMillis()` return UTC values.

-----

### `format` Module

```grob
// Table output — auto-columns from struct fields, returns string
print(results.format.table())

// Table output — explicit column selection and ordering
print(results.format.table(columns: ["repo", "staleCount", "asOf"]))

// List output — one field per line, key: value — for single-record detail
print(item.format.list())

// CSV output — comma-delimited, header row included
print(results.format.csv())

// Typical pattern — filter, project, format, print
print(
    results
        .filter(r => r.staleCount > 0)
        .select(r => #{ repo: r.repo, stale: r.staleCount, asOf: r.asOf })
        .format.table()
)

// Write formatted output to a file
fs.writeText("report.txt", results.format.table())

// Numeric formatting — .NET pattern strings
label := format.number(total, "N2")      // "1,234.56"
label := format.number(pct, "P1")        // "12.3%"

// Date formatting — .NET pattern strings (also available on date instance)
label := format.date(d, "dd MMM yyyy")   // "05 Apr 2026"
```

Full signatures:

```
format.table(items: T[]): string
format.table(items: T[], columns: string[]): string
format.list(item: T): string
format.csv(items: T[]): string
format.number(value: int | float, pattern: string): string
format.date(value: date, pattern: string): string
```

All functions return `string`. The caller decides what to do with it — `print()`,
`log.info()`, `fs.writeText()`, string concatenation, or anything else. There is no
write-to-stdout side effect. Column widths are auto-sized to content. Alignment:
strings left-aligned, numbers right-aligned. Per-column alignment and width control
are post-MVP.

Compiler rewrite: `.format.table()`, `.format.table(columns: [...])`, `.format.list()`,
`.format.csv()` in chained position are compiler-recognised namespace prefixes — rewritten
to the free-function form at compile time. No runtime `.format` property exists on any
type. The type checker registers the chained forms for `T[]` (table, csv) and `T`
(list) array types.

`format.number()` and `format.date()` are free functions, not chained. Pattern strings
follow .NET conventions (`"N2"`, `"P1"`, `"dd MMM yyyy"`, etc.).

-----

### `fs` Module — Shape (Sketch)

```grob
// List files in a directory (non-recursive)
files := fs.list("C:\\Reports")

// List recursively
files := fs.list("C:\\Reports", recursive: true)

// File object properties
file.name        // "report_2026.xlsx"
file.path        // "C:\\Reports\\report_2026.xlsx"
file.directory   // "C:\\Reports"
file.extension   // ".xlsx"  — always lowercased, always includes dot
file.size        // 204800   — bytes
file.modified    // date value — last write time
file.created     // date value — creation time
file.isDirectory // false

// File object methods
file.rename("report_2025.xlsx")
file.moveTo("C:\\Archive")
file.moveTo("C:\\Archive", overwrite: true)
file.copyTo("C:\\Backup")
file.copyTo("C:\\Backup", overwrite: true)
file.delete()

// Module functions — existence checks
fs.exists("C:\\Reports\\file.txt")        // bool
fs.isFile("C:\\Reports\\file.txt")        // bool
fs.isDirectory("C:\\Reports")             // bool

// Directory creation
fs.ensureDir("C:\\Reports\\2026\\April")  // creates full path if not exists, no-op if exists
fs.createDir("C:\\Reports\\NewFolder")    // fails with IoError if already exists

// Deletion
fs.delete("C:\\Reports\\old.txt")         // file or empty dir — refuses non-empty dir (IoError)
fs.deleteRecursive("C:\\Reports\\Old")    // directory and all contents

// Read / write
content := fs.readText("C:\\config.json")
lines   := fs.readLines("C:\\data.txt")   // string[]
fs.writeText("C:\\output.txt", content)   // creates or overwrites
fs.appendText("C:\\log.txt", line)        // creates or appends

// Copy / move by path — overwrite: false by default (throws IoError if destination exists)
fs.copy("C:\\source.txt", "C:\\dest.txt")
fs.copy("C:\\source.txt", "C:\\dest.txt", overwrite: true)
fs.move("C:\\source.txt", "C:\\dest.txt")
fs.move("C:\\source.txt", "C:\\dest.txt", overwrite: true)
```

`File` is a built-in type registered by the `fs` stdlib plugin at startup — the type
checker knows its full shape. Calling an undefined property or method on a `File` value
is a compile error.

**Encoding conventions:**

- `fs.readText()` reads as UTF-8. If a BOM (byte order mark) is present, it is
  consumed and the encoding is detected from it (UTF-8, UTF-16LE, UTF-16BE). If
  no BOM is present, UTF-8 is assumed. No encoding parameter in v1 — UTF-8 covers
  the vast majority of scripting use cases on modern Windows.
- `fs.writeText()` and `fs.appendText()` write as UTF-8 without BOM — the modern
  Windows convention. No encoding parameter in v1.
- `fs.readLines()` follows the same encoding rules as `fs.readText()`. Lines are
  split on `\n` and `\r\n` — both are handled transparently. The returned strings
  do not include line terminators.

`file.extension` is always lowercased and always includes the dot — callers never need
to normalise it. `".jpg"`, `".xlsx"`, `".csv"` — always that form.

Two levels of delete by design: `fs.delete()` refuses a non-empty directory and throws
`IoError`. `fs.deleteRecursive()` makes the destructive intent explicit at the call site.

-----

### `guid` Module — Shape

```grob
// Generation
id := guid.newV4()                                              // random
id := guid.newV7()                                              // time-ordered (RFC 9562)
id := guid.newV5(guid.namespaces.url, rgId, "storage", env)    // deterministic, variadic names

// Well-known namespaces (RFC 4122)
guid.namespaces.dns   // 6ba7b810-9dad-11d1-80b4-00c04fd430c8
guid.namespaces.url   // 6ba7b811-9dad-11d1-80b4-00c04fd430c8
guid.namespaces.oid   // 6ba7b812-9dad-11d1-80b4-00c04fd430c8

// Parsing
id := guid.parse("550e8400-e29b-41d4-a716-446655440000")   // throws RuntimeError if invalid
id := guid.tryParse(someString)                              // returns nil if invalid

// Sentinel
empty := guid.empty   // 00000000-0000-0000-0000-000000000000

// Instance properties
id.version    // int — 4, 5, or 7
id.isEmpty    // bool — true if all zeros

// String representation — canonical form is lowercase with hyphens
id.toString()         // "550e8400-e29b-41d4-a716-446655440000"
id.toUpperString()    // "550E8400-E29B-41D4-A716-446655440000" — Azure ARM endpoints
id.toCompactString()  // "550e8400e29b41d4a716446655440000"     — storage names, keys

// Uppercase-no-hyphen: compose when needed
id.toCompactString().upper()   // "550E8400E29B41D4A716446655440000"

// Azure storage account name from deterministic GUID
storageName := "sa${storageId.toCompactString()}"

// String interpolation calls toString() implicitly
log.info("Resource ID: ${myGuid}")

// Comparison
id1 == id2
id1 != id2

// Map key pattern (v1 — keys must be string)
tags := map<string, string>{
    "storageId": storageId.toString()
}
```

`guid` is a core module — auto-available, no import required. `guid` is a primitive
type distinct from `string`. `guid == string` is a compile error.

`guid.newV5()` takes variadic `name: string...` segments — concatenated before hashing.
Same inputs always produce the same GUID. This mirrors Bicep's `guid()` function for
idempotent Azure resource naming.

**Compile-time validation:** When `guid.parse()` is called with a string literal
argument, the type checker validates the GUID format at compile time and produces a
compile error if malformed. Runtime-dynamic strings are validated at runtime as normal.

**Excluded versions:** Version 1 (leaks machine identity via MAC address) and
Version 3 (MD5-based, superseded by v5) are not exposed.

-----

## `exit()` — Built-in Function

```grob
exit()     // exit with code 0
exit(0)    // explicit success
exit(1)    // signal failure to calling process

// Typical usage — fail fast after error
try {
    process.runOrFail("az", ["version"])
} catch e {
    log.error("Azure CLI not found.")
    exit(1)
}

// Signal success/failure based on result
if (results.isEmpty) {
    log.warning("No results found.")
    exit(0)
}
```

`exit()` is not a module function — no namespace, always available, same category as
`print()`. When called inside a function, it throws an uncatchable internal `ExitSignal`
that unwinds the entire call stack. The VM catches it at the top level, flushes output
buffers, and terminates with the specified code. It cannot be caught with `try/catch`.

Default exit codes: normal completion → `0`. Unhandled `GrobError` → `1`.

-----

## Conditional Expressions

```grob
// Ternary — two-way inline choices
label  := isActive ? "on" : "off"
prefix := count == 1 ? "item" : "items"

// Switch expression — multi-branch value selection
status := used_pct switch {
    >= crit_percent => "CRITICAL",
    >= warn_percent => "WARNING",
    _               => "OK"
}

// Switch on a value directly
message := http_code switch {
    200 => "OK",
    404 => "Not found",
    500 => "Server error",
    _   => "Unknown"
}

// Switch expression result assigned or passed directly
log.info(http_code switch {
    200 => "Request succeeded",
    _   => "Request failed with code ${http_code}"
})
```

Ternary `? :` for simple two-way choices — familiar to every C#/Go developer, no
ceremony. Switch expression for multi-branch — C# 8 style, each arm is `pattern => value`,
`_` is the catch-all. The type checker enforces exhaustiveness: missing `_` when not all
cases are covered is a compile error. All arms must return the same type — mismatch is a
type error. No `if/else` in expression position — these two forms cover the use cases
without introducing a third syntax.

-----

## Lambdas and Closures

```grob
// Single-expression lambda
files.filter(f => f.extension == ".jpg")
branches.filter(b => !b.name.contains("HEAD"))

// Closure — captures variable from enclosing scope
cutoff := date.today().minusDays(staleDays)
branches.filter(b => date.parse(b.lastCommit) < cutoff)

// Multi-parameter lambda
items.sort((a, b) => a.size > b.size)

// Block-body lambda
raw.split("\n")
    .filter(line => line.length > 0)
    .map(line => {
        parts := line.split("|")
        #{ branch: parts[0], date: parts[1], author: parts[2] }
    })

// Anonymous struct literal in lambda — #{ } not { }
results.select(r => #{ repo: r.name, stale: r.staleCount, asOf: r.asOf })
```

`{ }` after a lambda arrow is always a block body. `#{ }` is always an anonymous struct
literal. The parser never has to guess — no ambiguity.

Closures capture variables from the enclosing scope. The upvalue mechanism follows clox:
while the enclosing function is active, the upvalue holds a reference to the stack slot.
When the enclosing function returns, the value is copied to the heap. Each capturing
lambda becomes a `Closure` object at runtime — a `BytecodeFunction` plus its upvalue array.

-----

*Document updated April 2026 — pre-implementation review: `json.write()` and `json.encode()`*
*updated with `compact: bool = false` parameter (pretty-printed default);*
*`date` timezone conventions specified (local time default for constructors);*
*`fs.readText()` encoding conventions specified (UTF-8 default, BOM auto-detection);*
*`fs.readLines()` line terminator handling specified.*
*Previous: `Grob.Http` full locked signatures; `Grob.Zip` full spec added;*
*`env` module fully specified (all five functions); `format` module fully specified (returns string, full signatures);*
*`json` module updated with `json.encode()` and `json.Node` detail; Community Plugins section restored.*
*Previous: `guid` module shape added; `Grob.Crypto` shape added;*
*`fs.copy`/`fs.move` updated with `overwrite: bool = false` parameter;*
*`process` module signatures updated with `timeout: int = 0` parameter.*
*Document created April 2026 — extracted from Grob___Decisions___Context_Log.md.*
*Authorised decisions recorded in Grob___Decisions___Context_Log.md.*
*This document is the implementation reference — the decisions log is the authority.*
