# Grob — Open Questions

> Design questions requiring decisions before implementation reaches them,
> the deferred work register — decided work with an owner or `unowned` and a
> completion criterion — and resolved questions with their full rationale
> preserved.
> Decisions authorised in the decisions log — April 2026 design sessions.
> This document preserves the reasoning behind each question and resolution.
> When this document and the decisions log conflict, the decisions log wins.

---

## Open Questions

These are unresolved. They require a decision before implementation reaches them.
Listed in rough priority order — earlier questions affect more downstream design.

---

### OQ-013 — `Grob.Llm` Plugin

**Question:** What is the shape of a first-party plugin for calling large language
models from Grob scripts?

**Tentative direction:** Provider-agnostic facade plugin (`import Grob.Llm`),
sitting alongside `Grob.Http`, `Grob.Crypto`, `Grob.Zip`. The plugin owns
completions, streaming, structured output via `completeAs<T>`, and token counting.
It does not own the MCP protocol — that lives in `Grob.Mcp` (OQ-014).

The plugin abstracts over multiple backends — Anthropic, OpenAI, Azure OpenAI,
Ollama — via an internal provider interface. Provider selection is per-call or
configured via `env`. Credential handling follows the established pattern
(`env.require()`).

Tentative surface, subject to design:

```grob
import Grob.Llm

reply := llm.complete(
    model:     "claude-sonnet-4-5",
    prompt:    "Summarise: ${text}",
    maxTokens: 500,
)

print(reply.text)

type Triage {
    severity:     string
    rootCause:    string
    suggestedFix: string?
}

triage := llm.completeAs<Triage>(
    model:  "claude-sonnet-4-5",
    prompt: "Triage this stack trace: ${stack}",
)

if (triage.severity == "critical") {
    // typed access, no JSON spelunking
}
```

`completeAs<T>` is the design point that earns Grob’s keep here — it consumes
the constrained generics infrastructure (OQ-001) and the JSON boundary mechanism
(OQ-003) that already exists. No new type-system machinery; the language already
supports it. This is the same shape as `mapAs<T>()` for JSON.

**Rationale for plugin status, not stdlib:** the bar for stdlib is “every script
needs this” — `fs`, `json`, `path`, `env` clear that bar. LLM calls do not. They
are a specific integration with strong opinions (provider, model, auth) that maybe
30% of users will ever touch. Pulling in provider SDKs and token-counting
machinery has no place in `Grob.Stdlib`. Model APIs evolve every six months; the
language cannot. Plugin isolation gives the right cadence boundary.

**Rationale for provider-agnostic:** the `process.run` vs `process.runShell`
precedent — name things by what they do, keep the surface tight, leave room for
substitution. Coupling the plugin to a single provider would be a versioning trap.

**Defer until:** post-v1, after `Grob.Http` ships (the plugin is an HTTP client
wrapper at heart). No commitment until Sprint 12 retrospective.

---

### OQ-014 — `Grob.Mcp` Plugin

**Question:** What is the shape of a first-party plugin for the Model Context
Protocol — both serving Grob scripts as MCP tools and consuming MCP servers from
Grob scripts?

**Tentative direction:** Single first-party plugin (`import Grob.Mcp`) covering
both directions of the protocol. Pure protocol implementation — JSON-RPC 2.0,
capability negotiation, tool/resource/prompt primitives. Knows nothing about any
LLM provider. Sits alongside `Grob.Llm` (OQ-013) but does not depend on it.

**Server side — the design point:** a script that already declares its
parameters can be exposed as an MCP tool with one call. The existing typed `param`
declarations and their decorators (`@allowed`, `@minLength`, `@maxLength`,
`@secure`) reflect into the JSON Schema that the MCP protocol requires for tool
input descriptors. No new decorators, no schema DSL, no boilerplate — the
language feature already specified does the work.

```grob
import Grob.Mcp

@minLength(3)
@maxLength(20)
param issueId: string

param verbose: bool = false

// the param declarations above become the tool's JSON Schema input descriptor
mcp.serve(stdio: true)
```

This is the position that makes Grob the lowest-friction language for writing
MCP tools — beating Python (no schema enforcement) and TypeScript (more ceremony)
on their own ground, by leaning on type-system features that already exist for
other reasons.

**Client side:** a Grob script can connect to an MCP server and invoke tools.
Tentative surface:

```grob
client := mcp.connect("https://jira-mcp.example.com")
ticket := client.callTool("get_issue", #{ id: "PROJ-123" })
```

**Composition pattern.** Neither plugin knows about the other. The script author
wires them together — exactly as `fs` and `json` are composed today:

```grob
import Grob.Llm
import Grob.Mcp

jira    := mcp.connect("https://jira-mcp.example.com")
ticket  := jira.callTool("get_issue", #{ id: "PROJ-123" })

summary := llm.complete(
    model:  "claude-sonnet-4-5",
    prompt: "Summarise this ticket for the standup: ${ticket.description}",
)

print(summary.text)
```

That decoupling is structural — an MCP server does not need an LLM (filesystem
or git MCP servers do not), and an LLM call does not need MCP (most scripts
will simply complete and return). Two layers, two responsibilities, composed
at the script level.

**Rationale:** MCP was donated to the Linux Foundation under the Agentic AI
Foundation in December 2025 with co-stewardship from Anthropic, Block, and
OpenAI. An official C# SDK exists, which lowers the implementation cost
substantially. The protocol is now the cross-vendor standard for AI tool
integration — equivalent to LSP’s role for editors. Grob’s design strengths
(typed `param` declarations, JSON stdin/stdout pipeline, opinionated low-ceremony
syntax) align with the shape of a well-formed MCP tool. The market signal in
2026 is consistent — small, focused, governed agents beat autonomous systems —
and that is exactly the shape of script Grob is designed for.

**November 2026 note (D-434).** Since this was written, harnesses have moved towards
having the model write a short program that calls tools in place of individual tool
calls (`grob-agentic-strategy.md` §4). That adds a third design point to the two above:
typed bindings generated from an MCP server's tool schemas, so a script calls a tool as
a typed function and the checker catches a wrong argument before the run. It is the
client-side mirror of `param` reflection and belongs in this plugin's design.

**Defer until:** post-v1, after `Grob.Http` ships (HTTP transport is the
production-recommended MCP transport; stdio is local-only). No commitment
until Sprint 12 retrospective. Likely a v1.1 or v1.2 plugin.

---

### OQ-015 — `Grob.Sql` Plugin

**Question:** What is the shape of a first-party plugin for relational database
access from Grob scripts? Target backends are SQL Server (primary, given the
Windows / BDO target audience), PostgreSQL, MySQL, and SQLite.

**Tentative direction:** Provider-agnostic facade plugin (`import Grob.Sql`),
sitting alongside `Grob.Http`, `Grob.Crypto`, `Grob.Zip`. The plugin owns
connections, parameterised queries, typed result mapping via `queryAs<T>`,
transactions, and connection pooling. Backend selection is by connection-string
provider hint or explicit factory call. Built on ADO.NET internally — the .NET
ecosystem already has battle-tested drivers for every target backend.

Tentative surface, subject to design:

```grob
import Grob.Sql

type Customer {
    id:    int
    name:  string
    email: string
}

conn := sql.connect("Server=...;Database=...;Integrated Security=true",
                    timeoutSeconds: 30)

customers := conn.queryAs<Customer>(
    "SELECT id, name, email FROM customers WHERE active = @active",
    #{ active: true },
)

conn.close()
```

**Three design points that earn first-party status:**

1. **`queryAs<T>` consumes existing infrastructure.** Same shape as JSON
   `mapAs<T>()` — the constrained generics machinery (OQ-001) and the
   row-to-record mapping concept already exist for JSON. SQL row mapping is
   the same problem with a different source.
2. **Parameterised queries are not optional.** The `query()` and `queryAs<T>()`
   signatures take a parameters argument as their _required_ second positional
   parameter — analogous to `process.run(cmd, args[])` requiring an array even
   when empty. There is no string-only overload. Empty parameters are passed as
   `#{}`. The PowerShell `Invoke-Sqlcmd` willingness to accept string-built SQL
   is the footgun this design refuses to repeat. **API shape is the security
   boundary, not string syntax** — see OQ-017 for the same principle applied to
   string interpolation.
3. **Transactions as a scoped primitive.** `conn.transaction(fn => { ... })`
   with automatic commit on lambda return and rollback on exception. The lambda
   form makes the transaction boundary lexically obvious and prevents the
   “forgot to commit” / “forgot to dispose” class of bug.

**Rationale for plugin status, not stdlib:** ADO.NET driver dependencies are
heavy and provider-specific. Connection pooling configuration is opinionated
and varies per backend. Many users will never touch a database. The bar for
stdlib is “every script needs this” and SQL does not clear it. The plugin
isolation also means SQL Server, PostgreSQL, MySQL and SQLite drivers can be
sub-packages (`Grob.Sql.SqlServer`, `Grob.Sql.PostgreSql`, etc.) loaded only
where needed — same pattern as `Grob.Http`’s `auth.*` sub-namespace.

**Rationale for first-party, not community:** the v1 target audience —
Windows developers and sysadmins, BDO consultants — runs on SQL Server.
“Pull a report from the DB and email it” is the exact gap Grob is designed
to fill, and a fragmented community plugin landscape would produce three
incompatible APIs that all do parameterisation slightly differently. The
parameterised-query API shape is too important to leave to community
consensus.

**Open sub-questions to resolve at design time:**

- **Exception leaf.** Does SQL get its own `SqlError` leaf in the
  `GrobError` hierarchy, or does it share `IoError` (connection failure) and
  fold query errors into `RuntimeError`? Tentative: yes, a new leaf —
  database errors have a natural domain name and the bar D-284 set is “does
  this have a natural domain name?” The answer is yes for SQL.
- **Connection pooling exposure.** Lean on ADO.NET’s connection-string-based
  pool (zero API surface) or expose a `sql.pool()` primitive? Tentative:
  ADO.NET pooling, zero API surface in v1.
- **Async / streaming results.** Tentative: synchronous-only in v1.
  `IAsyncEnumerable<T>` over rows is post-MVP. Most scripts read result
  sets that fit comfortably in memory.

**Defer until:** post-v1. The two follow-on open questions surfaced by
realistic enterprise scripts (OQ-016 array aggregation, OQ-017 triple-backtick
interpolation) should resolve first or in parallel — the SQL API design is
where the parameterisation precedent is set.

---

### OQ-016 — Array Aggregation Methods

**Question:** Should `T[]` gain aggregation methods — `sum`, `max`, `min`,
`count`, `average` — accepting key-selector lambdas in the LINQ style?

**Background:** the `T[]` registry (D-280, D-281) currently exposes
transformation (`select`), filtering (`filter`), iteration (`each`), sorting
(`sort`), bounds queries (`first`, `last`, `contains`), and mutation
(`append`, `insert`, `remove`, `clear`). It does not expose aggregation.
Realistic enterprise scripts repeatedly want it — totalling values across a
collection, finding the maximum age in a result set, counting matching
records. The current workaround is an explicit `for` loop with an accumulator,
which is correct but reads less well than the LINQ equivalent and is a
recurring pattern across scripts.

**Tentative direction:** add the LINQ-shaped aggregation set to `T[]`:

```grob
arr.sum<U: Numeric>(fn: T → U): U
arr.average<U: Numeric>(fn: T → U): float
arr.max<U: Comparable>(fn: T → U): U?
arr.min<U: Comparable>(fn: T → U): U?
arr.count(fn: T → bool): int
```

`Numeric = int | float`. `Comparable` already exists in the registry per
D-281. `max`/`min` return nullable because an empty array has no maximum or
minimum value. `sum` on an empty array returns the zero of `U`. `average`
on an empty array throws `ArithmeticError` (division by zero) — consistent
with the language’s no-silent-NaN policy (D-273). `count` with a predicate
returns the number of matches; the existing `length` property handles
no-predicate counting.

Tentative surface:

```grob
total    := debts.sum(d => d.amount)               // float
oldest   := debts.max(d => d.daysOverdue)          // int?
critical := debts.count(d => d.severity == "high") // int
```

**Rationale:** every C# developer already reaches for these. They are LINQ’s
canonical surface and the LINQ-for-scripting identity (D-280) commits Grob to
that vocabulary. The constrained-generics machinery is in place. The
implementation is small — each method is a fold over the array. The cost of
_not_ having them is paid every time someone writes a script that does
arithmetic across a collection.

**Rationale for deferral despite small implementation cost:** the v1 sample
scripts have shipped without these methods. Adding them now is scope creep at
a sensitive moment — Sprint 1 has not started, and the risk of “one more
thing” snowballing into late-stage churn is real. Deferring lets the
community see realistic v1 scripts (with explicit `for` loops) and confirm
that the friction is genuine before committing language surface.

**Defer until:** post-v1. Likely v1.1. The supporting evidence is concrete —
real-world Grob scripts that needed aggregation and worked around it. If
that pattern shows up consistently in early adopter feedback, the case for
v1.1 inclusion is overwhelming.

---

### OQ-017 — Triple-Backtick Interpolation

**Question:** Should triple-backtick raw strings support an opt-in
interpolation form — for example `$```...``` ` — alongside the existing
verbatim form?

**Background:** D-127 specifies three string forms. Triple backtick
(`...`) is multiline verbatim, no interpolation. The rule is
principled: multiline implies raw, interpolation implies single-line. The
two forms do not overlap, which keeps the lexer simple and the developer
intent visible.

Realistic enterprise scripts have surfaced consistent friction with this
rule. Multiline interpolated content — HTML email bodies, JSON payload
templates, multi-paragraph log messages, formatted error output — all need
both shape and substitution. The current workaround is `+`-concatenation
across lines, which works but is verbose. A 30-line HTML email becomes 60
lines of `"<tr>" + "<td>${x}</td>" + ...`.

**Tentative direction:** add a `$```...``` ` form — explicit interpolation
prefix on triple-backtick — as an additive fourth string form. The default
triple-backtick remains verbatim and unchanged. Existing scripts are
unaffected.

Tentative surface:

````grob
// Verbatim — no interpolation, unchanged from v1
sql := ```
SELECT * FROM users WHERE active = 1
````

// Interpolating — explicit opt-in via $ prefix
html := $```

<h2>Report — ${title}</h2>
<p>${count} accounts above threshold</p>
```
```

The `$` prefix mirrors C#’s `$@"..."` for verbatim-interpolated strings,
which is the closest precedent and is well-understood by every C# developer
on day one.

**The injection question.** This question is sometimes raised as a security
concern: “doesn’t multiline interpolation make SQL injection easier?” The
honest answer is **no — the v1 rule does not prevent SQL injection and this
question is independent of the string form**. A script can already build an
injectable SQL string today using `+`-concatenation:

```grob
query := "SELECT * FROM users WHERE name = '" + userInput + "'"
conn.queryAs<User>(query)   // injected
```

The dangerous path is the _use site_ — passing a string-built query to
`queryAs<T>` instead of using parameters — not the _string syntax_.
The v1 triple-backtick rule is therefore security theatre on this dimension:
it makes the safe path (multiline HTML, JSON templates) verbose without
making the dangerous path (string-built SQL) any harder.

**The genuinely useful security control** is on the API side, not the
language side:

1. **`Grob.Sql` parameterised-query API shape** (OQ-015) makes the
   parameters argument required. There is no string-only overload.
2. **`grob check` lints** — proposed `W1601` warning category — flag
   interpolated strings flowing into known-dangerous sinks
   (`process.runShell`, `Grob.Sql` raw query methods if any are ever
   added, regex compilation from untrusted input). The lint is on the use
   site and applies to _any_ interpolated string, including the existing
   `"..."` form.

Both controls are independent of OQ-017 and apply whether the answer is
yes or no.

