# D-318 — drop-in block (NOT an integrated file)

> **What this is.** A ready-to-insert decisions-log entry plus its two lockstep
> companions (the summary-index row and the footer-changelog line), authored to
> the `logging-a-decision` skill's format. It is delivered as a standalone block
> because the design corpus was **not** in this planning session — only the Claude
> Code harness zip was. The `logging-a-decision` skill requires delivering the
> **full updated `grob-decisions-log.md`**, not a fragment; reconstructing a
> ~318-entry file from search snippets would violate the canonical-files-outrank-
> corpus-copies rule and risk silent drift. So: drop this entry into the real
> `grob-decisions-log.md` against the live file, or hand the corpus zip back and I
> will produce the integrated full file. **D-318 must exist before Sprint 5
> Increment B runs** — Increment B registers E0008–E0011 citing it.
>
> **Before inserting, verify on the live file:** (1) **D-318** is genuinely the
> next-free number (the kickoff, the six increment commands and the QA brief all
> reference D-318 — if the real next-free has moved past 318, renumber here and in
> those files together); (2) the summary-index row below matches the index's
> actual column layout; (3) the changelog line matches the footer's actual format.

---

## 1. The full entry (insert in `D-###` sequence, in the body)

```
### D-318 — Named-argument call-site diagnostics get dedicated codes (June 2026)

Area: Type system — named-argument diagnostics
Supersedes: none
Superseded by: none

The four named-argument call-site errors D-113 specifies — a named argument
before a positional one, a named argument naming a required (defaultless)
parameter, a duplicate named argument, and a named argument naming an unknown
parameter — get four dedicated error codes rather than folding into E0003 (wrong
number of arguments):

- E0008 — named argument before positional
- E0009 — named argument names a required parameter
- E0010 — duplicate named argument
- E0011 — unknown parameter name

Dedicated codes, not a fold into E0003, because each of the four is a distinct
mistake with a distinct fix, and the diagnostics are part of the product: E0008
wants "move named arguments after positional ones", E0009 "this parameter has no
default, pass it positionally", E0010 "this parameter is already supplied", E0011
"no parameter named X — did you mean Y?". Folding all four into E0003 would
mis-describe them (none is an arity error) and collapse four targeted suggestions
into one generic message, against the error-message quality bar. The alternative
considered — reuse E0003 for arity-shaped cases and E0004 for the rest — was
rejected for the same reason: the call-site binding errors are a category of their
own, not instances of arity or type mismatch, which still apply on the bound
argument set after binding succeeds.

The codes occupy the next-free slots in the E00xx Type block (E0006 and E0007
taken). Registered in Sprint 5 Increment B through their ErrorCatalog descriptors
(D-308); immutable once shipped (ADR-0017). Full calling-convention detail is in
grob-language-fundamentals.md (the named-argument rules) and D-113.

-----
```

---

## 2. The summary-index row (add in lockstep, at the top of the file)

Match the index's existing column layout. The row content:

```
D-318 — Named-argument call-site diagnostics get dedicated codes (E0008–E0011) — Type system — Jun 2026
```

---

## 3. The footer changelog line (add in lockstep, this session's change)

Match the changelog's existing format. The line content:

```
D-318 added — the four named-argument call-site errors (D-113) assigned dedicated
codes E0008–E0011 rather than folding into E0003; Sprint 5 Increment B registers
them.
```

---

## Notes on the rest of the Sprint 5 planning calls (no further D-### needed)

The other three planning forks settled this session do **not** warrant their own
decisions-log entries, following the Sprint 4 precedent (its A–F breakdown lived
in the kickoff, not a D-###):

- **The A–F increment breakdown** and the **bundling of the init state machine
  with narrowing into Increment E** are planning structure — recorded in
  `prompts/sprint-5/sprint-5-kickoff.md`, not the decisions log.
- **The array higher-order methods (`filter`/`select`/`sort`/`each`) being in
  Sprint 5 scope** is a reading of the existing §6 acceptance (which names them),
  not a new decision — recorded in the kickoff's planning constraints.
- **The §6 `map` → `select` correction** is a mechanical drift fix against the
  already-settled D-280 (which removed `map`), made as a one-word spec edit in
  Increment C — it needs no new decision, only the edit.
- **The `functions.grob` close-gate script** is a planning-defined smoke test like
  `hello.grob`/`calculator.grob` — recorded in the kickoff, defined in Increment F.

If on reading the live log you decide any of these warrants its own entry after
all, it would take the next-free number after D-318.
