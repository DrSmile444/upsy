# Report contract

Every ux-crux skill (the `review` entry point and each of the five domain skills) reports findings in this shape. With the `report` argument, the skill also writes the same report as a findings file (`findings-contract.md`) and renders it as an HTML report (`report-render.md`). Never collapse a review into a single opaque score — a blocker in accessibility or data loss outweighs many polished details, and an average would hide that.

## Required sections, in order

1. **Blockers** — every `blocker`-severity finding, listed first, regardless of which lens found it.
2. **Major issues** — every `major`-severity finding.
3. **Category health** — one line per lens that was evaluated (for the full `review` skill: Usability, Psychology, Accessibility, Product, Trust; for a single domain skill: just its own lens), summarizing what was checked and its overall state (e.g. "no blockers, two moderate findings").
4. **Missing states / missing context** — explicitly list what could not be assessed (see `evidence-model.md`'s `NOT ASSESSABLE`) and what evidence would resolve it, rather than silently omitting it.
5. **Top 3 highest-impact changes** — the recommendations that would most improve the reviewed experience, ranked, regardless of which section they came from.
6. **Moderate/minor findings** — every `moderate`- and `minor`-severity finding, listed in full after the top sections, not as the headline. The Top 3 list above is an additional ranked highlight of the highest-impact items — it does not replace listing every moderate/minor finding here; never truncate this section to a curated subset.
7. *(Optional)* a single 0-100 score, presented only as a secondary summary after the above. If included, it MUST be capped when severe findings exist:
   - any unresolved blocker caps the score at 59
   - two or more unresolved blockers cap the score at 39

## Per-finding fields

Every individual finding includes:

```yaml
id: <short stable id, e.g. UX-NAV-004>
title: <one line>
category: <lens: usability | psychology | accessibility | product | trust>
severity: blocker | major | moderate | minor
confidence: high | medium | low
evidence_status: VERIFIED | SUPPORTED | LIKELY | RISK | NOT_ASSESSABLE
observation: <what was actually seen in the evidence>
why_it_matters: <the user-facing consequence>
recommendation: <specific, actionable fix>
source_ids: [<rule ids from the relevant references file>]
false_positive_conditions:
  - <condition under which this finding would not apply>
validation_method:
  - <how to confirm this with users/data/a running build, if evidence_status is not VERIFIED>
```

## Worked example

```text
Overall: Needs revision
Blockers: 1 | Major: 2 | Moderate: 3

BLOCKERS
1. [Accessibility][VERIFIED][High confidence] Submit control is 32x32dp on Android.
   Impact: high accidental-tap and motor-accessibility risk on the primary
   conversion action.
   Fix: keep the visual icon if desired, but expand the tappable hit region
   to at least 48x48dp.
   Source: accessibility/mobile.md#T02

MAJOR ISSUES
1. [Trust][SUPPORTED][High confidence] Location permission is requested on
   first launch, before any location feature is invoked.
   Fix: request it in context, after the user selects "Find nearby", and
   explain the benefit first.
   Source: trust/mobile.md#O04, O05

2. [Usability][LIKELY][Medium confidence] Tapping Search reveals the query
   field, but the screenshots do not show whether it receives focus or opens
   the keyboard.
   Fix: if not already automatic, focus the field and show the keyboard when
   the search UI becomes ready.
   Source: usability/core.md#IE01, IE02

CATEGORY HEALTH
- Usability: 1 major (search focus, IE01/IE02), otherwise sound task flow.
- Accessibility: 1 blocker (touch target, T02); contrast (X03) and text
  scaling (X04) not assessable from the provided evidence.
- Trust: 1 major (permission timing, O04/O05); destructive actions use undo
  correctly (A04R).

MISSING STATES / MISSING CONTEXT
- Offline and permission-denied states were not shown in the provided
  evidence.
- Text-contrast and Dynamic Type behavior are NOT ASSESSABLE from a static
  screenshot; verify with a running build.

TOP 3 HIGHEST-IMPACT CHANGES
1. [T02] Enlarge the submit control's hit region to >=48x48dp.
2. [O04, O05] Move the location-permission request into context and add a
   benefit explanation.
3. [IE01, IE02] Confirm (with a running build) whether the search field
   autofocuses; fix if not.

MODERATE/MINOR FINDINGS
1. [Usability][SUPPORTED][Medium confidence] The primary CTA label reads
   "OK" instead of naming the action it performs.
   Fix: replace with a specific verb-led label (e.g. "Save changes").
   Source: usability/core.md#A02R

2. [Product][LIKELY][Medium confidence] The empty-cart state shows no
   recovery action or suggested next step.
   Fix: add a "Browse products" call to action alongside the empty-state
   message.
   Source: product/core.md#P07

3. [Accessibility][SUPPORTED][Low confidence] Help/support entry points use
   inconsistent labels across two screens ("Help" vs. "Support").
   Fix: standardize on one label and placement across the product.
   Source: accessibility/core.md#X11

Score: 52/100 (capped at 59 due to one unresolved blocker)
```

## Rule for skills

- Do not report a numeric score without the sections above; the sections are the actual deliverable.
- If a lens found nothing wrong, say so explicitly in category health rather than omitting the lens.
- Every finding must trace to at least one `source_ids` entry from a `references/` file — do not report a finding with no traceable rule behind it.
- Rule-ID citation is not limited to individual findings: every section of the report that states or summarizes a finding — each Category Health line and each Top 3 item, not only the Blockers/Major/Moderate/Minor finding blocks — must cite the rule ID(s) it is based on, as shown in the worked example above. Never present a Category Health line or a Top 3 item as untraceable prose.
- Never truncate the Moderate/minor findings section to a curated subset; the Top 3 list is an additional highlight, not a substitute for listing everything found.
