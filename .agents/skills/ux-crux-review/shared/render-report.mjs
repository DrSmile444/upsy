#!/usr/bin/env node
// Renders a ux-crux findings file into a single self-contained HTML report.
//   node render-report.mjs <findings.json> [report.html]
// Embeds every image referenced by `file`, checks the findings contract
// (findings-contract.md), and fills report-template.html, which sits next to
// this script. No dependencies; Node 18+.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PLACEHOLDER = "/*FINDINGS_JSON*/";
const SIZE_WARN = 8 * 1024 * 1024;

const SEVERITIES = ["blocker", "major", "moderate", "minor"];
const CONFIDENCE = ["high", "medium", "low"];
const STATUSES = ["VERIFIED", "SUPPORTED", "LIKELY", "RISK", "NOT_ASSESSABLE"];
const LENSES = ["usability", "psychology", "accessibility", "product", "trust"];
const SKILLS = ["review", ...LENSES];
const VERDICTS = ["VIOLATED", "NOT_VIOLATED", "NOT_ASSESSABLE", "NOT_APPLICABLE"];
const TRIAGE = ["wont-fix", "not-an-issue", "defer", "verify-first"];
const RULE_ID = /^[a-z]+\/[a-z0-9-]+\.md#[A-Z0-9]+$/;
const EXTERNAL = /(?:src|href)\s*=\s*["']?\s*(?:https?:)?\/\/|url\(\s*["']?\s*(?:https?:)?\/\/|@import/i;

export function validateFindings(d) {
  const errors = [];
  const err = (m) => errors.push(m);
  if (!d || typeof d !== "object") return ["findings file is not a JSON object"];
  if (d.contract !== "ux-crux-findings/1") err(`contract must be "ux-crux-findings/1"`);
  for (const k of ["report_id", "title", "language", "created", "overall"]) if (!d[k]) err(`missing ${k}`);
  if (!SKILLS.includes(d.skill)) err(`skill must be one of ${SKILLS.join(", ")}`);
  if (!["smart", "full"].includes(d.mode)) err(`mode must be smart or full`);
  if (!d.subject || !d.subject.summary) err("missing subject.summary");
  if (!Array.isArray(d.lenses) || !d.lenses.length) err("missing lenses");
  const rules = d.rules || {};
  for (const id of Object.keys(rules)) {
    if (!RULE_ID.test(id)) err(`rules: "${id}" is not a qualified rule id (<domain>/<file>.md#<ID>)`);
    if (!rules[id].text || !rules[id].sources) err(`rules: ${id} needs text and sources`);
  }
  const cite = (where, ids) => {
    if (!Array.isArray(ids) || !ids.length) return err(`${where}: no source_ids`);
    for (const id of ids) {
      if (!RULE_ID.test(id)) err(`${where}: "${id}" is not a qualified rule id`);
      else if (!rules[id]) err(`${where}: ${id} is missing from rules`);
    }
  };
  const images = d.images || {};
  const steps = new Map();
  for (const fl of d.flows || []) for (const s of fl.steps || []) {
    steps.set(`${fl.id}:${s.n}`, s);
    if (!["captured", "observed", "not_reached"].includes(s.status)) err(`flow ${fl.id} step ${s.n}: bad status`);
    if (s.status === "not_reached" && !s.reason) err(`flow ${fl.id} step ${s.n}: not_reached needs a reason`);
    if (s.image && !images[s.image]) err(`flow ${fl.id} step ${s.n}: image ${s.image} not in images`);
  }
  const ids = new Set();
  const top3 = new Set((d.top3 || []).map((t) => t.finding_id));
  if (!Array.isArray(d.findings)) return [...errors, "findings must be an array"];
  for (const f of d.findings) {
    const w = `finding ${f.id || "?"}`;
    if (!f.id) err("finding without id");
    if (ids.has(f.id)) err(`${w}: duplicate id`);
    ids.add(f.id);
    for (const k of ["title", "observation", "why_it_matters", "recommendation"]) if (!f[k]) err(`${w}: missing ${k}`);
    if (!LENSES.includes(f.category)) err(`${w}: category must be a lens`);
    if (!SEVERITIES.includes(f.severity)) err(`${w}: bad severity`);
    if (!CONFIDENCE.includes(f.confidence)) err(`${w}: bad confidence`);
    if (!STATUSES.includes(f.evidence_status)) err(`${w}: bad evidence_status`);
    cite(w, f.source_ids);
    if (!Array.isArray(f.false_positive_conditions) || !f.false_positive_conditions.length) err(`${w}: missing false_positive_conditions`);
    if (["LIKELY", "RISK", "NOT_ASSESSABLE"].includes(f.evidence_status) && !(f.validation_method || []).length)
      err(`${w}: ${f.evidence_status} needs validation_method`);
    const fixes = (f.options || []).filter((o) => !o.kind || o.kind === "fix");
    if (fixes.length < 1 || fixes.length > 3) err(`${w}: needs 1-3 fix options, has ${fixes.length}`);
    if (fixes.filter((o) => o.recommended).length !== 1) err(`${w}: exactly one fix option must be recommended`);
    const keys = new Set(fixes.map((o) => o.key));
    if (keys.size !== fixes.length || [...keys].some((k) => !/^[a-c]$/.test(k))) err(`${w}: fix option keys must be distinct a-c`);
    if (f.location) {
      const l = f.location;
      const pts = l.from != null ? [l.from, l.to] : [l.step];
      for (const n of pts) if (!steps.has(`${l.flow}:${n}`)) err(`${w}: location ${l.flow} step ${n} not in flows`);
    }
    const needsPic = f.severity === "blocker" || f.severity === "major" || top3.has(f.id);
    if (needsPic && !f.picture) err(`${w}: blocker, major and Top 3 findings need a picture`);
    if (f.picture) {
      const p = f.picture;
      if (!["capture", "reconstruction", "schematic"].includes(p.tier)) err(`${w}: bad picture tier`);
      if (!Array.isArray(p.panels) || !p.panels.length) err(`${w}: picture has no panels`);
      for (const x of p.panels || []) {
        if (!["before", "after"].includes(x.role)) err(`${w}: panel role must be before or after`);
        if (f.evidence_status === "NOT_ASSESSABLE" && x.role === "before") err(`${w}: NOT_ASSESSABLE finding must not show a before panel`);
        if (!x.image && !x.html) err(`${w}: panel needs image or html`);
        if (x.image && !images[x.image]) err(`${w}: image ${x.image} not in images`);
        if (/<script/i.test(x.html || "") || EXTERNAL.test(x.html || "") || EXTERNAL.test(x.css || ""))
          err(`${w}: panel html/css must have no scripts and no external URLs`);
      }
    }
    if (f.decision && !keys.has(f.decision.option) && !TRIAGE.includes(f.decision.option)) err(`${w}: unknown decision option`);
  }
  if (!Array.isArray(d.category_health) || !d.category_health.length) err("missing category_health");
  for (const h of d.category_health || []) cite(`category_health ${h.lens}`, h.source_ids);
  if (!Array.isArray(d.missing_context)) err("missing missing_context");
  if (!Array.isArray(d.top3)) err("missing top3");
  for (const t of d.top3 || []) {
    if (!ids.has(t.finding_id)) err(`top3: unknown finding ${t.finding_id}`);
    cite(`top3 ${t.finding_id}`, t.source_ids);
  }
  if (d.mode === "full") {
    if (!Array.isArray(d.checklist) || !d.checklist.length) err("full mode needs a checklist");
    for (const c of d.checklist || []) {
      if (!VERDICTS.includes(c.verdict)) err(`checklist ${c.rule}: bad verdict`);
      if (c.verdict === "VIOLATED" && !ids.has(c.finding_id)) err(`checklist ${c.rule}: VIOLATED row needs a finding_id`);
    }
  }
  for (const [id, im] of Object.entries(images)) if (!im.file && !im.src) err(`images.${id}: needs file or src`);
  return errors;
}

const MIME = { ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".png": "image/png", ".webp": "image/webp", ".gif": "image/gif", ".svg": "image/svg+xml" };

export function embedImages(d, baseDir) {
  for (const im of Object.values(d.images || {})) {
    if (im.src || !im.file) continue;
    const file = path.resolve(baseDir, im.file);
    const mime = MIME[path.extname(file).toLowerCase()];
    if (!mime) throw new Error(`unsupported image type: ${im.file}`);
    im.src = `data:${mime};base64,${fs.readFileSync(file).toString("base64")}`;
    delete im.file;
  }
  return d;
}

export function renderHtml(d, template = fs.readFileSync(path.join(HERE, "report-template.html"), "utf8")) {
  if (!template.includes(PLACEHOLDER)) throw new Error("template has no findings placeholder");
  const json = JSON.stringify(d).replace(/</g, "\\u003c").replace(/[\u2028\u2029]/g, (c) => "\\u" + c.charCodeAt(0).toString(16));
  return template.replace(PLACEHOLDER, () => json);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [input, outArg] = process.argv.slice(2);
  if (!input) {
    console.error("usage: node render-report.mjs <findings.json> [report.html]");
    process.exit(2);
  }
  const d = JSON.parse(fs.readFileSync(input, "utf8"));
  const errors = validateFindings(d);
  if (errors.length) {
    console.error(`findings file does not follow the contract:\n  - ${errors.join("\n  - ")}`);
    process.exit(1);
  }
  embedImages(d, path.dirname(path.resolve(input)));
  const out = outArg || path.join(path.dirname(input), "report.html");
  const html = renderHtml(d);
  fs.writeFileSync(out, html);
  const size = Buffer.byteLength(html);
  console.log(`report: ${out} (${(size / 1024).toFixed(0)} KB, ${d.findings.length} findings)`);
  if (size > SIZE_WARN) console.warn("report: larger than 8 MB; drop pictures from moderate/minor findings or recapture at a smaller size.");
}
