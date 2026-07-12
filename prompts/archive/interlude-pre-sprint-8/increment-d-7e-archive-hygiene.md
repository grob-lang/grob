# Pre-Sprint-8 Interlude — Increment D: 7E archive hygiene and QA brief note

**Branch:** `chore/7e-archive-hygiene`
**Authorises:** D-335, D-336 (both are corrections these documents must reflect)
**Model:** Haiku
**Depends on:** none

## Why

The archived Sprint 7E increment prompt carries two false citations that a cold-reader
will otherwise inherit and repeat. And the Sprint 7 QA brief needs a line about the four
`Sprint6IncrementBTests`, whose state depends on whether Increment C has landed. Both are
housekeeping — trivial to write, real to leave undone.

## Plan gate

Produce a numbered plan and stop for approval. The plan must report:

1. The path of the archived 7E prompt (expected under `prompts/archive/sprint-7/`) and
   the two lines carrying the false citations, quoted from the live file.
2. The path of the Sprint 7 QA brief (expected under `prompts/sprint-7/`).
3. Whether Increment C has landed at the time this runs — it determines the QA note's
   wording.

Do not edit until approved.

## Scope

**In:** the archived 7E prompt and the Sprint 7 QA brief, both under `prompts/`.

**Out:** every design document, every source and test project, `Grob.slnx`. This
increment touches prompt text only.

## The corrections

Both citation errors are in the **prompt**, not the corpus — the corpus was verified
correct during the D-335–337 capture. Do not "fix" the design docs; they are already
right.

1. **`finally` model citation.** The 7E prompt cites the `finally` model as D-332. It is
   **D-334**. D-332 is the `ValueStack` LOH right-sizing / benchmark fix, unrelated.
2. **Smoke-script location.** The 7E prompt states the four prior smoke scripts live in
   `grob-sample-scripts.md`. They live in `tests/Grob.Integration.Tests` and never lived
   in that document (D-337). `grob-sample-scripts.md` holds the thirteen release-gate
   validation scripts, a different family.

The archived prompt is a historical record. Do not rewrite its body as though the errors
were never made — append a dated **Correction** note at the top or foot recording both
fixes and pointing at D-334 and D-337. History stays legible; the correction is loud.

## The QA note

Add to the Sprint 7 QA brief a short note on the four `Sprint6IncrementBTests`:

- **If Increment C has landed:** the four assert the exact rendered struct form
  (`Config { host: "example.com", port: 8080 }`) and pass; no caveat needed beyond a
  pointer to D-336.
- **If Increment C has not landed:** the four currently assert the interim `[Config]`
  form and pass, but that form is **known-wrong and superseded by D-336**. The cold-reader
  must not read their green state as ratifying `[Config]` — the defect was first
  mis-diagnosed by inferring intent from `git log`, and these tests are the artefact most
  likely to mislead a reader doing the same.

## Acceptance

- [ ] The archived 7E prompt carries a dated correction note citing D-334 (finally) and
      D-337 (smoke-script location); its original body is preserved.
- [ ] The QA brief carries the `Sprint6IncrementBTests` note, worded for the actual
      landed state of Increment C.
- [ ] No design document, source project, or test project changed.
- [ ] Error-code count unchanged at 116.

## Commit

One commit. e.g. `docs(prompts): correct 7E archive citations and note Sprint6B test state
for QA (D-335, D-336, D-337)`.

## Guardrails

British English, no Oxford comma, never "simply". One concern — prompt text only. Do not
touch the corpus; it is correct.
