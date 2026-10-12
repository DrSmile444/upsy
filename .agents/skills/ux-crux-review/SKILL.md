---
name: ux-crux-review
description: Use when the user asks for a broad, comprehensive, general, or end-to-end UX review of a feature, screen, flow, app, or implementation — across multiple dimensions (usability, accessibility, psychology, product, trust) rather than one specific angle. If the request already narrows to one angle (e.g. "check accessibility", "is this manipulative"), prefer the matching domain skill instead. Accepts an optional "smart" or "full" argument — "smart" (the default) selects applicable lenses and rules using judgment; "full" performs a mandatory, exhaustive rule-by-rule sweep of every applicable lens instead. Also accepts "report", alone or with a mode (for example "full report"), to also write a findings file and an HTML triage report; a plain-language request for a visual or HTML report works the same way.
license: MIT
metadata:
  author: ux-crux
  version: "0.2.0"
---

Review the user-facing product, feature, flow, screen, or implementation described or shown, across every UX dimension relevant to the evidence provided. Never invent behavior that cannot be observed — see `shared/evidence-model.md`.

## Procedure

**Mode.** By default (no argument, or an explicit "smart" argument) this skill selects which lenses and rules to apply using judgment, as described below. When invoked with an argument recognizable as "full" (case-insensitive), it instead performs a mandatory, exhaustive sweep across every applicable lens: for every rule row in every `domains/<lens>/...` file loaded for the request's scope, explicitly record VIOLATED / NOT VIOLATED / NOT ASSESSABLE / NOT APPLICABLE before writing the narrative report, rather than relying on judgment to select a subset. Show this checklist before the report. If the argument is present but is neither "full" nor "smart", ask the user which mode they intended rather than guessing.

**When to choose.** Default to `smart` for everyday review work — it already reaches full recall on directly-evidenced violations, at negligible extra cost. Reach for `full` when the report itself needs to be defensible as a complete audit trail rather than a sampling: a pre-launch/release gate, a compliance- or safety-sensitive surface (payments, health data, legal/accessibility exposure), a re-check after a prior review turned out to have missed something, or handing findings to a stakeholder — legal, compliance, a client — who needs proof every rule was checked. `full` costs meaningfully more on a large rule catalog and only marginally more on a small one; weigh that against how much an audit trail is worth for this particular review.

**Report.** When the arguments include "report" (alone or with a mode, for example `full report`), or the user asks for a visual or HTML report, finish the review and its text report as usual, then write the findings file per `shared/findings-contract.md` and render it per `shared/report-render.md`. Without it, write no findings file and no page. "report" is not a mode: alone it means `smart` with a report, and `full report` means `full` with a report.

**Live evidence.** When the evidence is a live web page or a running build, follow `shared/flow-capture.md` before evaluating rules: it says when to walk the flow, how to capture each step, and when to stop and ask before any data change.

1. **Determine scope.** Read `references/review-model.md`'s lens-selection procedure. Decide which of usability, psychology, accessibility, product, and trust apply, and what evidence is actually available (screenshot, description, running build, code).
2. **State the scope before findings.** Name which lenses you applied and why, and which you could not apply due to the evidence type.
3. **Evaluate each applicable lens.** This package bundles every domain's rule content under `domains/<domain>/` (generated from that domain's own skill, so it is always present here regardless of whether the domain skills are also installed). Load only the `domains/<lens>/...` files you need for the lenses in scope — do not pull in all five domains' full reference material for a narrowly-scoped review. For example: `domains/usability/core.md` and `domains/usability/mobile.md`, `domains/psychology/*.md`, `domains/accessibility/core.md` and `domains/accessibility/mobile.md`, `domains/product/core.md`, `domains/trust/core.md` and `domains/trust/mobile.md`.
4. **Tag every finding** with an evidence status (`shared/evidence-model.md`), a severity (`shared/severity-model.md`), and a confidence level.
5. **Synthesize.** Merge overlapping findings across lenses per `references/review-model.md`'s cross-lens synthesis guidance — do not produce five separate mini-reports.
6. **Report** using the exact structure in `shared/report-contract.md`: blockers and majors first, category health per lens, missing states/context, top-3 highest-impact changes, then moderate/minor findings, with an optional capped score last.

## References

- `references/review-model.md` — lens selection, cross-lens synthesis, and the validation-methodology rules (V01-V06) that govern how confident this skill is allowed to be.
- `domains/usability/`, `domains/psychology/`, `domains/accessibility/`, `domains/product/`, `domains/trust/` — a generated, self-contained copy of each domain skill's own reference material (kept in sync with that domain's `src/skills/<domain>/references/` by the build step). Read these directly; do not assume the sibling domain skills are installed alongside this one.
- `shared/evidence-model.md`, `severity-model.md`, `report-contract.md`, `findings-contract.md`, `report-render.md`, `flow-capture.md` — shared across every ux-crux skill; read these once, they are not repeated here.

## Boundaries

This skill does not gatekeep the five domain skills — `usability`, `psychology`, `accessibility`, `product`, and `trust` are independently invocable and complete on their own for a narrowly-scoped request. Use this skill when the request is broad or the right lens is unclear, not by default for every UX question. The reverse also holds: this skill does not require any of those five domain skills to be installed either — its own package under `domains/` already carries everything it needs.
