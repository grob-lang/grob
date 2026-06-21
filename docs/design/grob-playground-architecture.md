# Grob — Playground Architecture (Sketch)

> Early design sketch. Authorised by **D-319** (June 2026).
> Status: direction settled, build deferred post-v1. Names are provisional.
> Canonical decisions live in `grob-decisions-log.md`; this doc carries the detail
> D-319 points at. Where this doc and the decisions log conflict, the log wins.

---

## 1. Purpose

A browser playground that type-checks and runs Grob in the page — the most direct
"try it" surface for `grob-lang.dev` and the LinkedIn series, and a recruitment
asset for the alpha/beta interest list. The reference points are the Topaz and Go
playgrounds: paste code, hit a button, see diagnostics or output, nothing to
install.

The playground is **not** a v1 deliverable and **not** a release gate. This doc
exists so the v1 stdlib and VM work is built against the right seams, so the
playground is a wiring exercise later rather than a retrofit.

---

## 2. The principle

> The engine is a pure, embeddable library. Every piece of OS contact goes through
> an injected host capability. The engine never assumes it owns the process.

This is the whole design. The playground is one host; the CLI is another; the LSP
and the test suite are two more. They differ only in which capability
implementations they wire in. Nothing OS-touching may live in `Grob.Core`,
`Grob.Compiler` or `Grob.Vm` — host contact stays in the host layer, exactly as the
CLI already keeps `Process.Start`, `Console.*` and `System.IO` out of the engine.

The playground is the forcing function for this discipline, but the discipline pays
the LSP and the tests too. It is not playground-specific cost.

---

## 3. Route decision

| | Route 1 — client-side | Route 2 — server-side |
|---|---|---|
| Shape | `Grob.Core` + `Grob.Compiler` + `Grob.Vm` + pure stdlib compiled to Blazor WebAssembly, run in the page | Real `grob` CLI run per request in a hardened sandbox |
| Hosting | Static assets on the existing Azure SWA | Containers / microVMs, per-request |
| Cost | None beyond static hosting | Compute per run |
| Security surface | None — no code leaves the machine | Running untrusted user code: sandboxing, resource limits, network isolation, ephemerality |
| `process` / real I/O | Not available | Available |

**Chosen: Route 1.** The deciding factor is the security surface, not the cost.
Executing arbitrary user scripts server-side is a real, ongoing security commitment
a solo project should not take on. Route 1 keeps "nothing is sent to a server" as a
stated feature. The one concession — no real `process` — is the right thing to
concede, because it is the one capability that genuinely cannot be made safe in a
browser. Route 2 stays on the table only if a future need is concrete enough to
justify the sandboxing burden; platforms that would suit it if it ever comes up are
Judge0 (purpose-built execution-as-a-service), Azure Container Instances (Azure-
native) and Fly.io (Firecracker microVMs).

---

## 4. Host capability contracts

The seam. These sit alongside `IGrobPlugin` so stdlib plugins can consume them and
hosts can implement them. Provisional home: `Grob.Runtime` (confirm against the live
`grob-solution-architecture.md` — see §9).

```csharp
// Grob.Runtime — host capability contracts

public interface IFileSystem {
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    void   WriteAllText(string path, string content);
    void   AppendAllText(string path, string content);
    IReadOnlyList<FileEntry> List(string path, bool recursive);
    void   EnsureDir(string path);
    void   Copy(string src, string dst, bool overwrite);
    void   Move(string src, string dst, bool overwrite);
    void   Delete(string path, bool recursive);
    // mirrors what the fs plugin needs, not the Grob surface 1:1
}

public interface IEnvironment   { string? Get(string key); }   // require() is plugin-side
public interface IProcessRunner { ProcessOutcome Run(string file, IReadOnlyList<string> args, ProcessOptions opts); }
public interface IStandardStreams { void Out(string s); void Error(string s); string? ReadLine(); }
public interface IClock         { DateTimeOffset Now { get; } TimeZoneInfo LocalZone { get; } }
public interface IRandomSource  { /* math.random, guid v4 — injectable so output can be pinned */ }
```

