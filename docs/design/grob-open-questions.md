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
    maxTokens: 500
)

print(reply.text)

type Triage {
    severity:     string
    rootCause:    string
    suggestedFix: string?
}

triage := llm.completeAs<Triage>(
    model:  "claude-sonnet-4-5",
    prompt: "Triage this stack trace: ${stack}"
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
    prompt: "Summarise this ticket for the standup: ${ticket.description}"
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
    #{ active: true }
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
| R-01 | **E4202 removed.** Its condition becomes a subset of E2202's once ordering is enforced, so it may not be removed earlier — doing so would leave the ordering diagnostic with no code to fall back to (D-414). Requires the `ErrorCatalog.cs` and registry edits in one commit (D-316); number permanently burned, never reused (D-410). | D-410, scheduled by D-414 | *Finish the param concern* increment (ordering enforcement) | E4202 absent from `ErrorCatalog.cs` and `grob-error-codes.md`; count 120; D-316 green |
| R-02 | **E2202's title widened** to name `type`, `const` and `readonly` declarations alongside `fn` and top-level statements — its current title is narrower than its actual condition (D-412). Title lives in `ErrorCatalog.cs`. | D-410, D-412, scheduled by D-414 | *Finish the param concern* increment (ordering enforcement) | Title updated in both locations in one commit; D-316 green |
| R-03 | **E4102's title widened** for `@minValue`/`@maxValue` — currently "invalid `@minLength` / `@maxLength` argument" — or a sibling code minted. D-414 groups it with R-01 and R-02 as turning on the ordering rule rather than on the grammar. | D-410, D-411, scheduled by D-414 | *Finish the param concern* increment (ordering enforcement) | Title covers the value pair, or a sibling exists; D-316 green |
| R-04 | **`Grob.Core`'s public surface narrowed.** It becomes SDK surface when `Grob.Runtime` publishes, while holding `Chunk`, `BytecodeFunction`, `CatchHandler`, `DiagnosticBag` and `IVmCallHost` — internals a plugin author should not see. Narrowing after publication is a breaking change. | D-419 | Sprint 11, **before first package publication** | An explicit allow-list names every type in `Grob.Core`'s intended public SDK surface; `Chunk`, `BytecodeFunction`, `CatchHandler`, `DiagnosticBag` and `IVmCallHost` are `internal` with `InternalsVisibleTo` to the assemblies that need them, and no public type sits outside the allow-list |
| R-05 | **Harness advertised-versus-built audit.** `.claude/` has never had the scrutiny the design corpus has had. Known: two skills, `src/CLAUDE.md` and — found by D-421, not on D-419's list — `plugins/CLAUDE.md:27` instruct authors to supply a `FunctionSignature` that has never existed, with a three-argument registration example that would not compile. Also found by D-421: `tests/CLAUDE.md:60` documents the error-example pair naming as `*_grob.txt`/`*_expected.txt`, while all 57 pairs on disk are `<case>.grob`/`<case>.expected.txt` — a convention an author would follow into a file the harness would not find. | D-419 | `FunctionSignature` increment (known sites); full sweep `unowned` | Known sites corrected in the commit that makes them true, **and** the full `.claude/` sweep is complete with every stale advertised interface it finds corrected or removed — scheduling the sweep does not close this item, only finishing it does |
| R-06 | **Type-name LSP mechanism specified.** §3.1.1 requires `ResolvedType`/`Declaration` on identifier *expression* nodes; D-416 ruled `TypeRef` is outside it. Go-to-definition on a type name therefore resolves by some path no document names — plausibly `UserTypeRegistry` plus `Symbol.DeclaredAt`, but unstated. | D-416 | `unowned` — before any LSP work | A document states how a type name resolves to its declaration, and §3.1.1 says whether it is in scope |
| R-07 | **D-418's corpus corrections.** `grob-type-registry.md` (which carries both `mapAs` conventions side by side), `grob-stdlib-reference.md`, wiki `json.md`/`csv.md`, and validation script 04, to the uniform whole-type convention; script 11's `[ASSUMPTION]` marker removed. Script edits touch `.grob` corpus files and their markdown publication together (D-417's sync guard). | D-418 | `mapAs<T>` increment (Sprint 9D) | All sites use `mapAs<T>(): T`; `ValidationScriptCorpusTests` and `ValidationScriptMarkdownSyncTests` green |
| R-08 | **Decorator release-gate coverage.** All seven decorators ship in v1 (D-411), but only `@secure` appears in any validation script — six would ship unexercised, which is the advertised-but-unbuilt shape in advance. | D-411 | Sprint 10 | At least one release-gate script exercises the full decorator surface |
| R-10 | **`docs/errors/Exxxx.md` written.** 121 codes at creation, zero documents; `--explain Exxxx` is specified to read them. Four sections each: cause, example, fix, see-also. | v1 requirements §10 | `unowned` — v1 release gate | A document exists for every code currently in the registry (count may move — R-01 alone drops it to 120 — so this checks against the registry's live contents, not a fixed number), and `--explain` resolves for each of them |
| R-11 | **Error-examples library harnessed.** 57 gold-master pairs, zero references anywhere in `tests/`, `src/` or `tooling/` — the negative-test release gate does not currently exist, and every expected-output file is unverified against the current compiler. | D-415, D-417 | *Error-examples harness* increment | A test runs all 57 pairs; every stale master is reconciled or quarantined with a documented reason |
| R-12 | **`FunctionSignature` implemented** — the contract D-081 required in April 2026 and D-419 specified. Re-sequenced ahead of Sprint 9C by D-420 so `fs` is the first module built on it rather than the fifth migrated onto it. | D-081, D-419, D-420 | *`FunctionSignature`* increment, **before Sprint 9C** | Every native routes through `FunctionSignature`; `RegisterNative` requires one; `NamespaceRegistry` produces rather than defines; no behaviour change |

### Closed items

| # | Item | Raised by | Closed by | How |
|---|---|---|---|---|
| R-09 | **§3.5's construct enumeration completed.** | D-417 | **D-421** | `grob-formatter-specification.md` §3.5 now enumerates every comma-or-newline-separated construct in the language and states that the enumeration is complete. Switch-expression arms joined Category B (and §3.2's wrappable list); a new Category C covers the four comma-separated constructs that are never wrapped — lambda parameter lists, function-type parameter lists, type-argument lists and `select` case pattern lists; `for k, v in` is named as a fixed pair rather than a list. Closed ahead of its Sprint 12 owner because D-421 could not state the grammar rule without it. |

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

_Document updated August 2026 — D-421. **R-09 is the register's first closed_
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
