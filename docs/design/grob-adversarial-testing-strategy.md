# Grob — Adversarial Testing Strategy

> **Purpose:** This document defines the pre-alpha adversarial test suite for Grob
> — the deliberate, unforgiving effort to break the language, subvert its runtime
> and exfiltrate its credentials before a stranger does. It is distinct from the
> cooperative test families (§12 unit/integration, the D-337 smoke family, the
> D-346 eleven-script validation suite), all of which verify that Grob does what it
> should. This suite verifies that Grob *fails well* when someone does what they
> should not.
>
> **Authority:** D-353 authorises this document. D-352 pins the `Grob.Http`
> redirect and credential-forwarding policy that Pillar 7 verifies. The decisions
> log remains the authority on conflict.
>
> **Status:** Planning. Harness skeleton and Pillar 1 layers 1–2 build during and
> immediately after Sprint 9; the CPU-heavy campaigns run in two passes — a Sprint
> 9/10 interlude for Pillars 1–6 and a post-Sprint-11 pass for Pillar 7 (§11).
>
> **Last updated:** July 2026

-----

## Table of Contents

1. [The Contract](#1-the-contract)
2. [Severity Ladder](#2-severity-ladder)
3. [Pillar 1 — Compiler Fuzzing](#3-pillar-1--compiler-fuzzing)
4. [Pillar 2 — Hostile `.grobc`](#4-pillar-2--hostile-grobc)
5. [Pillar 3 — Differential and Metamorphic Testing](#5-pillar-3--differential-and-metamorphic-testing)
6. [Pillar 4 — Stdlib and Environment Brutality](#6-pillar-4--stdlib-and-environment-brutality)
7. [Pillar 5 — Resource Exhaustion and Soak](#7-pillar-5--resource-exhaustion-and-soak)
8. [Pillar 6 — Cold-Read Usage Campaigns](#8-pillar-6--cold-read-usage-campaigns)
9. [Pillar 7 — Hostile Network Surface](#9-pillar-7--hostile-network-surface)
10. [The `Grob.Torture` Harness](#10-the-grobtorture-harness)
11. [Sprint 9/10 Hardening Interlude](#11-sprint-910-hardening-interlude)
12. [Alpha Exit Criteria](#12-alpha-exit-criteria)

-----

## 1. The Contract

Every adversarial test reduces to one invariant. State it once and gate on it:

> **No input — source text, bytecode, CLI arguments, environment, file system state
> or child-process behaviour — may ever produce an unhandled .NET exception, a host
> stack trace, a hang without a Ctrl+C response or an exit code outside the
> documented set. Every failure is a Grob diagnostic with an E-code on stderr.**

Any violation is a **P0** regardless of how contrived the input. This is the alpha
bar. A stranger's first experience of Grob crashing with a `NullReferenceException`
and a CLR stack trace is unrecoverable reputationally in a way that a wrong answer
is not. The harness's core assertion is literally *"captured stderr contains no
.NET stack frame"* — a regex over `   at Namespace.Type.Method(` frame lines and
the `Unhandled exception:` banner.

The documented exit-code set is the contract surface for automation, so it is
enumerated and pinned here as the reference the harness asserts against. Grob exit
codes:

| Code | Meaning |
| --- | --- |
| 0 | Success |
| 1 | Runtime error (uncaught `GrobError`) |
| 2 | Compile error (one or more diagnostics) |
| 3 | Usage error (bad CLI arguments, missing file) |
| *n* | Script-chosen via `exit(n)` — passthrough, `0..255` |

Any exit code the runtime itself produces outside `{0, 1, 2, 3}` is a P0. A
script-chosen `exit(n)` outside `0..255` is a P1 — the runtime must clamp or
diagnose, never let the host truncate silently to a surprising value.

-----

## 2. Severity Ladder

Findings are triaged the moment they land. The ladder is the shared vocabulary
between the harness's automatic classification and manual review.

| Severity | Definition | Disposition |
| --- | --- | --- |
| **P0** | Host crash, CLR stack trace on stderr, unresponsive hang, undocumented runtime exit code, or credential leak | Blocks alpha. Fix before the interlude closes. |
| **P1** | Graceful but wrong — incorrect result, misleading diagnostic, wrong E-code, exit-code clamp missing | Triaged with a fix or a documented D-### before alpha. |
| **P2** | Ugly but correct — unhelpful message wording, poor recovery cascade, missing `--explain` prose | Feeds the error-message quality bar; not an alpha blocker. |
| **P3** | Cosmetic or noted-for-later | Logged, deferred. |

**Automatic classification.** The harness assigns P0 on the no-stack-trace
assertion failing, on a wall-clock timeout with no Ctrl+C response, on a non-zero
process exit that is neither a clean diagnostic nor a documented code, and on a
credential-sentinel appearing in captured output. Everything else is triaged by
hand. **Deduplication** is by diagnostic signature — the normalised first stack
frame for a crash, or the E-code plus normalised message shape for a diagnostic —
so a fuzzer finding the same bug ten thousand ways files one finding, not ten
thousand.

-----

## 3. Pillar 1 — Compiler Fuzzing

The compiler front door. Three layers, increasing structure.

### 3.1 Byte and token mutation

Seed corpus: the eleven validation scripts, the five smoke scripts and the 40
error-example sources. Mutations:

- Byte flips, insertions and deletions at every offset.
- Truncation at every offset — a source that ends mid-token, mid-string,
  mid-block.
- Token-sequence splicing between seeds.
- Invalid UTF-8 byte sequences, lone surrogates, overlong encodings.
- BOM at start, BOM mid-file, multiple BOMs.
- NUL bytes embedded in source and inside string literals.
- Bidirectional-override characters (Trojan Source: `U+202E`, `U+2066`–`U+2069`)
  — the compiler must not let a right-to-left override make the lexed token
  stream disagree with the visible source. The desired posture is to reject or
  neutralise bidi controls outside string literals with a diagnostic.
- A single 10 MB line, a file of 10 million newlines, an identifier one megabyte
  long.

Target: the lexer and the D-300 error-recovering parser. The recovery machinery is
where cascade bugs and infinite synchronisation loops live — a parser that never
terminates on some mutated input is a P0 hang, caught by the harness wall-clock
timeout.

### 3.2 Grammar-based generation

A deterministic C# generator, sibling to `bench/Grob.Benchmarks/Generators/
SyntheticLargeGenerator.cs`, seeded for reproducibility. It produces *structurally
valid but pathological* programs from the grammar:

- Nesting depth 10,000 — blocks, parentheses, array literals, `if`/`else if`
  chains, `select` arms.
- Expression chains that stress operator-precedence recursion: 5,000 binary
  operators, deeply right-associated ternaries, `?.` chains hundreds deep
  (D-296 four-category resolution under load).
- 5,000-arm `select` and a 5,000-case switch expression (exhaustiveness checker
  under load, D-301).
- Closures capturing hundreds of upvalues across dozens of scope levels (D-115,
  D-296 upvalue resolution).
- Every escape sequence adjacent to every string form, every interpolation edge
  (`${}` empty, `${` unterminated, nested interpolation, `\$` at boundaries).
- Maximum-length identifiers, maximum literal magnitudes at every numeric
  boundary.

**Headline risk:** recursive-descent parsing on hostile nesting must produce a
diagnostic, not a `StackOverflowException` — which is uncatchable in .NET and kills
the process. This pillar is expected to force an explicit parser depth guard if one
does not already exist; the finding and the guard's threshold get their own D-###.
The guard's diagnostic reuses the existing depth-limit error family (the D-296-era
stack-overflow-at-depth-256 runtime rule has a compile-time analogue here).

### 3.3 Coverage-guided fuzzing (SharpFuzz)

SharpFuzz (libFuzzer for .NET) drives the lexer → parser → type-checker pipeline
in-proc, instrumented for coverage feedback. This is the industrial layer — it
finds the inputs no human generator writes. Runs as long CPU-hour campaigns, not
CI gates. Corpus minimisation and crash-repro extraction are part of the runbook;
each crash is preserved as its minimised input plus the exact repro command.

-----

## 4. Pillar 2 — Hostile `.grobc`

The VM currently trusts its bytecode because the compiler produced it. Pre-alpha,
someone *will* hand it a hand-edited or corrupted `.grobc`. This pillar fuzzes the
binary format per the D-298 skeleton:

- Truncated headers, wrong magic, wrong version, lying length fields.
- Constant-pool indices out of range.
- Jump targets past chunk end, into the middle of a multi-byte instruction, or
  negative.
- Opcode sequences engineered to underflow or overflow the operand stack.
- Declared arities that lie — a function claiming more or fewer parameters than
  its call sites provide.
- Upvalue indices pointing nowhere.
- Malformed constant-pool entries (a string constant with a length exceeding the
  chunk).

**Open decision this pillar forces (see §11):** does the VM get a load-time
verifier — Java-class-file style, validating the whole chunk before execution — or
per-instruction bounds checks with graceful diagnostics? A verifier is the better
language answer: it makes `.grobc` a trustable interchange format and keeps the
dispatch loop free of per-instruction guard branches. It is also real,
Sprint-scoped work. The fuzz campaign runs first against whatever the VM does
today, and the crash count makes the case for how much verifier is warranted. The
resulting decision is logged with its own D-###.

Either way, the contract holds: a malformed `.grobc` produces a `NetworkError`-class
load diagnostic, never a CLR crash.

-----

## 5. Pillar 3 — Differential and Metamorphic Testing

Grob has no second implementation to diff against, so oracles are manufactured.

### 5.1 Metamorphic transforms

Properties that must hold across semantics-preserving rewrites:

- **Formatter neutrality.** `fmt(x)` behaves identically to `x`. This stress-tests
  the formatter spec at fuzz scale, not just on curated inputs.
- **Formatter idempotence.** `fmt(fmt(x)) == fmt(x)` byte-for-byte — a formatter
  spec claim, now property-tested against generated programs.
- **Declaration reorder neutrality.** Reordering top-level declarations is
  behaviour-neutral. This specifically stress-tests the D-166 two-pass type
  checker's forward-reference handling.
- **α-renaming neutrality.** Consistent identifier renaming leaves behaviour
  unchanged.
- **`const` inlining fidelity.** A `const` binding and its inlined literal produce
  identical output (D-288/D-289 inlining correctness).

### 5.2 FsCheck arithmetic and semantics properties

FsCheck is already in the toolchain. Extend it to generated expression trees
evaluated two ways — by the VM, and by a trivial C# tree-evaluator over the same
AST. Any divergence is a bug in one of them. Targets:

- Checked-overflow behaviour at every `int` boundary, `int.MinValue / -1`.
- Float edge cases: `NaN`, `±Inf`, `-0.0`, subnormals, `NaN != NaN`.
- `%` sign semantics with mixed-sign operands.
- Short-circuit evaluation order — `&&`/`||` and `?.` must not evaluate the
  right-hand side when the left decides the result.
- `int → float` implicit conversion (the only implicit one) at magnitudes where
  precision is lost.

### 5.3 Cross-model spec adversaries

Give two vendor models — GPT-5.x Codex and one other supplier's model — the
**language spec only**, no implementation. Instruct each to (a) write programs
targeting corners the spec under-specifies and (b) predict exact stdout, stderr and
exit code. Run the programs against Grob. Three-way disagreement triages cleanly:

- **Both models agree, Grob differs** → likely an implementation bug (P1).
- **The two models disagree with each other** → the *spec is ambiguous* (a design
  finding, worth a D-### on its own — the spec is under-determined at that point).

This finds spec holes no fuzzer can, because the fuzzer has no notion of intended
behaviour. It is the cheapest source of the ambiguities that would otherwise
surface as confused alpha bug reports.

-----

## 6. Pillar 4 — Stdlib and Environment Brutality

This is where real-world Windows breaks things, and Windows developers and
sysadmins are the target audience. A torture matrix per module. Highlights, not
the full inventory (the harness fixture set is the full inventory).

### 6.1 `fs` and `path`

Reserved device names (`CON`, `NUL`, `COM1`–`COM9`, `LPT1`), paths past 260
characters with and without the long-path opt-in, trailing dots and spaces (which
Win32 silently strips), UNC paths (`\\server\share`), junction and symlink loops,
files locked by another process, read-only attributes, case-only renames on a
case-insensitive volume, non-UTF-8 file content under `fs.readText`, alternate data
streams (`file.txt:hidden`). Backtick raw strings are canonical for Windows paths
(D-285) — every fixture uses them, and the mutation set includes the escape-hazard
forms deliberately.

### 6.2 `process`

The notorious Windows argument-quoting rules: embedded quotes, trailing
backslashes, a mix of both. Children producing gigabytes of stdout — the classic
pipe-deadlock, where the child blocks on a full pipe the parent is not draining.
Children that never exit (does the D-OQ-012 timeout fire and reap cleanly?).
Children that outlive Grob. Exit codes above 255. `process.runShell` metacharacter
behaviour — documented and tested, not discovered by an alpha user. The `run` vs
`runShell` split (§11 security) is asserted: `run` performs no shell interpolation.

### 6.3 `regex`

Catastrophic backtracking. A ReDoS pattern such as `(a+)+$` against a long
non-matching input must not hang. .NET `Regex` supports a `matchTimeout`; Grob must
set one and surface expiry as a `GrobError`, not a hang. If it does not today, this
pillar forces the decision and the timeout value gets a D-###.

### 6.4 `json` and `csv`

1,000-deep nesting (recursion again — parser and any recursive serialiser),
duplicate keys, lone surrogates in strings, numbers exceeding `int64`, `1e999`,
CRLF/LF/BOM permutations, quotes within quotes within quotes, a CSV cell containing
every delimiter and newline form. `json.parse` on malformed input throws `JsonError`
— never a CLR parse exception.

### 6.5 Environment matrix

`cmd` vs PowerShell vs Windows Terminal. `chcp 850` vs `65001`. Redirected and
piped stdin — what does `input()` do non-interactively, against `TextReader.Null`
(D-344's closed-stdin path, E5305)? Windows 10, Windows 11 and Windows Server. x64
and ARM64. Ctrl+C at every pipeline stage, with exit-code assertions.

### 6.6 Credential-opacity gate

A dedicated `@secure` and `AuthHeader` leak hunt that mechanises the D-159/D-336
concern into a permanent gate. Force a sentinel secure value through every
rendering path — string interpolation, `ValueDisplay` nesting (`Inspect`),
`NetworkError` and every other error message, `log` at every level, catch-and-
rethrow, `formatAs.table`/`list`/`csv`, `toString()`, `--verbose` — then grep all
captured stdout and stderr for the sentinel. Any hit is P0. `AuthHeader.toString()`
returning `[AuthHeader]` is asserted, but the interesting failures are the paths
that never call `toString()` — a raw exception message that happened to interpolate
the header. This gate shares its sentinel machinery with Pillar 7.

-----

## 7. Pillar 5 — Resource Exhaustion and Soak

- **Allocation until OOM.** A script that allocates without bound — graceful
  diagnostic, or host death? The desired posture is a `GrobError`, though a true
  managed OOM may be unrecoverable; the finding sets expectations honestly.
- **VM-level recursion until stack exhaustion.** Call frames, not parser nesting.
  This must produce the `GrobError` stack-overflow-at-depth diagnostic, never the
  CLR `StackOverflowException`.
- **File-handle exhaustion.** Open handles until the OS refuses — clean diagnostic,
  no leak of the partially-opened state.
- **Soak.** The D-346 stability harness extended into multi-hour runs of looping
  real scripts, with heap-size and handle-count assertions at checkpoints. D-346's
  own run caught a one-time cache/registry warm-up step between iterations 1,000
  and 2,000 — exactly the class of thing soak finds and nothing else does. The soak
  cadence is longer than the interlude; it runs pre-release and on demand.

-----

## 8. Pillar 6 — Cold-Read Usage Campaigns

Distinct from Pillar 3's spec adversaries. Here models — and Chris, in a
deliberately naive hat — act as *day-one users*: given only the public docs and the
eleven validation-script task descriptions, they attempt real tasks and log every
confusion, misleading error message and doc/behaviour mismatch. Findings feed the
error-message quality bar (P2) and the long-form `--explain Exxxx` prose that is
scheduled for a dedicated pre-release session. This is the cheapest rehearsal of
the actual alpha available, and it is the one pillar whose output is primarily
about *legibility* rather than *correctness*.

-----

## 9. Pillar 7 — Hostile Network Surface

`Grob.Http` and `Grob.Crypto` are load-bearing across the validation suite (scripts
4, 7, 10, 11, 13). This pillar exists because two security classes — cross-origin
credential forwarding and SSRF-via-redirect — are handled badly by mocks and need
real infrastructure to prove. D-352 pins the policy this pillar verifies.

### 9.1 Two-tier infrastructure

Local Kestrel is the workhorse; the Azure tenant carries only what is irreducibly
real. The whole HTTP torture suite must not depend on Azure being up and
credentialed — that gates CI on a subscription and adds flakiness. The Azure side
is skipped when credentials are absent, so CI stays green offline.

| Case | Local hostile Kestrel | Azure tenant |
| --- | :---: | :---: |
| Slowloris, byte-trickle, never-ending chunked | ✓ | |
| Content-Length lies (over and under), connection reset mid-body | ✓ | |
| Decompression bombs (gzip → GB) | ✓ | |
| Malformed/truncated JSON, wrong Content-Type, giant/duplicate headers | ✓ | |
| `download()` partial-file cleanup on mid-stream reset | ✓ | |
| `verifySha256` against mismatched hash and mutating content (TOCTOU) | ✓ | |
| **Redirect credential forwarding** (cross-origin) | ✓ (two localhost ports) | ✓ (real cross-host) |
| **SSRF via redirect to IMDS** (`169.254.169.254`) | simulated | ✓ (genuinely reachable from an Azure VM) |
| Real TLS: expired cert, self-signed, wrong host, odd chains | | ✓ (Azure-managed + a deliberately-expired cert) |
| `auth.*` against real Azure AD (acquisition, expiry, 401-then-refresh) | | ✓ (throwaway app registration) |
| Real latency, DNS, network flakiness | | ✓ |
| **Positive:** validation scripts 4/7/11 against real Azure DevOps and ARM | | ✓ |

The harness spins up the local `HostileServer` (an ASP.NET Core Kestrel app) in-proc
per test, deterministic and CI-friendly. The Azure side is a Bicep-provisioned
resource group the harness targets by config.

### 9.2 The two headline finds

**Redirect credential stripping (D-352).** Server on port A returns `302` to port
B; port B echoes every header it received; assert the `Authorization` header is
*absent*. If present, P0 stdlib vulnerability. The Azure variant does the same
across real hosts; the IMDS variant makes the target `169.254.169.254` — if Grob
can be steered into hitting Azure's metadata endpoint carrying a token, that is the
worst-case realisation, and the tenant makes it a live test rather than a thought
experiment. D-352 also requires https→http downgrade to throw `NetworkError` and
the hop chain to cap at 10; both are asserted here.

**Credential opacity end-to-end.** Shares the sentinel machinery with §6.6, applied
to the HTTP surface specifically: push a sentinel bearer through `http.get` failure
paths, `NetworkError` messages, `--verbose` and `log`, then grep all output. Any
hit is P0.

### 9.3 The positive dimension

The tenant also lets validation scripts 4, 7 and 11 run against *real* Azure —
DevOps REST, ARM provisioning, `env.require` credential flow, `auth.bearer` against
a real PAT. This is the first time Grob's flagship use case is exercised end-to-end
outside a mock, and the closest rehearsal of the target-user experience before
alpha. It runs as a distinct **real-Azure integration** pass, separate from the
adversarial ones — green here is a different signal (it works) from green on the
adversarial cases (it cannot be subverted).

### 9.4 Infrastructure hygiene

The deliberately-vulnerable Azure endpoints are themselves an attack surface — an
open-redirect-to-IMDS box on the public internet is an SSRF pivot for someone else.
Lock the resource group to the runner's egress IP, hold no real secrets anywhere in
it, keep it short-lived and tear it down after each campaign. Meter cost: slowloris
and soak hold connections open for a long time against real endpoints.

-----

## 10. The `Grob.Torture` Harness

A new `tooling/Grob.Torture` console application, deliberately outside `tests/` —
these are campaigns, not CI gates. It drives `grob.exe` as a pure black box.

**Black-box mode (primary).** Process-level invocation of the built CLI with:

- Wall-clock timeouts per invocation (the hang detector).
- Full stdout, stderr and exit-code capture.
- The no-stack-trace assertion over stderr.
- The exit-code contract assertion (§1).
- The credential-sentinel grep over all captured output.
- Crash-artefact preservation: on any P0, the input plus the exact repro command
  is written to a `findings/` directory, named by diagnostic signature.
- Deduplication by diagnostic signature.

**In-proc mode (SharpFuzz).** For coverage-guided throughput against the compiler
pipeline, the harness exposes an in-proc entry point SharpFuzz instruments. This
mode trades the black-box fidelity of a real process boundary for orders-of-
magnitude more executions per second.

**Graduation to CI.** A small, fast, deterministic subset — the reserved-device-name
fixtures, the credential-opacity gate, the metamorphic properties at low iteration
counts, the malformed-`.grobc` corpus — graduates into `tests/Grob.Torture.Tests`
once the campaigns stabilise. This subset is a per-commit gate. The long campaigns
run on demand and pre-release, never in the per-commit path.

**Placement in the solution.** `tooling/Grob.Torture` sits alongside
`tooling/Grob.BenchCheck`. `tests/Grob.Torture.Tests` is a normal test project and
therefore falls under the D-335 membership gate — it must be referenced by
`Grob.slnx` or the build fails.

-----

## 11. Sprint 9/10 Hardening Interlude

The work splits by cost *and* by build-plan dependency. The build plan lands the
remaining **core** modules (`fs`, `date`, `json`, `csv`, `regex`, `process`) at
Sprint 9, but the first-party plugins (`Grob.Http`, `Grob.Crypto`, `Grob.Zip`) not
until **Sprint 11**. So the adversarial work runs in two passes, not one — and
Pillar 7 cannot be part of the 9/10 interlude, because its subject does not yet
exist.

### 11.1 During and immediately after Sprint 9

- Pillar 1 layers 3.1 and 3.2 (mutation and grammar-based generation).
- The `Grob.Torture` harness skeleton, black-box mode, the no-stack-trace and
  exit-code assertions, the `findings/` machinery.
- The Pillar 4 fixture set for the modules Sprint 9 lands — these modules are
  precisely what Pillar 4 hammers, and finding parser hangs and pipe deadlocks now
  is cheaper than finding them later.

### 11.2 The Sprint 9/10 interlude proper — Pillars 1–6

At Sprint 9 close the whole language, VM and core-stdlib surface is complete
(Sprint 8's `math`/`strings`/`path`/`env`/`log`/`formatAs`/`guid` plus Sprint 9's
`fs`/`date`/`json`/`csv`/`regex`/`process`). Everything that does not need a plugin
is torturable. This interlude, with its own Definition of Done, runs:

- Pillar 1 layer 3.3 (SharpFuzz campaigns).
- Pillar 2 (hostile `.grobc`).
- Pillar 3 in full, including 3.3 (cross-model spec adversaries).
- Pillar 4 in full across the core stdlib — the Windows environment brutality, ReDoS
  (`regex` is a Sprint 9 module, so the `matchTimeout` decision is forced here), pipe
  deadlock, reserved device names, the credential-opacity gate for `@secure`.
- Pillar 5 (exhaustion and the first soak run).
- Pillar 6 (cold-read campaigns against the core surface).
- Triage of every finding against the severity ladder, every P0 fixed, every P1
  dispositioned.

### 11.3 Post-Sprint-11 network-hardening pass — Pillar 7

The hostile network surface waits for `Grob.Http` at Sprint 11. This smaller pass
runs Pillar 7 in full — the local hostile Kestrel server, the two-tier Azure work,
the redirect and IMDS security tests, the HTTP-side credential-opacity gate — and is
where **D-352 is empirically verified**. D-352's policy is pinned now (this document
and its decision), so the Sprint 11 `Grob.Http` implementation builds to it; only the
verification is deferred to when there is something to verify. Because the
eleven-script validation suite itself depends on the plugins (scripts 4, 7, 10, 11,
13 use `Grob.Http`/`Grob.Crypto`), alpha is necessarily post-Sprint-11 anyway — the
network pass is not on a critical path it would otherwise shorten.

**Decisions the interludes are expected to force and log:**

1. The `.grobc` load-time verifier vs per-instruction bounds checks (Pillar 2).
2. The parser recursion-depth guard and its threshold (Pillar 1 §3.2).
3. The `regex` `matchTimeout` value (Pillar 4 §6.3), if not already set.
4. The `exit(n)` clamp/diagnose behaviour for out-of-range `n` (§1).

Each is a language-design or runtime decision, not a test detail, so each rides as
its own D-### rather than silently in a fix.

**Definition of Done — both passes:** §12 satisfied. The Sprint 9/10 interlude
satisfies every criterion that does not name Pillar 7 or D-352; the post-Sprint-11
pass closes the remaining two (Pillar 7 green, D-352 verified). Alpha ships only when
both are met.

-----

## 12. Alpha Exit Criteria

Quantitative, or it is theatre.

- **Zero P0s** across the calibrated coverage-guided fuzzing budget. The budget is
  calibrated during the interlude — 24 CPU-hours is a sane floor, not a ceiling —
  and the locked number is recorded in the D-353 follow-up alongside the crash
  count that justified it.
- **Full Pillar 4 matrix green** — every environment-brutality fixture passes on
  Windows 11, Windows Server and both architectures.
- **Every registry error code demonstrably reachable** by at least one fixture. An
  E-code no input can produce is either dead or a gap; both are findings. (This is
  the natural home for the error-code coverage audit the registry has wanted.)
- **Every P1 dispositioned** — a fix in the tree or a D-### recording the accepted
  behaviour.
- **Both cross-model spec adversaries run**, with every spec-ambiguity finding
  dispositioned as a clarifying D-### or an accepted under-determination.
- **D-352 verified** against real cross-host and IMDS-reachable endpoints — the
  credential-forwarding policy holds empirically, not just by construction.
- **The credential-opacity gate green** across §6.6 and §9.2 — the sentinel appears
  in no captured output on any path.

Meeting these is the alpha gate. The suite does not certify Grob correct; it
certifies that Grob fails the way it promises to, which is the property a stranger's
first hour actually depends on.

-----

## Sensitive-Topic Note

Pillars 3.3 and 6 involve running models from external suppliers against the spec
and docs. Those runs are cold-read QA inputs in the established GPT-5.x Codex
mould — the models predict and probe, Chris owns every disposition. No model output
is accepted into the corpus without review, consistent with the project's
AI-augmented-not-vibe-coded discipline.
