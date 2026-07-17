# Grob — Principal-Level Review at Sprint 9 Increment B Close

> **Scope.** A cold-read deep dive across the full design corpus and decisions log
> as of the Sprint 9 Increment B close (log tail D-355, error-code count 118). No
> changes have been made to any document. Every finding below was verified against
> the live corpus in this session, not recalled from prior context. Where a finding
> depends on source-tree state the corpus cannot confirm, it is marked
> **[verify on disk]**.
>
> **Reviewer stance.** Principal compiler architect and language designer. The
> filter applied throughout is the foundational principle: does this produce a
> better language? Process convenience was not accepted as grounds for deferral.

-----

## Verdict in one paragraph

The corpus is in strong shape. The decisions log discipline is genuinely working —
D-346, D-349 and D-351 each caught their own class of drift and recorded it honestly
rather than papering over it. The recent increment record (D-348 through D-355) is
the best-evidenced stretch in the log. The findings below are therefore not
symptoms of a failing process; most of them are gaps the process itself has already
surfaced and deferred, now due for scheduling. Two are new: a language-design
incoherence in `date` comparison semantics (F3) and a scalability problem in
named-type dispatch that Sprint 9's remaining increments will make seven times
worse if not addressed first (F2). One is a wrong-code class of bug that deserves
elevation above its current "named for scheduling" status (F1b).

-----

## Severity model

| Tier | Meaning |
| ---- | ------- |
| **P1** | Affects Sprint 9's remaining increments or produces wrong behaviour today. Fix or decide before Increment C. |
| **P2** | Must land before the Sprint 9/10 hardening interlude — the adversarial suite (Pillars 3 and 6) reads the corpus as the spec, so corpus defects become noise findings at scale. |
| **P3** | Housekeeping. Fold into the next natural touch of each file. |

-----

## P1 — Before Sprint 9 Increment C

### F1 — The collection method surface does not exist, and the release gate depends on it

**Evidence.** D-351 confirmed by code reading: `append`, `insert`, `remove`,
`clear`, `contains`, `first`, `last`, `length` and `isEmpty` on arrays have no
type checking, no compiler emission and almost no VM support. Only `filter`,
`select`, `sort` and `each` were ever built. `GrobArray` exposes `Add` and an
indexer — no `Insert`, `Remove`, `Clear` or `Contains`. The type registry
(`grob-type-registry.md` §`T[]`) documents the full surface as if it ships.

**Why P1, not a parked follow-up.** Three compounding pressures:

1. **The release gate calls it.** Script 2 of the validation suite uses
   `extensions.contains(file.extension)` on a `string[]`. The eleven scripts are
   the v1 gate; a gate script exercising an unimplemented method is a latent
   release blocker with no plan slot. D-351 records Chris's approval of a
   follow-up increment, but neither `grob-v1-requirements.md`'s Sprint 9 scope
   nor any later sprint names it. Approved-but-unscheduled is how the Sprint 5
   interlude pattern started.
2. **Sprint 9's remaining modules feed it.** `fs.list()` returns `File[]`,
   `json.Node.asArray()` returns arrays, `csv.Table.rows` returns rows,
   `regex.matchAll()` returns `Match[]`. Integration tests for Increments C–F
   cannot be written against realistic scripts if the arrays those modules return
   cannot answer `.length` or `.contains()`. Building the modules first and the
   collection surface after inverts the dependency.
3. **The map twin is almost certainly in the same state [verify on disk].**
   D-351's finding was array-scoped, but the failure mechanism it names —
   `ResolveMemberAccessCall`'s generic fall-through to bare `Unknown` with zero
   checking — is receiver-agnostic. `map`'s registry surface (`length`,
   `isEmpty`, `keys`, `values`, `get`, `set`, `contains`, `remove`, `clear`) has
   no landing record anywhere in the log. `env.all()` already returns a map at
   Sprint 8, and Script 11 iterates one.

