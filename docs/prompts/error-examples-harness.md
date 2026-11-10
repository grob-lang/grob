# Increment prompt — error-examples harness

**Authority: D-428**, the fourth increment of D-420's pre-9C run. Read D-428, D-420 and
D-308 in `docs/design/grob-decisions-log.md`, and R-05, R-11, R-18 and R-25 to R-29 in
the Deferred Work Register in `docs/design/grob-open-questions.md`, before starting.
This prompt implements them; where prompt and decision differ, D-428 wins and the
difference is a finding to report.

**Model and effort, by session.** This is not a structural sub-problem. It fixes no SDK
shape and changes no compiler, VM or stdlib code; its one production change is an
internal seam on `RunCommand`. The judgement is in the gate and in the three
specification rewrites of group 4, not in the code:

| Session | Work | Model | Effort |
|---|---|---|---|
| A | §1 investigation gate | Opus 5.5 | high |
| B | Change groups 1, 2 and 3 — seam, static layer, behavioural layer | Opus 5.5 | medium |
| C | Change groups 4 and 5 — R-18 rewrites, corrections and record | Opus 5.5 | medium |

A session may cover fewer groups than its row; it may not cover groups from two rows.

**Session-start check — before anything else in every session:**

1. Read `.grob-session/handoff.md` (absent in session A) and `git log` on the branch, and
   state which session row this is and which change groups it will cover.
2. State the model you are running as, from your own system context. If it does not match
   the row, **stop** and say so.
3. You cannot see the effort setting. State the row's expected effort and **wait for a
   one-word confirmation** before proceeding.

**Branch:** one concern, one branch. The concern is the error-examples harness.

Every change group in §2 ends as its own commit with the full suite green, and `/handoff`
is run after each so `.grob-session/handoff.md` names the last completed group, the next
one and anything open. A new session resumes from the handoff file and `git log` — never
from memory of a previous conversation.

**This prompt was committed with D-428** at `docs/prompts/error-examples-harness.md`.
Never retrofit it. If the work diverges, the divergence goes in the decisions-log entry.

---

## 1. Read-only investigation gate

*Session A — Opus 5.5, high.*

**No source edits until this gate is complete and reported.** Building, running the CLI
and running the test suite are expected. Changing production code, test code or any file
under `docs/errors/` is not.

Write the report to a **scratch file outside the working tree**. In chat give the path and
any STOP conditions that fired. End session A with `/handoff`, naming the report's path.

### 1.1 Re-measure the classification

D-428's classification was measured at `f747517` with the CLI built in a throwaway copy,
on SDK 10.0.114 with the `GROB-SEC-001` floor disabled because no 10.0.300 SDK was
reachable. **Re-measure on the supported SDK** before relying on it.

Run every pair through `grob run <slug>.grob` from the pair's own directory, closed stdin,
an environment with no Grob-relevant variables set. For each, record every diagnostic the
CLI writes: severity, code, file, start line, start column. **State the expected counts
before running:**

- 56 pairs (57 when R-11 was raised; D-425 removed `param-after-param-block-ends`). The
  README's batch notes account for 47 of them.
- 23 match their master on the full ordered list of diagnostics today.
- After the closed reconciliation set in §2.3 and §2.4, 27 match and 29 do not.

