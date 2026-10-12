# Findings contract

The findings file (`findings.json`) is the machine-readable form of the report defined in `report-contract.md`. A review skill writes it when it runs with the `report` argument; `report-render.md` turns it into the HTML report. The file holds the same findings, sections, and rule IDs as the text report — never more, never fewer.

Contract id: `ux-crux-findings/1`.

## Top-level fields

| Field | Required | Content |
|---|---|---|
| `contract` | yes | `"ux-crux-findings/1"` |
| `report_id` | yes | Stable id for this report, `<YYYY-MM-DD>-<slug>`. The page stores the reader's choices under it, so re-renders of one report keep the same id. |
| `title` | yes | Name of what was reviewed, in the report language. |
| `language` | yes | BCP 47 tag of the report language (`en`, `uk`, ...). See `report-render.md` for how to choose it. |
| `strings` | no | UI-string overrides for a language other than English; keys are listed in `report-render.md`. |
| `created` | yes | ISO date. |
| `skill` | yes | The skill that ran: `review`, `usability`, `psychology`, `accessibility`, `product`, `trust`. |
| `mode` | yes | `smart` or `full`. |
| `subject` | yes | `{ "summary": <one or two sentences>, "evidence": [<what was examined: URL, build, screenshots, code>], "platform": "web" \| "ios" \| "android" \| "other" }` |
| `overall` | yes | One-line verdict, as in the text report (for example "Needs revision"). |
| `lenses` | yes | `[{ "lens": <lens>, "applied": true \| false, "reason": <why applied or not> }]` |
| `rules` | yes | Map from every rule id cited anywhere in the file to `{ "text": <the Rule cell>, "sources": <the Sources cell> }`, copied verbatim from the reference file. |
| `flows` | no | Flow map (see below). Omit for a single-screen review. |
| `images` | no | Map from image id to `{ "file": <path relative to findings.json> }`, or to `{ "src": "data:image/jpeg;base64,..." }` once embedded. |
| `findings` | yes | Every finding (see below). |
| `category_health` | yes | `[{ "lens": <lens>, "summary": <line>, "source_ids": [<rule ids>] }]`, one per evaluated lens. |
| `missing_context` | yes | `[{ "text": <what could not be assessed>, "resolves_with": <evidence that would resolve it> }]` |
| `top3` | yes | `[{ "finding_id": <id>, "text": <change>, "source_ids": [<rule ids>] }]`, ranked. |
| `checklist` | `full` only | `[{ "rule": <rule id>, "verdict": "VIOLATED" \| "NOT_VIOLATED" \| "NOT_ASSESSABLE" \| "NOT_APPLICABLE", "note": <short>, "finding_id": <id, VIOLATED rows only> }]` |
| `score` | no | `{ "value": <0-100>, "note": <cap explanation> }`, capped as in `report-contract.md`. |

A rule id is always qualified: `<domain>/<reference file>#<ID>`, for example `usability/core.md#A08R`.

## Flows

```json
"flows": [{
  "id": "signup",
  "title": "Registration",
  "steps": [
    { "n": 1, "title": "Create your account", "url": "https://example.com/signup", "status": "captured", "image": "step-1" },
    { "n": 4, "title": "Enter the email code", "status": "not_reached", "reason": "The code is sent to an inbox the agent cannot open." }
  ]
}]
```

`status` is `captured` (the agent observed and captured the step), `observed` (observed without a capture), or `not_reached` (with `reason`).

## Per-finding fields

All fields of the per-finding block in `report-contract.md` (`id`, `title`, `category`, `severity`, `confidence`, `evidence_status`, `observation`, `why_it_matters`, `recommendation`, `source_ids`, `false_positive_conditions`, `validation_method`), plus:

- `location` — `{ "flow": <flow id>, "step": <n> }` for a finding on one step, `{ "flow": <flow id>, "from": <n>, "to": <n> }` for a finding on a transition between steps. Omit for a single-screen review.
- `options` — one to three fix options, `[{ "key": "a", "kind": "fix", "text": <full decision with what changes and what it costs>, "recommended": true }]`. Exactly one is recommended, and its text matches `recommendation`. Every fix option satisfies every rule in `source_ids`; add an alternative only when it has a different trade-off (cost, scope, design impact), never to fill a slot. The page adds the triage options (`wont-fix`, `not-an-issue`, `defer`, and `verify-first` for `LIKELY`/`RISK`) itself.
- `picture` — required for every `blocker` and `major` finding and every Top 3 finding; omit for others. See below.
- `decision` — written only by the round loop: `{ "option": <key>, "comment": <text>, "date": <ISO date> }`.