**Also inside this cluster [verify on disk]:** `sort`'s comparator predates both
`guid` and `date`, yet the registry lists both as valid sort keys
(`U: Comparable`). A `Struct`-discriminated sort key needs a dispatch arm the
pre-Sprint-8 implementation cannot have had. If absent, sorting by `date` either
faults or — worse — silently compares by whatever the fallback is.

**Recommendation.** Add a formally scoped increment cluster to the Sprint 9 plan,
sequenced **before Increment C (`fs`)**:

- **Increment C0a — array method surface.** The nine members across type checker,
  compiler emitter, `GrobArray` and VM dispatch, with element-type enforcement
  riding on D-351's `ArrayTypeDescriptor`. Includes the `sort` key-type audit
  (`date`/`guid` arms) and the registry's `const`/`readonly` mutation-method
  rejection (which currently has nothing to attach to).
- **Increment C0b — map method surface.** Same shape for the map registry
  members, plus confirmation that indexer sugar (`m[k]` / `m[k] = v`, already
  live via D-348/D-350) and `get`/`set` agree on nil-on-miss semantics.

If sequencing pressure forces a choice, C0a is non-negotiable before `fs`; C0b
can trail to before `json` (Increment D), whose object nodes will otherwise be
the second map-shaped surface with no methods.

### F1b — `arr[i] += v` and `arr[i]++` are wrong-code bugs, not missing features — elevate

**Evidence.** D-350: both forms hit the same early-return and **silently drop
emission** — the statement compiles to nothing, including the RHS's side effects.
D-351 declares A4 "unblocked". No plan slot exists.

**Why elevation.** A missing feature fails loudly. This fails silently — a script
runs, prints success and has done nothing. By D-353's own contract philosophy
("fails well"), silent misbehaviour is the worst class. It will also be found
immediately by Pillar 3's differential testing and Pillar 1's grammar-based
fuzzing, at which point it becomes a P0 interlude finding with repro management
overhead instead of a scheduled fix.

**Recommendation.** Two-step:

1. **Immediate tourniquet (one-line class of change):** until A4 lands, the
   compiler's `VisitCompoundAssignment`/`VisitIncrement` index-target branches
   should raise a compile-time diagnostic rather than returning silently. Reuse
   the nearest existing code or an internal-error path — the point is that no
   Grob program can reach the silent-drop state, not that the message is polished.
2. **A4 proper**, folded into the C0 cluster above (it needs C0a's element
   typing for its read-modify-write anyway, per D-351).

### F2 — Named-type dispatch is hand-rolled per type, and Sprint 9 is about to multiply it by seven

**Evidence.** D-355: `date` needed a hand-added `"date"` arm in
`ResolveSignatureType`, `ResolveNamedFieldType` and `TryGetNamedStructTypeName`
alongside the existing `"guid"` arms — plus its own `NamespaceRegistry` entry,
type-checker method/property validation (`ValidateDateMethodCall`/
`CheckDateMethodArgs`), VM dispatch arms keyed off the struct name and a
`ValueDisplay` `toString()` registration. That is at least six dispatch surfaces
per named type, each string-matched.

**The multiplier.** Sprint 9's remaining scope introduces `File`, `json.Node`,
`csv.Table`, `CsvRow`, `Regex`, `Match` and `ProcessResult` — seven more named
types. Sprint 11 adds `Response`, `AuthHeader` and `ZipEntry`. Hand-adding arms
per type per site is O(types × sites) copy-paste, and D-355's own record shows
the failure mode: miss one annotation-position site and `f: File` resolves as
`E1001` ("not a type"). With seven types landing across four increments, one
missed arm somewhere is close to certain.

**Recommendation.** An **Increment C0c — named-type registration table** before
`fs`: a single declarative registry (name, constructor entries, property table,
method table with arities and parameter types, `toString()` renderer,
nominal-mismatch rule) that the three annotation-position resolvers, the
method-call validator, the VM dispatch and `ValueDisplay` all consult.
`guid` and `date` migrate onto it as the proving cases; `File` through
`ProcessResult` then land as data, not as code spread across six sites. This is
the same architectural move `NamespaceRegistry` (D-342) already made for module
members — extend the idea to instance surfaces. Log as its own D-###; it refines
D-149, D-303 and D-355.

