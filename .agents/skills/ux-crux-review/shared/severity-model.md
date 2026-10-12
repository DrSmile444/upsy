# Severity model

Severity describes how much a finding would hurt the user or the product if left unaddressed — assuming the finding is real. It is independent of evidence status (`evidence-model.md`) and confidence: a `NOT ASSESSABLE` finding can still carry a severity, describing how bad it would be *if* confirmed.

## Levels

### Blocker
The user cannot complete a critical task; meaningful data can be lost; accessibility excludes core use for a class of users; irreversible, financial, or privacy harm is plausible; or the flow contains a navigation trap with no way out.

Example: a "Delete photo" action has no confirmation and no undo, and is reachable by a single accidental tap next to a frequently-used control.

### Major
Materially increases failure, confusion, accidental action, or discoverability cost in a primary flow, but does not by itself block task completion for every user.

Example: a Search tap reveals a text field that is not focused and does not open the keyboard, forcing a redundant second tap on every use of Search.

### Moderate
Noticeable friction, extra effort, ambiguity, or inconsistency that does not usually block task completion.

Example: a multi-step flow uses "Next" on one screen and "Continue" on the next for the same kind of progression action.

### Minor
A polish, readability, or consistency issue with limited behavioral impact.

Example: inconsistent icon style between two secondary actions that do not compete for attention with the primary task.

## Rule for skills

- Severity is assigned per finding, not per screen or per report. A single blocker does not make every other finding on the same screen a blocker.
- Do not inflate severity to make a finding feel more actionable — an accurate `moderate` is more useful than an inflated `major`.
- Do not deflate severity because the evidence status is weak (e.g. `LIKELY`) — evidence status and severity are independent axes; report both honestly (see `report-contract.md`).
- A momentary error a user notices and self-corrects within a moment or two, with no lasting confusion or lost work (Krug's "kayak problem" — a kayak that rolls and rights itself is part of normal use, not an emergency), is scored lower than a genuine roadblock that stalls the user or requires external help to escape, even when both stem from the same underlying ambiguity. This refines how the existing four levels above are applied to self-correcting slips; it does not add a fifth level or a parallel vocabulary.
- A finding from an `Expert practice` rule (see `evidence-model.md`, "Rule source strength") is scored no higher than `moderate`, whatever the apparent impact, because the rule rests on an author's stated practice and not on a study.