**Rationale for the proposed direction:** the additive form is small in
scope, principled in syntax (`$` prefix is the established C# idiom),
zero-impact on existing scripts, and removes recurring real-world friction.
The injection concern that might motivate keeping the v1 rule is illusory —
the rule does not prevent injection and the proper controls live elsewhere.
Keeping v1 simple by deferring is fine; keeping v1 simple _and_ claiming
the simplicity is for security reasons is not.

**Rationale for deferring despite the case for inclusion:** v1 scope is
locked. The form is genuinely additive — no existing script needs to
change — so post-v1 introduction has no migration cost. The decision can
wait for real-world feedback on whether the friction is as recurring as the
script-design exercises suggest.

**Defer until:** post-v1, paired with OQ-015. The `Grob.Sql` API shape
sets the parameterisation precedent, and the lint architecture (`W1601`)
touches both. Likely v1.1.

---

### OQ-018 — Code Signing and Release Provenance for the First Public Artifact

**Question:** When the Grob runtime first ships as a distributable artifact,
what is the signing approach, and how is the provenance chain that the hardening
interlude (D-317) scaffolds completed?

**Context.** The B increment of the Sprint 4→5 interlude built the supply-chain
floor: deterministic builds, a CycloneDX SBOM on every CI run, and
`actions/attest-build-provenance` wired into the release workflow but guarded so
it activates only when a `v*` tag publishes real artifacts. What it deliberately
did **not** do is sign a shipping executable, because there is no shipping
executable in v1 — "compile to executable" is post-MVP and winget distribution
is future. Signing belongs with the first public release, not scaffolded against
a build that ships nothing.

**To decide at that point:** the code-signing approach for `grob.exe` (and any
installer), signed release artifacts, and whether to complete the chain to full
SLSA Level 3, including reproducible-build attestation across machines beyond the
deterministic compilation already in place.

**This is a deliberate deferral, not an omission.** It is unrelated to OQ-013
(the `Grob.Llm` plugin); the earlier association of signing with OQ-013 was a
framing error and is corrected here.

**Defer until:** the first public release of a `grob` executable or installer.

---

### OQ-019 — Machine-Readable Diagnostic Contract

**Question:** What exactly does `grob check --json` emit, what does Grob promise about
it, and how does `Diagnostic` grow to carry help and fixes?

**Raised by:** D-434. **Must resolve before:** the Sprint 12 kickoff prompt.

**To decide:**

- **The schema.** Grob's own versioned JSON, SARIF or the first with the second as
  an option. SARIF buys existing consumers and costs a verbose shape built for static
  analysis results, not compiler repair loops.
- **The stability promise.** Error codes are immutable once shipped. Whether field
  names, range conventions (zero- or one-based, UTF-16 units or code points) and
  severity values carry the same promise, and how the schema version moves.
- **`Diagnostic`'s growth.** It is a record of code, message, range and severity
  today. R-25's renderer already needs `note` and `help`. Fixes add a list of text
  edits and an applicability level — Rust's machine-applicable and maybe-incorrect
  split is the obvious model.
- **Foreign idioms.** Which constructs get a dedicated diagnostic (`var`, `let`,
  `function`, `def`, a trailing semicolon, `null`, `else if` spellings), where they
  are recognised — the parser is the likely home, under D-300's recovery rules — and
  whether each is its own code or a `help` line on an existing one.
- **Run-time errors.** Whether `grob run --json` reports an unhandled error and its
  stack frames under the same contract.
- **Exit codes.** Whether they change in JSON mode. The tentative answer is no.

**Tentative direction:** Grob's own schema, one document per run, versioned, with
fixes as text edits carrying an applicability level. SARIF only if a consumer asks.

---

### OQ-020 — The Agent Context Kit

**Question:** What does `grob skills` print, how is it kept true and what does
`grob new` scaffold for a harness?

**Raised by:** D-434. **Must resolve before:** the Sprint 12 kickoff prompt.

**To decide:**

- **Content and budget.** Three parts: a language reference cut to a stated token
  ceiling; the idioms that differ from C#, Go, PowerShell and Python; translation
  pairs. What the ceiling is, and whether there is a short form and a full form.
- **Format.** Plain Markdown on stdout, the Agent Skills `SKILL.md` layout or both.
  Whether `grob new` writes an `AGENTS.md` section, a skill directory or neither
  unless asked.
- **Source of truth.** Hand-written, generated from `grob-language-fundamentals.md`,
  or hand-written and checked against it. `.claude/skills/writing-grob-source` is the
  seed.
- **The gate.** Every sample in the kit compiles under the public-samples gate
  (D-430). How the kit's stdlib coverage is kept in step with `NativeCatalogue`.

**Tentative direction:** hand-written Markdown embedded in the binary, gated like any
public sample, with the stdlib section generated from the catalogue.

---

### OQ-021 — The Agent Benchmark

**Question:** How is "an agent can write Grob" measured, and how do the results feed
design?

**Raised by:** D-434. **Defer until:** Sprint 12 closes, since it measures what that
sprint builds.

**To decide:** the task set and how it covers the four driving use cases without
needing live Azure or SharePoint tenants; which harnesses and models, and how a run is
made repeatable when models change underneath it; the scoring — first attempt, and
within three rounds of `grob check --json`; where it runs and what it costs; and the
rule that turns a result into a design input. A baseline is never adjusted to absorb a
known defect (D-313's principle). A result that argues for a syntax change is subject
to D-434 Decision 4.

---

### OQ-022 — Restricted Run and Plan Mode

**Question:** What is the surface of a restricted run, and what does a script do under
a recording host?

**Raised by:** D-435. **Defer until:** post-v1. D-436 keeps the route open meanwhile.

**To decide:**

- **Where grants are stated.** CLI flags, `grob.json`, a declaration in the script
  the checker can read or a combination. A declaration in the script lets `grob check`
  report what a script needs before anything runs, which is the strongest form.
- **Granularity.** Per module, per read or write, per path prefix, per executable
  for `process`, per host for `Grob.Http`.
- **Plugins.** How a plugin is granted by name, and how the documentation states that
  a grant removes the gate for that plugin (D-435 Decision 3).
- **The denial.** A new `GrobError` subtype or an existing one, whether it is
  catchable, and its error code.
- **Plan mode.** Reads return real data. What a recorded write returns to the script,
  how a later read of a path written earlier in the same run behaves, what `process.run`
  returns when it is not run, and the shape of the report.
- **Plugins under plan mode.** A granted plugin is arbitrary code outside the
  capability seam (D-073), so a recording host cannot see or stop what it does. Two
  answers are open: a plan run refuses every plugin import, granted or not, which keeps
  the report complete and makes scripts that use a plugin unplannable; or a plan run
  allows a granted plugin and the report states that the plugin's effects are neither
  recorded nor prevented. Raised by review of PR #244 and not decided here.
- **Compile-time reach.** Whether the checker can prove a script never calls an
  ungranted capability, so a denial is a compile error where that is provable.

**Constraints already fixed:** no sandbox claim (D-435 Decision 2); the mechanism is an
alternate capability set at the composition root (D-343); interface members are one
operation each, read or change (D-436 Decision 2).

---

### OQ-023 — Embedding Host Package

**Question:** What does Grob publish so a .NET agent host can run a script in-process
with its own natives and limits?

**Raised by:** D-435. **Defer until:** post-v1, after R-04 narrows `Grob.Core`'s public
surface.

**Context.** The playground (D-319) and the error-examples harness (D-428) are
embedding hosts already, both inside the repository. A public package makes the route
available to a host that wants agent-written scripts checked before they run and
confined to functions it supplies. `FunctionSignature` (D-426) already gives
host-supplied natives full type checking.

**To decide:** the package and its surface; resource limits — an instruction budget,
wall-clock time, memory — and how each is reported; whether a run can suspend at a
host call and resume; and which assemblies become supported public API as a result.

---

### OQ-024 — The Shell `process.runShell` Invokes

**Question:** Which shell runs a `process.runShell` command on each platform, and
what does a script author have to know about the difference?

**Raised by:** D-442. **Must resolve before:** the Sprint 9C `process` increment
prompt, because the answer is behaviour the increment builds.

**Context.** D-075 names `runShell` as the form where shell interpretation is wanted
and says nothing about which shell. On Windows the candidates are `cmd.exe`, Windows
PowerShell and `pwsh`; on Linux, `/bin/sh` and `bash`. The same command string means
different things under each, so a `runShell` script is not portable by default.

**To decide:** the default on each platform; whether a script can name the shell;
whether the `ProcessResult` or the documentation states which shell ran; and what the
documentation says about portability. `process.run` with an argument array involves no
shell, so its behaviour does not depend on which shell a platform has, and D-075 makes it
the primary form. That removes one source of difference and no more: a script is portable
only if the executable it names, and the arguments it passes, exist on both platforms.

**Tentative direction:** `cmd.exe /c` on Windows and `/bin/sh -c` on Linux, the
lowest common denominator on each, with an optional named parameter to choose another.
Documentation states plainly that `runShell` strings are platform-specific.

---

### OQ-025 — Installing Grob on Linux

**Question:** Where does Grob live on Linux, and how does it get there?

**Raised by:** D-442. **Must resolve before:** Sprint 11, which builds `grob install`
and the package search path, and before the first public release.

**Context.** `grob-install-strategy.md` is written for Windows throughout:
`%USERPROFILE%\.grob\`, `%ProgramFiles%\Grob\packages\`, `grob.exe` and `winget`.
`release.yml` already produces `grob-linux-x64.tar.gz`, with no stated way to install
it.

**To decide:** the user root — `~/.grob/` to mirror Windows, or the XDG base
directories; the system scope and whether one exists; the three-level package search
path's Linux form; delivery — a tarball and an install script, a package manager or a
container image; and whether `linux-arm64` is published, given how many development
machines and runners are ARM.

**Tentative direction:** `~/.grob/` for symmetry with the Windows layout, a tarball
with an install script first and a container image soon after, since a container is
where an agent session is most likely to meet Grob.

---

### OQ-026 — Inline and Stdin Source for `grob run`

**Question:** What is the command-line surface for running a script whose source is
not in a file, and how does it behave?

**Raised by:** D-444. **Must resolve before:** the agent-scripting interlude's first
increment prompt, because the answer is behaviour that increment builds (R-48).

**Context.** D-444 makes both forms v1: source read from stdin, and source passed
inline as an argument. `grob run` today takes a file path only. The forms are for short
scripts an agent writes and discards, so the diagnostics and exit codes must be exactly
those a file would produce.

**To decide:** the spelling — `grob run -` for stdin is the common convention, and the
inline flag could be `-e` (Node, Perl, Ruby), `-c` (Python, the POSIX shells) or a long
form; whether the inline form needs `run` at all; the name diagnostics give the source
in place of a file name; what `input()`, `json.stdin()` and `csv.stdin()` see when the
script itself arrived on stdin; how `param` values reach an inline script once Sprint 10
lands; that `.grobc` caching does not apply; whether `grob check` accepts the same forms
when it lands in Sprint 12; and quoting on each platform's shells, since an inline
script with `${}` interpolation and double quotes is awkward to quote in PowerShell and
in POSIX shells alike.

**Tentative direction:** `grob run -` and `grob run -e <source>`, with `<stdin>` and
`<inline>` as the names in diagnostics. When the source came from stdin the script
sees stdin at end of input, and the documentation says so. `grob check` accepts both
forms from the start. The quoting cost makes stdin the form the scripting guide (R-50)
recommends for anything longer than a line.

---

### OQ-027 — Do the Foreign-Idiom Diagnostics Leave R-36?

**Question:** Do the diagnostics that answer a neighbouring language's construct with
the Grob form (`var x = 1` answered with `x := 1`) move from Sprint 12 into the
agent-scripting interlude?

**Raised by:** D-444. **Must resolve before:** the agent-scripting interlude's prompts
are written, because the answer decides the interlude's scope.

**Context.** R-36 bundles four things in Sprint 12: `grob check --json`, `note` and
`help` lines with structured fixes, the foreign-idiom diagnostics and `grob skills`.
The idiom diagnostics do not need `--json`. They are the cheapest correction for the
interference `grob-agentic-strategy.md` §3 describes, and the interlude is the first
time an agent writes Grob every day. Whether they need R-25's renderer depends on
whether the Grob form is carried as a `help` line or as message text.

**To decide:** whether to split R-36; if so, whether the idiom diagnostics ship with
the Grob form in the message text now and move to a `help` line when R-25 lands, at the
cost of rewriting their gold masters once; which idioms are covered, and from what
evidence; and how any new codes are allocated under D-316 in the increment that mints
them.

**Tentative direction:** none yet. The case for waiting is that the interlude produces
the evidence the idiom list should rest on. The case for moving is that every session
until Sprint 12 pays for each idiom the compiler does not yet name.

---

### OQ-028 — Evaluation Order of Reordered Arguments

**Question:** When a call uses named arguments, in what order are its arguments
evaluated?

**Raised by:** D-446. **Must resolve before:** v1, since the answer is behaviour a
script can observe and a later change would break scripts that came to rely on it.

**Context.** Verified at `9f7384d`: `EmitReorderedArguments` in `Grob.Compiler` emits
a call's arguments in the order the parameters are declared whenever the call names
an argument or omits a default. `f(b: g(), a: h())` therefore runs `h` before `g`. A
call that supplies every parameter positionally is emitted in source order. No
decision states either rule; D-113 fixes how arguments bind and says nothing of when
they are evaluated. D-446 extends the convention to natives and lets them inherit
today's behaviour, so that there is one behaviour and not two, without ruling that it
is right. C# and Python both evaluate in the order written.

**To decide:** whether evaluation follows the order written or the order declared;
if the order written, how the emitter delivers arguments to their slots, since the
callee receives a positional list; whether a default's expression, which is compiled
at the call site, is evaluated in its parameter's position or after every written
argument; and whether the rule is one a diagnostic can teach or only the reference
can state.

**Tentative direction:** the order written, with defaults after. It is what a reader
of the call expects and what the languages Grob sits beside do. The cost is in the
emitter, for script functions and natives alike.

---

## Deferred Work Register

**Authority: D-420.** This is the single tracking home for work that is
**decided but not yet done**. It exists because a deferral recorded only in
prose inside an append-only decision entry has nowhere to record its own
current state: the entry that defers is frozen at the moment of deferral, so
whether an item is still outstanding can only be reconstructed by re-reading
every entry since. That reconstruction is what the register replaces.

An open question asks *what should we do*. A register item has that answer and
asks *who does it, and when*. Items are added by the decision that defers them
and move to **Closed** below with the D-number that closed them — never
deleted, so the record of what was deferred and for how long survives.

Every item carries an **owner**: a named increment, a named sprint, or
`unowned`. An item may sit as `unowned` — that is honest — but an item without
a **completion criterion** may not be added, because "done" must be checkable
by someone who was not in the conversation that deferred it.

The register does not change the authority model. The decisions log remains the
authority; the register tracks the status of work the log has already
authorised, and every item cites the decision that raised it.

### Open items

| # | Item | Raised by | Owner | Done when |
|---|---|---|---|---|
| R-04 | **`Grob.Core`'s public surface narrowed.** It becomes SDK surface when `Grob.Runtime` publishes, while holding `Chunk`, `BytecodeFunction`, `CatchHandler`, `DiagnosticBag` and `IVmCallHost` — internals a plugin author should not see. Narrowing after publication is a breaking change. | D-419 | Sprint 11, **before first package publication** | An explicit allow-list names every type in `Grob.Core`'s intended public SDK surface; `Chunk`, `BytecodeFunction`, `CatchHandler`, `DiagnosticBag` and `IVmCallHost` are `internal` with `InternalsVisibleTo` to the assemblies that need them, and no public type sits outside the allow-list |
| R-06 | **Type-name LSP mechanism specified.** §3.1.1 requires `ResolvedType`/`Declaration` on identifier *expression* nodes; D-416 ruled `TypeRef` is outside it. Go-to-definition on a type name therefore resolves by some path no document names — plausibly `UserTypeRegistry` plus `Symbol.DeclaredAt`, but unstated. | D-416 | `unowned` — before any LSP work | A document states how a type name resolves to its declaration, and §3.1.1 says whether it is in scope |
| R-07 | **D-418's corpus corrections.** `grob-type-registry.md` (which carries both `mapAs` conventions side by side), `grob-stdlib-reference.md`, wiki `json.md`/`csv.md`, and validation script 04, to the uniform whole-type convention; script 11's `[ASSUMPTION]` marker removed. Script edits touch `.grob` corpus files and their markdown publication together (D-417's sync guard). | D-418 | `mapAs<T>` increment (Sprint 9D) | All sites use `mapAs<T>(): T`; `ValidationScriptCorpusTests` and `ValidationScriptMarkdownSyncTests` green |
| R-08 | **Decorator release-gate coverage.** All seven decorators ship in v1 (D-411), but only `@secure` appears in any validation script — six would ship unexercised, which is the advertised-but-unbuilt shape in advance. | D-411 | Sprint 10 | At least one release-gate script exercises the full decorator surface |
| R-10 | **`docs/errors/Exxxx.md` written.** 121 codes at creation, zero documents; `--explain Exxxx` is specified to read them. Four sections each: cause, example, fix, see-also. | v1 requirements §10 | `unowned` — v1 release gate | A document exists for every code currently in the registry (count may move — R-01 alone drops it to 120 — so this checks against the registry's live contents, not a fixed number), and `--explain` resolves for each of them |
| R-13 | **Lexer line-continuation suppression covers `Greater` and `LeftBrace`.** `Lexer.ApplyLineContinuation` elides a newline before a closing bracket only when the closer is `RightParen` or `RightBracket`. `Greater` (`ParseTypeArgumentList`'s terminator) and `LeftBrace` (`select` case patterns' terminator) are absent, so a multi-line type-argument or case-pattern list whose closer sits on its own line fails to parse with `E2001` at the closer — reproduced by D-422 both before and after its own change, and independent of trailing commas, since it recurs with no comma in the source at all. | D-422 | `unowned` — a lexer-surface change, out of scope for the parser-only increment that found it | `map<string,\n int\n>` and `select (x) { case 200,\n201\n{ … } }` both parse; D-422's two workaround tests (`TypeArgs_MultiLine_NoTrailingComma_Parses`, `CasePatterns_MultiLine_NoTrailingComma_Parses`) are rewritten to put the closer on its own line rather than glued to the last token |
| R-14 | **`ParseSelect`'s case-pattern loop gains a D-405/D-406 local recovery wrapper.** The map-entry, field-init and switch-arm loops it otherwise mirrors all have one; this loop does not, so a malformed pattern propagates uncaught out of `ParseSelect` and cascades into a second, unrelated diagnostic at the next top-level resync. Confirmed by D-422 before and after its change: `case , 200 { a }` yields the root-cause `E2001` plus a spurious second `E2001` at the file's next resync point. | D-422 | `unowned` — pre-existing and unrelated to the trailing-comma change that found it | `ParseSelect`'s pattern loop uses the `ParseXOrError`/`SkipToNextLiteralElementBoundary` pattern; a single malformed pattern yields exactly one diagnostic, and D-422's two tests (`CasePatterns_LeadingComma_StillDistinguishableAsE2001`, `CasePatterns_TrailingCommaThenGenuinelyMalformedPattern_StillFailsAtThatToken`) are tightened from `bag.Diagnostics[0]` to `Assert.Single` |
| R-15 | **Decorator constraints enforced at parameter-binding time.** D-424 splits decorator handling in two: static validation (name, arity, literal kind, target type) lands with the decorator AST node; *enforcement* — rejecting a supplied value against `@allowed`, `@minLength`, `@maxLength`, `@minValue`, `@maxValue` or `@pattern` — needs a bound parameter value and is Sprint 10 work. Until it lands, a statically valid decorator has no runtime effect whatsoever, which is a weaker state than "unexercised": the script compiles and the constraint silently does not apply. | D-424 | Sprint 10 (parameter binding) | Each of the seven decorators rejects a violating supplied value with the specified diagnostic, exercised by the release-gate script R-08 requires; `@pattern` may land later with `regex` if that increment has not yet run |
| R-16 | **A `type` or `fn` after top-level code has no error code.** §19 requires top-level code to follow all declarations, and D-425's ordering walk classifies a `type` or `fn` appearing after a statement, `const` or `readonly` as a backward step — but only §19 categories 1 and 2 have codes allocated (E2201, E2202), so the walk deliberately stays silent rather than borrowing a code that names something else. `fn f(): int { return 1 }` after `x := 1` therefore compiles clean against a normative rule. Either the rule is narrower than §19's prose reads and the prose should say so, or the condition needs a code of its own. | D-425 | `unowned` — a taxonomy decision, not an implementation gap; the walk already has the hook | §19's prose and the enforced rule agree: either a code exists and `ReportOrderingError` gains its arm with a test per pair, or §19 states that `type`/`fn` placement relative to top-level code is unenforced and why |
| R-17 | **`@secure` and `@pattern` have no error code of their own.** §19's Decorators table assigns E4101 to `@allowed` and E4102 to the four length and value constraints, and D-424 Decision 7 fixes E4102's title at exactly those four. Nothing covers `@secure`'s arity check or `@pattern`'s arity and string-literal-kind checks, so D-425 raises both through E4002 with distinct wording, its stop conditions forbidding a new code. That works, but E4002's title — "decorator not permitted here" — describes a placement error, not an argument one. | D-425 | `unowned` — an error-taxonomy decision | Either §19's table names the code these checks report, or a code is allocated and E4002 narrows back to placement and target-type errors |
| R-21 | **Array parameter element types are declared but not enforced.** `path.joinAll(parts: string[])` and `strings.join(parts: string[])` carry `SignatureType.ArrayOf(string)`, but the checker accepts any array there, as it did before signatures (gate report F4). Only return threading reads `ElementType` (`string.split`'s `string[]`). | D-426, recorded by D-427 | `unowned` | The checker rejects an array argument whose element type does not match the parameter's declared element type, with an existing code or one allocated by the procedure, and a test per enforcing native pins it |
| R-23 | **Nullable named types render as `struct?` in diagnostics.** E0101 on `nd.year` and E0004 on `d.isBefore(nd)` say `'struct?'`, not `'date?'` (gate report F10). Cosmetic and pre-existing; pinned by the probe corpus. | D-426, recorded by D-427 | `unowned` | A diagnostic naming a nullable named type spells it `'date?'`/`'guid?'`, the corpus is updated by that increment and a test pins one case per named type |
| R-24 | **The compile-benchmark allocation gate is red.** `Compile_TenPrints` (+12.4%, 17,872 B) and `Compile_TwoExpressions` (+11.0%, 10,232 B) breach the 10% per-sprint allocation threshold on every *`FunctionSignature`* increment run (groups 4, 7 and 5–6); no baseline was updated (D-313). The breach was present to within 8 bytes on the first run, and the last benchmark run on `main` was `1e44b54` (August), so whether the increment's catalogue path introduced it or `main` carried it in is not established. D-426 Decision 5 condition 3 is therefore recorded as not met as worded. **Origin established (D-430 Decision 10)**: Benchmarks on `windows-latest` on 13 November measured `b202509` at 17,776 B (+11.8%) and 10,200 B (+10.7%), and `main` at `ce0b1ec` at 17,872 B and 10,232 B (runs 43520418431 and 43520422511). The breach predates the increment, which added 96 B and 32 B, so the catalogue path did not introduce it and Sprint 9C is not blocked; it entered between `1e44b54` and `b202509`. The two runs' timings are not comparable (different CPUs); their allocations are. | D-426, recorded by D-427, amended by D-430 | The hardening/performance sprint before Sprint 10, which sweeps benchmark drift in one pass; bisecting `1e44b54..b202509` is its first step (D-430 Decision 10). The `b202509` measurement half is done | A Benchmarks run on the pre-increment base `b202509` establishes the breach's origin (met by D-430), **and** the gate is green on `main` — by removing the allocation, or by a logged decision that re-derives the ceiling (D-313 forbids a silent baseline move) |
| R-25 | **The §10 diagnostic renderer.** The gold masters are written in `grob-v1-requirements.md` §10's format — the code's title, a source snippet, a caret span, `note` and `help` lines, stack-frame chains and `warning[...]` severity — and the CLI prints `error[code]: <call-site message>` with a `line:col-line:col` range and no snippet. `Diagnostic` carries one message string, so the structured `note` and `help` lines have nowhere to come from. `grob-solution-architecture.md` assigns an error formatter to `Grob.Cli`; no sprint schedules it. **Two message defects found by D-429's gate**, both in output the harness does not compare: the CLI never sets `Console.OutputEncoding`, so `type-cycle-no-terminating-field`'s `→` reaches a redirected stderr on Windows as byte 0x1A; and E1004's message suggests a malformed member, "access a member such as 'path..'". | D-428, amended by D-429 | Sprint 12 — the structured `note`/`help` shape recommended as an input to the 9C rebuild, so 9C's new throw sites are not written twice | The CLI renders every diagnostic in §10's format with its title from `ErrorCatalog`; `Diagnostic` carries notes and help as structured data; the harness compares every non-quarantined master byte for byte, with LF line endings, in place of the tuple comparison; a non-ASCII character in a message survives a redirected stderr; E1004's suggestion names a real member |
| R-26 | **The compiler records no source column.** `Chunk` has a column table and the VM passes it to `GrobRuntimeException`, but every emission site in `Grob.Compiler` uses the overload that writes 0, so `--> file:line` is all a runtime diagnostic can show. Seven gold-master pairs wait on it. The `.grobc` source map already carries a column field (`grob-grobc-format.md`), so no format change follows. | D-428 | Sprint 9/10 hardening interlude (D-353) | Every instruction that can raise a runtime error carries the column of its source node's span start; the pairs quarantined against this item pass; a compiler test pins the column for each throwing opcode family |
| R-27 | **Code divergences between the gold masters and the compiler.** For each, the master states one code and the compiler raises another for the same source: E0006 (method undefined on a type — the compiler raises E1002), E0007 (implicit `float` to `int` — E0001), E1101 (shadowing warning — nothing), E1201 (forward reference in a function body — E1001), E1202 (use before declaration in a block — E1001), E0001 for `bool + int` (the compiler raises E0002, whose registry description fits) and E5201 (the master's nil dereference is now rejected statically as E0101, leaving no shown runtime path). **The first five codes have no throw site at all**, the shape D-424 found for the decorator and ordering codes. Also: a decorator before a local binding raises E2001 where the same mistake on a function parameter raises E4002 (D-424 Decision 2). | D-428 | `unowned` — before R-10, since documenting a code the compiler never raises documents a fiction | For each, a decision says which side is right; either the code gains its throw site and its pair passes, or the code is retired under D-410's clarification of ADR-0017 and its pair is rewritten or removed; the decorator case reports one code in both positions |
| R-28 | **Location divergences.** `nullable-interpolation` (the compiler points at `${`, the master at the interpolated expression), `unterminated-string-literal` (the lexer reports the column after the opening quote, the master the quote itself) and `type-cycle-no-terminating-field` (the compiler points at the cycle's first field, the master at the field that closes it). | D-428 | `unowned` | A decision fixes each convention, the losing side changes, the three pairs leave the quarantine and a test pins each |
| R-29 | **Cascades against D-300's one diagnostic per root cause.** A missing `{` after a function signature yields five diagnostics (E2014, E2001 twice, E2203, E1001); a local named after a core module yields E1102 citing a declaration at "line 1" plus E1004 on its use; `@allowed` given an undefined name yields E1001 plus E4101; and, measured by D-429's gate, `nil-dereference-at-runtime` yields E0101 plus an E0005 on the `unknown`-typed value it returns. **Two more shapes, measured by D-430 on the public samples**: `formatAs`'s bespoke `table`, `list` and `csv` arms report E0004 ("cannot determine its columns") on an `unknown` receiver, chained or function form — `print(ys.formatAs.table())` with `ys` undefined yields E1001 plus E0004; and a member read off an untyped lambda parameter over an `unknown` receiver passes `unknown` into a typed native parameter — `ys.filter(b => date.parse(b.when) < date.today())` yields E1001 plus E0004. Five public samples are blocked on it: D-430 counted six, but W9's correction removed `formatAs.md` block 3's cascade (D-437). | D-428, amended by D-429, D-430 and D-437 | `unowned` | Each case yields one diagnostic at its root cause and is pinned by a test; `unexpected-token-missing-brace` leaves the quarantine; `nil-dereference-at-runtime` reports no E0005; the two `unknown` shapes report only the E1001, and the public samples carrying `blocked-by=R-29` lose the token |
| R-31 | **Undefined type names in annotations are accepted silently.** `x: Foo := 1`, `fn f(r: Foo): int`, `fn f(): Foo`, `xs: Foo[] := [1]` and `(a: Foo) => a` all compile and run with `Foo` undeclared; `map<string, Foo>` surfaces only as an E0004 naming `'unknown'`. Field annotations report E1001 through `ResolveNamedFieldType`; the other positions resolve through a path that returns `GrobType.Unknown` without a diagnostic. Seen in the corpus by D-429's gate: `ambiguous-type-reference` (quarantined for Sprint 11) raises only an E0005, because `r: Response` resolves silently and the return then mismatches `unknown`. **A sixth position, named by D-430**: a type argument, as in `mapAs<Repo[]>()` with `Repo` undeclared, which eight public samples write. It cannot be measured until `mapAs<T>` lands, since D-426 fences type parameters. D-430 also measured that no public sample annotates an undefined type in the five positions above, so R-31 does not join the run. | D-428, amended by D-429 and D-430 | Sprint 9/10 hardening interlude (D-353) — recommended first in it | Every annotation position reports E1001 at the type reference, as the field position does, with §3.1.1's sentinels on the error path and a test per position; the type-argument position is included once `mapAs<T>` makes it reachable |
| R-33 | **`grob-lang.dev`'s samples are outside every gate.** The site's source is another repository, so the public samples gate (D-430) cannot see it. Measured by D-430 on 13 November: its one sample, `organise.grob`, calls `fs.list`, `fs.ensureDir`, `file.moveTo` and `path.join`, does not compile today because `fs` is not built, and is shown in a terminal printing "47 files organised" from a script that has no `print`. D-417's principle — every public sample compiled by a gate — does not yet reach it. | D-430 | `unowned` — the site's repository | Every Grob sample the site shows is either generated from a block this repository's public samples gate checks, or checked by a gate of the site's own against a released `grob`; any output the site shows is what the script prints |
| R-34 | **No release-gate script exercises `regex`.** None of the eleven validation scripts uses a regex literal or the `regex` module. D-186 required a `regex.compile()` script only if regex literals were cut; D-432 keeps them and requires a script anyway, on R-08's footing — surface that would ship unexercised by the release gate. The script is the log filter that `grob-v1-requirements.md` §14's row 12 described without a script behind it. It reads a log through `fs`, so it lands with whichever of the two increments comes second. | D-432 | Whichever of the `regex` and `fs` increments lands second | A validation script in `grob-sample-scripts.md` and its corpus fixture, guarded by `ValidationScriptCorpusTests` and D-417's sync guard, uses a regex literal, `regex.compile`, `matchAll`, a named group and `replace`, and runs green with expected output |
| R-35 | **Every `fs` and `process` native reaches the OS through an injected capability.** D-319 sketched `IFileSystem` and `IProcessRunner`; D-343 landed the mechanism; D-430 gives `IFileSystem` its first member. Sprint 9C builds the two modules through which a script changes the machine, and D-435's restricted run and plan mode are an alternate host only if they are built through the seam. | D-436 | Sprint 9C — the `fs` and `process` increments | `IFileSystem` carries the `fs` surface and `IProcessRunner` the `process` surface, both in `Grob.Runtime`, each member one operation that either reads state or changes it; `json` and `csv` file reads and writes go through `IFileSystem`; no type in `Grob.Stdlib` calls `System.IO.File`, `Directory`, `FileInfo`, `DirectoryInfo`, `FileStream` or `System.Diagnostics.Process` directly, and R-32's source guard is widened to keep it so; the `fs` tests run over an in-memory `IFileSystem` and the `process` tests over a scripted `IProcessRunner` |
| R-36 | **The agent-facing CLI surface.** `grob check --json`, `note`, `help` and structured fixes on `Diagnostic`, foreign-idiom diagnostics and `grob skills` (`grob-agentic-strategy.md` §5 and §6). | D-434 | Sprint 12, after OQ-019 and OQ-020 resolve and R-25, R-26 and R-29 close; OQ-027 may move the foreign-idiom diagnostics into the agent-scripting interlude (D-444) | `grob check --json` emits one document conforming to the schema OQ-019's resolution fixes, pinned by a test over the error-examples corpus; every diagnostic in it carries a column; each foreign idiom that resolution lists has a gold master showing the Grob form; `grob skills` prints the kit OQ-020's resolution specifies, and every sample in it compiles under the public-samples gate |
| R-37 | **The agent benchmark exists and has run.** "First-class for agents" is a claim until it is measured, and D-434 Decision 4 makes the benchmark the only evidence that authorises a syntax change on these grounds. | D-434 | `unowned` — after R-36, once OQ-021 resolves | A committed task set covering the four driving use cases; a recorded run against at least two harnesses given only the kit, reporting first-attempt success and success within three rounds of `grob check --json`; results and the date committed under `docs/design/` |
| R-38 | **Restricted run and plan mode built.** D-435 sets the posture and the limit of the claim; the surface is OQ-022. | D-435 | `unowned` — post-v1, after OQ-022 resolves | A run mode in which every standard-library call that needs a capability the host did not grant fails before it acts, as does a plugin import the host did not grant; proven by tests over the real CLI pipeline that cover at least an `fs` read, an `fs` write, a `process` run, a `json` and a `csv` file read and write and a plugin import, each denied and each granted; a plan run that reports each state change made through the capability seam without performing it, with plugin imports under a plan run handled as OQ-022's resolution fixes and that handling proven by test; documentation that describes the feature as a capability-gated standard library, states what it does not protect against and does not use the word sandbox |
| R-39 | **The README's first sample becomes runnable before parameters bind.** `README.md`'s opening block reads the required parameters `from` and `to` and the defaulted `dir`, and renames files it lists through `fs.list`. It passes the public samples gate only because `fs` is excused (D-430 Decision 3). When the `fs` increment builds `fs`, the excused list's catalogue guard forces `fs` off it and the block is run. It then fails on `from`, since no `param` binds before Sprint 10 (D-430 measured E1001 on a parameter with or without a default), so that increment marks it `blocked-by=R-39`. Its file operations must meet the harness's in-memory `IFileSystem`, which R-35 (D-436) provides, never the real filesystem. Gate report F6. | D-437 | The 9C `fs` increment, which meets it first; Sprint 10 for parameter values | The gate supplies fixture values for `param` declarations as D-430 Decision 8 does for environment variables, and `README.md`'s first block passes under the harness's injected capabilities with no `blocked-by` token |
| R-40 | **The synthetic large-script generator is unbuilt.** `grob-benchmarking-strategy.md` §4.1 and §7.4 specify a deterministic generator for a `synthetic-large.grob` compile benchmark. `bench/Grob.Benchmarks/` has no `Generators/` directory and `Compile/` holds only `CompileBenchmarks.cs`. D-409 raised it; D-431 Decision 1 row 5 sent it here once the corpus sweep's gate confirmed no row tracked it. | D-409, recorded by D-438 | The hardening and performance sprint before Sprint 10 | `bench/Grob.Benchmarks/Generators/SyntheticLargeGenerator.cs` produces a deterministic `synthetic-large.grob` of 1,000 lines or more from a fixed seed, and a compile benchmark runs over it |
| R-41 | **A `map<K, V>` argument is not checked against a `map<K, V>` parameter.** Passing a `map<string, int>` to `fn f(m: map<string, string>)` compiles and runs, where the array mirror raises E0004. A binding does enforce `V` (E0001), and `for k, v` binds `v` as `V`. Measured by the corpus sweep's verification of review F5-1 against D-374; `grob-type-registry.md` carries the build-status note. | D-438 | `unowned` | The call raises E0004 at the argument, mirroring arrays, and a test pins it |
| R-42 | **A non-unary `sort` key selector reaches the VM.** `xs.sort((a, b) => a.size > b.size)` passes the checker and fails at run time with "internal error: opcode GetProperty 'size' on receiver of kind Int not yet implemented". `sort` takes a key selector (`grob-type-registry.md`), and under D-353's contract an internal error on user input is P0. Found by the corpus sweep's eye-read of the wiki's `fragment` blocks (`Functions.md`, since corrected). | D-438 | `unowned` — a candidate for the Sprint 9/10 hardening interlude (D-353) | A lambda of the wrong arity passed as a key selector is a compile-time diagnostic under an existing code (E0003 or E0004, by `allocating-an-error-code`'s fold-or-register judgement), and a test pins it |
| R-43 | **Diagnostics render generic types bare.** E0001 and E0004 name a `map<string, int>` as `'map'` and a `string[]` as `'array'`, so a message can read `'map'` to `'map'`. Cosmetic and pre-existing, the generic-type counterpart of R-23. Seen in passing by the corpus sweep's F5-1 probes. | D-438 | `unowned` | A diagnostic naming a generic type renders its type arguments (`map<string, int>`, `string[]`), with a test for each of E0001 and E0004 |
| R-45 | **Release artifacts are never executed before they are published.** `release.yml` publishes `win-x64` and `linux-x64` as self-contained, single-file, trimmed builds and packages each without running it. Trimming removes code the build cannot see being used, so a tree that passes every test can still ship an executable that fails to start, on either platform. | D-442 | `unowned` — before the first public release | `release.yml` runs each published artifact on its own platform before packaging it — at least `grob version` and a script that prints, reads a file and runs a process — and the release fails if either artifact fails |
| R-46 | **The corpus still describes a Windows-only language.** D-442 makes Windows and Linux equal, and changed only the requirement, ADR-0007 and the strategy. Every example in `grob-stdlib-reference.md`, `grob-language-fundamentals.md`, `grob-sample-scripts.md` and `docs/wiki/` that carries an absolute Windows path, `grob-install-strategy.md` throughout, the Windows-only environment matrix in `grob-adversarial-testing-strategy.md` and the `grob.exe` naming in `grob-v1-requirements.md` predate it. | D-442 | `unowned` — before the documentation site is built | Every example follows D-442 Decision 3 and still passes the public-samples gate; `grob-install-strategy.md` covers Linux as OQ-025's resolution fixes; the adversarial environment matrix names a Linux distribution; each standard-library function whose behaviour differs by platform says so in `grob-stdlib-reference.md`; the executable is named without `.exe` wherever the text is not Windows-specific |
| R-47 | **A lambda parameter's type annotation is ignored.** `(m: int) => m + 1` parses, but `VisitLambda` registers every parameter as `Unknown`, or as the type a method's function shape expects (D-443), and never reads the annotation, so a wrong annotation passes silently and a right one adds nothing. | D-443 | `unowned` | A lambda parameter's annotation types it; an annotation that disagrees with a function shape's parameter type is a diagnostic, with tests for each |
| R-48 | **`grob run` cannot take source from stdin or inline.** It takes a file path only, so an agent must write a file before running a short script it will discard. D-444 makes both forms v1. | D-444 | The agent-scripting interlude, after OQ-026 resolves | Both forms OQ-026's resolution fixes run a script through the same front end and VM path as a file; diagnostics name the source as that resolution says and are otherwise identical to the file form, pinned by tests that run one error example both ways; exit codes 0, 1 and 2 match the file form; stdin's state inside a script that arrived on stdin is pinned by a test; the tests run on Windows and Linux in CI |
| R-49 | **Nothing measures the start-up requirement against a release artifact.** `grob-v1-requirements.md` §1 requires `grob run hello.grob` under 500 ms cold and 200 ms warm. `release.yml` publishes single-file trimmed builds without ReadyToRun or Native AOT, and no job times them. An agent pays start-up on every script. | D-444 | The agent-scripting interlude | `hello.grob` timed cold and warm on the `win-x64` and `linux-x64` release builds, method and figures recorded in `grob-benchmarking-strategy.md`; where either breaches §1, a logged decision chooses the remedy (ReadyToRun, Native AOT or a narrower change) and the figures are re-taken after it; where neither breaches, the entry records that. A re-taken figure that still breaches §1 does not hold R-49 open: R-49 closes, and a new register item opens, owned by the v1 release gate, done when both figures meet §1 on both release builds |
| R-50 | **No guide teaches an agent to script in Grob.** `.claude/skills/writing-grob-source` is written for contributors editing `.grob` files in this repository, not for scripting with the standard library. | D-444 | The agent-scripting interlude | A repository skill for scripting with Grob covers `fs`, `process`, `json`, `csv`, `regex`, `io`, `env` and `path`, the inline and stdin forms R-48 builds and paired examples translating short PowerShell and Python scripts into Grob; every sample in it compiles under the public-samples gate (D-430); it states that it is the seed of OQ-020's kit |
| R-51 | **Nothing tells Claude Code to script in Grob, or on which binary.** | D-444 | The agent-scripting interlude, after R-48 and R-50 | The repository `CLAUDE.md` tells Claude Code to prefer Grob for scripts it writes to do a job, points at R-50's skill, keeps gating scripts out of Grob until Pillars 1–6 close (D-444 Decision 5) and states the permission limit D-444 records; a tooling script provisions a Grob binary built from `main`, not from the working tree, on Windows and Linux, and the rule names it |
| R-52 | **Two `fs` failures have no error code.** D-150 throws `IoError` when a copy or move finds its destination and `overwrite` is false, and D-109 has `fs.delete` refuse a directory that is not empty. Neither has a code of its own. Only E5305, the residual, would catch them, and its description says the bar for raising it stays high because specific failures get specific codes. D-445 also leaves two cases to the increment's budget: the error for a `File.rename` whose name contains a separator, and the error when the current directory on Linux lies beneath a name that contains `\` (D-445 Decision 7). A design session cannot mint a code (D-316). | D-445 | The 9C `fs` increment | `ErrorCatalog.cs` and `grob-error-codes.md` carry a code for "destination already exists", raised by `fs.copy`, `fs.move`, `File.copyTo` and `File.moveTo` under `overwrite: false` and by `fs.createDir`, and a code for "directory not empty", raised by `fs.delete` and `File.delete`, both minted in the increment's commit under D-316; the rename case and the current-directory case are each folded or minted by a ruling the increment's decision records; `grob-stdlib-reference.md`'s failure table names the codes in place of its two R-52 cells |
| R-53 | **Three platform facts about `fs` are stated nowhere because nobody has measured them.** What `File.created` holds on a Linux file system that records no creation time; what moving a directory to another volume does on each platform; and how a link is reported — its `isDirectory`, its `size` and what a broken one returns. D-442 Decision 1 requires a function's documentation to state where behaviour differs by platform, and D-445 would not write down a guess. | D-445 | The 9C `fs` increment | Each of the three is observed on `windows-latest` and `ubuntu-latest` by a test over the OS-backed `IFileSystem`, and `grob-stdlib-reference.md`'s `fs` section states the observed behaviour in place of its reference to R-53 |
| R-54 | **The wiki describes the April `fs`.** `docs/wiki/Standard-Library/fs.md` and `docs/wiki/Type-Registry/File.md` list fourteen functions, `File` methods returning `void` and no `overwrite` parameter, and every example is a `C:\` path. The wiki shows what is built (D-440), so it changes with the increment that builds `fs`, not with the decision. | D-445 | The 9C `fs` increment | Both pages match D-445's surface as built; every example follows D-442 Decision 3 and passes the public samples gate (D-430) with `fs` off the excused list |
| R-56 | **Array methods, map methods, `print`, `exit` and calls through a function value have no parameter names, so they cannot take a named argument.** Array and map instance methods, `print` and `exit` have no `FunctionSignature` (`input` has one, and binds), and a function type carries no parameter names (D-326). Since D-452 each of them refuses a named argument with E0011, while every callable with a signature binds by D-113: `xs.insert(0, value: v)` is an error while `date.parse(text, pattern: "dd/MM/yyyy")` binds, a difference a script's author has no way to predict. Array `sort` is the one exception: it binds `descending` from a one-entry view and refuses any other name (D-452 Decision 2). | D-447 | `unowned` — it needs a decision on signatures for generic receivers first | For each of the four kinds — array methods, map methods, `print` and `exit`, and calls through a function value — either it gains parameter names and binds by D-113 as every other callable does, or a decision records why it stays positional and `grob-language-fundamentals.md` §12.2 states the reason; the row does not close while any of the four is unaddressed |
| R-57 | **Some calls through a function value check no arguments, and the VM does not check a call's argument count.** D-448 checks a function value's arguments, E0003 for the count and E0004 for each type, where the checker knows the callee's function type: a parameter, a binding whose type is a function type, a grouped callee, a lambda called where it is written, a call's result and a named struct's field declared with a function type. It does not know the type of a declared function held in a binding (`h := twice`), of an anonymous struct's field or of an array element, and those calls check nothing. The VM's `Call` arm sets the new frame's base from the count the call passed and never compares it with the callee's arity, for a closure or for a bytecode function. Such a wrong call runs: with `h := twice`, where `twice` declares a local `y := x * 2` and returns it, `h(3, 100)` prints `100`, because the surplus argument sits in the slot the compiler gave `y`. A lambda's argument types wait on R-47, which types its parameters. Where the arguments are checked, a function type (D-326) records each parameter's kind but not an array parameter's element type or a struct parameter's name, so a `string[]` passes for an `int[]` and one struct for another. The crash oracle's first campaign met three more shapes, measured through `grob run` at `3dd32fb`: `bx := #{ v: #{ count: 1 } }.v` then `bx.count()` stops with an internal error, `Call target is not a function (kind: Int)`, and a lambda's parameter called as a function, `arr.each(x => x())`, does the same; and a switch whose arms are a one-parameter lambda and a two-parameter function, called as `k(4)`, stops with `GetSlot: slot 2 out of range (stack top 2)`, the count unchecked (D-451). | D-448 | `unowned` | A call through a declared function held in a binding, through an anonymous struct's field and through an array element of function type reports E0003 for a wrong count and E0004 for a wrong argument type, each with a test asserting line and column; a call through a function value rejects with E0004 an array argument whose element type, or a struct argument whose name, differs from the parameter's, as a direct call does; the VM's `Call` arm refuses, rather than runs, a call whose argument count differs from a closure's or a bytecode function's arity, with a test for each, and the benchmark gate shows no regression from the check; and a call through a value that is not a function, `bx.count()` with `count` an `int` and `x()` in `arr.each(x => x())`, gets a diagnostic or a registered runtime error, not an internal error, with a test for each |
| R-58 | **An `int` where a `float` is expected is accepted but not widened in some positions.** The checker accepts an `int` wherever a `float` is expected, but the compiler emits `IntToFloat` only for an arithmetic or comparison operand and for a ternary or switch arm that unifies to `float`, so the value arrives as an `int` and the first `float` operation on it stops with an internal error, `GrobValue kind mismatch: expected Float, actual Int`. Measured through `grob run`: `x: float := 1` then `x / 2`, and `fn add(a: float, b: float): float` called as `add(1, 2)`, both on `main` at `67b268d`; a `float` return of `1`, a `float` struct field given `1`, a parameter default `x: float = 1`, `math.sqrt(4)` and a call through a function value with a `float` parameter, at `cbf6203`. D-452 widens an argument bound to a `float` parameter of a script function or a native, so `add(1, 2)` and `math.sqrt(4)` now run; the other positions remain. | D-448 | `unowned` | Every position where the checker accepts an `int` for a `float` — a binding, an assignment, a return, a struct field, a parameter default, or an argument to a function value — passes a `float`: a compiler test per position asserts the `IntToFloat`, and an integration test per position runs the program to its `float` result |
| R-60 | **The array methods that take a function do not check that argument.** `filter`, `select`, `sort` and `each` accept any value where they call a function, so a wrong one reaches the VM and stops with an internal error, `InvokeCallable: value of kind Int is not callable`. Measured through `grob run` at `e9e5dbc`: `xs.filter(5)`, `xs.select(5)`, `xs.sort(5)` and `xs.each(5)` each stop so, and so does `xs.select(f)` with `f: (fn(int): int)? := nil`. Of the four, only `filter` checks its function argument at all: a lambda predicate whose inferred return is known and is not `bool` is E0004. A named-type method that takes a function is checked against its shape (D-443), so `r.replace("abc", f)` with a nullable `f` is E0004 there. Array methods have no `FunctionSignature` (R-56). The crash oracle's first campaign found that `filter`, `select` and `each` check no argument count either: `arr.filter()`, `arr.select()` and `arr.each()` type-check and stop with an unhandled `IndexOutOfRangeException` from `ArrayNatives`, reported as E9001, and `arr.filter(x => x > 1, 5)` runs with the surplus argument ignored, measured through `grob run` at `3dd32fb`; `sort` and `contains` report E0003 (D-451). | D-449 | `unowned` | `filter`, `select`, `sort` and `each` refuse an argument that is not a function, and a nullable function, with E0004 at the argument, and a wrong argument count with E0003, each with a test asserting line and column; an integration test shows each still runs with a lambda and with a function-typed binding |
| R-62 | **`fn(): void` is a type everywhere but a struct field.** A binding `h: fn(): void` and a parameter `f: fn(): void` are accepted and run, but a struct field `f: fn(): void` is E1001, `'void' is not defined`. Measured at `e9e5dbc`. No document shows `void` as a function type's return, so which behaviour is intended is not recorded. | D-449 | `unowned` | A decision records whether `void` may be a function type's return; every position that takes a type annotation — a binding, a parameter, a return, a struct field and a nested function type — then follows it, with a test for each |
| R-63 | **An operand of unknown type compiles to an assumed type.** A lambda with no type context, such as `f := (n) => n + 2.5`, has parameters of unknown type. D-450 has the compiler assume such an operand has the other operand's type, and assume `int` when both are unknown, so a call whose argument has another type stops with an internal error. Measured through `grob run` at `c2ea052`: `(n) => n + 2.5` given `1` stops with `GrobValue kind mismatch: expected Float, actual Int`, where `int` widens to `float` everywhere else; `(n) => n + 1` given `1.5` with `expected Int, actual Float`; `(a, b) => a + b` given two strings or two floats, and `(a, b) => a < b` given two strings, with `expected Int`. `(a, b) => a + b` given `1` and `2` prints `3`. Nothing at compile time says which type the operand has, and no opcode widens a value only when it is an `int`. R-47 does not cover it: these parameters carry no annotation to read. The crash oracle's first campaign met a binding named in its own lambda as such an operand: `wrap := (s) => "[" + wrap` then `wrap(1)` stops with `GrobValue kind mismatch: expected String, actual Function`, measured through `grob run` at `3dd32fb` (D-451). | D-450 | `unowned` | A decision records how an operand of unknown type is compiled — the parameter types inferred from the lambda's calls, the lambda refused without a type context or the operation chosen at run time — and a test per arithmetic and relational operator shows a call given `int`, `float` and `string` arguments either runs to the right result, `int` widened to `float` where the other operand is a `float`, or is refused at compile time with a diagnostic; and the campaign's reproducer, `wrap := (s) => "[" + wrap` then `wrap(1)`, gets a diagnostic or a registered runtime error, not an internal error, with a test |
| R-65 | **A nullable named-type method parameter rejects `nil` and a nullable value.** `CheckNamedTypeArgumentType` compares an argument against a named-type method's parameter type without the parameter's nullability, so a parameter declared `T?` refuses `nil` and a `T?` binding with E0004. No first-party named-type method has a nullable parameter yet, so no shipped call fails; `File` is the first type expected to register one. | D-452 | The 9C `fs` increment, where `File` first registers a nullable parameter | A named-type method parameter of type `T?` accepts `nil`, a `T` and a `T?`, and refuses another type with E0004 at the argument, each with a test asserting line and column |
| R-66 | **A guarded primitive method call on a non-nil nullable value faults the VM.** `ns: string? := "ab"` then `print(ns?.padLeft(5))` type-checks and stops with `internal error: opcode GetProperty 'padLeft' on receiver of kind String not yet implemented`. Measured through `grob run` at `9c5ff8d`. The `?.` arm emits a run-time property lookup, which a primitive does not have: a primitive method is compile-time sugar for a qualified native call (D-066). Found by the native named arguments gate (report §1.4); D-452 changed the call's bytes (the default is now filled) but not the fault. The crash oracle's CI slice (D-451) hits it from seven raw literals in the compiler tests: `?.` calls on `string?`, `int?`, `float?` and `bool?`, and the property read `s?.length` on a non-nil `string?`, which takes the same run-time lookup. | D-452 | `unowned` | `ns?.padLeft(5)` prints `   ab` and a nil `ns` gives `nil`, for each primitive receiver type, with a compiler test asserting the emitted bytes and an integration test running it |
| R-67 | **A member call on a function value reports no E1002 and faults the VM.** `fn g(x: int): int` then `h := g` and `h.frobnicate(1)` type-checks and stops with `internal error: opcode GetProperty 'frobnicate' on receiver of kind Function not yet implemented`. Measured through `grob run` at `9c5ff8d`. `DispatchMemberAccessCall` has no arm for a receiver of known type with no member surface and returns `Unknown`, so the unknown member is not reported. `b.frobnicate(1)` on a `bool` is E1002, through the primitive surface. A property read on a function value takes the same path: `print(addOne.length)`, with `addOne` a declared function, stops with `opcode GetProperty 'length' on receiver of kind Function not yet implemented`. Found by the crash oracle's mutator at `40ae287`. | D-452 | `unowned` | A member call on a receiver of known type with no member surface, a function value included, is E1002 at the member, with a test asserting line and column; a receiver of unknown type stays permissive |
| R-68 | **A constant expression that divides by zero or overflows crashes the compiler.** `const x := 1 / 0` and `const x := 1 % 0` stop with an unhandled `GrobInternalException` from `Compiler.FoldDivisiveConstant`, and `const m := -9223372036854775807 - 1` then `const x := m / -1` stops with an unhandled `System.OverflowException`. The checker accepts all three. Measured through `grob run` at `acd3bec` by the crash oracle's gate (report §1, F-1): a .NET stack trace and exit `0xE0434352`. | D-451 | `unowned` | Each of the three reports a compile-time diagnostic with a registered code at the operator, with a test asserting the code, line and column, and the oracle reports no finding for any of them |
| R-70 | **The parser has no depth guard.** Nesting `x := ((((…1…))))` overflows the stack between 200 levels, which run, and 500, which crash, on the CLI's main thread. A `StackOverflowException` cannot be caught, so the process dies with `Stack overflow.` and exit `0xC00000FD`. The adversarial strategy's §3.2 expected this. Measured at `acd3bec` (gate report §1, F-3). | D-451 | Sprint 9/10 hardening interlude (D-353's strategy, §11) | Nesting past a documented limit is a parse diagnostic with a registered code, with a test at the limit and one past it on a 1 MB stack |
| R-71 | **E0206 leaves the receiver of an optional assignment target without a `Declaration`.** `c?.port = 9090` reports E0206 and leaves the identifier `c` with a null `Declaration`, a §3.1.1 violation (D-311). The raw literal in `CompilerFieldAccessTests.FieldAssign_OptionalTarget_WithTcErrors_EmitsNoSetProperty` hits it. The oracle checks `Declaration` alone: `ResolvedType` is an enum whose default, `Unknown`, is also a legal type, so an unset one cannot be detected without a source change. Measured by the crash oracle at `acd3bec` (gate report §1, F-4). Its first campaign found the same in every optional target: a compound assignment, `p?.x += 1`, an increment, `p?.x++`, and a nested receiver, `p.address?.city = "Paris"`; and an identifier in the assigned value is left so too, `bx?.op = ops[0]` and `c?.count += one` (D-451). | D-451 | `unowned` | The checker resolves the receiver of an optional assignment target and the identifiers in its value, in an assignment, a compound assignment and an increment, a checker test asserts `Assert.Same` on each `Declaration` after E0206, and the slice's known entry is removed |
| R-73 | **`exit(n)` outside `0..255` reaches the host unchanged.** `exit(300)` exits `300` on Windows and `44` under Git Bash, and `exit(-1)` exits `-1`. D-353's strategy (§1) makes this a P1: the runtime must clamp or diagnose. D-451 records it without choosing. `exit(5000000000)` overflows the `checked((int)code)` cast and is reported as `E5001 integer overflow`, a registered but misleading error. Measured at `acd3bec` (gate report §1, F-6). | D-451 | `unowned` | A decision chooses clamping or a diagnostic, a test on each side of the range pins it, and the oracle's `ExitRange` class follows the decision |
| R-74 | **AST node ranges are not uniform.** `Parser.RangeFrom` ends 41 node kinds at the start of the next token, every statement, `TypeRef`, `Parameter`, `CallArgument` and `LambdaExpr` among them, so `date?` in `nd: date? := nil` has the range of `date? :`. A single-parameter lambda `x => x > threshold` ends at the start of its last token. The gate's census called binary expressions and calls exact, but the mutator's range tests (crash oracle, group 4) measure otherwise: a binary expression and a call end at the start of their last token (`1 + 2` ends at the `2`, `g()` at the `)`), and a literal or identifier has a point range, so none of them ends where its last token does. Diagnostics print `Range.Start`, so a printed location is right, but a printed span can run one token long or end a token short. Measured by a range census over every seed that parses, at `acd3bec` (gate report §1, F-7). | D-451 | `unowned` | Every node's `Range.End` is the end of its last token, with a parser test per node kind |
| R-75 | **A value that cannot be indexed is accepted by the checker and stops the VM.** `one := 1` then `print(one[0])` type-checks and stops with an internal error, `GetIndex on receiver of kind Int is not supported`. A `bool` and a `float` stop the same way, and so does a named-type value: `g := guid.newV4()` then `print(g[0])` stops with `... kind Struct ...`. An indexed compound assignment, `one[0] += one`, takes the same path. Measured through `grob run` at `40ae287`; the crash oracle's mutator reaches it by swapping an identifier or a value into an index position (D-451). The first campaign found that a `string` and a `float` stop the same way: `print("abc"[0])` with `... kind String ...` and `arr := 1.5` then `print(arr[0])` with `... kind Float ...`, measured through `grob run` at `3dd32fb`. | D-451 | `unowned` | An index on a receiver of a type that has no index, a primitive and a named type among them, is a compile-time diagnostic with a registered code at the index, with a test asserting line and column; the oracle reports no finding for any of them |
| R-76 | **A member read on a value of unknown type that lacks the member stops the VM.** `id := #{ v: guid.newV4() }.v` then `print(id.nope)` type-checks and stops with an internal error, `GetProperty: guid has no member 'nope'. Type checker should have rejected this before emission.`, where the same read on `id := guid.newV4()` is E1002 at the member. Reading a value through an anonymous struct's field gives it an unknown type (R-57), the checker permits any member on a value of unknown type, and the VM then treats the missing member as a broken invariant, not as a Grob runtime error a script could catch. Measured through `grob run` at `40ae287`; the crash oracle's mutator reaches it by routing a value through an anonymous struct (D-451). The first campaign found that a member the type does have stops the same way when it is a method the compiler lowers, or a property a script cannot write, measured through `grob run` at `3dd32fb`: `d := #{ v: date.now() }.v` then `d.toString()` stops with `GetProperty: date has no member 'toString'`, `d.year += 1` with `SetProperty: struct 'date' has no field 'year'`, and `r := #{ v: /a/ }.v` then `r.test()` with `GetProperty: Regex has no member 'test'`; a `Match` from a routed array stops so too. | D-451 | `unowned` | A decision records what a member read, a member call and a member assignment on a value of unknown type do when the VM cannot resolve the member: a registered runtime error, or a diagnostic once the checker knows the type; a test pins each, and the oracle reports no finding for the reproducers |
| R-81 | **A `return` inside a `try` whose `finally` holds a `try`/`finally` crashes the compiler.** `VisitReturn` compiles each enclosing finally body inside a `foreach` over `_tryFinallyContexts`, and a `try`/`finally` in that body adds to the same list, so the compile stops with `InvalidOperationException: Collection was modified after the enumerator was instantiated`, reported as E9001 since D-451. Measured through `grob run` at `3dd32fb`: `fn g(): int` whose body is a `try` that returns `6`, with a `finally` holding an empty `try`/`finally`; with `catch` in place of the inner `finally` it compiles and prints `6`. Found by the crash oracle's first campaign in a generated program (D-451). | D-451 | `unowned` | The program compiles and prints `6`, running the inner finally once on the return path, with a compiler test asserting the emitted finally bodies and an integration test; the oracle reports no finding for the reproducer |
| R-82 | **A named function used as a value has type `unknown`.** The checker registers a top-level `fn` name as `unknown` in pass 1 and `VisitFnDecl` leaves it so, so every check on a bare reference passes or fails for that reason. Measured through `grob run` at `3dd32fb`: with `fn g(): int`, `x := g + 1` type-checks and stops with an internal error, `GrobValue kind mismatch: expected Int, actual Function`, where it should be E0002, and a function's own name inside its body, `return f + a`, does the same; and `apply(twice, 4)` against a parameter `g: fn(int): int` is refused with E0004, `'unknown' is not assignable to parameter 'g' of type 'fn'`, a well-typed program refused. R-57's unchecked call through `h := twice` follows from it. Found by the crash oracle's first campaign, and met by its generator (D-451, decision AH). | D-451 | `unowned` | A bare reference to a named function has the function's type: `g + 1` is E0002 at the operand, with a test asserting line and column, and `apply(twice, 4)` type-checks and prints `8`; R-57's binding case is measured again |
| R-83 | **A function with a return type can end without returning, and then returns `nil`.** No check requires a function whose return type is not `void` to return on every path, and no document says whether it must. Measured through `grob run` at `3dd32fb`: `fn f(n: int): int` that returns only inside `if (n > 0)` prints `nil` for `f(0)`, a non-nullable `int` holding `nil` with no error; and `fn f(n: int): int {}` then `f(0) + 1` stops with an internal error, `GrobValue kind mismatch: expected Int, actual Nil`. E0005 is a return of the wrong type, not a missing one. Found by the crash oracle's first campaign, whose `delete-statement` operator removes a final `return` (D-451). | D-451 | `unowned` | A decision records whether a function with a return type other than `void` must return on every path, and how one that does not is reported; the checker or the runtime enforces it with a test, and the oracle reports no finding for the reproducers above |
| R-84 | **Assigning a member of `nil` stops the VM, where reading one is a runtime error.** `c := nil` then `print(c.x)` raises E5201, `nil dereference: cannot access member on nil value`, but `c.x = 5` type-checks and stops with an internal error, `SetProperty on non-struct receiver of kind Nil`. Measured through `grob run` at `3dd32fb`. Found by the crash oracle's first campaign (D-451). | D-451 | `unowned` | A member assignment on `nil` raises E5201 as a member read does, with a VM test; the oracle reports no finding for the reproducer |
| R-85 | **More than 255 global names stop the compiler.** A global name's index is a one-byte operand, and the names share the top level's constant pool with its literals, so a script of 300 bindings stops the compile with `Bytecode operand overflow: global name 'v128' index 257 exceeds the 1-byte limit of 255. Wide-operand opcodes are planned for a future sprint.`, reported as E9001. Measured through `grob run` at `3dd32fb`. The plan the message names is recorded in no document. A local's slot, an argument count and an array literal's length are one-byte operands too. Met by the crash oracle's generator, which keeps each program to 100 names for it (D-451, decision AG). | D-451 | `unowned` | A decision records whether the one-byte operands widen (an opcode change under ADR-0013) or a program past them gets a registered compile-time diagnostic; a 300-binding script gets that, not an internal error, with a test |
| R-86 | **An annotated `int` binding and an inferred one make different element types.** With `a: int := 5` and `b := 0`, `n > 0 ? [b] : [a]` is E0002, `Ternary arms must produce the same type: array element types do not match`, and with `v26: int := v25`, `[[v26, 2], [2, v25]]` is E0001, `Array literal elements must share a nested element type`. Both programs are well typed. Measured through `grob run` at `3dd32fb`. Met by the crash oracle's generator, which writes ternaries and switch expressions over scalars only because of it (D-451, decision AE). | D-451 | `unowned` | Both programs type-check and run, with checker tests |
| R-87 | **Whether a ternary or an array literal unifies `nil` with `T` to `T?` is not recorded.** `x: float? := false ? nil : 0.0` is E0001, `Ternary arms must produce the same type; found 'nil' and 'float'`, and `[nil, 1]` is E0001, `Array literal elements must share a type; found 'nil' and 'int'`, while a binding annotated `float?` accepts `nil` and `0.0` alike. Measured through `grob run` at `3dd32fb`. Met by the crash oracle's generator (D-451). | D-451 | `unowned` | A decision records whether a ternary, a switch expression and an array literal unify `nil` with `T` to `T?`, and the checker follows it with tests |
| R-88 | **A struct copied into a new binding, or chosen by a switch expression, loses its type's name.** After `a := T { f: 1 }` and `b := a`, `b.f` is `unknown`, so `U { g: b.f }` with `g: int` is E0001, `Cannot assign value of type 'unknown' to field 'g' of type 'int'`, and `ys: T[] := [b]` is E0001, `Cannot assign value of type 'array' to binding of type 'array'`. A switch expression whose arms are `T` values does the same. Measured through `grob run` at `3dd32fb`. Met by the crash oracle's generator, which annotates every struct binding because of it (D-451, decision AE). | D-451 | `unowned` | A copied struct and a switch over structs keep the type `T`, with checker tests for a field read and for an array of the copy |
| R-89 | **The elements of an array held in a struct field, or returned by `filter` or `select`, are `unknown`.** `for x in t.xs`, with `xs` an `int[]` field, and `for y in [1, 2].filter(v => v > 1)` give `x` and `y` the type `unknown`, so `U { g: x }` with `g: int` is E0001, `Cannot assign value of type 'unknown' to field 'g' of type 'int'`. Measured through `grob run` at `3dd32fb`. Met by the crash oracle's generator, which reads such arrays only through `.length` because of it (D-451, decisions AD and AE). | D-451 | `unowned` | Those elements have the array's element type, with checker tests for a field array, a `filter` result and a `select` result |
| R-90 | **A script that runs out of memory ends as an internal error, and the VM cannot stop inside the instruction that runs out.** `m := map<string, int>{}` then a `for i in 1..40` loop of `m.set("k${m}", i)` triples the text of `m` each time round and, after about 15 s, stops with E9001, `OutOfMemoryException`, measured through `grob run` at `3dd32fb`. The crash oracle's gate chose to report an out-of-memory as E9001 and not to register a code for it; nothing tracked that choice. Such a run also passes the oracle's watchdog: the VM checks its cancellation between instructions (D-319), and one interpolation of a large value runs for seconds, so no host can bound it. Found by the crash oracle's first campaign in two benchmark mutants (D-451). | D-451 | `unowned` | A decision records whether running out of memory is a registered runtime error and whether one operation's size is bounded; the oracle's two timeout records are triaged against it |
| R-92 | **`exit` takes an argument of any type.** `exit("s")`, `exit(nil)`, `exit(1.5)` and `exit(true)` type-check and stop with an internal error, `GrobValue kind mismatch: expected Int, actual String` and its kin, measured through `grob run` at `3dd32fb`. `exit` has no `FunctionSignature` (R-56), so nothing checks its argument. Found by the crash oracle's first campaign, whose `replace-value` operator changes an `exit` argument (D-451). | D-451 | `unowned` | `exit` with an argument that is not an `int` is E0004 at the argument, with a test asserting line and column; the oracle reports no finding for the reproducers above |
| R-93 | **An array index of a type other than `int` is accepted.** With `names := ["a", "b"]`, `names[names]`, `names["x"]` and `names[1.5]` type-check and stop with an internal error, `GrobValue kind mismatch: expected Int, actual Array` and its kin, measured through `grob run` at `3dd32fb`. R-75 is the receiver's side of the same gap. Found by the crash oracle's first campaign (D-451). | D-451 | `unowned` | An array index of a type other than `int` is a compile-time diagnostic with a registered code at the index, with a test asserting line and column; the oracle reports no finding for the reproducers above |
| R-94 | **A nullable error value can be thrown.** `a: IoError? := nil` then `throw a` type-checks and stops with an internal error, `Throw operand is not a struct (kind Nil). The type checker should have rejected a non-GrobError throw operand before emission.`, measured through `grob run` at `3dd32fb`; `throw a ?? a` does the same. Found by the crash oracle's first campaign (D-451). | D-451 | `unowned` | A decision records whether throwing a value of a nullable error type is refused at compile time or raises a registered runtime error when it is `nil`; a test pins it, and the oracle reports no finding for the reproducers above |
| R-95 | **The crash oracle's black-box check runs reproducers unisolated.** With `--grob`, a campaign runs each distinct reproducer through the real CLI, in the campaign's `blackbox/` directory but with the machine's own file system, processes, environment and network; `HostFakes` isolates only the in-process oracle. Mutants of the validation-script seeds call `fs` with absolute paths. No native the CLI registers has an effect outside the child process yet (gate report §4.2), so nothing reaches the machine today; `BlackBoxTests.RealCatalogue_RegistersOnlyWhatTheBlackBoxCheckMayRunUnisolated` lists the namespaces and named types judged so and fails on a new one. Chris's ruling on #271's review (D-453). | D-453 | The 9C `fs` increment, before its first native that writes, deletes or moves a file | The black-box check runs reproducers where they cannot reach the machine's files, processes or network, by an isolated environment or by a runner that refuses any reproducer reaching a namespace with host effects, enforced in the runner, not by a filter on the source; a test shows a reproducer calling a writing native leaves the machine unchanged; and the test's list then admits the new namespaces |

### Closed items

| # | Item | Raised by | Closed by | How |
|---|---|---|---|---|
| R-01 | **E4202 removed.** | D-410, scheduled by D-414 | **D-425** | E4202 is gone from `ErrorCatalog.cs` and `grob-error-codes.md` in one commit; the count moved 121 → 120 across the catalog, the summary index, the footer total and `ErrorCodeCountTests`' anchor, and the D-316 agreement gate is green. Its number is permanently burned (D-410's clarification of ADR-0017). D-414's ordering constraint was honoured: E2202's throw site landed three commits before the removal, so the ordering condition was never left uncoded. `docs/errors/examples/param-after-param-block-ends/` went with it. |
| R-02 | **E2202's title widened.** | D-410, D-412, scheduled by D-414 | **D-425** | Retitled to "`param` after `type`, `fn`, `const`, `readonly` or top-level statement" in `ErrorCatalog.cs`, the summary row and the full entry in one commit; D-316 green, total unchanged at 120. |
| R-03 | **E4102's title widened for `@minValue`/`@maxValue`.** | D-410, D-411, scheduled by D-414 | **D-425** | Resolved by widening rather than by minting a sibling, per D-424 Decision 7: the four share one validation shape and differ only in the argument's type, and a code per decorator pair scales badly against a seven-decorator set. Retitled in `ErrorCatalog.cs`, the summary row and the full entry in one commit, with the description rewritten to match; D-316 green, total unchanged at 120. |
| R-09 | **§3.5's construct enumeration completed.** | D-417 | **D-421** | `grob-formatter-specification.md` §3.5 now enumerates every comma-or-newline-separated construct in the language and states that the enumeration is complete. Switch-expression arms joined Category B (and §3.2's wrappable list); a new Category C covers the four comma-separated constructs that are never wrapped — lambda parameter lists, function-type parameter lists, type-argument lists and `select` case pattern lists; `for k, v in` is named as a fixed pair rather than a list. Closed ahead of its Sprint 12 owner because D-421 could not state the grammar rule without it. |
| R-12 | **`FunctionSignature` implemented.** | D-081, D-419, D-420, D-426 | **D-427** | Every native registered through `IPluginRegistrar` carries a `FunctionSignature`: the only public `RegisterNative` overload takes one, pinned by a reflection test. One `NativeCatalogueBuilder` pass builds the one `NativeCatalogue` the type checker reads and the VM is constructed over; `NamespaceRegistry`'s tables, `PrimitiveMemberRegistry` and `NamedTypeRegistry` are deleted with their three agreement tests; `date` and `guid` register their instance surfaces from `DatePlugin` and `GuidPlugin`; primitive members register through an internal surface. D-426 Decision 5: suite green (conditions 1 and 2, the probe corpus byte-identical at every group); condition 3 **not met as worded** — the benchmark gate failed on `Compile_TenPrints`/`Compile_TwoExpressions` allocation with no baseline updated, and the breach's origin is not established (tracked as R-24). |
| R-19 | **The `date` and `guid` hidden-field layout is written in three assemblies.** | D-426 | **D-427** | `Grob.Core`'s `DateLayout` (`TypeName`, `ValueFieldName`, `RoundTripFormat`) and new `GuidLayout` (`TypeName`, `ValueFieldName`) are the only declarations; `DateNatives`, `GuidNatives`, `DatePlugin`, `GuidPlugin` and the checker's `date` comparison rule read them. `NamedTypeLayoutTests` fails if `Grob.Compiler`, `Grob.Vm` or `Grob.Stdlib` declares a string constant equal to a layout name. The VM and the plugin each keep a parse/format helper over the shared constants, since they are DAG siblings. Taken in group 8 at Chris's direction (5 November), not because the move needed it. |
| R-11 | **Error-examples library harnessed.** | D-415, D-417, refined by D-428 | **D-429** | `ErrorExampleMasterTests` checks every master's titles against `ErrorCatalog`, its `-->` lines, snippets and first caret against its own source, and the README's index against the directories; `ErrorExampleCorpusTests` runs every pair through `RunCommand`'s own pipeline over injected capabilities and compares the ordered `(severity, code, file, line, column)` list with its master's. Pairs are discovered by directory. The 29 pairs that do not match are rows of the strict manifest `docs/errors/examples/quarantine.md`, each blocked by an open register item, an unregistered namespace or Sprint 11 and each required to fail exactly as recorded. 56 pairs: 27 pass, 29 quarantined. |
| R-18 | **Three decorator gold masters are stale.** | D-425 | **D-429** | Rewritten against D-410's braceless grammar and D-424's decorator semantics. Before the rewrite `unknown-decorator` raised E4201 plus a cascading E1001 and `invalid-allowed-argument` raised E4201 plus E0205 (its `const` array, D-289), not the codes their masters stated; `decorator-not-permitted-here`'s decorator before a local binding raised E2001, now tracked in R-27. Each new source raises exactly one diagnostic — E4001 at 2:1, E4101 at 2:10 and E4002 at 2:12 on a function parameter (D-424 Decision 2) — and each passes the harness; `unknown-decorator`'s help lists D-411's seven decorators. |
| R-32 | **Two stdlib ambient reads sit outside `RunCommand`'s capability seam.** | D-429, amended by D-430 | **D-437** | `IClock.LocalZone` and `IFileSystem.CurrentDirectory` are capabilities on D-343's pattern, supplied by `HostCapabilities` at the composition root and injected through `RunCommand`'s seam. `date.now`, `date.today`, `date.of`, `date.ofTime`, `toLocal`, both forms of `date.parse` and `path.resolve` read them, and each is tested under injected values. No explicit or implicit read of the host zone or current directory remains in `Grob.Stdlib`; `StdlibAmbientReadGuardTests` in `Grob.Consistency.Tests` keeps it so. `date.parse` classifies its input lexically, because no `DateTimeStyles` combination avoids a host read. `FindSystemTimeZoneById` stays, as an explicit lookup. One accepted behaviour change: on Windows a drive-relative path on another drive resolves to that drive's root. |
| R-05 | **Harness advertised-versus-built audit.** | D-419, owners split by D-426, full sweep owned by D-431 | **D-438** | Every file in `.claude/` (28), the four `CLAUDE.md` files and `AGENTS.md` read against the tree by the corpus sweep's change group 3, every advertised interface either true, corrected or removed. D-427 had closed the eight `FunctionSignature` sites and D-429 the four pair-naming sites. Among the rest: `src/CLAUDE.md`'s emission rule cited D-315, which is about `break` and `continue` in `select`; `sonar-pr-review` read a `sonar-project.properties` that never existed; `tdd-cycle`'s examples used a lexer API and error codes that never existed; `emitting-closures-and-upvalues` said upvalues close only on return; two commands split `test` and `feat` commits; root `CLAUDE.md` denied the `grob-reviewer` agent, claimed a Fable-pinned agent and said Sonar is not a NuGet analyzer. The commit bodies list every class of correction. |
| R-30 | **A regex literal compiled to broken bytecode.** | D-428, amended by D-430 and D-432 | **D-440** | A regex literal type-checks as a `Regex` and compiles to one constant `Regex` struct per site, which `RegexPlugin` compiles once per run; an invalid pattern is E0602 at the checker. `CompilerRegexTests` and `TypeCheckerRegexTests` cover both branches, and `Sprint9CRegexTests` runs literals end to end. The five public samples that carried `blocked-by=R-30` pass and have lost the token; one interpolated a `string?` and was corrected. |
| R-44 | **The function form of `replace` and `replaceAll` built.** | D-440 | **D-443** | `r.replace(s, m => m.value.upper())` and `r.replaceAll(...)` type-check with `m` typed `Match` and run: the signature union gained `FunctionShape` (amending D-426 Decision 1), a named-type method carries the function form as an overload on its one binder and a lambda takes its parameter types from the shape. `TypeCheckerFunctionShapeTests`, `RegexNamedTypeTests` and `Sprint9CRegexTests` cover both forms, and `grob-stdlib-reference.md` has lost its "not built yet" note. |
| R-59 | **A call through a nullable function value refused unless it is safe.** | D-448 | **D-449** | A call through a binding, a parameter, a struct field called with `.` or a call result of nullable function type reports E0101 at the callee, naming a nil check, `?.` or `??` as fits; `TypeCheckerFunctionTypeTests` asserts code, message, line and column for each. A call narrowed by `if (f != nil)`, a call through `??` and a struct field called with `?.` compile, and `Sprint5IncrementFunctionTypeTests.NullableFunctionValues_CalledSafely_Run` runs all three. A `?.` call through a nullable function field is nullable, so `x: int := bx?.op()` is E0104. The `call-nullable-function-value` pair pins the diagnostic. |
| R-61 | **A binary operator with one operand of unknown type takes its opcode from the known operand.** | D-449 | **D-450** | `VisitBinary` gives a single unknown operand the known operand's type before choosing an opcode, so `"[" + s` and `s + "]"` emit `Concat` and `s < "z"` the `string` comparison; `CompilerUnknownOperandOpcodeTests` asserts the opcode for each form, and `Sprint5IncrementFunctionTypeTests.UntypedLambdaParameter_StringOperators_Run` runs `names.select((n) => "Hi " + n)` and both concatenations. The checker refuses a known operand the operator has no opcode for with E0002 at that operand (`TypeCheckerUnknownOperandTests`), so no program it accepts reaches `ThrowUnsupportedBinaryOp` through this path. An operand whose assumed type is wrong at run time is R-63. |
| R-20 | **Natives bind named arguments.** | D-426, recorded by D-427 | **D-452** | A named argument on a namespace function, a primitive method or a named-type method binds by D-113 as on a script function, reporting E0008 to E0011 (`TypeCheckerNativeNamedArgumentTests`, `TypeCheckerPrimitiveNamedArgumentTests` and `TypeCheckerNamedTypeNamedArgumentTests`). `RejectNamedNativeArgs` and `RejectNamedMemberArgs` are gone. A skipped default is filled from the signature's constant, proved by `regex.compile(p, timeout: 2)` and on members the tests register. An overloaded method binds names against the signature its positional arguments choose. `d.addDays(days: 1)` reports E0009, and the probe corpus and `TypeCheckerDateTests` say so. |
| R-22 | **`strings.join`'s documented default removed.** | D-071, recorded by D-427 | **D-452** | Chris ruled the document wrong, not the code. `grob-type-registry.md` shows `separator: string` with no default and says `strings.join(arr)` is E0003, matching the registered signature, and R-55's drift check now holds the two together. |
| R-55 | **A native's defaulted parameter names pinned to its documentation.** | D-446 | **D-452** | `tooling/Grob.DriftCheck` reads every core signature live from the catalogue and compares each defaulted parameter's name and default with `grob-stdlib-reference.md` and `grob-type-registry.md`. `DefaultedParameterNameTests` shows it failing on a renamed defaulted parameter, on a reference that spells one differently and on a defaulted parameter the documents do not show. On the tree as shipped it passes over eight defaults, `sort`'s `descending` included. |
| R-64 | **The CLI exits with the documented codes.** | D-451 | **D-453** | A compile-time diagnostic exits `2`, a usage error `3` (an unknown command, a missing path, a file that does not exist or cannot be read), a runtime error `1` and `exit(n)` `n`, through `RunCommand`, `ReplCommand` and `CommandLineRunner`. `ExitCodeTests` pins each through the CLI, and the ten tests that asserted `1` for a checker or parser code assert `2`. `Error-Handling.md` and `CLI/Commands.md` state the set. |
| R-72 | **`exit(n)` in the REPL ends the session with `n`.** | D-451 | **D-453** | The REPL's handler around each entry ends the session with a script's exit code (ruling Q3), and an internal failure in an entry is reported as E9001 while the session continues. `ReplInternalErrorTests.ScriptExit_EndsTheSession_AndReturnsItsCode` and `ScriptExit_ThrownInAPhase_IsNotAnInternalError` pin the exit, and `InternalErrorReportTests` the report. |
| R-77 | **A switch expression gives its value with operands already on the stack.** | D-451 | **D-454** | The subject stays on the operand stack: each tested arm copies it with the new `Dup` opcode and a taken arm pops it, so no arm writes a local slot. `SwitchExprEmissionTests` asserts the full sequence for a switch as the right operand of `+`, and no `GetLocal` or `SetLocal` for a switch as a native's argument, a method's receiver, an anonymous struct's field, an interpolated part, an index assignment's value and a compound index assignment's value; with no slot left to assert, those stand in for the criterion's emitted slots. `SwitchExprOperandStackTests` runs each of the row's reproducers through the CLI and prints the right value: `1 + (5 switch { 0 => 1, _ => 2 })`, a switch as an anonymous struct's field, `math.pow`'s argument and `.isEmpty`'s receiver, and as the value of `xs[0] = (…)` and `t.f1 = (…)`. Its three entries left the slice's known list, which passes without them. |
| R-79 | **A finally that a throw runs finds the stack as the normal path leaves it.** | D-451 | **D-455** | `TryRegion` records the slot its `try` began at, `BaseSlot`, which the compiler sets from `_nextSlot` and a catch binding already takes; `RunFinallyExceptional` closes upvalues from that height and trims to it before the finally runs. `VirtualMachineFinallyTests` has VM tests for a local declared in the try body, in a catch body that throws and in a nested try, and for a throw from a callee's frame; `CompilerFinallyTests` pins `BaseSlot`. `FinallyOnThrowStackTests` runs each of the row's reproducers through the CLI: a `try` that declares a local and throws past it, a `catch` that declares a local and throws, a throw from a called function, a runtime error the VM raises (`10 / 0`) and one a native raises (`date.parse("nope")`). R-79 had no entry in the slice's known list. |
| R-78 | **A `??` operand is widened, and chooses its opcode, by the type the checker gave it.** | D-451 | **D-456** | The checker records the type it gave a `??` on `BinaryExpr.NilCoalesceType`, and `GetBinaryResultType` returns it. `NilCoalesceOperandTypeTests` asserts the opcodes for an `int?` left operand beside a `float` (`IntToFloat`, `SubtractFloat`) and for a `nil` left operand (`DivideFloat`), with a comparison, a ternary arm and a compound assignment's value besides. `NilCoalesceOperandTests` runs both of the row's reproducers through the CLI, `n: int? := 7` then `2.25 - (n ?? 3)`, and `(nil ?? 10.0) / 4`, and prints `-4.75` and `2.5`. Its entry left the slice's known list, which passes without it. |
| R-80 | **A finally that a throw runs ends at its own region's `TryEnd`.** | D-451 | **D-457** | `TryRegion` records the offset of its closing `TryEnd`, `TryEndOffset`, which the compiler sets as it writes it; the VM's bounded finally run ends when that instruction executes in the finally's own frame, where it had counted the `TryBegin` and `TryEnd` it executed. A `try` nested in the finally and left past its own `TryEnd`, by an error a typed `catch` does not match caught further out, a `break`, a `continue` or a callee's `return`, had left the count high, so the run went on past the finally. `VirtualMachineFinallyTests` has VM tests for one, two and three typed catches missing before a catch-all further out and for none, for a jump out of a nested `try` body, a callee returning from inside its own `try` and a nested `try`/`finally` run on the way to a catch; each asserts the first error as E5904 at its line and that nothing after the outer `try` runs. `CompilerFinallyTests` pins `TryEndOffset`. `NestedTryInFinallyTests` runs the row's reproducer through the CLI, printing `caught second` and then reporting `RuntimeError: first` as E5904, with its sibling shapes. The oracle's `check` reports no finding for any of them. R-80 had no entry in the slice's known list. |
| R-91 | **An error a native's callback does not catch leaves the callback's dispatch at its floor and is routed on from the native's call site.** | D-451 | **D-458** | `InvokeCallable` records the frame count its re-entrant dispatch was entered at; when `PropagateThrow` finds no handler in the callback's frame it unwinds that frame and reports the error unhandled, and the error leaves the native in an internal exception that the `Call` arm which ran the native routes on from its own frame, as it routes a native's own fault. The walk had gone on to the script's handler, and the callback's dispatch ran the handler and the rest of the script itself before popping an empty stack. `VirtualMachineCallbackThrowTests` has VM tests for an error raised in a `select`, `filter`, `sort` and `each` callback and caught by the script, each printing the first element only and ending with the script's local alone on the stack, with the row's overflow, an error two re-entrant dispatches deep, one in a function the callback calls, one the callback catches and, uncaught, E5002 at the callback's line. `CallbackThrowTests` runs the row's reproducer through the CLI with its sibling shapes, among them a `finally` in the called function and one around the call, a callback error in a `finally` that a throw runs, and E5904 at the `throw` when nothing catches it. The oracle's `check` reports no finding for the reproducers. R-91 had no entry in the slice's known list. |
| R-69 | **A native's host exception is an internal error that names the native, and a native's bad input is a catchable error.** | D-451 | **D-459** | `RunNative` runs every native, from the `Call` arm and from `InvokeCallable`'s nested-native path, and an exception that is not a `NativeFaultException`, a Grob error, an `OverflowException`, an exit, a cancellation or one of the VM's control signals leaves it as a `GrobInternalException` naming the native, the exception's type and the call's line; the CLI reports it as E9001 and no `catch` handles it. `toZone` with an unknown zone raises `ParseError` E5702, and `replace` with an empty search text and `guid.newV5` with a namespace that is not a `guid` raise `RuntimeError` E5903, each through `NativeFaultException`. Choosing E5903 exposed a native fault walked twice, which ran a script's `finally` twice, and that is fixed with it. `VirtualMachineNativeHostFaultTests` has VM tests for the net, for a native called from a callback and one that is the callback, and for each exception it lets through. `DatePluginTests`, `StringMethodsPluginTests` and `GuidPluginTests` have the stdlib tests for the three natives, with adversarial rows. `NativeInputFaultTests` runs each through the CLI caught, uncaught at its line, in a callback and under a `finally`, and `InternalErrorReportTests` runs a native whose clock throws. The oracle's `check` reports no finding for the three reproducers or for `date-members.clean.grob`. Its two entries left the slice's known list. |

---

## Resolved Questions

These questions have been decided. Full rationale is preserved here for reference.
One-line resolutions are recorded in the confirmed decisions table of
`grob-decisions-log.md`.

---

### OQ-005 — Value Representation

**Status: RESOLVED — May 2026 (D-303)**

**Decision:** `GrobValue` is a hand-rolled tagged-union `readonly struct` on the
shape locked provisionally in D-297. The shape is permanent. NaN boxing is
rejected.

The struct keeps the three private fields established in D-297 (a
`GrobValueKind` discriminator, a `long _scalar` slot for primitives, an
`object? _reference` slot for reference types), the nine-variant discriminator
set (`Nil`, `Bool`, `Int`, `Float`, `String`, `Array`, `Map`, `Struct`,
`Function`), the encapsulation contract (private fields, public factory statics,
`Kind`/`IsX` predicates, strict `AsX()` accessors, `TryAsX(out)` accessors, full
equality), and the .NET 10 LTS target. With OQ-005 closed, the
"provisional pending OQ-005" caveat is removed from the on-struct XML doc and
from `grob-vm-architecture.md`. Everything else stands.

**Rationale.** NaN boxing exploits IEEE 754's unused NaN payload bits to pack
type tag plus payload (pointer, int, bool, nil) into a single `ulong`. In C —
where clox uses it — this is a clean optimisation: pointers are 48-bit user-
space integers, no GC exists, `free()` is explicit. In .NET, that pattern is
a hard mismatch with the platform.

The .NET GC is a moving collector. It finds live references by walking GC
metadata emitted by the JIT — root-set stack frames, static fields and reference-
typed fields inside reachable objects. A `ulong` is, to the GC, an integer. It
is never scanned for references, and it is never updated when a compacting
collection moves the underlying object. Packing a managed reference into a
`ulong` therefore breaks GC tracing in a way the runtime cannot detect:
collections can free objects the VM still holds, compaction can leave packed
addresses pointing at moved memory, and there is no exception or warning when
it goes wrong.

The escape hatches do not rescue the design.

- `GCHandle.Alloc(Pinned)` keeps an object at a fixed address but pins it for
  the handle's lifetime, fragmenting the heap and degrading collection
  performance — the opposite of what NaN boxing was meant to buy.
- A hybrid shape (NaN-boxed primitives plus a separate `object?` reference
  slot) gives up the single-word size that was NaN boxing's only meaningful
  win while keeping all the bit-manipulation cost. The pattern pays for the
  technique without receiving the benefit.

Either path replaces a clean managed design with `unsafe` code threaded through
the VM's hot path and manual handle bookkeeping.

The benefit is also small in context. Grob's hot path is I/O — REST calls,
JSON parsing, process spawning, file reads. The cache-pressure argument that
justifies NaN boxing for a tight-loop numeric interpreter (clox's Pratt-parsed
expressions, the fib benchmark) does not transfer to a script hitting an Azure
DevOps API. An 8-byte vs 24-byte value struct does not move the wall-clock
needle on workloads dominated by network latency and stdlib allocation.

Two further factors. **Debuggability** — a tagged union is legible in a watch
window (`Kind = Int, _scalar = 42` reads as the value it represents); a NaN-
boxed `ulong` reads as an opaque 16-hex-digit number unless a decode helper
is installed in the debugger. **Extensibility** — adding a tenth variant to
a tagged union is an enum case and a field accessor; NaN boxing's bit budget
is finite, every new kind contends with the float-NaN payload space, and
every existing kind's bit pattern is part of the wire contract.

The clox detour was the right preparation. NaN boxing is worth seeing in its
native habitat to know why it does not transplant. In C, it is the
optimisation the language gives you. In C#, it is a technique fighting the
platform for a benefit the workload does not need.

D-297's encapsulation boundary was always the right shape regardless of which
way OQ-005 resolved. The full byte-level layout, the encapsulation contract,
and the .NET 11 `[Union]` migration signpost remain in
`grob-vm-architecture.md` under the renamed "GrobValue Representation"
section. Supersession chain: D-142 → D-297 → D-303.

---

### OQ-006 — GC Strategy

**Status: RESOLVED — May 2026 (D-304)**

**Decision:** Grob delegates heap memory management to the .NET garbage
collector. No custom mark-and-sweep collector is shipped in v1. The step in the
implementation order historically allocated to "GC" is a no-op. "Custom garbage
collector" is added to the explicitly-out-of-scope list in
`grob-v1-requirements.md` §13.

**Scope of the decision.**

- Heap objects (`string`, `GrobArray`, `GrobMap`, `GrobStruct`, `GrobFunction`,
  plugin-registered reference types) are ordinary CLR objects, allocated
  normally and reclaimed by the .NET GC when no live `GrobValue` references
  them.
- Primitive Grob values (`int`, `float`, `bool`, `nil`) live in the `_scalar`
  field of `GrobValue` and never reach the heap. They generate zero GC
  pressure.
- The `_reference` field of `GrobValue` is the single root the GC sees per
  slot. Stack slots, locals, the globals table and the constant pool
  participate in the normal GC root walk by virtue of being arrays of
  `GrobValue` reachable from VM state.
- No finaliser is required on `GrobValue` or any runtime-internal type.

**What does not exist in v1.**

- No mark phase, sweep phase, allocation-threshold trigger, or
  `CollectGarbage()` entry point in `Grob.Vm`.
- No custom heap data structure (no `_heapHead`/`_heapSize`/`Allocate()`
  plumbing). Each runtime reference type is allocated by `new` and managed by
  the CLR.
- No GC tuning surface in `grob.json` or the CLI. The runtime exposes no GC
  settings of its own; users may set CLR GC switches (server GC, concurrent
  GC) via standard .NET configuration if they choose, but this is not a Grob
  feature.

**Rationale.** Grob's target workload is I/O-bound scripting — Azure CLI
orchestration, DevOps REST tooling, JSON/CSV transformation, file-system
walks. The hot path is stdlib calls, not allocation churn. Heap pressure in
this workload is dominated by short-lived string objects from `json.parse()`,
`fs.readText()` and string interpolation — exactly the pattern the .NET
generational GC handles well in Gen 0.

A custom mark-and-sweep collector would compete with the runtime's collector
rather than replace it (CLR-allocated objects cannot be hidden from the .NET
GC). The cost is significant: a parallel object lifecycle, a marking algorithm
correctly synchronised with the VM's frame walk, an allocation hook on every
heap-bound value construction, and a new class of latent bug (use-after-free
in marked-then-collected-incorrectly cases). The benefit is theoretical until
profiling shows a real workload where .NET's collector is the bottleneck.

clox implements its own collector because C has no choice. Grob is in C#; the
choice is whether to add a redundant layer to a managed runtime that already
solves the problem. The answer is no.

The benchmarking infrastructure (D-302) provides the empirical surface to
revisit this. `[MemoryDiagnoser]` on every benchmark records allocations and
Gen 0/1/2 collection counts in the baseline. If a real script later shows GC
pressure that the .NET collector handles badly, the data to substantiate the
case will exist. v1 is not the right time to act on a problem that has not
been measured.

**Migration path if v1 evidence forces a revisit.** Additive, not destructive:
introduce a managed-side weak-reference tracking table inside `Grob.Vm`,
register heap-bound `GrobValue` constructions through that table, and run a
periodic walk over reachable VM state to identify which entries are still
live at the VM-semantic level. Even this path does not replace the .NET GC;
it sits above it, identifying retention patterns the platform's collector
cannot see (e.g. closure-captured arrays retained beyond their useful
lifetime). That is a Grob-aware memory-introspection feature — already noted
as deferred post-v1 in D-302 — not a competitor to the platform collector.

---

### OQ-009 — `GrobValue` Provisional Representation

**Status: RESOLVED — April 2026 (D-297)**

**Decision:** `GrobValue` is a hand-rolled `readonly struct GrobValue : IEquatable<GrobValue>` under .NET 10 LTS. Three private fields:
a `GrobValueKind` discriminator, a `long _scalar` slot holding `int`/`bool`/`float`
(floats stored via `BitConverter.DoubleToInt64Bits` to avoid allocation), and an
`object? _reference` slot for reference types. Total 24 bytes on x64 with alignment.

**Discriminator set — nine variants:** `Nil`, `Bool`, `Int`, `Float`, `String`,
`Array`, `Map`, `Struct`, `Function`. Plugin types (`date`, `guid`, `File`,
`ProcessResult`, `json.Node`, `Regex`, `Match`, `csv.Table`, `CsvRow`,
`Response`, `AuthHeader`, `ZipEntry`) and user-declared `type`s all use the
`Struct` kind; runtime type discrimination happens at the type-registry level
via the boxed reference. This keeps `GrobValueKind` small and stable as plugins
register new types.

**Encapsulation contract:** private fields, public factory statics
(`FromBool`, `FromInt`, …, plus `Nil` singleton); inspection via `Kind` and
`IsX` predicates; strict accessors (`AsX()`) that throw `GrobInternalException`
on kind mismatch; try-accessors (`TryAsX(out)`) for plugin and runtime
defensive code; full `Equals`/`GetHashCode`/`==`/`!=`. No callers outside
`Grob.Core` access the fields directly.

**Provisional-pending-OQ-005:** the internal layout is the only thing OQ-005
may change; the public API surface is stable. Documented in code (XML doc on
the struct), in `grob-vm-architecture.md`, and as the supersession chain
D-142 → D-297. The .NET 11 `[Union]` attribute migration path (post-GA, when
.NET 11 is battle-tested) is signposted in `grob-vm-architecture.md` as a
future one-commit upgrade — adding `[Union]` and `IUnion` to the existing
struct gains compile-time exhaustiveness checking on `switch` without
disturbing layout, factories, or accessors.

**Rationale:** `GrobValue` must be defined before the first line of `Grob.Core`
is written. OQ-005’s full value representation decision (tagged union vs NaN
boxing) is deferred until clox is complete because that decision requires
real bytecode-VM experience to make well. The provisional shape isolates the
OQ-005 decision behind a clean boundary so the eventual retrofit, whatever
shape it takes, is localised to `Grob.Core` and does not leak into
`Grob.Compiler` or `Grob.Vm`. Hand-rolled rather than .NET 11 `union` because
the compiler-generated `union` form boxes value-type cases on every
assignment — wrong cost profile for a VM hot path — and the `[Union]`
escape hatch produces the same hand-rolled struct anyway, only with an
attribute attached. .NET 10 LTS rather than .NET 11 STS because LTS gives
v1 room to ship and stabilise without a forced migration.

Full byte-level layout, encapsulation contract and rationale in
`grob-vm-architecture.md`.

**Follow-on:** OQ-009 resolved the _provisional_ shape so `Grob.Core` could
ship before OQ-005 landed. OQ-005 (above) has since closed (D-303): the
tagged union is permanent, NaN boxing rejected. The provisional framing is
removed from the corpus; the shape OQ-009 locked is now the final shape.

**Status: RESOLVED — April 2026 (D-298)**

**Decision:** `.grobc` files use a skeleton binary format with a fixed-shape
header (magic bytes `0x47 0x52 0x4F 0x42` — ASCII “GROB” — followed by a
`uint16` format version field starting at 1, little-endian throughout),
followed by sectioned content for the constant pool, instruction stream,
source map, and symbol table. Cache files live in a `.grob/cache/` side
directory next to the source `.grob` file, mtime-driven invalidation,
`.gitignore`-friendly. The `.grob` source file is canonical; `.grobc` is
optional cache.

The `.grob/cache/` side directory matches the convention used by Python’s
`__pycache__` and similar tools — generated artefacts stay separate from
source, are trivial to `.gitignore` and never clutter the working directory.

Per-opcode operand encoding remains incremental, governed by ADR-0013 —
opcodes land sprint-by-sprint and the operand layout is documented at the
opcode’s source of definition. The skeleton spec covers the framing; the
per-opcode detail follows.

**Rationale:** The format needs to be versionable from day one because
retrofitting versioning is expensive. ADR-0013 already locked the stability
rule (immutable opcode numbers once shipped, format version increment on
breaking change). What was left open — magic bytes, header layout, constant-
pool wire format, source-map shape — is now fixed at the level needed for
Sprint 1 implementation. Cryptographic signing, compression, encryption,
and multi-chunk packaging are explicit non-features for v1.

Full byte-level layout, implementation notes and rationale in
`grob-grobc-format.md`. Supersession chain: D-143 → D-298.

---

### OQ-007 — `for...in` Loop and Iterable Protocol

**Status: RESOLVED — April 2026**

**Decision:** `for...in` is special-cased in v1. The compiler handles exactly
three cases:

1. **Numeric range** (`for i in 0..10 { }`) — already confirmed. Lowered to `while`.
2. **`T[]` array** (`for item in arr { }`, `for i, item in arr { }`) — index-based
   lowering to `while`. Both single and two-identifier forms supported.
3. **`map<K, V>`** (`for k, v in myMap { }`) — iterates insertion-order keys.
   Two-identifier form **required**. Single-identifier form on a map is a compile
   error with a suggestion to use `for k in myMap.keys` instead. Lowered to a
   `while` loop over an internal keys array.

Any other type in `for...in` subject position is a compile error.

**Formal iterable protocol:** Post-MVP. The compiler architecture accommodates it
without rework — the three special cases become the first implementors when the
protocol is defined.

**Rationale:** Every v1 use case in the sample scripts is array or range iteration.
A formal protocol adds `Grob.Runtime` surface, type checker conformance checking,
and plugin author complexity — none of which is justified in v1. `map<K, V>` is
special-cased because `for k, v in myMap` is immediately natural and the
alternative (`for k in myMap.keys { v := myMap[k] }`) is visibly clunky for a
type that is now first-class.

---

### OQ-001 — Generics Scope

**Status: RESOLVED — April 2026**

**Decision:** Constrained generics. The type checker and compiler understand generic
type parameters internally. Users consume generic functions via stdlib and plugins
(`mapAs<T>()`, `filter`, `map` etc) but cannot declare generic functions or types
in v1. Evolution to user-facing generics is an additive grammar extension — no
architectural rework required.

**Rationale:** Gets type-safe collections and JSON deserialisation without committing
to full user-facing generic syntax on day one. Implementation scope is meaningfully
smaller than full generics. Closes no doors — the type checker already understands
generics, the grammar simply doesn’t expose the declaration syntax yet. Analogous
to Go pre-1.18.

**Plugin constraint:** Plugins that expose generic functions must express type
parameters via `FunctionSignature` in `Grob.Runtime`. Designed in from the start.

---

### OQ-002 — Struct / Record Types

**Status: RESOLVED — April 2026 (SharpBASIC retrospective)**

**Decision:** Grob needs user-defined struct/record types.

**Evidence:** The Sunken Crown required parallel arrays as a substitute for records.
The retrospective verdict: _“Messy, wasteful, and slow.”_ The absence of a `type`
keyword was the single biggest language limitation revealed by writing a real program.

**Confirmed direction:** `type` keyword, structural types, fields declared inside
the block. Immutable by default, opt-in mutability. JSON deserialisation (`mapAs<T>()`)
maps JSON keys to fields by name.

```grob
type Repo {
    org:     string
    project: string
    name:    string
}
```

---

### OQ-003 — JSON and the Type System Boundary

**Status: RESOLVED — April 2026**

**Decision:** `mapAs<T>()` is the confirmed boundary mechanism — a generic method
understood by the type checker, consuming the constrained generics infrastructure.
JSON nodes are accessed via `json.Node` with typed accessors (`asString()`, `asArray()`
etc.) and mapped to user-defined types via `mapAs<T>()`. Full json module API specified
in `grob-stdlib-reference.md`.

---

### OQ-004 — Error Handling Model

**Status: RESOLVED — April 2026**

**Decision:** Exceptions as the runtime error model. See confirmed decisions for detail.

---

### OQ-008 — `date` as a Built-in or Stdlib Type

**Status: RESOLVED — April 2026**

**Decision:** `date` is a core stdlib type — auto-available, no import required.
Single type holds both date and time. Full API locked — see confirmed decisions.

---

### OQ-011 — `Grob.Crypto` API Shape

**Status: RESOLVED — April 2026**

**Decision:** First-party plugin (`import Grob.Crypto`). Full API:

- `crypto.sha256File(path: string): string` — lowercase hex, streams internally
- `crypto.md5File(path: string): string` — lowercase hex, streams internally
- `crypto.sha256(value: string): string` — lowercase hex, UTF-8 encoded
- `crypto.md5(value: string): string` — lowercase hex, UTF-8 encoded
- `crypto.verifySha256(path: string, expected: string): bool` — constant-time comparison
- `crypto.verifyMd5(path: string, expected: string): bool` — constant-time comparison

All hex output is lowercase. File functions stream internally — never load full file
into memory. Verify functions use constant-time comparison for security. SHA-1,
SHA-512, HMAC, byte array output — all post-MVP.

---

### OQ-012 — `process.run()` Timeout Behaviour

**Status: RESOLVED — April 2026**

**Decision:** All four process functions get `timeout: int = 0` as a named parameter.
`0` means infinite — runs until the process completes or the OS kills it. On timeout
expiry, throws `ProcessError("Process timed out after {n} seconds: {cmd}")`.
`ProcessResult` is unchanged — no `timedOut` property. The throw is the signal.

Full signatures:

```grob
process.run(cmd: string, args: string[], timeout: int = 0): ProcessResult
process.runShell(cmd: string, timeout: int = 0): ProcessResult
process.runOrFail(cmd: string, args: string[], timeout: int = 0): ProcessResult
process.runShellOrFail(cmd: string, timeout: int = 0): ProcessResult
```

**Rationale:** Option 3 from the original question. No silent default kill avoids
surprising behaviour for long-running legitimate processes. `timeout: int` is
available when the caller needs it. `ProcessError` on timeout with a clear message.
`ProcessResult` does not need `timedOut` — the throw communicates the condition.

---

_Resolved questions are summarised as one-line entries in the confirmed decisions_
_table of `grob-decisions-log.md`. The full rationale is preserved here._

---

_Document updated November 2026 — D-459. **R-69 closed**: a native's host exception is an internal error_
_that names the native, and `toZone`, `replace` and `guid.newV5` raise catchable errors for their bad input._
_Previous: November 2026 — D-458. **R-91 closed**: an error a native's callback does not catch leaves the_
_callback's re-entrant dispatch at its floor and is routed on from the native's call site._
_Previous: November 2026 — D-457. **R-80 closed**: a finally that a throw runs ends_
_at its own region's `TryEnd`, since `TryRegion` records that instruction's offset. R-91's row_
_records that its cause is not R-80's._
_Previous: November 2026 — D-456. **R-78 closed**: a `??` operand is widened, and_
_chooses its enclosing operator's opcode, by the type the checker gave it._
_Previous: November 2026 — D-455. **R-79 closed**: a finally that a throw runs finds the_
_stack as the normal path leaves it, since `TryRegion` records the slot its `try` began at._
_Previous: November 2026 — D-454. **R-77 closed**: a switch expression keeps its subject_
_on the operand stack, copied by the new `Dup` opcode, so it gives its value where operands are_
_already below it._
_Previous: November 2026 — D-453. **R-64 and R-72 closed**: the CLI exits with the_
_documented codes, and `exit(n)` in the REPL ends the session with `n`. **R-68 to R-95 added** by_
_the crash oracle: R-68 to R-74 from its gate, R-75 and R-76 from its mutator, R-77 to R-94,_
_eighteen root causes, from its first campaign of 200,000 programs, and R-95, the black-box_
_check's isolation, owned by the 9C `fs` increment. **R-57, R-60, R-63, R-69, R-71, R-75 and_
_R-76 extended** with the shapes the campaign found; R-63's criterion names the campaign's_
_reproducer; R-67 extended and R-74 corrected by the mutator's range tests._
_Previous: November 2026 — D-452. **R-20, R-22 and R-55 closed**: natives bind named_
_arguments, `strings.join`'s documented default is removed and a drift check pins defaulted_
_parameter names to the references. **R-58 narrowed**: an argument to a script function or a_
_native is widened. **R-65 to R-67 added**: a nullable named-type parameter, owned by `fs`; a_
_guarded primitive method call that faults the VM; a member call on a function value with no E1002._
_Previous: November 2026 — D-451. **R-64 added**: `grob run` exits `1` on a compile error and a usage_
_error, where the documented set gives `2` and `3`; the crash oracle increment owns it._
_Previous: November 2026 — D-450. **R-61 closed**: with one operand of unknown type, a binary_
_operator takes its opcode from the known operand, which must suit the operator._
_**R-63 added**: an operand of unknown type compiles to an assumed type, so a call given another type stops._
_Previous: November 2026 — D-449. **R-59 closed**: a call through a nullable_
_function value reached the VM; it is E0101 unless a nil check, `?.` or `??` makes it safe._
_**R-60 added**: the array methods that take a function do not check that argument. **R-61 added**:_
_a binary operator with one operand of unknown type takes its opcode from the wrong side, which crashes_
_the compiler on `"[" + s` and miscompiles `s + "]"`._
_**R-62 added**: `fn(): void` is refused only as a struct field's type._
_Previous: November 2026 — D-448. **R-57 added**: the calls through a function value_
_whose type the checker does not know check no arguments, and the VM does not check a call's_
_argument count; a named struct's function-typed field is known, an anonymous struct's is_
_not. **R-58 added**: an `int` where a `float` is expected is not widened in a binding, a return, a struct field, a parameter default or an argument._
_**R-59 added**: a call through a nullable function value is not checked for `nil` and reaches the VM._
_Previous: November 2026 — D-447. **R-56 added**: array methods, map methods and the_
_built-ins have no signatures and so refuse a named argument._
_Previous: November 2026 — D-446. **OQ-028 opened**: the order in which a reordered_
_call's arguments are evaluated. **R-20 re-owned and its criterion fixed on binding**: the_
_*native named arguments* increment, ahead of the 9C `fs` increment. **R-55 added**: a drift_
_check that pins a native's defaulted parameter names to its documentation._
_Previous: November 2026 — D-445. **R-52 to R-54 added**: the two `fs` error codes the_
_increment mints, three platform facts it measures and the wiki pages it brings into line._
_Previous: November 2026 — D-444. **OQ-026 and OQ-027 opened**: the surface of inline_
_and stdin source for `grob run`, and whether the foreign-idiom diagnostics leave R-36. **R-48 to_
_R-51 added**: the agent-scripting interlude's four items. R-36's owner cell notes OQ-027._
_Previous: November 2026 — D-443. **R-44 closed**: the function form of `Regex.replace`_
_and `replaceAll` is built. **R-47 added**: a lambda parameter's type annotation is ignored._
_Previous: November 2026 — D-442. **OQ-024 and OQ-025 opened**: the shell_
_`process.runShell` invokes on each platform, and how Grob installs on Linux. **R-45 and R-46_
_added**: release artifacts executed before they are published, and the corpus brought into_
_line with two equal platforms._
_Previous: November 2026 — D-440. **R-30 closed**: a regex literal compiles to a constant_
_`Regex` and runs, and the five samples it blocked pass. **R-44 added**: the function form of_
_`replace` and `replaceAll`, deferred to the `regex` increment's third pull request._
_Previous: November 2026 — D-438. **R-05 closed**: the corpus sweep read every file in_
_`.claude/`, the four `CLAUDE.md` files and `AGENTS.md` against the tree. **R-40 to R-43 added**:_
_the unbuilt synthetic large-script generator, a `map` argument unchecked against its parameter,_
_a two-parameter `sort` selector reaching the VM and generic types rendered bare in diagnostics._
_Previous: November 2026 — D-437. **R-32 closed**: the local time zone and the current_
_directory are capabilities, and a source guard keeps `Grob.Stdlib` free of both reads._
_**R-29** amended: five public samples are blocked on it, not six, after W9. **R-39 added**:_
_the README's first sample becomes runnable when `fs` is built, before Sprint 10 binds_
_parameters._
_Previous: November 2026 — D-434, D-435, D-436. **OQ-019 to OQ-023 opened**: the_
_diagnostic contract, the agent context kit, the agent benchmark, restricted run and plan_
_mode, and the embedding host package. **R-35 to R-38 added**: the Sprint 9C capability_
_seam, the agent-facing CLI surface, the benchmark and the restricted run. OQ-014 gains a_
_note on typed tool bindings._
_Previous: November 2026 — D-432. **R-30's owner** becomes the `regex` increment as_
_the first of the rebuilt Sprint 9C, with D-428's interim loud failure withdrawn. **R-34_
_added**: no release-gate script exercises `regex`; D-432 requires one._
_Previous: November 2026 — D-431. **R-05's full `.claude/` sweep is owned** by the_
_*corpus sweep* increment, which takes it as part of the same activity — advertised against_
_built — rather than leave it `unowned` through Sprint 9C. No row opens or closes._
_Previous: November 2026 — D-430. **R-24's origin is established**: the allocation_
_breach predates the *`FunctionSignature`* increment, which added 96 B and 32 B of it, so 9C is_
_not blocked and bisection goes to the hardening sprint. **R-29** gains two `unknown`_
_cascade shapes the public samples measured; **R-30** records the five samples that use a_
_regex literal; **R-31** names the type-argument position and records that no sample hits_
_the other five; **R-32** gains the implicit local-zone reads and the shape D-430 fixes._
_**R-33 added**: `grob-lang.dev`'s sample is outside every gate. This entry also stands in_
_for D-429, whose commit closed R-11 and R-18, amended R-25, R-29 and R-31 and opened R-32_
_without a line here._
_Previous: November 2026 — D-428. **R-11 refined**: the library holds 56 pairs,_
_not 57, and the criterion names the strict quarantine manifest and the static layer._
_**R-18** moves to the error-examples harness increment, whose subject it is. **R-20**_
_is owned by the 9C increment that first registers `File`, since `moveTo` and `copyTo`_
_are two-parameter named-type methods; the criterion now admits binding as well as_
_E0011. **R-24**'s measurement on `b202509` is due before 9C. **R-25 to R-29 added**_
_from measuring the gold masters: the unscheduled §10 renderer, the missing runtime_
_column, code divergences (five codes never raised), location divergences and_
_cascades. **R-30 and R-31 added** from measuring the public samples: a regex literal_
_that crashes the VM, and undefined type names accepted silently in most annotation_
_positions._
_Previous: November 2026 — D-427. **R-12 closed** by the *`FunctionSignature`*_
_increment: every registrar native carries a signature, one catalogue replaces the_
_three tables, and the instance surfaces register from their plugins; Decision 5's_
_benchmark condition is recorded as not met as worded (allocation breach of unestablished_
_origin, no baseline updated). **R-19 closed** in the same increment at Chris's direction: the_
_`date` and `guid` layouts are declared once, in `Grob.Core`, with a reflection guard._
_**R-05 updated**: its eight `FunctionSignature` sites are corrected; the four naming_
_sites and the full sweep remain. **R-20 to R-23 added** from the gate report's_
_findings: named arguments on named-type methods (F3), array element-type_
_enforcement (F4), `strings.join`'s unbuilt default (F5) and `struct?` in nullable_
_named-type diagnostics (F10). **R-24 added** for the red compile-benchmark_
_allocation gate, owned by the pre-Sprint-10 hardening sprint._
_Previous: October 2026 — D-426. **R-12 refined**: its criterion now names_
_the three native surfaces that migrate and the four that stay bespoke, so "every_
_native" is checkable, and it states the single-source outcome rather than_
_"`NamespaceRegistry` produces", which supported two readings. **R-05's owner split**:_
_the eight `FunctionSignature` sites stay with that increment, which makes them true;_
_the four `*_grob.txt` naming sites move to the error-examples harness increment,_
_which is the one that makes the real naming load-bearing. **R-19 added**: the_
_`date` and `guid` hidden-field layout is written in `Grob.Core`, `Grob.Vm` and_
_`Grob.Stdlib`, found while scoping the named-type move. `unowned`, with an explicit_
_condition under which the `FunctionSignature` increment absorbs it._
_Previous: September 2026 — D-425. **R-01, R-02 and R-03 closed** by the_
_*finish the param concern* increment: E4202 removed with the count moving_
_121 → 120; E2202 retitled to name `type`, `const` and `readonly`; E4102 widened_
_to name `@minValue` and `@maxValue` rather than gaining a sibling. Each_
_was a D-316 co-commit, which is what they were waiting for._
_**R-16, R-17 and R-18 added**, all three found by the increment rather than_
_predicted by it. R-16: a `type` or `fn` after top-level code steps backwards_
_against §19's prose, but only categories 1 and 2 have codes allocated, so the_
_ordering walk stays silent there — either the prose is wider than the rule or_
_the condition needs a code. R-17: §19's Decorators table leaves `@secure` and_
_`@pattern` with no code for their arity and argument-kind checks, so both_
_report E4002, whose title describes a placement error rather than an argument_
_one. R-18: three decorator gold masters are stale against D-410's braceless_
_grammar and were never test-enforced, which is why nobody noticed._
_Previous: D-424. **R-15 added**: decorator constraints_
_enforced at parameter-binding time. D-424 splits decorator handling into static_
_validation, which lands with the decorator AST node, and enforcement against a_
_supplied value, which needs parameter binding and is Sprint 10. The deferred_
_half gets a row because a split without a tracker for the far side is how a_
_half-built feature reads as finished — the state it leaves behind is weaker_
_than R-08's "unexercised", since a statically valid decorator compiles clean_
_and then does nothing at all. Owned rather than `unowned`: Sprint 10 is where_
_parameter binding lives, and the criterion names R-08's release-gate script as_
_the exercise, so the two items close together or not at all._
_Previous: September 2026 — D-423. **R-13 and R-14 added**, the two_
_pre-existing gaps D-422 found and reported. Both were named only in D-422's_
_prose; neither reached this document, so for the fifteen days between that_
_entry landing and this one the register said they did not exist while the_
_decisions log said they had been filed here. That is the precise failure D-420_
_built the register to prevent — a deferral recorded inside an append-only_
_entry, which is frozen and can never say "done" — recurring in the second_
_increment after its creation, which is why D-423 records it rather than_
_quietly adding the rows. Both are `unowned`, honestly: R-13 is a lexer-surface_
_change and R-14 a recovery-wrapper addition, and neither has a scheduled_
_increment. Each carries a completion criterion that names the test D-422_
_wrote around the gap, so "done" is checkable by someone who was not in the_
_conversation that deferred it._
_**R-05 sized**: 39 files in `.claude/`, seven live `FunctionSignature` claims_
_across four files and four files carrying the `*_grob.txt` naming against 57_
_on-disk pairs that use `<case>.grob`. This is the first read of `.claude/`_
_from the live tree — every prior corpus zip excluded dotfiles, which is why an_
_item raised in August could not be sized until September._
_Previous: August 2026 — D-421. **R-09 is the register's first closed_
_item**, moved to the Closed Items table with the D-number that closed it,_
_exercising the mechanism D-420 built and confirming an item can leave Open_
_without being deleted. It closed ahead of its Sprint 12 owner because D-421_
_could not state the trailing-comma grammar rule without knowing which_
_constructs the rule applies to. **R-05 widened by two sites**, both found by_
_D-421 while verifying something else: `plugins/CLAUDE.md:27` is a third_
_harness file instructing plugin authors to supply a `FunctionSignature` that_
_has never existed, and was not on D-419's list of known sites;_
_`tests/CLAUDE.md:60` documents the error-example pair naming as_
_`*_grob.txt`/`*_expected.txt` while all 57 pairs on disk use_
_`<case>.grob`/`<case>.expected.txt`. Both are the R-05 shape — harness_
_documentation describing an interface the tree does not have — and both are_
_recorded rather than fixed, since R-05's criterion requires the full sweep,_
_not a running correction of sites found in passing._
_Previous: August 2026 — Deferred Work Register added (D-420), giving_
_work that is decided but not yet done a single tracking home with a named_
_owner and a checkable completion criterion per item. Twelve items on creation,_
_R-01 through R-12. It sits between Open Questions and Resolved Questions_
_because it is the third state: settled, but not yet done. Created because a_
_deferral recorded only in prose inside an append-only entry has nowhere to_
_record its own current state — the deferring entry is frozen, so whether an_
_item is still outstanding can only be reconstructed by re-reading every entry_
_since. R-01 to R-03 carry D-414's scheduling: D-410's three registry changes_
_turn on the ordering rule rather than the grammar, so they are correctly_
_outstanding rather than overdue._

_Previous: Document updated June 2026 (interlude B) — OQ-018 (code signing and release_
_provenance for the first public artifact) added, deferred until the first_
_public release of a `grob` executable or installer. Authorised by D-317, which_
_scaffolds the SBOM and build-provenance chain this question completes. Noted as_
_unrelated to OQ-013 (`Grob.Llm` plugin): the earlier association of signing_
_with OQ-013 was a framing error, corrected here._
_Document updated May 2026 — OQ-015 (`Grob.Sql` plugin shape), OQ-016 (array_
_aggregation methods on `T[]`), and OQ-017 (triple-backtick interpolation via_
_`$```...``` ` prefix) added as open questions, all deferred until post-v1._
_Surfaced by realistic enterprise script design exercises against the v1_
_language and stdlib surface; tentative directions captured so the eventual_
_design conversations start from a known position. OQ-013 and OQ-014 import_
_examples updated from `import "..."` to `import Grob.Llm`/`import Grob.Mcp`_
_to match the documented plugin import convention; param block example in_
_OQ-014 corrected from `param { ... }` block syntax to the bare-line form_
_per `grob-language-fundamentals.md`._
_Updated May 2026 — OQ-005 resolved (`GrobValue` is a tagged union —_
_permanent, NaN boxing rejected on managed-runtime grounds, see D-303);_
_OQ-006 resolved (lean on .NET GC, no custom mark-and-sweep in v1, see_
_D-304). Both relocated from "Open Questions" to "Resolved Questions"_
_with full rationale preserved. With these closures every Sprint-1-blocking_
_open question is resolved._
_Previous: April 2026 — OQ-013 (`Grob.Llm` plugin shape) and OQ-014_
_(`Grob.Mcp` plugin shape) added as open questions, both deferred until_
_post-v1 with `Grob.Http` as a prerequisite. Tentative directions captured_
_so the eventual design conversation starts from a known position._
_Previous: April 2026 — post-Session-G cleanup: OQ-009 and OQ-010_
_body sections relocated from “Open Questions” to “Resolved Questions”_
_to match their resolved status. No content change to the resolutions_
_themselves — full rationale preserved._
_Previous: April 2026 — OQ-009 resolved (`GrobValue` provisional representation,_
_hand-rolled tagged-union struct under .NET 10 LTS, see D-297);_
_OQ-010 resolved (`.grobc` binary format skeleton spec, see D-298 and `grob-grobc-format.md`)._
_Previous: OQ-011 resolved (`Grob.Crypto` API shape);_
_OQ-012 resolved (`process.run()` timeout behaviour)._
_Previous: OQ-007 resolved (`for...in` iterable types)._
_Document created April 2026 — extracted from grob-decisions-log.md._
_Authorised decisions recorded in grob-decisions-log.md._
_This document is the implementation reference — the decisions log is the authority._