The capability surface is deliberately the plugins' needs, not a 1:1 of the Grob
`fs` API. Grob-level conveniences (`fs.readLines`, the `File` object's properties)
are composed in the plugin on top of these primitives; the host only supplies the
primitives.

---

## 5. The host bundle, and two hosts

```csharp
public sealed class GrobHost {
    public required IFileSystem      FileSystem  { get; init; }
    public required IEnvironment     Environment { get; init; }
    public required IProcessRunner   Process     { get; init; }
    public required IStandardStreams Streams     { get; init; }
    public required IClock           Clock       { get; init; }
    public required IRandomSource    Random      { get; init; }
    public CancellationToken         Cancellation { get; init; }   // dispatch-loop seam (§8)
}
```

Same contracts, different wiring — this is the entire difference between the two
hosts:

```csharp
// Grob.Cli — production
new GrobHost {
    FileSystem  = new SystemFileSystem(),        // System.IO
    Process     = new SystemProcessRunner(),     // Process.Start
    Environment = new SystemEnvironment(),
    Streams     = new ConsoleStreams(),
    Clock       = new SystemClock(),
    Random      = new SystemRandom(),
};

// Grob.Playground (Blazor WASM) — post-v1
new GrobHost {
    FileSystem   = new VfsFileSystem(seedTree),     // in-memory, upload-fed, zip-exportable
    Process      = new UnsupportedProcessRunner(),  // clear in-hierarchy error (§7)
    Environment  = new SyntheticEnvironment(seedVars),
    Streams      = new PaneStreams(outputPane, inputPane),
    Clock        = new SystemClock(),               // or PinnedClock for reproducible output
    Random       = new SeededRandom(seed),          // or SystemRandom
    Cancellation = budgetCts.Token,
};
```

---

## 6. Per-module disposition

How each of the thirteen core modules behaves under the playground host:

| Module | Disposition |
|---|---|
| `strings` `math` `regex` `path` `formatAs` `guid` | Unchanged — pure or RNG-only, the production plugin runs as-is in WASM |
| `json` `csv` | Unchanged for parse/serialise; only `stdin()`/`stdout()` reroute to the panes |
| `date` | Unchanged — clock and timezone come from `IClock` (browser by default) |
| `log` | Unchanged plugin, output via `IStandardStreams.Error` to the diagnostics pane |
| `fs` | **VFS plugin** — in-memory path→bytes, seeded + upload + zip-export |
| `env` | **Synthetic** — seeded map, optionally user-editable in a side panel |
| `process` | **Unsupported** — see §7 |

Three host capabilities change (`fs`, `env`, `process`) plus stream rerouting for the
I/O-touching functions. Everything else is the production plugin, unmodified. That
small surface is the dividend of the stdlib being pluggable in the first place.

The VFS `fs` implementation is also the unit-test filesystem double — one artefact,
two uses.

---

## 7. The "not supported" behaviour

`process` is the one genuine concession. It must stay in Grob's character — clear on
failure, never a crash — and must never leak into the shipped language.

- **Type-checks normally.** `process.*` are real signatures; a script using them is
  valid Grob. The playground does not reject it at parse time.
- **Pre-flight banner.** On Check, scan for calls into unsupported host capabilities
  and show a non-blocking banner: "this script uses `process`, which the playground
  can't run." No surprise after Run.
- **Runtime diagnostic.** If run anyway, the playground `process` plugin raises a
  normal error *in the `GrobError` hierarchy*, so a `try/catch` around it behaves
  exactly as in production, with a host-supplied message naming the limitation at
  the call site.
- **No new error code.** This is a host condition, not a language error. The shipped
  registry is immutable surface (ADR-0017); a tooling-only state has no business in
  it. The host supplies the message; the registry stays clean.

User-facing phrasing throughout: **"some functionality not currently supported in
the playground."**

The playground adapts to Grob. Grob never bends to the playground.

---

## 8. Correctness points that bind regardless

Two of these are not playground features — they are things the language must get
right anyway, which the playground happens to depend on.

**`exit()` is a non-catchable unwind — already designed.** `Grob.Runtime` already
defines `ExitSignal`, an uncatchable internal signal for `exit()` unwinding. It
unwinds the run *outside* the `GrobError` hierarchy, so a bare `catch e` (D-274)
cannot reach it; the host catches it at the top of the run and reads the exit code,
and the runtime never calls `Environment.Exit`. The playground host catches
`ExitSignal` exactly as the CLI host does. Nothing new to build here — confirmed
against `grob-solution-architecture.md`.

**The VM is fresh per run.** No static mutable run-state. The playground
re-instantiates on every Run, the LSP re-runs constantly, the test suite runs
thousands of times per process. This extends the parser's statelessness discipline
(D-300) to the runtime. The `ErrorCatalog` statics (D-308) are immutable shared data
and are fine.

**The dispatch loop needs a budget/cancellation seam.** Blazor WASM is
single-threaded by default, so a `while (true) {}` script freezes the tab with no
way to interrupt. The dispatch loop should check a budget or cancellation token
every N instructions:

```csharp
if ((++_steps & BudgetMask) == 0)
    _host.Cancellation.ThrowIfCancellationRequested();
```

Production sets it to unlimited (`CancellationToken.None`); the playground sets a
budget. The counter lives on the **VM instance**, so the budget is continuous across
re-entrant native→VM→lambda execution, and cancellation surfaces as
`OperationCanceledException` — outside `GrobError`, so a Grob `catch e` cannot
swallow it, the same uncatchable property as `ExitSignal`. **Adopted into v1 and
folded into Sprint 5 Increment C** (where the re-entrant call-back bridge lands, so
the budget-spans-the-bridge property is testable there). It also serves the LSP and
is a sane safety valve for `grob run`. See §9.

---

## 9. Accommodation map

Where each seam is naturally built, and what it costs to retrofit if missed:

| Seam | Built in | Retrofit cost |
|---|---|---|
| VM dispatch-loop budget/cancellation | **Sprint 5 Increment C** (adopted) | n/a — done in v1 |
| `print` `input` `log`, stdout/stderr/stdin via `IStandardStreams` | Sprint 8 | Medium |
| `exit()` as non-catchable unwind | **already designed** (`ExitSignal`) | n/a |
| `fs` / `env` / `process` via injected capabilities | Sprint 8 (`env`), Sprint 9 (`fs`, `process`) | **High** — plugin internals |
| Static / explicit plugin registration (no mandatory reflection-scan, for trimming + AOT under WASM) | Plugin loader / module system (late) | Medium |
| Structured diagnostics surface (code, `SourceLocation`, severity) the host renders | LSP-facing work | Low — likely already structured; confirm |
| Capability-interface DAG home | **confirmed: `Grob.Runtime`** | n/a |

**The top row is now in v1** — the dispatch-loop seam is adopted and folded into
Sprint 5 Increment C. `exit()` was already designed (`ExitSignal`), and the
capability-interface home is confirmed (`Grob.Runtime`, §10). The remaining rows get
built right by knowing the constraint going into the sprint that builds them; none
needs pre-emptive scaffolding.

---

## 10. DAG placement (confirmed against the live solution architecture)

Confirmed: the capability interfaces belong in `Grob.Runtime`, beside `IGrobPlugin`,
`GrobError` and `ExitSignal`. The stdlib plugins reference `Grob.Runtime` already
(to implement the plugin contract) and consume the capability interfaces from there;
both hosts (`Grob.Cli`, future `Grob.Playground`) reference it to implement them.
This keeps `Grob.Compiler` and `Grob.Vm` from referencing each other and keeps
`Grob.Core` as the only shared ground.

Verified against the live `grob-solution-architecture.md`: `Grob.Vm` already
references `Grob.Core` and `Grob.Runtime` (it dispatches plugin/native functions),
so the VM can reach the capabilities; exposing them through the existing `GrobVM`
registration surface adds no new reference and introduces no cycle. One consequence
to note: because `Grob.Runtime` is the public plugin NuGet contract, the capability
interfaces become part of that public surface — which is correct, since a
third-party plugin doing I/O should also route through `IFileSystem` rather than
calling `System.IO` directly, otherwise it bypasses the VFS and breaks the sandbox.

A future `Grob.Playground` host project is additive — it sits at the top of the DAG
like `Grob.Cli`, references the engine and `Grob.Stdlib`, and ships none of the
OS-touching host implementations the CLI carries.

---

## 11. Open items

- ~~Adopt the dispatch-loop budget/cancellation seam into v1 VM scope?~~ **Done** —
  adopted and folded into Sprint 5 Increment C.
- ~~Confirm capability-interface home in the live solution architecture.~~ **Done** —
  confirmed `Grob.Runtime` (§10).
- Confirm the diagnostics surface is consumable as structured data, not only
  formatted text (shared with the LSP).
- Playground UX: lead with **Check** (all-errors-at-once, the actual differentiator)
  over **Run**. `param` blocks can drive an input form via the same introspection
  the MCP `param`→JSON-Schema reflection uses (OQ-014).
- Reproducible output: `PinnedClock` + `SeededRandom` if shared snippets should
  render identically (as Topaz pins its output). Free, given `IClock` / `IRandomSource`.
- Payload size: Blazor WASM ships the runtime plus the engine assemblies — a few MB
  on first load. Trimming handles most; AOT if it matters.