This is the single highest-leverage item in this review: it converts four
increments of error-prone dispatch plumbing into table entries and shrinks the
adversarial suite's attack surface before the fuzzer meets it.

### F3 — `date` equality and ordering are incoherent across offsets — decision needed before fixtures ossify

**Evidence chain.**

- D-169: equality is value equality; structs compare field-by-field.
- D-355: `date` is a struct whose single field is a **round-trip-formatted
  `DateTimeOffset` string** — offset included.
- D-354: `LessDate`/`GreaterDate` exist, and `<=`/`>=` lower via `Not`. The
  comparison **basis** — instant or string — is stated nowhere in the corpus.
  `grob-stdlib-reference.md`'s timezone conventions section is silent on it.
- The stdlib reference's comparison block explicitly lists `d1 == d2`.

**The incoherence.** `toUtc()`, `toLocal()` and `toZone()` make mixed-offset
values of the same instant routine. If `==` follows D-169 (field-by-field string
equality) it is offset-sensitive; if `LessDate` parses to `DateTimeOffset` and
uses its ordering — the only sensible implementation, and almost certainly what
shipped **[verify on disk]** — it is instant-based. Then for
`a := date.now()` and `b := a.toUtc()`: `a < b` is false, `a > b` is false and
`a == b` is false. Trichotomy is violated. A value that is neither less than,
greater than, nor equal to another is exactly the kind of surprise the
"LINQ-for-scripting, a language that doesn't surprise you" identity exists to
forbid. `sort` by a `date` key inherits whichever semantics the comparator gets,
compounding F1's sort-key gap.

**Recommendation.** A D-### amending D-169 for nominal `date` (and reviewing
`guid` while there):