Then compare what you measured with the **gate-time** verdicts, not §2's final ones. The
gate runs before any reconciliation, so the 23 pairs expected to pass now are §2's 27
minus the five that pass only after their rewrite (S5, S6 and R-18's three in §2.4), plus
`circular-initialisation`, which passes until M9 gives it a column. **A pair in those 23
that fails is a STOP.** A pair outside them that passes is good news and a finding; report
it and say whether it stays out of the manifest. A difference from §2's post-reconciliation
verdicts is a finding to report with its effect on the manifest row, never a STOP, since
the gate cannot observe those verdicts.

### 1.2 Confirm the static defects

D-428 found eleven masters that contradict their own source text or the catalogue —
`-->` column disagreeing with its own caret, a caret starting on whitespace, a snippet line
not matching the source line it numbers, a title not matching `ErrorCatalog` or a location
with no column. They are listed under §2.2. Write the static checks as a throwaway script,
run them against every master and confirm no master outside the list fails them.

### 1.3 Propose the seam

The behavioural layer runs `RunCommand`'s own pipeline, never a copy of it (D-428
Decision 3). `RunCommand.Run` builds its catalogue internally from
`PluginRegistration.BuildCatalogue`, over the OS-backed `SystemEnvironment`,
`SystemClock` and `SystemRandomSource`. Propose an **internal** seam through which a
test supplies the catalogue's capabilities, and show that:

- the public constructor and `grob run`'s observable behaviour are unchanged;
- with the seam in use, nothing on the pipeline reads `Environment`, `DateTime`, `Random`
  or `Console` directly — trace every ambient read reachable from `Run` and list them;
- the seam needs no new project reference (`Grob.Cli` already grants
  `InternalsVisibleTo` to `Grob.Integration.Tests`).

### 1.4 Propose the manifest grammar

Propose the exact table grammar of `docs/errors/examples/quarantine.md` (D-428
Decision 4), including the *Observed* cell's encoding of an ordered tuple list (a pair with
no column records none), and the parser's rejection messages for: an unknown pair, a
duplicate pair, an unknown category, an unparseable *Blocked by* cell, an unparseable or
empty *Observed* cell, an `R-##` that is not an open register row, and a namespace that is
registered in the catalogue.

### 1.5 Confirm the register

Confirm R-25 to R-29 exist as open rows with the pairs §2.3 cites, and that R-18 is open.
Report any register text the gate's measurement contradicts.

---

## 2. Scope

Five change groups, each a green commit. Land them in this order.

### 2.1 Change group 1 — the seam

*Session B.* The internal `RunCommand` seam proposed in §1.3, with tests showing an
injected environment is what `env.get` reads and that the public constructor still builds
the OS-backed catalogue.

### 2.2 Change group 2 — the master parser and the static layer

*Session B.* A master parser in `Grob.Integration.Tests` with unit tests over inline
strings, then a per-pair static theory over the live corpus (`ErrorExampleMasterTests`).
The static checks, all of them never quarantinable:

- every header is `error[Exxxx]: <title>` or `warning[Exxxx]: <title>`, and its title
  equals `ErrorCatalog`'s title for that code;
- the source's first line is a `//` comment naming the same code as the first header;
- every `-->` line names the pair's own file by bare name and carries a line **and a
  column**;
- every numbered snippet line equals the source line it numbers;
- the first caret under the `-->` line starts at the `-->` column and not on whitespace;
- exactly one trailing newline, no ANSI escape, no emoji and no hedge word from
  `house-style` — emoji meaning pictographs, not typography: the CLI itself prints `—`,
  and `invalid-implicit-conversion-float-to-int`'s master uses `→`, so
  `prose-check.ps1`'s arrow-inclusive range is the wrong test;
- the README's code-to-slug index and the pair directories are a bijection.

The masters that fail these today are corrected **in this commit**, because the checks
cannot go green without them:

