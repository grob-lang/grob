# The crash oracle

The crash oracle (D-451) checks one rule: **no program text, valid or not, may make Grob raise
an internal error or throw a host exception.** A program either gets diagnostics with
registered codes, or compiles and runs, with any failure a Grob runtime error. The oracle runs
programs through the lexer, parser, checker, compiler and VM in process, against fake
capabilities, and classifies how each run ended. It lives in `tooling/Grob.Torture`, and its
tests, the CI slice among them, in `tests/Grob.Torture.Tests`.

This page covers running it by hand, reading what it finds and keeping the CI slice green.
The design is in the increment prompt (`docs/prompts/crash-oracle.md`), the adversarial testing
strategy's Pillar 1 (`docs/design/grob-adversarial-testing-strategy.md`, D-353) and the landing
entry in the decisions log.

---

## What counts as a finding

| Outcome | Allowed? |
|---|---|
| Diagnostics, each with a code registered in `ErrorCatalog` and a location inside the source | yes |
| A clean run | yes |
| `exit(n)` with `n` in `0..255` | yes |
| A Grob runtime error with a registered code | yes |
| *Bounded*: the VM stopped at its time bound, as a legal `while (true) {}` does | yes, counted |
| A `GrobInternalException` or any host exception, in any phase | **finding** |
| `exit(n)` outside `0..255` | **finding** |
| A diagnostic or runtime error with an unregistered code, or a location outside the source | **finding** |
| An identifier left without a `Declaration` after checking (§3.1.1, D-311) | **finding** |
| A run past its watchdog, the VM's bound plus an allowance for the earlier phases | **finding** |
| A campaign worker that died (a stack overflow or an out-of-memory) | **finding** |

A finding names the phase it arose in. One from the checker or later means the checker
accepted a program it should have refused, or a later phase lost an invariant: report it as
that, not as a VM bug.

## The three commands

Run the tool from the repository root, in Release:

```bash
dotnet run -c Release --project tooling/Grob.Torture -- <command>
```

The build's own executable, `tooling/Grob.Torture/bin/Release/net10.0/Grob.Torture` (with
`.exe` on Windows), takes the same commands.

| Command | What it does | Exit code |
|---|---|---|
| `check <file>...` | Runs each file through the oracle at the CI slice's bounds and prints its outcome and any findings with their signatures | `0` when nothing was found, `1` when something was |
| `corpus [<root>]` | Runs the CI slice by hand: the seed corpus, its fixed sample of mutants and the fixed sample of generated programs. Lists every finding as *known* (with its register row) or *new*, every stale known entry and every problem with the known-findings list | `0` when the slice is green, `1` when it is not |
| `campaign --out <dir> --programs <n> --seed <n> [--workers <n>] [--grob <path>] [--root <dir>]` | The long mode: a campaign over generated programs and mutants, run by hand and never on a schedule (D-451 Decision 4) | `0` when nothing was found, `1` when something was |

A usage error, including a file or directory that cannot be read, exits `3`.

## Running a campaign

```bash
dotnet build src/Grob.Cli -c Release
dotnet run -c Release --project tooling/Grob.Torture -- campaign \
    --out .grob-session/torture/<date> --programs 200000 --seed <n> \
    --grob src/Grob.Cli/bin/Release/net10.0/grob
```

`--grob` names the CLI the black-box check runs; on Windows it is `grob.exe`. Leave it out to
skip the check. **The check is not isolated**: it runs each reproducer with the machine's own
capabilities, not the oracle's fakes. That is safe only while no native the CLI registers
writes or deletes a file, starts a process or reaches the network; `BlackBoxTests` fails when
the CLI gains a namespace or named type nobody has judged, and R-95 owns the isolation that
must come before `fs` can write.

- **The plan.** Half the programs are written by the generator and half are mutants of the
  seed corpus, alternating, with no source planned twice. The plan depends on the campaign seed
  and the seed corpus: a seed's id holds a hash of its text, so editing a fixture, an error
  example, a public sample or a raw literal changes the mutants planned. Reproducing a campaign
  takes the same campaign seed over the same commit. A smaller plan of one campaign seed over
  one corpus is a prefix of a larger one, so a pilot of 10,000 is the start of the full run.
- **The workers.** `--workers` child processes, four by default, each run their share of the
  plan. A stack overflow kills a worker, not the campaign: the worker keeps a journal of the
  program and phase it is in, and the supervisor records a death as a finding for that program
  and starts the worker again past it.
