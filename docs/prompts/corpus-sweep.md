# Increment prompt — corpus sweep

**Authority: D-431**, the sixth and last increment of D-420's pre-9C run. Read D-431, D-430,
D-420, D-414 and D-428 in `docs/design/grob-decisions-log.md`, R-05 in the Deferred Work
Register in `docs/design/grob-open-questions.md`, and **the landing entry of increment 5**
(*public samples compile*), before starting. This prompt implements them; where prompt and
decision differ, D-431 wins and the difference is a finding to report.

**This increment depends on increment 5 having landed.** It reads two things that exist only
once it has: the public samples gate, which verifies every wiki block this sweep edits, and
the *Findings for the corpus sweep* section of increment 5's landing entry, which is part of
this sweep's list. Find that entry by its subject, not by a number: it is the entry after
D-430 that records *public samples compile* as landed and closes R-32.

**Model and effort, by session.** No production or test code changes. The work is reading
the corpus against the tree and deciding, item by item, whether a sentence is still true.
The judgement is in the gate and in the `.claude/` sweep, where every advertised interface
is checked against what is built:

| Session | Work | Model | Effort |
|---|---|---|---|
| A | §1 investigation gate | Opus 5.5 | high |
| B | Change groups 1 and 2 — script counts and the release gate; the design corpus, the wiki and the root documents | Opus 5.5 | medium |
| C | Change group 3 — `.claude/` (R-05) | Opus 5.5 | medium |
| D | Change groups 4 and 5 — the `// Eventual path:` lines, record | Opus 5.5 | medium |

A session may cover fewer groups than its row; it may not cover groups from two rows.

**Session-start check — before anything else in every session:**

1. Read `.grob-session/handoff.md` (absent in session A) and `git log` on the branch, and
   state which session row this is and which change groups it will cover.
2. State the model you are running as, from your own system context. If it does not match
   the row, **stop** and say so.
3. You cannot see the effort setting. State the row's expected effort and **wait for a
   one-word confirmation** before proceeding.

**Branch:** one concern, one branch. The concern is the corpus sweep.

Every change group in §2 ends as its own commit with the full suite green, and `/handoff`
is run after each so `.grob-session/handoff.md` names the last completed group, the next
one and anything open. A new session resumes from the handoff file and `git log` — never
from memory of a previous conversation.

**This prompt was committed with D-431** at `docs/prompts/corpus-sweep.md`. Never retrofit
it. If the work diverges, the divergence goes in the decisions-log entry.

---

## 1. Read-only investigation gate

*Session A — Opus 5.5, high.*

**No edits until this gate is complete and reported.** Write the report to a **scratch
file outside the working tree**. In chat give the path and any STOP conditions that fired.
End session A with `/handoff`, naming the report's path.

### 1.1 Confirm increment 5 has landed

Its landing entry exists, R-32 is closed, the public samples gate is in the required CI job
and green on `main`, and the entry has a *Findings for the corpus sweep* section. **If any
is false, stop.**

### 1.2 Verify every item on D-431's list

D-431 Decision 1 enumerates every deferral to the corpus sweep in the decisions log, the
principal review's corpus-session items, the findings of 6 and 13 November and increment
5's findings. For each row marked *sweep*, run **D-414's two queries**: the first proves the
decision that retired the form, the second greps the scope (§1.4) for every site that still
carries it. List every site by file and line. For each row marked *closed*, confirm the
closing decision says what D-431 says it does. For each row marked *register*, confirm no
open row already tracks it.

Expected, measured at `ce0b1ec`; state these before running and explain every difference:

- **Script counts.** "thirteen" validation or sample scripts at `grob-sample-scripts.md`
  lines 3 and 4; `grob-v1-requirements.md` lines 72, 74, 75, 1050, 1319, 1323, 1365, 1372,
  1460 and 1490; `grob-benchmarking-strategy.md` lines 107, 205, 347, 388 and 414, with
  lines 291 and 301 already saying the count is stale. **The correct thirteens stay**:
  thirteen core modules and the thirteen-level precedence table. Footers and changelogs are
  records and stay.
