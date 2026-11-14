# Increment prompt — public samples compile

**Authority: D-430**, the fifth increment of D-420's pre-9C run. Read D-430, D-420, D-428,
D-429, D-417 and D-343 in `docs/design/grob-decisions-log.md`, and R-24 and R-29 to R-33 in
the Deferred Work Register in `docs/design/grob-open-questions.md`, before starting. This
prompt implements them; where prompt and decision differ, D-430 wins and the difference is
a finding to report.

**Model and effort, by session.** One change group is production code: R-32 moves the
host's local time zone and current directory behind capabilities, which changes a public
`Grob.Runtime` interface before its first publication. The rest is a test harness built
on D-429's seam and a closed list of documentation corrections. The judgement is in the
gate, in R-32's shape and in the thirteen corrections, not in the volume of code:

| Session | Work | Model | Effort |
|---|---|---|---|
| A | §1 investigation gate | Opus 5.5 | high |
| B | Change groups 1 and 2 — R-32's capabilities, the harness | Opus 5.5 | medium |
| C | Change groups 3, 4 and 5 — wiki corrections, the corpus enters the gate, record | Opus 5.5 | medium |

A session may cover fewer groups than its row; it may not cover groups from two rows.

**Session-start check — before anything else in every session:**

1. Read `.grob-session/handoff.md` (absent in session A) and `git log` on the branch, and
   state which session row this is and which change groups it will cover.
2. State the model you are running as, from your own system context. If it does not match
   the row, **stop** and say so.
3. You cannot see the effort setting. State the row's expected effort and **wait for a
   one-word confirmation** before proceeding.

**Branch:** one concern, one branch. The concern is the public samples gate.

Every change group in §2 ends as its own commit with the full suite green, and `/handoff`
is run after each so `.grob-session/handoff.md` names the last completed group, the next
one and anything open. A new session resumes from the handoff file and `git log` — never
from memory of a previous conversation.

**This prompt was committed with D-430** at `docs/prompts/public-samples-compile.md`.
Never retrofit it. If the work diverges, the divergence goes in the decisions-log entry.

---

## 1. Read-only investigation gate

*Session A — Opus 5.5, high.*

**No source edits until this gate is complete and reported.** Building, running the CLI
and running the test suite are expected. Changing production code, test code or any
document is not.

Write the report to a **scratch file outside the working tree**. In chat give the path and
any STOP conditions that fired. End session A with `/handoff`, naming the report's path.

### 1.1 Re-measure the corpus

D-430 measured at `ce0b1ec` with the CLI built in a throwaway copy, on SDK 10.0.115 with
the `GROB-SEC-001` floor, MinVer and the Sonar analyser removed and no apphost, because
neither SDK 10.0.300 nor NuGet was reachable. **Re-measure on the supported SDK** before
relying on it.

The corpus is every fenced block whose info string starts with `grob` in `README.md` and in
`docs/wiki/**/*.md`, excluding `docs/wiki/ADR/` (D-430 Decision 1). Extract each with a
throwaway script and run it through `grob run` from a scratch directory, with closed stdin
and an environment holding no Grob-relevant variable. Record every diagnostic, the exit
code and whether the CLI reported an internal error. **State the expected counts before
running:**

- **123 blocks** across 38 files.
- **38** run with no diagnostic and exit 0.
- **24** fail only with E1001 on a namespace in D-430 Decision 3's list.
- **51** fail type-checking for other reasons, **5** fail to parse, **2** fail at run time
  with E5801 (`ADO_PAT` unset), **3** end in `error: internal error: value stack underflow`
  (R-30).

Then apply §2's verdict rules to the text as it stands, with no info strings: expect
**38 run, 24 excused, 36 fragments, 3 crashes and 22 others**. Of the 22, fourteen are
corrected by §2.3 (two of those also carry a blocker), two need only §2.2's fixture
environment and six carry a blocker with no correction. The three crashes carry
`blocked-by=R-30`.

**A block among the 38 that does not run clean is a STOP.** A count that differs elsewhere
is a finding: report each block that moved, with the reason measured and its effect on
§2's tallies.

### 1.2 Confirm the corrections

For each of W1 to W13 in §2.3: apply the correction to a scratch copy of the block, run
it, and confirm the verdict §2.3 predicts. Then run D-414's second query for each defect —
grep `README.md` and `docs/wiki/` for the pattern, not only the block — and list every
other site. A site in a `grob` block that §2.3 does not list is a finding and a STOP: the
closed list is wrong. A site in prose or a table on a page §2.3 touches is listed for
increment 6, not fixed here, unless §2.3 names it.

