# Grob — Advertised-vs-Built Corpus Audit

**Date:** July 2026
**Corpus state:** D-367 (post-`date`-equality), `src.zip` and `docs.zip` of the same date
**Method:** every documented user-facing surface diffed against live dispatch in `src/`.
Claims verified against source, not against other documents.
**Scope:** language constructs, type-member surfaces, stdlib module functions, error-code
registry. CLI/tooling and plugin-ecosystem docs excluded by agreement (phased future work
by design).

> **Status: partly superseded — this is a dated snapshot, not a live tracker.**
> The findings below record the corpus as it stood at **D-367** and are kept verbatim as the
> record of why the follow-on work was commissioned. Since then: **A1 (numeric instance
> methods) is closed by D-369**; the error-code registry is at **119 codes**, not 118, after
> D-376 added **E0016** (duplicate key in map literal); and `map`'s **construction** syntax,
> unbuilt when B1/B2 were written, is built by **D-376** — its query/mutation members
> (`length`/`keys`/`values`/`get`/`set`/…) remain unbuilt and are still correctly scheduled
> as C0b-2. The decisions log is the authority for current state; check it before using this
> document for release planning.

---

## Summary

| # | Finding | Class | Priority |
|---|---------|-------|----------|
| A1 | Numeric instance methods (`int`/`float`/`bool`) advertised, unbuilt, unscheduled — **used by release-gate scripts** | Advertised-but-unbuilt | **P1** |
| A2 | The wiki — the user-facing surface — carries essentially no build-status markings | Structural | **P1** |
| B1 | `grob-stdlib-reference.md`'s Status column reads "Specified" for all thirteen modules, including eight now built | Documented-and-divergent | P2 |
| B2 | `map` member surface entirely unbuilt (correctly scheduled as C0b; wiki unmarked) | Advertised-but-unbuilt | P2 |
| C1 | `array` member surface partially built — 4 of ~8 advertised members dispatch (correctly scheduled as C0a; wiki unmarked) | Advertised-but-unbuilt | P3 |
| C2 | `grob-stdlib-reference.md` has no detailed section for `path`, `log`, `strings`, `csv`, `regex` | Structural | P3 |
| ✓ | Error-code registry: 118 codes, no dead codes, no undocumented codes thrown *(119 since D-376 added E0016)* | **Correctly-marked** | — |
| ✓ | Language constructs: all twenty keywords present in lexer and parser | **Correctly-marked** | — |
| ✓ | `array.select()` / `.map()` naming: doc and source agree, `.map()` deliberately absent | **Correctly-marked** | — |

---

## A1 — Numeric instance methods: a release-gate blocker (P1)

**This is the same class, and the same severity, as the `string`-methods gap caught
before D-363 — and it was not closed by it.**

`PrimitiveMemberRegistry` (`Grob.Core/PrimitiveMembers/`) registers members for
**`GrobType.String` only**. `GrobType.Int` and `GrobType.Bool` appear in that file solely
as *return* types (`length → Int`, `contains → Bool`); no `Int`, `Float` or `Bool`
**receiver** has a member entry. There is no dispatch arm for a numeric or boolean
receiver anywhere in `TypeChecker.Expressions.cs`.

The wiki advertises, with no build-status caveat:

| Wiki page | Advertised members | Built |
|-----------|-------------------|-------|
| `Type-Registry/int.md` | `toString()`, `toFloat()`, `abs()` + statics (`int.min`, `int.max`, …) | none |
| `Type-Registry/float.md` | 7 members incl. `round()`, `abs()`, `ceil()`, `floor()` + statics (`float.clamp`, …) | none |
| `Type-Registry/bool.md` | `toString()` | none |

**The release gate depends on them.** `grob-sample-scripts.md`:

- line 215 — `size_mb: (f.size / 1024.0 / 1024.0).round(2)` → `float.round`
- line 768 — `used_pct := ((d.used.toFloat() / total.toFloat()) * 100.0).round(1)` →
  `int.toFloat` ×2 and `float.round`
- line 140 — `year := file.modified.year.toString()` → `int.toString`
  (`.year` is an `int` property; `toString` is built for `string` and `guid` **only** —
  `NamedTypeRegistry:76`, `PrimitiveMemberRegistry:75` — not for `int`)

**Not scheduled, not cut.** No sprint in `grob-v1-requirements.md` schedules primitive
numeric methods, and §16's v1 Scope-Cut List does not name them. They are in exactly the
limbo `string` occupied: advertised as shipping, unbuilt, unowned.

**Mitigating:** the mechanism now exists. D-363 built the primitive-member dispatch path,
D-364/D-365 the default-argument fill. This is a surface-population increment against
proven machinery — `float.round(decimals = 0)` even exercises the default-argument path
D-365 just proved on the second branch. It is not a new subsystem.

**Recommendation.** One increment, `int`/`float`/`bool` instance members plus the
type-static functions (`int.min`, `float.clamp`, …), before Increment C. Same shape as
the string-methods increment; scoped by the wiki pages above and
`grob-type-registry.md`'s corresponding sections. This is the second release-gate
blocker this audit class has found; it is worth running the increment rather than
scheduling it into the interlude.

---

## A2 — The wiki carries no build-status markings (P1, structural)

Of **80 wiki files**, exactly one (`Standard-Library/json.md`) contains any
"not yet / pending / planned" language. Every type page and module page otherwise reads
as a description of shipped behaviour.

Meanwhile the **design corpus has been progressively made honest**: `grob-type-registry.md`
now carries build-status notes from F5-1 (`map<K, V>` typing), D-362 (numeric surface),
D-363 (`string`, "built except three") and D-365 (`string` complete).