| # | Pair | Correction | Why |
|---|---|---|---|
| M1 | `file-not-found-via-stdlib` | `-->` 3:9 → 3:10 | caret is at 10, on `fs`; 9 is a space |
| M2 | `json-parse-error` | `-->` 4:7 → 4:8 | caret is at 8, on `json`; 7 is a space |
| M3 | `process-exit-nonzero` | `-->` 3:10 → 3:11; title → ``process exit non-zero (under `OrFail`)`` | caret is at 11; title is the catalogue's (D-308) |
| M4 | `integer-division-by-zero` | `-->` 5:14 → 5:10 | caret spans the expression from 10; 14 is inside `total` |
| M5 | `required-env-var-not-set` | `-->` 3:9 → 3:10 | caret is at 10; 9 is a space |
| M6 | `forward-reference-in-function` | `-->` 4:14 → 4:15 | caret is at 15, on `tag` |
| M7 | `nil-dereference-at-runtime` | `-->` 4:12 → 4:14 | caret is at 14, on `content` |
| M8 | `manifest-version-mismatch` | `-->` 1:1 → 3:1; the snippet's line number 1 → 3 | the `import` is on line 3; line 1 is the comment |
| M9 | `circular-initialisation` | `-->` 5 → 5:12, with a caret under `product` | every master carries a column (D-428 Decision 2) |
| M10 | `use-before-declaration-in-block` | `-->` 6:26 → 6:27, caret moved one right | 26 is a space; `sep` starts at 27 |
| M11 | `file-not-found` | source line 2 `const settings :=` → `settings :=`; master `-->` 2:19 → 2:13, snippet line and caret follow | the caret started on a space; and D-289 — a call is not a compile-time constant, so the source was stale too and is fixed once rather than twice |

Plus the four index rows the README lacks: `invalid-guid-literal` (E0601),
`math-domain-violation` (E5006), `namespace-used-as-value` (E1004) and
`unknown-namespace-member` (E1003).

M1 to M10 are master corrections by D-428 Decision 4's second ground — the master
contradicts its own source or the catalogue. M11 is the first ground as well. None changes
a code. Change nothing else in these masters.

### 2.3 Change group 3 — the behavioural layer and the manifest

*Session B.* `ErrorExampleCorpusTests` runs every pair through the seam, normalises the
CLI's output per D-428 Decision 2, and compares the **full ordered list** of
`(severity, code, file, line, column)` tuples the master states against the list the CLI
writes. The manifest parser reads `docs/errors/examples/quarantine.md`. Quarantined pairs
are run and **must fail exactly as their *Observed* cell records**. One that passes fails
the harness with a message to remove its row; one whose output differs from *Observed*
fails it with the recorded list against the actual one.

**Write the corpus theory first and confirm it red**, with no manifest, before any
reconciliation. Record which pairs fail; it should be 34 — the 33 §1.1 measured, plus
`circular-initialisation`, which M9 gave a column the runtime does not yet report.