Also search the 38 clean blocks for a comment claiming an error (`compile error`, `error:`,
`not allowed`, an `Exxxx`). D-430 found one, W13. Another is a STOP: a passing block that
claims to fail is a defect this gate cannot see.

### 1.3 Trace R-32

R-32's row names the explicit reads. D-430 found implicit ones the row did not list.
Trace **every read of the host's local time zone or current directory reachable from
`RunCommand.Run`**, explicit or implicit, and list each with its file and line. D-430
expects eight in `Grob.Stdlib` and none elsewhere:

| Read | Site |
|---|---|
| `TimeZoneInfo.Local.GetUtcOffset` | `DatePlugin.cs` — `date.today`, `date.of`, `date.ofTime` |
| `DateTimeOffset.ToLocalTime()` | `DatePlugin.cs` — `toLocal` and `Now()` behind `date.now` |
| `DateTimeStyles.AssumeLocal` | `DatePlugin.cs` — both forms of `date.parse` |
| `Path.GetFullPath(string)` | `PathPlugin.cs` — `path.resolve` |

`TimeZoneInfo.FindSystemTimeZoneById` beside them takes an explicit zone identifier; it is
a lookup, not an ambient read, and stays (D-430 Decision 7).

Then confirm D-430 Decision 7's shape compiles against every current implementer of
`IClock` — list them — and that `HostCapabilities`, `InertCapabilities` and the
error-examples harness's `HarnessCapabilities` are the only places that assemble
capabilities. A read outside `Grob.Stdlib`, or an assembly point not listed, is a STOP.

### 1.4 Measure what a `param` does

Run a block declaring `param environment: string` and reading `environment`, with no
arguments. D-430 measured a runtime `E1001 Undefined global 'environment'` at line 3 with
no column: parameter binding is Sprint 10's. Confirm that no block §2 expects to run reads a
declared `param`. One that does is a finding; it is marked `fragment` only if a reader
would see it as one, and otherwise reported for the Sprint 10 increment.

### 1.5 Propose the info-string grammar

Propose the exact grammar D-430 Decision 2 and Decision 4 describe, and the harness's
rejection message for each of: an unknown token; `fragment` twice; a `blocked-by=` value
that is not `R-` and two or more digits; an `R-##` absent from the register; an `R-##`
present but closed. State how a fence opened with `Grob`, `grob-` or indentation is
treated. D-430 found one indented `grob` fence in the repository, at
`grob-language-fundamentals.md:2106`, outside the corpus; confirm none is inside it.

### 1.6 Confirm the register

Confirm R-24 and R-29 to R-33 read as D-430 amended them. Report any register text the
gate's measurement contradicts.

---

## 2. Scope

Five change groups, each a green commit. Land them in this order.

### 2.1 Change group 1 — R-32: the local zone and the current directory become capabilities

*Session B.* **This group may change `Grob.Runtime`, `Grob.Stdlib` and `Grob.Cli`**, and
the tests of each, and nothing else in `src/`. D-429 was forbidden to touch `Grob.Stdlib`;
D-430 Decision 7 authorises this group alone to do so.

- **`IClock` gains `TimeZoneInfo LocalZone { get; }`.** The clock answers "what time is it
  here", as .NET's own `TimeProvider.LocalTimeZone` does and as
  `grob-playground-architecture.md` §4 sketches. `SystemClock` returns
  `TimeZoneInfo.Local`.
- **`IFileSystem` lands in `Grob.Runtime` with one member, `string CurrentDirectory
  { get; }`**, on D-319's name. The 9C `fs` increment grows it; it is not designed here.
  `Grob.Cli` gains the OS-backed `SystemFileSystem`; `InertCapabilities` gains an inert one.
- **Every read §1.3 listed moves onto the capability.** `TimeZoneInfo.Local` becomes
  `clock.LocalZone`. `ToLocalTime()` becomes `TimeZoneInfo.ConvertTime(value, zone)`.
  `DateTimeStyles.AssumeLocal` goes, and the parse splits on whether the input carries an
  offset, because a `DateTimeOffset` parse without that flag still takes the host zone's
  offset for offset-free input. **Offset-free input** is parsed as a local date and time
  and given `clock.LocalZone`'s offset for that local time, so a time in a daylight-saving
  gap or overlap resolves exactly as `TimeZoneInfo.Local.GetUtcOffset` resolves it today.
  **Input with an explicit offset keeps that offset.** `path.resolve`'s
  `Path.GetFullPath(p)` becomes `Path.GetFullPath(p, fileSystem.CurrentDirectory)`, keeping
  the empty-input case.