- **`grob-v1-requirements.md` §14's table has thirteen rows.** Rows 12 and 13 describe
  scripts that never existed (D-346); D-431 Decision 1 says what replaces them.
- **Windows paths against D-285**: 37 lines in `README.md` and the wiki write a path as a
  double-quoted `"C:\\..."`; count the living design documents.
- **`.claude/`**: 27 files at `ce0b1ec`. R-05's row says 39 at `f7acb83`. Account for the
  difference from `git log -- .claude` before sizing the sweep.

### 1.3 Decide each item's group

Assign every verified site to a change group in §2. A site whose correction needs code or a
decision D-431 does not take is not a sweep site: list it as a register row to open, with an
owner and a completion criterion, or as a question for Chris.

### 1.4 Confirm the scope

The scope is D-431 Decision 2's. List every file in it, and every file excluded with the
reason. A file that is neither — not plainly living, not plainly a record — is a
question for Chris, not a judgement in session.

### 1.5 Plan the renumbering

For the 42 error-example pairs whose source carries `// Eventual path:` on line 2, write the
transform as a throwaway script and run it on a scratch copy: delete line 2 of the source;
in the master, decrement every line number at or past 2 in each `-->` line, each numbered
snippet line and each stack-chain location; in `quarantine.md`, decrement the same in each
quarantined pair's *Observed* cell. Then run both harness layers on the scratch copy. **Every
case must be green with no other change.** Report any pair that is not, and stop: the
transform is wrong, not the pair.

---

## 2. Scope

Five change groups, each a green commit. Land them in this order.

### 2.1 Change group 1 — script counts and the release gate

*Session B.* The script-count sites §1.2 listed, each edit citing D-346. Fix
`grob-sample-scripts.md`'s scope header first. In `grob-v1-requirements.md` §14, rows 12 and
13 go and a sentence after the table names what will add release-gate scripts: R-08's,
exercising the full decorator surface, and R-34's regex script (D-432). §12's table row and its "integration tests in `Grob.Integration.Tests`" sentence
say where each gate runs: the validation scripts in `Grob.Compiler.Tests`, the
error-examples harness and the public samples gate in `Grob.Integration.Tests`.

### 2.2 Change group 2 — the design corpus, the wiki and the root documents

*Session B.* Every *sweep* row in D-431 Decision 1 that it does not assign to group 1, 3
or 5, and every site in increment 5's
*Findings for the corpus sweep* that is documentation. Each edit cites the decision that
makes it true. For a site inside a `grob` block in the wiki or the README, the public samples
gate re-checks it: a block that changes class fails the gate, and that is a STOP, not a
reason to change its info string.

### 2.3 Change group 3 — `.claude/` (R-05)

*Session C.* Every file in `.claude/` and every `CLAUDE.md` and `AGENTS.md`, read against
the tree. **An advertised interface is anything a reader would act on**: a type, member or
method name; a file path or naming convention; a command, script or test project; a
procedure step; a model name; a count. Each is either true of the tree, corrected to what is
true, or removed. Root `CLAUDE.md`'s model policy is rewritten per D-431 Decision 5. R-05
closes with this increment's D-number.

### 2.4 Change group 4 — the `// Eventual path:` lines

*Session D.* The transform §1.5 proved, on the real tree, as one commit (D-431 Decision 4).
42 sources lose line 2; their masters and the quarantine manifest's *Observed* cells move
with them. Both harness layers green with no other change. The commit message lists the 42
pairs, says the transform is mechanical and cites D-431.

### 2.5 Change group 5 — record

*Session D.*

- **The decisions-log entry** in three-location lockstep. **Take the D-number from the live
  tail of the merged log**, never from memory or this prompt. It lists every row of D-431
  Decision 1 with its outcome and every register row opened.
- **The register**: R-05 to Closed; any row §1.3 found it needs, each with an owner and a
  completion criterion.
- **The non-entry sections of the decisions log** per D-431 Decision 1's F11 and F12 rows:
  the *Project Status* table and the *Related Documents* table.

### Out of scope — report, do not fix

- **Any change to a file that is not markdown**, except group 4's `.grob` and
  `.expected.txt` files. No source, test, build or workflow change.
- **The body of any decisions-log entry**, including an index row's prose. Entries are
  append-only; a correction is a new entry's line.
