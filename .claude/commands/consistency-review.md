---
description: Run an adversarial cross-document and code consistency review against the decisions log across the five review dimensions.
argument-hint: [scope — whole corpus, or a named doc/area]
---

# Run a consistency review

Conduct an adversarial consistency review of: **$ARGUMENTS** (default to the whole
corpus if no scope is given).

This command pairs with the `grob-design-reviewer` sub-agent — for a large or
whole-corpus pass, delegate to that agent so the review runs read-only and reports
without resolving. The dimensions and standard below are the same either way.

`docs/design/grob-decisions-log.md` is the authority. Every other document is
supplementary; where one conflicts with the log, the log wins and the other doc is
wrong. Verify file state directly — a footer changelog or session summary claiming an
edit landed is not evidence it did. Read the actual content.

Review across the five dimensions, in order, and report findings under each heading:

1. **Internal consistency** — counts, names, enum membership, signatures, decision
   references that disagree between docs or between a doc and the code.
2. **Staleness** — residue of superseded decisions. When you find one suspect pattern
   (`format.` after the `formatAs` rename, `.map(` after `select` superseded it), grep
   the whole corpus and report the full scope, not the first hit.
3. **Gaps** — anything an acceptance criterion or a `D-###` requires that is not
   actually specified. Treat the known confirmed gaps as baseline; look for new ones.
4. **Design quality** — does each decision, read fresh, still serve the foundational
   principle and the one-way principle? Name the trade-off where it does not.
5. **Document quality** — broken cross-references, wrong section numbers, ADR numbering
   drift, duplicate documents.

For each finding give: what, where (file + line/section), which authority it
contradicts, and the resolution options — not a chosen resolution. Rank blockers
(acceptance or implementation correctness) first, then quality, then cosmetics. Confirm
a contradiction is real before reporting it; two compatible descriptions at different
detail levels are not a conflict. If a dimension is clean, say so — do not manufacture
findings.

Do not edit anything. Produce the review; resolution is the maintainer's call.

**Model:** use Opus for this — it turns on judgement, not mechanics.