1. **Comparison and equality are both instant-based** for `date`-vs-`date` —
   matching .NET's own `DateTimeOffset` operator semantics (`==` compares the
   instant; `EqualsExact` is the offset-sensitive variant Grob does not expose).
   Implementation: an `EqualDate` dispatch arm, or a documented special case in
   the existing `Equal` handler keyed off the nominal name — the checker already
   knows how to gate a nominal date-vs-date pair (D-354's own mechanism).
2. **`daysUntil`/`daysSince` define their basis** in the same entry — calendar
   days in whose offset, or 86,400-second periods between instants. Currently
   unspecified; Scripts 7 and 8 depend on the answer.
3. Gold masters and fixtures for `date` equality are written **after** this
   decision, not before. This is why it is P1: Increment B's test surface is
   still fresh, and every week of fixtures written against accidental
   string-equality semantics raises the cost of the fix.

### F4 — `date.parse(str, pattern)` is documented but does not exist — decide, then align

**Evidence.** `grob-stdlib-reference.md` line 388 shows
`date.parse("05/04/2026", "dd/MM/yyyy")` as live surface. D-354 and D-355 both
state the two-argument overload is **not** built, is not in the Sprint 9 scope
bullet, and that `NamespaceRegistry`'s one-entry-per-member model cannot express
arity overloads — an open design question left unnamed by any D-###.

**Why it matters now.** CSV workflows are `date.parse`'s natural habitat and
`csv` is Increment E. Real-world CSV dates are rarely ISO 8601 — Script 5's
domain (CSV processing) will hit this in anger, and every cold reader of the
stdlib reference will write the two-argument call and get a diagnostic the spec
says cannot happen. This is precisely the class of spec-vs-surface divergence the
Pillar 6 cold-read campaigns are designed to catch; better to catch it in
planning.

**Recommendation.** Decide by D-### before Increment E, one of:

- **(a) Ship it in Sprint 9** — the honest fix. The arity-overload question is
  smaller than it looks: `parse` needs an *optional second parameter*, not true
  overloading, and the named-type registry from F2 can carry optional-parameter
  arity natively (as `copy(src, dest, overwrite: bool = false)` in `fs` already
  demands — Increment C needs default-argument dispatch regardless, so `parse`
  rides the same mechanism).
- **(b) Cut it from v1** — then the stdlib sample is corrected in the same
  session the decision is logged, and the cut lands on the scope-cut list.

(a) is the better language. Pattern parsing is table stakes for a scripting
language whose driving use cases are sysadmin data plumbing; PowerShell,
Python and Go all have it. Recommend (a), sequenced after F2's registry so the
optional-argument machinery is built once.

-----

## P2 — Before the Sprint 9/10 hardening interlude

The unifying rationale: Pillar 3's cross-model spec adversaries and Pillar 6's
cold-read campaigns treat the corpus as the specification. Every internal
contradiction below will be independently rediscovered by three models and
several hours of campaign time, then need dispositioning as an ambiguity
finding. Cheaper to sweep them first.

### F5 — `map<K, V>` static typing is hollow, and the registry overclaims it

**Evidence.** D-351, by code reading: `TypeRef.TypeArguments` is parsed and never
consulted; `"map"` resolves to the flat `GrobType.Map` tag everywhere; `for k, v
in m` binds `v` as `Unknown`. Meanwhile `grob-type-registry.md` §`map<K, V>`
states as fact: "The type checker knows `map<string, string>` and
`map<string, int>` as distinct types." That sentence is false against the
implementation, and D-351 corrected the log's record (D-112) without correcting
the registry's claim.

**Why this is a language-quality item, not a doc nit.** The identity is *a
statically typed scripting language*. `env.all()`, `response.headers` and every
JSON-object-shaped workflow traffic in maps; a `for k, v` loop whose `v` is
untyped `Unknown` silently forfeits the core promise on one of the two workhorse
collection types. Arrays got their fix in A3; maps deserve the mirror before v1.

**Recommendation.** Two acts:

1. **Now (doc honesty):** amend the registry sentence to state the *intended*
   semantics with an explicit build-status note citing D-351, so cold readers and
   cross-model adversaries see a known gap, not a claim to falsify.
2. **Scheduled (the fix):** a `MapTypeDescriptor` increment mirroring D-351's
   `ArrayTypeDescriptor` shape — same three-tier carriage (symbol field,
   literal-node dictionary, call-result dictionary), threading through map
   literals, indexer read/write, `get`/`set`, `for k, v` binding and
   signature/field enforcement. Natural home: alongside F1's C0b, sharing its
   machinery. v1 keys are `string`-only, which halves the work — only `V` needs
   real inference.

### F6 — The "thirteen scripts" sweep D-346 deferred is now due — including the validation doc's own header

**Evidence.** D-346 surfaced (deliberately without sweeping) that the validation
suite has only ever held **eleven** scripts. Verified live this session, the
stale "thirteen" still stands in:

- `grob-sample-scripts.md`'s **own scope header** — the living bible opens by
  miscounting itself ("the thirteen release-gate validation scripts");
- `grob-v1-requirements.md` — at least five sites (§validation suite intro,
  Sprint 12 acceptance, the test-project table's `Grob.Integration.Tests` row
  and the D-337 authority note);
- `grob-benchmarking-strategy.md` §7.3's build-out note ("its thirteen") —
  beyond the §6 sites D-346 already corrected.

Note the trap for whoever executes the sweep: "thirteen core modules" and the
"thirteen-level precedence table" in the same files are **correct** thirteens.
The sweep is for script counts only.

**Recommendation.** One dedicated corpus session: grep-verified sweep of every
script-count citation to eleven, each edit citing D-346, with the sample-scripts
header fixed first. Historical D-### bodies (D-309, D-313, D-337) stay as
written per the append-only rule. Schedule before the interlude.

### F7 — `grob-vm-architecture.md` carries a sketch opcode enum that contradicts the shipped instruction set

**Evidence.** The doc's `OpCode` listing includes `LessEqual`/`GreaterEqual` as
opcodes; D-354 establishes `<=`/`>=` have **no** dedicated opcodes (they lower via
`Not`). It shows untyped `Less`/`Greater`; the real enum has typed families
(`LessInt`/`LessFloat`/`LessString` plus the new `LessDate`/`GreaterDate`). It
lacks `GetIndex`/`SetIndex` entirely. The surrounding prose frames it as
conceptual clox-mapping, but nothing marks it non-normative, and the doc is
listed as an authority for VM architecture.

**Recommendation.** Cheapest sufficient fix: a clearly worded non-normative
banner on the sketch ("illustrative clox-era shape; the shipped enum is the
authority, growth governed by ADR-0013 and the `adding-an-opcode` procedure")
plus a pointer to where the real enum lives. A full refresh is not required for
the interlude; the banner is. Without it, Pillar 3's spec adversaries will
"find" the `LessEqual` opcode three times over.

### F8 — Schedule the `endToEnd` benchmark build-out at Sprint 9 close, or the "primary gate" stays empty into alpha

**Evidence.** `grob-benchmarking-strategy.md` names end-to-end script benchmarks
the **primary** regression gate, yet the category registers `gating: false` and
has produced no fresh benchmarks through nine sprints (D-341 and D-347 both note
it untouched, correctly — the workload didn't exist). After Sprint 9 close it
does: roughly seven of the eleven scripts (those not requiring `Grob.Http`/
`Grob.Crypto` — Scripts 1, 2, 3, 5, 6, 8 and 9 on current module dependencies)
become runnable.

**Recommendation.** Add to the Sprint 9 close checklist: capture the first
`endToEnd.json` from the runnable subset via `benchmark.yml` (D-309 — no local
captures), freeze `endToEnd.origin.json` from the same run and flip the rolling
axis to gating. Log as a D-### citing D-313's build-out note as the condition now
met. The remaining four scripts join the workload at Sprint 11 as a logged
baseline extension, not a re-freeze. Related standing debt to carry visibly, not
act on yet: `compile.origin.json` remains CPU-mismatched (informational under
D-333) awaiting its deliberate re-freeze.

### F9 — The regex literal grammar is unspecified, and its `/` disambiguation rule is incomplete

**Evidence.** `/pattern/flags` appears only in the decisions log's April-era
entry with a one-line context rule: after an operator, assignment or opening
paren `/` is a regex literal; after a value it is division.
`grob-language-fundamentals.md` has no regex-literal grammar section at all —
its only `/` treatment is the division rules.

**The gap in the rule as stated.** It doesn't cover `/` after `return`, after
`,` in an argument list, after `{`, after `[`, at statement start on a fresh
line (significant in a newline-terminated language), or after a closing paren —
where `(a + b) / 2` demands division but `if (x) /re/...` never occurs because
Grob's `if` takes no parens... unless it does in some forms. This is exactly the
JavaScript lexer hazard, and Grob's no-semicolon statement rules make the
predecessor-token set *the* load-bearing definition. Pillar 1's grammar-based
generation needs the complete rule to fuzz against; implementing Increment F
from the one-liner guarantees divergence between intent and lexer.

**Recommendation.** A grammar-first design step (D-331 discipline) before the
regex increment: enumerate the full predecessor-token classification in a new
fundamentals section, with the worked ambiguous cases as spec examples, logged
as its own D-###. Half a session of design that saves an interlude's worth of
fuzzer findings.

-----

## P3 — Housekeeping, next natural touch

### F10 — D-353 cites "scripts 4/7/10/11/13" — script 13 does not exist

Verified this session: only Scripts 4, 7, 10 and 11 touch `http.*`/`crypto.*`.
D-353 was written *after* D-346 corrected the count, yet reproduced the ghost
numbering. Append-only discipline applies: a short reconciliation entry on the
D-349 precedent (or a line in the next entry's body), not an edit to D-353.

### F11 — The authority document's own Project Status table is five sprints stale

The decisions log opens with "Sprints 1–4 complete; Sprint 4→5 interlude". It was
last corrected in interlude A (D-316) and never since. Either update it each
sprint close (add to the sprint-close checklist alongside the smoke scripts and
the tag) or delete the implementation row in favour of a pointer to the
requirements doc's sprint plan — a status row that is reliably wrong is worse
than no row. The clox row and mascot rows are fine; only the moving rows rot.

### F12 — Related Documents table staleness

The log's closing table lists `sparky-character-sheet-v1.png` and
`sparky-illustrator-brief.pdf` against actual filenames
`sparkycharactersheetv1.png` and `Sparky___Illustrator_Brief.pdf`, and predates
`grob-adversarial-testing-strategy.md`, `grob-benchmarking-strategy.md` and
several other current documents. Fold into the F6 sweep session.

### F13 — Minor stdlib sample nits, same F6 session

`toIsoDateTime()`'s sample renders `Z` for a value the surrounding samples
construct as local time (`date.ofTime(...)`) — the offset would render, not `Z`,
under D-355's canonical renderer. One-line sample fix when the doc is next open.

-----

## Consolidated remediation plan

| # | Item | Action type | When | Vehicle |
| - | ---- | ----------- | ---- | ------- |
| F1b-1 | Silent-drop tourniquet for `arr[i] += v` / `arr[i]++` | Code (small) | Immediately | Micro-increment, before C0 |
| F2 | Named-type registration table; `guid`/`date` migrate | Code + D-### | Before Increment C | New Increment C0c |
| F1 | Array method surface (incl. `sort` date/guid keys) | Code + D-### | Before Increment C | New Increment C0a |
| F1 | Map method surface | Code + D-### | Before Increment D | New Increment C0b |
| F1b-2 | A4 compound assignment / increment on index targets | Code + D-### | With C0a | Folded into C0 cluster |
| F3 | `date` equality/ordering coherence decision; instant basis; `daysUntil` basis | D-### then code | Before date fixtures grow | Planning session + micro-increment |
| F4 | `date.parse(str, pattern)` — recommend build via C0c's optional-arg machinery | D-### then code | Before Increment E | Decision now; lands with E |
| F5-1 | Registry `map<K, V>` claim corrected to honest gap note | Doc | Now | Next corpus session |
| F5-2 | `MapTypeDescriptor` | Code + D-### | Before v1; ideally with C0b | C0b or dedicated increment |
| F9 | Regex literal grammar section; complete `/` disambiguation | Spec + D-### | Before Increment F | Grammar-first design session |
| F6 | Thirteen→eleven sweep (sample-scripts header, requirements, benchmarking §7.3) | Doc sweep | Before interlude | Dedicated corpus session |
| F7 | VM doc opcode sketch: non-normative banner | Doc | Before interlude | Same corpus session as F6 |
| F8 | First `endToEnd` capture + origin freeze + gating flip | Process + D-### | Sprint 9 close | Sprint-close checklist addition |
| F10 | D-353 "script 13" reconciliation note | D-### line | Next log entry | Piggyback |
| F11 | Project Status row: sprint-close checklist item or delete | Doc/process | Sprint 9 close | Checklist addition |
| F12, F13 | Related Documents table; `toIsoDateTime` sample | Doc | With F6 sweep | Same session |

**Sequencing note.** The proposed Increment C0 cluster (C0c registry → C0a
arrays → C0b maps, with F1b and F4 riding along) sits between now and the
current Increment C. It is real scope added to Sprint 9, and the honest framing
is that Sprint 9 was under-scoped: it planned six modules against a collection
substrate that D-351 has since shown was never finished. Paying that debt before
`fs`/`json`/`csv` land on top of it is cheaper than after — the alternative is
Sprint-5-style post-close interludes, a pattern the process notes already name
as the thing to avoid.

**Verify-on-disk list** (each flagged inline above): map method implementation
state; `sort`'s `date`/`guid` key arms; `LessDate`'s actual comparison basis;
whether the harness audit's F4 (Husky→pre-commit) correction has been applied to
root `CLAUDE.md` on disk.

-----

*Prepared July 2026 at Sprint 9 Increment B close. No corpus files were modified.
All D-### numbers referenced are existing entries; new decisions proposed above
take their numbers from the live tail (next free: D-356) at authoring time, per
the three-location lockstep discipline.*