- **`HostCapabilities` gains `FileSystem`**; `System()` supplies the OS-backed set, and
  `ReplCommand` composes it too. `HarnessCapabilities` gains a fixed zone and a fixed
  directory: the zone a custom one with a non-zero offset, so a native that ignores the
  capability and computes in UTC cannot pass by coincidence.
- **A source guard in `Grob.Consistency.Tests`**: no file under `src/Grob.Stdlib/` contains
  `TimeZoneInfo.Local`, `ToLocalTime(`, `AssumeLocal`, `Directory.GetCurrentDirectory`,
  `Environment.CurrentDirectory` or a one-argument `Path.GetFullPath(`. It is a text scan,
  the shape `NamedTypeLayoutTests` uses for layout names, and says which file and pattern
  failed.

`grob run`'s observable behaviour does not change: the OS-backed capabilities read what the
natives read before. Tests: `date.today`, `date.now`, `date.of`, `date.ofTime`, `toLocal`
and both `date.parse` forms under an injected zone, each with an offset-free input whose
result does not change with the host zone and an input with `+02:00` that keeps `+02:00`;
`path.resolve` under an injected directory; one test per form that the OS-backed set still reads the host. **R-32 closes
with this group's D-number in group 5.**

### 2.2 Change group 2 — the harness, without the corpus

*Session B.* In `tests/Grob.Integration.Tests/PublicSamples/`, beside the error-examples
harness (D-430 Decision 5). It runs every block through `RunCommand`'s own pipeline over the
seam and reuses `HarnessCapabilities`, `CliDiagnosticReader` and `DeferredWorkRegister`. It
does not reuse the quarantine manifest's parser, because there is no manifest.

- **Extraction.** Every fence opened by ```` ``` ```` followed directly by `grob` and either
  end of line or a space, in `README.md` and `docs/wiki/**/*.md` except `docs/wiki/ADR/`,
  read from the source tree at test time and discovered by glob. A block's identity is its
  repository-relative path and the line of its opening fence; its display name adds the
  nearest heading, so a failure names the page and section. Read tolerating `\r\n`.
- **The info string**, per §1.5: after `grob`, zero or more space-separated tokens, each
  `fragment` or `blocked-by=R-##`. Any other token fails the harness with §1.5's message.
- **Verdicts**, D-430 Decision 2 to Decision 4:

  | Class | How it is run | Passes when |
  |---|---|---|
  | bare `grob` | the full pipeline: compiled and, with no compile error, executed | no diagnostic, exit code 0 and no internal error |
  | `grob fragment` | the full pipeline, which stops at its compile errors | at least one diagnostic, and every diagnostic is E1001 |

  Before either test, every E1001 whose subject is a namespace on the excused list is set
  aside. **A bare block left with no diagnostic but at least one set aside passes as
  excused and is not executed**; its exit code is not checked. A bare block with nothing
  set aside is executed. A `fragment` block that writes no diagnostic fails, because it is
  a complete program and its marker is wrong.
- **The excused list** is closed and lives in the harness: `fs`, `json`, `csv`, `process`
  and `regex`, each of which must be absent from `StdlibCatalogue.ForCompileOnly()` — the
  error-examples harness's guard — and `http`, `auth`, `crypto` and `zip`, the Sprint 11
  plugin namespaces, checked by strictness alone, as D-428 checks `Sprint 11`.
- **Blockers.** A block carrying `blocked-by=R-##` must fail its class, and every `R-##`
  it names must be an open register row. One that passes fails the harness with a message
  to remove the token; a closed or absent row fails it with the register guard's messages.
- **Fixtures** (D-430 Decision 8): one environment for the whole corpus, holding fake
  values for exactly the variables the runnable blocks read — `ADO_PAT` and `BASE_URL` as
  measured. A fixture variable that no block reads fails the harness, so the list cannot
  outgrow the corpus. The clock, zone and directory are `HarnessCapabilities`' fixed ones.

Unit tests over inline markdown cover every rule above before the corpus is wired in;
§4 lists them.