Then the source reconciliations, each the language having moved under the master (D-428
Decision 4's first ground):

| # | Pair | Change | Authority | Then |
|---|---|---|---|---|
| S1 | `array-index-out-of-range` | `const names :=` → `names :=` | D-289: an array literal is not a compile-time constant | quarantined, R-26 |
| S2 | `array-index-out-of-range-in-function` | `[]string` → `string[]` | D-327 | quarantined, R-26 |
| S3 | `shadowed-declaration-loop-var` | `[]string` → `string[]` | D-327 | quarantined, R-27 |
| S4 | `use-before-declaration-in-block` | `[]string` → `string[]` | D-327 | quarantined, R-27 |
| S5 | `invalid-escape-sequence` | line 3 → `dest := "C:\users"`, line 4 → `print(dest)`; master's snippet line follows; its second note lists `` `\n`, `\r`, `\t`, `\\`, `\"`, `\$` `` | `path` is a core module (ADR-0010, D-342); one root cause per example; `grob-language-fundamentals.md` §8's escape set | **passes** at 3:12 |
| S6 | `argument-type-mismatch-string-for-int` | line 4 → `return "*".repeat(count)`; master `-->` 7:15 → 7:14, caret one left | `repeat` is a `string` method, not a `strings` member (`grob-type-registry.md`'s `string` table); the argument starts at 14 | **passes** |
| S7 | `catch-after-catch-all` | `log.warn(` → `log.warning(` | the `log` surface as registered and documented | quarantined, `fs` |
| S8 | `permission-denied-in-nested-call` | `fn archiveLogs(dest: string) {` → `fn archiveLogs(dest: string): void {` | `grob-language-fundamentals.md` §12: explicit return types, `void` for no value | quarantined, `fs` |

Then the manifest, exactly these 29 rows plus the three R-18 rows below:

| Pair | Category | Blocked by |
|---|---|---|
| `catch-after-catch-all` | unbuilt | `fs` |
| `file-not-found` | unbuilt | `fs` |
| `file-not-found-via-stdlib` | unbuilt | `fs` |
| `json-parse-error` | unbuilt | `fs`, `json` |
| `permission-denied-in-nested-call` | unbuilt | `fs` |
| `try-without-catch-or-finally` | unbuilt | `fs` |
| `process-exit-nonzero` | unbuilt | `process` |
| `ambiguous-type-reference` | unbuilt | Sprint 11 |
| `manifest-version-mismatch` | unbuilt | Sprint 11 |
| `plugin-not-installed` | unbuilt | Sprint 11 |
| `unknown-plugin` | unbuilt | Sprint 11 |
| `array-index-out-of-range` | gap | R-26 |
| `array-index-out-of-range-in-function` | gap | R-26 |
| `call-stack-overflow` | gap | R-26 |
| `circular-initialisation` | gap | R-26 |
| `integer-division-by-zero` | gap | R-26 |
| `math-domain-violation` | gap | R-26 |
| `required-env-var-not-set` | gap | R-26 |
| `forward-reference-in-function` | divergent | R-27 |
| `invalid-implicit-conversion-float-to-int` | divergent | R-27 |
| `nil-dereference-at-runtime` | divergent | R-27 |
| `shadowed-declaration-loop-var` | divergent | R-27 |
| `type-mismatch-bool-arithmetic` | divergent | R-27 |
| `undefined-method-on-int` | divergent | R-27 |
| `use-before-declaration-in-block` | divergent | R-27 |
| `nullable-interpolation` | divergent | R-28 |
| `type-cycle-no-terminating-field` | divergent | R-28 |
| `unterminated-string-literal` | divergent | R-28 |
| `unexpected-token-missing-brace` | divergent | R-29 |
| `unknown-decorator` | stale | R-18 |
| `invalid-allowed-argument` | stale | R-18 |
| `decorator-not-permitted-here` | stale | R-18 |

Each row's *Reason* cell says in one sentence what the pair is waiting for, measured, not
paraphrased from this prompt. Each row's *Observed* cell is the tuple list the CLI writes
for the pair **after** this group's source reconciliations, measured through the seam, not
copied from §1.1; S1–S4, S7 and S8 change their pairs' output. The unbuilt `fs` and
`process` pairs that are runtime errors
will meet R-26 once their module lands; say so in their reason.

The *Blocked by* guards: an `R-##` must be an open row of the register; a namespace must
be absent from `StdlibCatalogue.ForCompileOnly()`; `Sprint 11` is checked by strictness
alone. Green at the end of this group: **24 pass, 32 quarantined.**

### 2.4 Change group 4 — R-18's three rewrites

*Session C.* Rewrite each source and master against the braceless grammar and the
decorator semantics D-424 and D-425 fixed. The scenarios are verified to raise exactly one
diagnostic each; the masters' titles come from the catalogue and their notes and help are
written to §10 and the README's voice rules:

| Pair | New source | Expected | Authority |
|---|---|---|---|
| `unknown-decorator` | `@admin` on line 2, `param user: string` on line 3, `print("Hello, ${user}")` after a blank line | E4001 at 2:1; help lists the seven decorators | D-410, D-411 |
| `invalid-allowed-argument` | `@allowed(ENVS)` on line 2, `param environment: string = "dev"` on line 3, `readonly ENVS := ["dev", "staging", "prod"]` after a blank line, then `print(environment)` | E4101 at 2:10 | D-412's order, D-286, D-424 |
| `decorator-not-permitted-here` | `fn connect(@secure token: string): int {` / `return token.length` / `}` | E4002 at 2:12 | D-424 Decision 2 |

Line 1 of each is the README's single `//` comment naming the code and intent; no
`Eventual path` line. Remove the three manifest rows. Green: **27 pass, 29 quarantined.**

The original `decorator-not-permitted-here` scenario — a decorator before a local binding —
now raises E2001 where the same mistake on a function parameter raises E4002. That is
recorded in R-27; do not change the parser.

### 2.5 Change group 5 — corrections and record

*Session C.* Each lands now because the harness makes it true:

- **`docs/errors/examples/README.md`**: delete *File layout in project knowledge vs on
  disk*; rewrite *Running the examples* (there is no `grob check` until Sprint 12; the
  harness is how the gate runs, `grob run` from the pair's directory reproduces one pair);
  replace the regeneration recipe and *Adding a new example* step 5 with D-428 Decision 4's
  authorship rule; add a *Quarantine* section pointing at the manifest; end the footer's
  batch notes with one for the harness instead of a running count — the directory is the
  count.
- **R-05's naming sites**: `writing-an-error-test/SKILL.md` (its frontmatter description,
  the intro, step 2, step 3's authorship instruction, step 5's `errors-examples-README.md`
  pointer — pairs are discovered by directory, there is nothing to wire — and the
  checklist), `writing-grob-source/SKILL.md:152-154`, `allocating-an-error-code/SKILL.md:92`
  and `tests/CLAUDE.md:60-64`. Archived prompts under `prompts/archive/` are records and are
  not edited.
- **`grob-v1-requirements.md` §14**: one sentence stating the negative-test release gate —
  every pair passes the harness and the quarantine manifest is empty — citing D-428. Leave
  §14's "thirteen" alone; that is increment 6's.
- **The decisions-log entry** in three-location lockstep. **Take the D-number from the
  live tail of the merged log**, never from memory or this prompt.
- **The register**: R-11 and R-18 to Closed with that D-number; R-05 records its naming
  sites as done, the full sweep still open; R-25 to R-29 amended where §1 measured
  differently.

### Out of scope — report, do not fix

- **Any change to `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Runtime` or
  `Grob.Core`.** A pair that passes only by changing one of them is quarantined.
- **The §10 renderer** (R-25): titles, snippets, carets, notes, help and stack-frame
  chains are checked statically, not compared against output.
- **Runtime columns** (R-26), the divergences (R-27 to R-29), the regex literal (R-30) and
  undefined type names (R-31).
- The `// Eventual path:` line in the sources not rewritten here. Removing it renumbers
  every line of the pair.
- The notes, help and chain lines of masters not rewritten here.
- `grob check` and the compile-error exit code 2 — Sprint 12.

**D-420's stopping rule applies.** Findings go to the register, not into the run, unless
they block this increment or Sprint 9C.

---

## 3. Implementation notes

**One pipeline.** The behavioural layer calls `RunCommand.Run` through the seam. A test
that lexes, parses, checks, compiles and runs on its own is a second pipeline that can
disagree with the CLI, which is the mirror D-426 removed for natives.

**The whole list, in order.** A master states every diagnostic its source produces. An
extra diagnostic in the output fails, a missing one fails, a reordered one fails. D-300's
one diagnostic per root cause is exactly what an extra diagnostic violates.

**Normalisation is narrow** (D-428 Decision 2). Assert the rendered path equals the path
the harness passed, then compare by file name. Reduce a `line:col-line:col` range to its
start. A location with no column does not acquire one; it fails against a master that has
one. Nothing else is normalised. Message text is not compared: the CLI prints the call
site's message where the master has the code's title, and that is R-25's to close.

**The master grammar.** Header `^(error|warning)\[(E\d{4})\]: (.+)$`; location
`^  --> (\S+):(\d+)(?::(\d+))?$`; snippet `^ *(\d+) \|(?: (.*))?$` — `unterminated-string-literal` numbers an empty line; caret line `^ *\| *\^`.
Files are LF on disk (`.gitattributes`); read tolerating `\r\n`, since the CLI writes
`Environment.NewLine` and CI runs on Windows.

**Discovery, not registration.** Pairs are read from the source tree by walking up from
`AppContext.BaseDirectory`, as `ValidationScriptCorpusTests` does, and enumerated as
directories. A new pair is in the gate the moment it exists; the index bijection catches an
unindexed one.

**No skips.** A quarantined pair is a manifest row, never `Skip =` on an attribute. A skip
hides a pair that starts passing; strictness reports it. Strictness is against *Observed*,
not against "any failure": a pair that fails differently is a regression behind a blocker
that does not describe it.

**One case per pair.** Theory data carries the slug as its display name, so a failure
names the pair and the mismatch, expected against actual.

---

## 4. Tests

TDD throughout — red, green, refactor. Never merge known-wrong code.

Required:

- **Comparer**: identical lists pass. Each of these fails, with its own test: an extra
  actual diagnostic; a missing one; a reordered pair; a differing code, line, column or
  severity; an actual with no column against a master with one.
- **Normalisation**: a range reduces to its start; a rendered path that is not the path
  passed fails.
- **Master parser**: every line form above, a header with no location, two diagnostics in
  one master.
- **Static layer**: each check has a failing inline example.
- **Manifest**: each rejection §1.4 lists; strictness — a quarantined pair that passes
  fails; a quarantined pair whose output differs from its *Observed* list in code, line,
  column, severity or count fails; an `R-##` that is closed fails; a namespace present in
  the catalogue fails.
- **Corpus**: every pair through both layers.
- **Seam**: the injected environment is the one read; the public constructor is unchanged.

**Mutation-verify** each guard. For each: weaken it, **predict the exact failing tests by
name before running**, confirm, restore.

1. Compare the first diagnostic only. Predicted: the extra- and missing-diagnostic comparer
   tests; no corpus case.
2. Drop the column from the comparison. Predicted: the column comparer test, and strictness
   on the nine quarantined pairs whose only mismatch is the column — R-26's seven plus
   `nullable-interpolation` and `unterminated-string-literal`.
3. Make quarantine a skip. Predicted: both strictness tests, the passing pair and the
   observed drift.
4. Remove the open-row guard. Predicted: the closed-`R-##` manifest test.
5. Remove the namespace guard. Predicted: the registered-namespace manifest test.
6. Remove the caret-against-location check, then separately revert M3 in a scratch copy.
   Predicted: the inline caret test; then `process-exit-nonzero`'s static case alone.
7. Remove the rendered-path assertion. Predicted: the path normalisation test.
8. Accept any failure for a quarantined pair, skipping the *Observed* comparison.
   Predicted: the observed-drift manifest test; no corpus case.

---

## 5. Deliverables

- Five change groups per §2, each its own green commit.
- **56 pairs through both layers; 27 pass; 29 quarantined**, with any difference from §2
  explained pair by pair.
- Error-code count **unchanged at 120**; no `ErrorCatalog` edit; D-316 gate green.
- The full suite green, with the test count; coverage of `Grob.Cli` reported against the
  D-328 bar.
- The eight mutation verifications, each with its prediction and the observed failures.
- A decisions-log entry in three-location lockstep, and the register updated per §2.5.

---

## 6. STOP conditions

Stop and report, do not proceed:

- The session-start check finds the running model does not match the session's row.
- A pair §2 expects to pass does not, on the supported SDK.
- A pair passes only by changing `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Runtime` or
  `Grob.Core`. Quarantine it if the manifest can say why honestly; stop if it cannot.
- Any pair produces an internal error (`error: internal error:`) or hangs.
- The seam changes `RunCommand`'s public behaviour, or cannot keep every ambient read off
  the pipeline.
- A pair would reach a real filesystem, process or network resource under the seam.
- A master would change for a reason outside D-428 Decision 4's three grounds.
- The change needs a new error code, an `ErrorCatalog` title or description edit, an
  opcode, a grammar change or a `GrobType`, `GrobValueKind` or `TokenKind` variant.
