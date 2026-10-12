# Evidence model

Every finding any ux-crux skill reports carries an **evidence status**. The status describes how strongly the available evidence supports the claim being made — not how serious the issue would be if true. Severity (see `severity-model.md`) is a separate axis.

Never assert a status the evidence does not support. A screenshot cannot prove keyboard/focus behavior; a design description cannot prove real-device touch target size; a code diff without a running build cannot prove actual screen-reader reading order. When in doubt, choose the weaker status.

## Statuses

### VERIFIED
The evidence directly and unambiguously demonstrates the finding. Typically requires a running build, live interaction trace, or source code that leaves no reasonable alternative reading.

Example: a code diff shows a `Button` with no `accessibilityLabel`/`contentDescription` prop and no visible text child — VERIFIED missing accessible name.

### SUPPORTED
The evidence strongly implies the finding, but requires one reasonable interpretive step (not direct observation).

Example: a screenshot shows a destructive "Delete account" button next to a generic "Are you sure?" confirmation with Yes/No — SUPPORTED insufficient confirmation copy (the screenshot shows the copy; it does not prove the action is actually irreversible, but the label strongly implies it).

### LIKELY
A relevant UX principle strongly predicts a problem, but the provided evidence cannot directly confirm it — usually because the artifact type (static image, description, spec) cannot show the behavior in question.

Example: a screenshot shows a Search icon and, in a second screenshot, a revealed text field — LIKELY missing autofocus, because a screenshot cannot show whether the field received focus or the keyboard opened.

### RISK
Something worth testing or watching, not a claim that a defect exists. Used for heuristic concerns, contextual rules, and psychological mechanisms whose real-world effect depends on audience/product context this review cannot know.

Example: a flow uses a default pre-selected option that benefits the business — RISK of an unfair default, pending confirmation of whether the default also serves the user's own goal.

### NOT ASSESSABLE
The available evidence cannot answer the question at all. State this explicitly instead of guessing or silently skipping the topic.

Example: reviewing a static design mockup — NOT ASSESSABLE for real touch-hit-region size, VoiceOver/TalkBack reading order, or actual network-error recovery behavior.

## Confidence

Every finding also carries a confidence level, independent of its evidence status:

- **High** — directly observable and backed by a platform contract, accessibility requirement, or multiple strong sources.
- **Medium** — likely correct but some product/audience context is missing.
- **Low** — heuristic or contextual concern; presented as a question to investigate, not a verdict.

## Rule source strength

Every rule in a skill's reference tables carries an `Evidence` value that describes the source behind the rule. It is separate from the evidence status of a finding above. `Strong` marks a standard, platform contract, or study; `Contextual` marks a rule whose application depends on product or audience; `Expert practice` is defined here.

`Expert practice` marks a rule whose source is a published book by a named author who states the practice without citing a study. A rule carries it only after a corroboration search found nothing that contradicts the claim; a claim contradicted by a study is not shipped. The `Sources` cell names the work and year and states "author's stated practice; no study cited". Videos, articles, e-books, and blog posts do not qualify and need independent corroboration.

A finding from an `Expert practice` rule is reported with evidence status `RISK`, confidence `Low`, and a severity no higher than `moderate`, and names the author and work so the reader can weigh the claim.

## Rule for skills

1. State the evidence status and confidence alongside every finding.
2. Never upgrade a finding's status because the underlying UX principle is strong — principle strength affects severity and confidence, not evidence status.
3. When the evidence type (screenshot vs. running build vs. code vs. description) cannot settle a question, say so as NOT ASSESSABLE or LIKELY, and name what evidence would resolve it (see `report-contract.md`'s `validation_method`).
4. This model applies identically across all six skills (`review` and the five domain skills) — do not invent a domain-specific evidence scale.