So the two halves of the corpus have diverged in *kind*: the design documents record what
is built, the user-facing wiki records what is intended, and nothing marks the difference.
**This is the mechanism that let A1 hide** — and that let the `string` gap reach a
release-gate script before anyone noticed.

**Recommendation.** A build-status convention applied to the wiki, mirroring the design
corpus's. Minimum viable: a status line at the head of each type and module page — *Built*,
*Partially built (see note)*, or *Specified, not yet built* — with the partial cases naming
what is missing. This is doc-only work, no code, and it is the single highest-leverage
corpus change available: it converts "advertised" into a claim someone can falsify at a
glance, rather than one that must be audited against source.

Worth pairing with the pre-interlude sweep rather than run standalone, **except** for the
`int`/`float`/`bool` pages, which should be marked as part of A1's increment so they are
not left advertising an unbuilt surface a day longer.

---

## B1 — Stdlib reference Status column uniformly stale (P2)

`grob-stdlib-reference.md`'s "Core Modules — Auto-Available" table carries a **Status**
column reading `Specified` for **all thirteen** modules. Eight are now built and registered
in `Grob.Stdlib` (78 natives total):

| Built (registered natives) | Not yet built |
|---|---|
| `date` (7), `env` (5), `formatAs` (3), `guid` (5), `log` (5), `math` (16), `path` (11), `strings` (1) | `fs`, `json`, `csv`, `regex`, `process` |

The five unbuilt ones are Sprint 9 Increments C–F — **correctly scheduled**, not findings.

The finding is the column itself: a status field that has never changed value is worse than
no column, because it presents as maintained. Either update it (and keep it updated at each
module's landing) or remove it and let the wiki status convention from A2 carry the load.
I would remove it — one status mechanism, in the user-facing place, is better than two.

---

## B2 — `map` member surface entirely unbuilt (P2)

`TypeChecker.Expressions.cs` has **no `GrobType.Map` dispatch arm at all**. `Type-Registry/map.md`
advertises `length`, `isEmpty`, `keys`, `values`, `get()`, `set()` and more, unmarked.

This is **correctly scheduled** — C0b, already in the queue, and `grob-type-registry.md`
carries the F5-1 honesty note about `map<K, V>` typing. The finding is narrow: the wiki page
is unmarked (A2's general case), and C0b's scope should be understood as the *whole* member
surface, not an increment on top of partial work — there is nothing there today.

---

## C1 — `array` member surface partially built (P3)

Built: `IsArrayHigherOrderMethod` (`TypeChecker.Expressions.cs:445`) dispatches exactly
**`filter`, `select`, `sort`, `each`**.

Advertised in `Type-Registry/array.md`: `length`, `isEmpty` (properties), `first()`,
`last()`, `contains()`, `filter()`, `select()`, `each()` and more.

So `first`, `last`, `contains`, and the `length`/`isEmpty` properties are advertised and
undispatched. **Correctly scheduled** as C0a. Noted here so C0a's scope is sized against the
real gap rather than the assumed one, and because the wiki page is unmarked (A2).

---

## C2 — Stdlib reference lacks sections for five modules (P3)

`grob-stdlib-reference.md` has detailed sections for `env`, `process`, `json`, `date`,
`formatAs`, `fs`, `guid`, `math` — but **not** for `path`, `log`, `strings`, `csv` or
`regex`, which appear only as table rows. The wiki has full pages for all fourteen
(thirteen modules plus `strings`).

Not a user-facing gap — the wiki covers it — but a structural inconsistency in the design
corpus, and one that makes the design document a misleading place to check a module's
surface. Fold into the pre-interlude doc sweep.

---

## Clean results (recorded so they need not be re-derived)

**Error-code registry — clean.** 118 documented codes, matching every landing record's
count. Every documented code is referenced in `src/`; the only apparent exception, `E6001`,
is a *reserved-range boundary* (`E6001–E8999`), not a code. The apparent undocumented codes
in source (`E0999`, `E1999`, … `E9999`) are category range descriptors in `ErrorCatalog.cs`
comments, not thrown codes. No dead codes, no undocumented codes thrown.

**Language constructs — clean.** All twenty keywords (`select`, `case`, `switch`, `try`,
`catch`, `finally`, `throw`, `for`, `while`, `if`, `else`, `fn`, `type`, `param`, `import`,
`const`, `readonly`, `break`, `continue`, `return`) are present in the lexer and parser.

**`array.select()` naming — clean.** `array.md` states `.map()` is deliberately not used
and `.select()` is the projection method; source agrees (`IsArrayHigherOrderMethod` accepts
`select`, not `map`). Doc and code aligned, including the deliberate absence.

---

## Recommended disposition

1. **A1 — numeric instance methods:** its own increment, before Increment C. Release-gate
   blocker; mechanism already exists. Mark the three wiki pages in the same increment.
2. **A2 — wiki build-status convention:** pre-interlude doc sweep, as its anchor item.
3. **B1 — stale Status column:** remove it in that same sweep, in favour of A2's convention.
4. **B2 / C1 — map and array:** no action beyond confirming C0b and C0a scopes against the
   real (larger than assumed) gaps; wiki marking handled by A2.
5. **C2 — missing design-doc sections:** pre-interlude sweep.

**The meta-point.** This audit found one release-gate blocker (A1) and one structural cause
(A2). The structural cause is the more valuable fix: A1 is the third instance of the same
class — after `map<K, V>` typing and `string` methods — and each was found by chance rather
than by mechanism. A build-status convention that a cold reader can check makes the fourth
instance visible without an audit.