`evidence_status` uses the underscore form `NOT_ASSESSABLE` in this file.

## Pictures

```json
"picture": {
  "tier": "capture",
  "caption": "The frame marks the \"Continue\" button; the dashed frame marks where the heading should repeat its label.",
  "panels": [
    { "role": "before", "image": "step-2",
      "markers": [ { "type": "box", "x": 8, "y": 71, "w": 84, "h": 9, "label": "Continue" },
                   { "type": "dash", "x": 8, "y": 6, "w": 60, "h": 7, "label": "no matching heading" } ] },
    { "role": "after", "html": "<div class=\"btn\">Verify email</div>", "css": ".btn{...}" }
  ]
}
```

- `tier`: `capture` (a screenshot exists), `reconstruction` (the agent rebuilt the fragment from DOM, styles, or code), or `schematic` (simple shapes).
- A panel shows either an `image` (id from `images`) with optional `markers`, or `html` with optional `css`. `html` and `css` are self-contained: no scripts, no external URLs, images only as `data:` URIs.
- `role`: `before` (as found) or `after` (with the recommended fix). The page labels every non-captured `before` panel "Reconstruction" and every `after` panel "Proposal".
- A `NOT_ASSESSABLE` finding has no `before` panel.
- `markers`: `box` (solid frame: what is there), `dash` (dashed frame: what is missing), `gap` (a measured distance; `x`, `y`, `h` for a vertical gap, label is the number), `line` (horizontal line at `y`: a fold or cut). Coordinates are percent of the image (0-100). Take every number in a label from a measurement, not from the picture.
- `caption` is one line that names each marker and says what to compare.

## Example

```json
{
  "contract": "ux-crux-findings/1",
  "report_id": "2026-10-01-checkout",
  "title": "Mobile checkout screen",
  "language": "en",
  "created": "2026-10-01",
  "skill": "review",
  "mode": "smart",
  "subject": { "summary": "Android checkout screen, from a written description.", "evidence": ["written description"], "platform": "android" },
  "overall": "Needs revision",
  "lenses": [{ "lens": "accessibility", "applied": true, "reason": "Touch target sizes are described." }],
  "rules": { "accessibility/mobile.md#T02": { "text": "Android interactive touch targets are at least 48x48 dp; ...", "sources": "<Sources cell, verbatim>" } },
  "findings": [{
    "id": "UX-ACC-001", "title": "Place order hit area is 30x30dp", "category": "accessibility",
    "severity": "blocker", "confidence": "high", "evidence_status": "VERIFIED",
    "observation": "The Place order control is 30x30dp with an icon and no label.",
    "why_it_matters": "Missed and accidental taps on the main conversion action.",
    "recommendation": "Keep the icon and expand the hit region to at least 48x48dp.",
    "source_ids": ["accessibility/mobile.md#T02"],
    "false_positive_conditions": ["The 30x30dp size is the artwork only and the touch region is already 48x48dp."],
    "validation_method": ["Measure the touch region with the layout inspector."],
    "options": [
      { "key": "a", "kind": "fix", "text": "Keep the icon and expand the hit region to at least 48x48dp. One layout change, no visual change.", "recommended": true },
      { "key": "b", "kind": "fix", "text": "Replace the icon with a full-width labeled button. Larger change; also fixes the missing label." }
    ],
    "picture": { "tier": "schematic", "caption": "The inner box is the 30dp control; the dashed box is the 48dp minimum.", "panels": [{ "role": "before", "html": "..." }, { "role": "after", "html": "..." }] }
  }],
  "category_health": [{ "lens": "accessibility", "summary": "1 blocker (touch target).", "source_ids": ["accessibility/mobile.md#T02"] }],
  "missing_context": [{ "text": "Contrast is not assessable from a description.", "resolves_with": "A screenshot or running build." }],
  "top3": [{ "finding_id": "UX-ACC-001", "text": "Expand the Place order hit region to 48x48dp.", "source_ids": ["accessibility/mobile.md#T02"] }]
}
```