### 2.3 Change group 3 — the wiki corrections

*Session C.* The closed list. Each is the documentation contradicting the language or its
own reference, with the authority that says so. Change nothing else in these blocks. Each
correction is verified by running its block through the CLI, as §1.2 did.

| # | Page and block | Correction | Authority | Then |
|---|---|---|---|---|
| W1 | `Language-Specification/Error-Handling.md`, *`try / catch`* | `catch IoError e {` → `catch (e: IoError) {`; `catch JsonError e {` → `catch (e: JsonError) {` | D-274; `grob-language-fundamentals.md`'s `try`/`catch` grammar | excused (`fs`, `json`) |
| W2 | `Language-Specification/Script-Parameters.md`, *The `param` Block* | `param env:` → `param environment:`, and every later use of that parameter's name on the page, prose and invocation examples included | D-412 (a `param` joins the top-level name space; E1102); ADR-0010 and D-342 (core module names) | runs |
| W3 | `Language-Specification/Script-Parameters.md`, *Decorators* | each decorator moved to its own line **above** the declaration it modifies; `env` renamed as W2 | D-410; §19's production `{ decorator newline } "param"` | runs |
| W4 | `Language-Specification/Syntax.md`, *Variables* | the third declaration's `x` → a new name; its comment kept | `:=` declares (E1102 on a second `:=`) | runs |
| W5 | `Language-Specification/Syntax.md`, *Statement Termination* | the trailing-operator example's leading `+` lines → `price +` / `tax +` / `shipping`; the second `result :=` → a new name. **The same operator correction in `grob-language-fundamentals.md` §14**, whose example this block copies; nothing else in §14 | §14's Case 1 table: an operator suppresses the newline only when it ends the line | fragment |
| W6 | `Plugins/Grob-Zip.md`, *Examples* | every double-quoted Windows path → a backtick raw string | D-285; `"C:\backup"` is an invalid `\b` today (E2005) | excused (`zip`) |
| W7 | `Standard-Library/date.md`, *Parsing* | the second `d :=` → a new name | as W4 | runs |
| W8 | `Standard-Library/date.md`, *File age check*; `Type-Registry/File.md`, *Move old files to archive*; `Language-Specification/Functions.md`, block 5 | `minusDays(n)` → `addDays(-n)`; and `Type-Registry/date.md`'s `minusDays` table row removed | `grob-type-registry.md`'s `date` surface has no `minusDays`; `grob-stdlib-reference.md` writes `addDays(-30)` | excused (`fs`) twice; `Functions.md` is `blocked-by=R-29` (§2.4) |
| W9 | `Standard-Library/formatAs.md`, block 3 | `entries.formatAs.table(["name", "size_mb"])` → `entries.formatAs.table(columns: ["name", "size_mb"])` | `grob-stdlib-reference.md`'s `formatAs.table(items, columns)`, whose chained form takes `columns:` (E0011 today) | `blocked-by=R-29` (§2.4) |
| W10 | `Standard-Library/guid.md`, *Examples* | rewritten against the reference's surface: `guid.newV4()`, `guid.newV5(guid.namespaces.url, …)`, `guid.newV7()`, no postfix `!`, and the invalid-input example through `guid.tryParse`, which returns nil | `grob-stdlib-reference.md`'s `guid` module; `guid.parse` throws, and a literal argument is checked at compile time (E0601) | runs |
| W11 | `Type-Registry/array.md`, *Typed deserialisation* | the one-line `type User { name: string, email: string }` → a newline-separated body | `grob-language-fundamentals.md` §10; D-406 | excused (`json`) |
| W12 | `Standard-Library/log.md`, block 1 | `${env}` → `${environment}` | `env` is a core module and not a value (E1004); D-342 | fragment |
| W13 | `Language-Specification/Types.md`, the nullable block | the comment `// compile error — name might be nil` corrected to say what happens | `grob-language-fundamentals.md` §13: `print()` accepts nullable values; interpolation does not (D-279) | runs |

Green at the end of this group: the suite is unchanged, since the corpus is not yet wired.

### 2.4 Change group 4 — the corpus enters the gate

*Session C.* Wire the theory to the live corpus, **with no info strings yet, and confirm it
red first.** Predict the failing count before running: **49** — 38 fragments, six R-29
blocks and five R-30 blocks. Record which fail.

Then add the info strings:

- **`blocked-by=R-30`** on the five blocks that use a regex literal: `regex.md` blocks 1, 2
  and 3, `Type-Registry/Match.md` block 1 and `Type-Registry/Regex.md` block 1. Three crash
  the VM. Two fail first on the `unknown` the literal is typed as: `regex.md` block 2 is a
  program whose other diagnostic is an excused `fs`, and `Regex.md` block 1 is also a
  `fragment`.
- **`blocked-by=R-29`** on the six blocks whose only remaining failure is a cascade from
  an `unknown` value: `formatAs.md` blocks 1, 3, 4 and 5, `Functions.md` block 5 and
  `Plugins/Grob-Http.md` block 3. The first five are also fragments; `Grob-Http.md`'s is a
  program whose other diagnostics are excused `http` and `auth`.
- **`fragment`** on 44 blocks: the 36 §1.1 measured, W5's, W12's and the six blocked
  fragments above.

Green at the end of this group, with the tallies stated in the commit message: **45 run,
29 excused, 38 fragments, 11 blocked (six of them fragments), 0 failing, 123 in all.** Any
difference from these is explained block by block.

### 2.5 Change group 5 — corrections and record

*Session C.* Each lands now because this increment makes it true:

- **`grob-v1-requirements.md` §14**: one sentence beside D-428's, stating the public
  samples gate — every `grob` block in `README.md` and the wiki passes it, the excused list
  is empty and no block carries `blocked-by` — citing D-430.
- **`.claude/skills/writing-grob-source/SKILL.md`** gains the info-string grammar: when a
  block is a program, when it is a `fragment`, what `blocked-by` means and that a block
  outside the corpus is not checked. `CONTRIBUTING.md` points at it if it gives guidance on
  documentation samples; say whether it does.
- **The decisions-log entry** in three-location lockstep. **Take the D-number from the
  live tail of the merged log**, never from memory or this prompt. It ends with a
  *Findings for the corpus sweep* section: every documentation defect §1.2 listed outside
  the closed list, by file and line. Increment 6 reads that section as its input.
- **The register**: R-32 to Closed with that D-number; R-29 and R-30 amended where §1
  measured differently from D-430; any new row a finding needs, with an owner and a
  completion criterion.

### Out of scope — report, do not fix

- **Any change to `Grob.Compiler`, `Grob.Vm` or `Grob.Core`**, and any change to
  `Grob.Runtime`, `Grob.Stdlib` or `Grob.Cli` outside group 1. A block that passes only by
  changing one of them carries a blocker.
- **The cascades** (R-29), **the regex literal** (R-30) and **undefined type names** (R-31).
- **Expected-output comparison.** No block states its output in a form the harness could
  check (D-430 Decision 2).
- **Style that compiles**: Windows paths written `"C:\\..."` against D-285, trailing commas
  §3.5 requires in multi-line literals, the retired term "`param` block" in a heading.
  Each parses and type-checks; each is increment 6's.
- **`docs/design/`**, except W5's one line in §14; **`docs/wiki/ADR/`**;
  **`grob-sample-scripts.md`**, which keeps D-417's guard; **`grob-lang.dev`** (R-33).
- **Parameter binding** — Sprint 10.

**D-420's stopping rule applies.** Findings go to the register, not into the run, unless
they block this increment or Sprint 9C.

---

## 3. Implementation notes

**One pipeline.** The harness calls `RunCommand.Run` through the seam. A test that lexes,
parses, checks, compiles and runs on its own is a second pipeline that can disagree with
the CLI, which is the mirror D-426 removed for natives. Fragments go through the same
pipeline; it stops at their compile errors, which is the front end they need.

**Excusing is narrow.** Only an E1001 whose subject is a listed namespace is set aside.
Nothing else is normalised, and no other code is excused in any class.

**No skips.** A block that cannot pass carries a `blocked-by` token, never `Skip =` on an
attribute. A skip hides a block that starts passing; strictness reports it.

**The blocker travels with the block.** It is in the info string, not a manifest keyed by
line or ordinal, because a wiki block has no identity a heading edit or an inserted block
above it cannot move (D-430 Decision 4). GitHub renders a fence by its first info-string
word, so the tokens are invisible to a reader and visible to a reviewer.

**Discovery, not registration.** Files are found by glob and blocks by scanning. A new
wiki page is in the gate the moment it exists.