- **`prompts/archive/` and `docs/prompts/`.** Archived prompts are records (D-428 Decision
  6) and issued prompts are never retrofitted (D-431 Decision 3).
- **Dated records** (D-431 Decision 2) and **footers and changelogs** in living documents.
- **ADR bodies.** An ADR page may gain a status amendment and a *What Changed Since This
  ADR* section, on ADR-0016's precedent; its decision text is not edited.
- **Anything needing code or a decision D-431 does not take.** It goes to the register.
- **Outside the repository**: `grob-lang.dev` (R-33), and the claude.ai Project's
  instructions, which still say 86 codes, 40 pairs and `_grob.txt`. Those are Chris's.

**D-420's stopping rule applies.** Findings go to the register, not into the run, unless
they block this increment or Sprint 9C. The run ends with this increment; nothing it finds
becomes a seventh.

---

## 3. Implementation notes

**Two queries, every time** (D-414). The decision proves a form retired; the grep finds what
still uses it. A sweep that runs only the first reviews changes; it does not sweep.

**Correct thirteens and records stay.** The trap in the script count is real: "thirteen core
modules" and the "thirteen-level precedence table" are true. A changelog line saying
"expanded from eleven to thirteen rows" is a true record of an edit that was wrong.

**Cite the authority in the edit.** Each spec edit cites the decision that makes it true
(the corpus's own rule). A sentence corrected without a citation reads as an opinion.

**The gates are the guards.** The public samples gate re-checks every wiki and README block
this sweep touches; the error-examples harness re-checks every pair group 4 renumbers. Prose
has no gate; the second query run again after the sweep, returning no site in scope, is its
check.

---

## 4. Tests

This increment adds no test. Its edits are verified by the two gates that exist and by the
second queries.

**Mutation-verify** the gates over the sweep's own edits. For each: make the change on the
branch, **predict the exact failing tests by name before running**, confirm, revert.

1. Restore one renumbered master's `-->` line to its old line number. Predicted: that pair's
   static case, since its first caret no longer sits under the line the location names; and
   its behavioural case if the pair passes. A quarantined pair's behavioural case stays
   green, because it compares against *Observed*, not the master — which is why the static
   layer is never quarantinable.
2. Restore one deleted `// Eventual path:` line in a passing pair's source, leaving its
   master renumbered. Predicted: that pair's static case, on the snippet line, and its
   behavioural case, on the location.
3. Leave one quarantined pair's *Observed* cell unrenumbered. Predicted: that pair's corpus
   case, on observed drift.
4. Revert one D-285 path rewrite in a runnable wiki block to `"C:\Reports"`, single
   backslash. Predicted: that block's public samples case, on E2005.
5. Re-run every second query of §1.2 after group 5. Predicted: no site in scope.

---

## 5. Deliverables

- Five change groups per §2, each its own green commit.
- Every row of D-431 Decision 1 with its outcome: swept with its sites, confirmed closed, or
  registered with the row's number.
- R-05 closed. Both gates green; the public samples gate's tallies unchanged from increment
  5's landing; the error-examples harness at 27 passing and 29 quarantined.
- Error-code count **unchanged at 120**; no `ErrorCatalog` edit.
- The five mutation verifications, each with its prediction and the observed result.
- A decisions-log entry in three-location lockstep, and the register updated per §2.5.

---

## 6. STOP conditions

Stop and report, do not proceed:

- The session-start check finds the running model does not match the session's row.
- Increment 5 has not landed (§1.1).
- §1.5's transform leaves any case red on the scratch copy.
- An edit would touch an entry body, an archived or issued prompt, a dated record, a footer
  or changelog line or an ADR's decision text.
- Either gate is red after an edit, or green only by adding a `fragment` or `blocked-by`
  token, a quarantine row or a changed *Observed* cell outside group 4.
- A master's code or column would change, or a renumbered caret would sit on a different
  token.
- A file in the scope is neither living nor a record by D-431 Decision 2, and Chris has not
  ruled on it.
- The change needs code, a test, a new error code or a decision D-431 does not take, and the
  item blocks the sweep. If it does not block the sweep, it goes to the register instead.
