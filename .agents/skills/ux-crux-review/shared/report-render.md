# Report render

How a ux-crux skill turns a review into an HTML report. The report is a triage page: the reader sees each finding with its rule, the rule's source, a picture, and options, picks one option per finding, and pastes the decisions back to the agent. It is one self-contained HTML file that opens offline.

Files in `shared/`:

- `findings-contract.md` — the findings file format. Write the file to this contract.
- `report-template.html` — the page. Never edit it per report; the findings data fills it.
- `render-report.mjs` — checks the findings file against the contract, embeds images, and writes the page.

## 1. Choose the folder

Write to `ux-crux-reports/<YYYY-MM-DD>-<slug>/` in the working directory, unless the user names another place: `findings.json`, an `img/` folder for captures, and `report.html`. Re-renders of one report use the same folder and the same `report_id`. Tell the user the folder path and suggest adding `ux-crux-reports/` to `.gitignore`; do not edit `.gitignore` yourself.

## 2. Choose the language

Write the report in the language the user writes in during the conversation. If the language is unclear, use English. An explicit language request from the user overrides this. Set `language` to its BCP 47 tag. For a language other than English, add a `strings` object that translates every UI string below. Keep rule IDs, severity, evidence status, confidence, and the quoted rule text and Sources text exactly as written in the catalog.

UI string keys: `lede`, `flow_map`, `flow_sub`, `blockers`, `major`, `health`, `missing`, `top3`, `minor`, `checklist`, `checklist_sub`, `observation`, `why`, `rules`, `location`, `source`, `decision`, `recommended`, `fix`, `t_wont_fix`, `t_not_an_issue`, `t_defer`, `t_verify_first` (short button labels), `wont_fix`, `not_an_issue`, `defer`, `verify_first` (the matching explanations), `applies_if`, `how`, `comment`, `decided`, `copy`, `clear`, `copied`, `progress` (keeps `{n}` and `{m}`), `captured`, `reconstruction`, `proposal`, `schematic`, `before`, `after`, `not_reached`, `observed`, `step`, `lenses`, `not_applied`, `mode`, `evidence`, `none`, `no_data`, `all`, `score`, `resolves`, `na_before`, `clear_confirm`, `made_with`, `confidence_fmt` (keeps `{c}`), `rule`, `verdict`, `sections`, `skip`, `reason_needed`, `copied_n` (keeps `{n}`), `no_reason` (keeps `{k}`), `show_text`, `hide_text`.

## 3. Write the findings file

Write every finding, Category health line, Missing context entry, and Top 3 item from the text report, with the same rule IDs. For each cited rule, copy its Rule cell and Sources cell verbatim from the reference file into `rules`.

**Options.** For each finding, write one to three fix options:

- Each option is a full decision: what changes, where, and what it costs. "Expand the hit region to 48x48dp; one layout change, no visual change" is good; "Fix the button" is not.
- Every option satisfies every rule the finding cites. Never offer an option that still violates the rule.
- Add an alternative only when it has a different trade-off (cost, scope, design impact). One valid fix is a complete answer.
- Mark exactly one option recommended. Its text matches `recommendation`.

The page adds the triage options by itself: "won't fix", "not an issue" (shows `false_positive_conditions`), "defer", and "verify first" for `LIKELY` and `RISK` findings (shows `validation_method`).

## 4. Pictures

Give a picture to every `blocker` and `major` finding and every Top 3 finding. Other findings get a text card. Choose the most faithful tier the evidence supports:

1. **Capture** — a screenshot exists (from the user, or from flow capture in `flow-capture.md`). Crop to the part in question at capture time and mark it with `box` (what is there), `dash` (what is missing), `gap` (a measured distance), `line` (a fold or cut). Coordinates are percent of the image. Take every number in a label from a measurement.
2. **Reconstruction** — the DOM, styles, or source code is available. Rebuild only the affected fragment as self-contained `html` and `css`: the `before` panel as found, the `after` panel with the recommended fix. Keep it small; no scripts, no external URLs, images only as `data:` URIs.
3. **Schematic** — nothing better is available. Draw the idea with simple boxes and labels in `html`/`css`: layout, hierarchy, sizes, order of steps.

Rules:

- Put one idea in each picture. Two panels side by side compare like with like: same crop, same scale.
- The page labels every non-captured `before` panel "Reconstruction" and every `after` panel "Proposal". Never present a reconstruction as a capture.
- A `NOT_ASSESSABLE` finding has no `before` panel: the state was not observed. Show only the `after` panel, or no picture, and say in Missing context what evidence would resolve it.
- Write a one-line `caption` that names each marker and says what to compare.
- Capture JPEG at quality about 70 and at most 1280px wide. Keep the page under 8 MB; drop pictures from non-required findings first.

## 5. Render

Run the renderer from this skill's `shared/` folder:

```bash
node <skill-dir>/shared/render-report.mjs ux-crux-reports/<folder>/findings.json
```

It prints every contract violation and writes nothing until the file is valid. Fix the findings file and run it again.

If Node is not available, fill the template yourself: copy `report-template.html` to `report.html` and replace the text `/*FINDINGS_JSON*/` with the findings JSON, with every `<` written as `<` and every image given as `src` (a `data:` URI) instead of `file`. With Python:

```bash
python3 - <<'PY'
import base64, json, mimetypes, pathlib, sys
d = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "findings.json")
data = json.loads(d.read_text())
for im in data.get("images", {}).values():
    if "file" in im:
        p = d.parent / im.pop("file")
        im["src"] = f"data:{mimetypes.guess_type(p.name)[0]};base64," + base64.b64encode(p.read_bytes()).decode()
tpl = pathlib.Path("<skill-dir>/shared/report-template.html").read_text()
out = json.dumps(data, ensure_ascii=False).replace("<", "\\u003c")
(d.parent / "report.html").write_text(tpl.replace("/*FINDINGS_JSON*/", out, 1))
PY
```

The Python path does not check the contract, so check the findings file against `findings-contract.md` yourself before you render.

## 6. Hand over

Give the user the path to `report.html` and one line: how many findings, how many blockers and majors. The report stays a local file. If the host can publish HTML pages, offer it in one line and publish only after the user says yes. Before that, warn the user when the report holds screenshots taken behind a login or with personal data.

## 7. The decision round

1. The reader presses "Copy decisions" and pastes lines like `UX-NAV-004 (A08R): a` or `UX-ACC-002 (T02): not-an-issue — the touch region is already 48dp`.
2. For each line, write `decision` (`option`, `comment`, today's date) into that finding in `findings.json`.
3. Render again to the same `report.html`. Decided findings show their decision.
4. Act on the decisions only as the user asks: a decision is a record, not an instruction to change code.
5. A "not an issue" decision with a comment that names a false-positive condition does not change the rule; report it back so the user can see the reason was recorded.
