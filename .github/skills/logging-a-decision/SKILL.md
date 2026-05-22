---
name: 'Logging a Decision'
description: 'How to add an entry to the Grob decisions log in the established ADR style, keep the index and changelog in lockstep, and maintain supersession links.'
---

# Logging a Decision

`docs/design/grob-decisions-log.md` is the authority for the whole project. When a
design decision is made — a feature shaped, an open question closed, a previous decision
changed — it is captured here as a numbered `D-###` entry. This skill is for adding one
correctly. Note: deciding *what* the decision is belongs to the maintainer; this skill
is about recording a decision that has been made, in the right form.

## When an entry is needed

- A new design decision with implementation consequences.
- An open question (`OQ-###`) being resolved — the entry closes it and the open-questions
  doc is updated to point at the resolving `D-###`.
- A change to an existing decision — the new entry supersedes the old, and both ends of
  the link are maintained.
- A trade-off that regresses performance for correctness or clarity — logged with the
  rationale so the benchmark baseline change is explained.

## The entry format

Match the existing entries exactly. The shape is:

```
### D-### — Short title (Month Year)

Area: <the subsystem or concern>
Supersedes: <D-### or none>
Superseded by: <D-### or none>

<Short prose body. State the decision first, plainly. Then the rationale —
why this and not the alternatives. Point at the spec doc that carries the
full detail rather than reproducing it here. Keep it tight; the log is a
record of decisions, not a spec.>

-----
```

- The number is the next free `D-###` in sequence. Check the tail of the file for the
  current highest.
- `Area` is a short noun phrase naming the subsystem (`VM — value representation`,
  `Control flow — select exhaustiveness`).
- The body leads with the decision, then the rationale. Where alternatives were
  rejected (NaN boxing, custom GC), name them and say why — the rejection reasoning is
  part of the record's value.
- Point at the authoritative spec doc for byte-level or full detail; do not duplicate it.

## Keep three things in lockstep

A decisions-log change touches three places in the same edit:

1. **The full entry** in the body, in sequence.
2. **The summary index** at the top of the file — add the row so the index and the
   entries never diverge.
3. **The footer changelog** — record this session's substantive change.

If you close an open question, also update `grob-open-questions.md` so the question
moves from open to resolved with a pointer to the `D-###`.

## Supersession links are bidirectional

When `D-new` supersedes `D-old`:

- `D-new` gets `Supersedes: D-old`.
- `D-old` gets `Superseded by: D-new`.

Both ends. A one-way link is a defect that misleads the next reader. If an ADR in
`docs/wiki/ADR/` is affected (as ADR-0016 was superseded by D-288 and D-291), note it
there too and in `Home.md`.

## Deliver complete files

Per the project's working pattern, deliver the full updated `grob-decisions-log.md`, not
a diff or a patch. Every session output is a complete, ready-to-use file.

## Checklist

- [ ] Decision was actually made (this records it; it does not invent it)
- [ ] Next free `D-###` used; format matches existing entries exactly
- [ ] Body leads with the decision, then rationale, rejected alternatives named
- [ ] Points at the spec doc for full detail rather than duplicating it
- [ ] Summary index row added in lockstep
- [ ] Footer changelog updated
- [ ] Supersession links set on both ends if applicable
- [ ] `grob-open-questions.md` updated if an `OQ-###` is closed
- [ ] Affected wiki ADR and `Home.md` updated if applicable
- [ ] Delivered as a complete file, not a patch