**One case per block.** Theory data carries the block's display name, so a failure names
the page, the section and the mismatch: the diagnostics written against the class's rule.

---

## 4. Tests

TDD throughout — red, green, refactor. Never merge known-wrong code.

Required:

- **R-32**: per §2.1.
- **Extraction**: a block at end of file; two blocks on one page; an indented fence; a
  fence `Grob` or `grob-x`, not extracted; a `\r\n` file; a file under `docs/wiki/ADR/`,
  not extracted.
- **Info string**: each rejection §1.5 lists; both tokens on one fence; tokens in either
  order.
- **Verdicts**: a bare block that runs clean passes; one that writes a diagnostic, exits
  non-zero or reports an internal error fails, each its own test; a bare block whose only
  diagnostic is an excused E1001 passes and is not executed; one with an excused E1001 and
  another diagnostic fails; a fragment with only E1001s passes; a fragment with no
  diagnostic fails; a fragment with an E1002 fails.
- **Guards**: a listed core namespace present in the catalogue fails; a blocked block that
  passes fails; a closed `R-##` fails; an absent `R-##` fails; a fixture variable no block
  reads fails.
- **Corpus**: every block, one case each.

**Mutation-verify** each guard. For each: weaken it, **predict the exact failing tests by
name before running**, confirm, restore.

1. Compile bare blocks without executing them. Predicted: the internal-error verdict test;
   the three R-30 corpus cases that crash, each now passing while blocked.
2. Accept any diagnostic in a fragment. Predicted: the fragment E1002 test; the six
   blocked fragments — `formatAs.md` 1, 3, 4 and 5, `Functions.md` 5 and `Regex.md` 1 —
   each now passing while blocked.
3. Let a fragment pass with no diagnostic. Predicted: the empty-fragment test; no corpus
   case.
4. Excuse every E1001, not only listed namespaces. Predicted: the verdict test for a bare
   block with a non-namespace E1001; and the 38 unblocked fragment corpus cases, each left
   with no diagnostic once its E1001s are set aside. The six blocked fragments still fail
   on their non-E1001 diagnostic, so stay green.
5. Remove the catalogue guard on the excused list. Predicted: the registered-namespace
   guard test.
6. Remove the open-row check on `blocked-by`. Predicted: the closed-row guard test.
7. Ignore unknown info-string tokens. Predicted: the unknown-token test.
8. Drop the fixture environment. Predicted: the three corpus cases that read `ADO_PAT` or
   `BASE_URL`; the unused-fixture guard test.
9. Read `TimeZoneInfo.Local` again in `date.today`. Predicted: the injected-zone `today`
   test and the source guard.

---

## 5. Deliverables

- Five change groups per §2, each its own green commit.
- **123 blocks: 45 run, 29 excused, 38 fragments and 11 blocked**, with any difference
  explained block by block.
- R-32 closed. Error-code count **unchanged at 120**; no `ErrorCatalog` edit; D-316 gate
  green.
- The full suite green, with the test count; coverage of `Grob.Cli` and `Grob.Stdlib`
  reported against the D-328 bar.
- The nine mutation verifications, each with its prediction and the observed failures.
- A decisions-log entry in three-location lockstep, with its *Findings for the corpus
  sweep* section, and the register updated per §2.5.

---

## 6. STOP conditions

Stop and report, do not proceed:

- The session-start check finds the running model does not match the session's row.
- A block among §1.1's 38 does not run clean on the supported SDK.
- §1.2 finds a `grob` block with one of W1 to W13's defects that §2.3 does not list, or a
  clean block claiming an error other than W13.
- §1.3 finds an ambient read outside `Grob.Stdlib`, or a capability assembly point not
  listed.
- R-32's change alters `grob run`'s observable behaviour, or needs a change outside
  `Grob.Runtime`, `Grob.Stdlib`, `Grob.Cli` and their tests.
- A block passes only by changing `Grob.Compiler`, `Grob.Vm` or `Grob.Core`, and no open
  register row says why. Raise the row first; stop if it cannot be stated honestly.
- Any block hangs, or reaches a real filesystem, process or network resource under the seam.
- A correction would change a block for a reason outside §2.3, or §2.4's tallies cannot be
  reached without a `fragment` or `blocked-by` the measurement does not support.
- The change needs a new error code, an `ErrorCatalog` title or description edit, an
  opcode, a grammar change or a `GrobType`, `GrobValueKind` or `TokenKind` variant.