- **Minimising.** Each finding is minimised as soon as it is made, in the worker, so
  minimising runs in parallel. A program runs with 2 s of VM time; a minimising candidate runs
  with 25 ms, since deleting lines often deletes a loop's update.
- **Resuming.** Run the same command over the same directory and the campaign carries on
  where it stopped. A directory that holds a campaign with other settings is a usage error.
- **The black-box check.** With `--grob`, each distinct reproducer is run through the real CLI
  at the end, and the report says whether a user would see E9001 or a .NET stack trace.
- **Time.** On a six-core machine with four workers, about 57 programs a second, minimising
  included: a 10,000-program pilot took three minutes, and 200,000 programs took 58 minutes of
  workers and 9 more for the report and the black-box check of their 10,000 reproducers.

The directory holds:

| Path | Holds |
|---|---|
| `campaign.json`, `plan.txt` | The settings and the plan |
| `results/shard-<k>.jsonl` | One line per program a worker ran: its id, outcome and findings |
| `findings/<signature-hash>/<program-hash>.json` | One record per failing program and signature |
| `unminimised/shard-<k>.txt` | Programs whose records keep the whole program, and why |
| `blackbox/` | The reproducers the black-box check ran |
| `report.md` | The report |

## Reading a finding

A **signature** is the phase, the class and the class's own fields, with numbers, quoted text
and paths taken out of the message:

```text
Run|Exception|GrobInternalException: GrobValue kind mismatch: expected Int, actual String.
```

**A signature does not identify a root cause.** Two causes can stop the VM with the same
message, and one cause can stop it with several. So nothing is set aside because its signature
was seen before.

A **record** keeps the program as it failed beside the smallest program minimising found that
fails with the same signature:

| Field | Holds |
|---|---|
| `programId` | `generated/<campaign-seed>/<index>` for a generated program, `<seed-id>/<operator>/<draw>` for a mutant, the seed's id for a seed |
| `signature`, `message`, `firstFrame` | The signature, the message as observed and the first stack frame in the pipeline |
| `original`, `reproducer` | The program as it failed and its reproducer |
| `possiblyMultiCause` | Minimising from the front and from the back disagreed, or the original still fails once the reproducer's lines are removed: look for a second cause |

The report lists, for each signature, its distinct reproducers shortest first. Triage starts
there:

1. Run the shortest reproducer through `grob run` and confirm it fails the same way.
2. Shrink it further by hand until each line is needed, and find the cause in the source.
3. Compare it with the register's open rows in `docs/design/grob-open-questions.md`. A known
   row is matched by its reproducer, never by signature alone.
4. File each new root cause as one row, with the reproducer and the output measured through
   `grob run`, or add the new shape to the row it matches.

To rebuild a program from its id, use `CampaignPlan.Write`, or `ProgramGenerator.Generate` for
a generated one, with the campaign seed.

## Keeping the CI slice green

The CI slice is `CorpusSliceTests`. It runs every seed, a fixed sample of about 550 mutants
and the first 200 generated programs at a fixed campaign seed, and fails on any finding the
known-findings list does not name.

The list is `tests/Grob.Torture.Tests/known-findings.json`:

```json
{ "row": "R-60", "signature": "Run|Exception|GrobInternalException: …", "programs": ["<program id>", "…"] }
```

- An entry names its open register row, the signature and the programs it covers. **Only those
  programs are suppressed**: a new program with a known signature is a finding until it is
  triaged.
- An entry whose row is missing from the register, or closed, fails the slice. So does a stale
  entry, one whose program is gone or no longer makes the finding: a fix that closes a row
  takes its entries with it.

**Adding a known entry**, when the slice reports a new finding:

1. Run `corpus` and copy the program id and the signature from its `new` line.
2. Triage the program as for a campaign finding. If its cause has no row, file one.
3. Add the program to the entry for that row and signature, or add an entry.
4. Run `corpus` again and confirm the program reads as `known`.

A seed's id holds a hash of its text, so editing a fixture, an error example, a public sample
or a raw string literal in a test changes the ids of the seed and its mutants. An entry that
names the old id then goes stale, and the new program must be triaged again. Changing the
generator or the mutator does the same to the programs it writes. **Replacing an id**, after
such a change:

1. Triage the program under its new id, and add the new id to its entry as above.
2. Remove the old id from the entry, and the entry itself if no program is left in it.
3. Run `corpus` again and confirm it reports no `new` and no `stale` line.
