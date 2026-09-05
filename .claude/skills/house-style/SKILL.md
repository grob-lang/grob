---
name: house-style
description: >
  The Grob house style for every word the project ships in English — decisions-log
  entries, spec and wiki prose, README and docs pages, diagnostic message text, code
  comments, commit messages, PR replies, review reports and hand-off summaries. Use
  whenever you write or edit prose in any of those surfaces, and run the checklist
  before handing anything back. This is the prose counterpart to writing-grob-source,
  which governs .grob files. The rules here are not preferences; they are the standard
  the corpus is already written to, and drift shows up as review churn.
---

# Grob house style

Grob's written surface is part of the product. A developer meets the language
through its diagnostics, its spec and its docs before they ever read the source.
Prose that reads as sloppy makes the language read as unfinished.

This skill is the single home for those rules. Other skills and commands point
here rather than restating a partial subset, which is how the rules drifted in
the first place — stated in nine places, checked in one.

## The rules

**British English.** `-ise` not `-ize`, `-isation` not `-ization`. `colour`,
`behaviour`, `analyse`, `recognise`, `initialise`, `serialise`, `centre`.
`licence` as a noun, `license` as a verb. `whilst` is fine but `while` is
usually better.

Exception, and it is absolute: **identifiers are never anglicised.** .NET
`Initialize`, `Serialize`, `Color`, `IsAuthorized` and every other framework
name keeps its American spelling, as does any Grob API name already settled in
the type registry. The rule governs prose, not code. A sentence about
`GrobValue.Initialize` is British English describing an American identifier.

**No Oxford comma.** `red, white and blue` — never `red, white, and blue`.

This is the rule that leaks most often, so know exactly what it is. The Oxford
comma is the one before the final conjunction in a list of three or more items.
It is *not* every comma that precedes `and`. These are correct and must not be
"fixed":

- `The parser recovers, and the checker continues.` — two independent clauses,
  the comma is doing clause work.
- `When the budget covers the code, and only then, allocate it.` — a
  parenthetical.

The test: remove the conjunction. If what remains is a list of parallel items,
the comma before it was an Oxford comma and must go. If what remains is two
sentences, leave it.

**Never "simply".** Also avoid `just`, `obviously`, `of course`, `merely` and
`straightforward` used to characterise difficulty. They tell a reader who is
stuck that they should not be stuck. If the thing genuinely is small, the
sentence will read that way without being told.

**No emoji.** Not in compiler output, not in CLI output, not in diagnostics, not
in docs, not in commit messages. Nowhere.

**No filler.** `in order to` is `to`. `at this point in time` is `now`. `it is
important to note that` is nothing at all — delete it and start with the note.

**Em dashes.** Spaced, as the corpus uses them — like this. Not `--`, not
unspaced.

**Prose that leads with the point.** State the decision, the finding or the
answer first, then the rationale. The corpus is read by people looking something
up, not people reading start to finish.

## Diagnostics have an extra bar

Error message text carries all of the above, plus:

- Says what went wrong, where, and how to fix it where the fix is obvious.
- Addresses the reader's code, not the compiler's internals. `cannot assign
  string to int` beats `type unification failed`.
- No blame, no exclamation marks, no personality. Sparky lives on the website,
  not in stderr.

## Commit messages have an extra bar

All of the above, plus imperative mood, lowercase first letter and no trailing
full stop in the subject. `/commit-message` carries the full conventions; this
skill governs the words inside them.

## Checklist

Run this before handing back any prose. It is short deliberately — a checklist
that is skipped is worse than none.

- [ ] British spellings throughout the prose; identifiers left alone
- [ ] Every `, and` / `, or` in a list of three or more had the comma removed
- [ ] No `simply`, `just`, `obviously`, `merely`
- [ ] No emoji
- [ ] No `in order to`, no `it is important to note`
- [ ] Em dashes spaced, not `--`
- [ ] Leads with the point

## Where this applies

Everything the project ships in English. Named explicitly because these are the
surfaces where the rules were previously absent:

- `docs/design/grob-decisions-log.md` entries — the densest prose in the project
- Spec documents and `docs/wiki/` pages
- Increment prompts and their hand-off summaries
- Review reports, consistency-review findings, proposals
- PR replies and review comments
- Diagnostic message strings and `///` doc comments
