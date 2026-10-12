# Psychology: social and interpersonal trust signals

See `../../shared/evidence-model.md` and `severity-model.md` for how to report findings from these rules. Read `ethics.md` before treating any mechanism below as a recommendation rather than a description.

This file covers the psychological, interpersonal side of trust — predictability, consistency, and social signals that make a product *feel* trustworthy. It is distinct from the separate `ux-crux/trust` domain skill, which covers product-level trust mechanics such as permission timing and destructive-action safety; the two are complementary lenses on the same theme.

| ID | Rule | Evidence | Default severity | Sources |
|---|---|---|---|---|
| PT01 | Trust should be earned progressively: match the size of requests for data, money, permissions, or commitment to demonstrated value and established trust. | Strong | Major | NN/g Hierarchy of Trust: The 5 Experiential Levels of Commitment; NN/g The Reciprocity Principle |
| PT02 | Predictability, consistent behavior, visible system status, and recovery from mistakes are trust signals; surprising state changes or unexplained automation reduce trust. | Strong | Major | NN/g Efficiency vs. Expectations; NN/g Ten Usability Heuristics; NN/g Hierarchy of Trust |
| PT03 | Permission, privacy, price, subscription, and data-use copy must be concrete and realistic; do not rely on vague reassurance. | Strong + ethical | Major | Apple HIG Privacy; NN/g Deceptive Patterns in UX; NN/g Hierarchy of Trust |
| PT04 | Cancellation, rejection, privacy choices, and downgrade paths should not carry materially more friction than the corresponding opt-in/upgrade path without a legitimate reason. | Ethical guardrail | Blocker | NN/g Deceptive Patterns in UX |
| PT05 | Do not use confirmshaming or emotionally loaded wording to penalize refusal. | Ethical guardrail | Major | NN/g Deceptive Patterns in UX |
| PT06 | Respect user expectations for control even when automation is technically more efficient; make consequential automatic behavior visible and reversible when possible. | Strong | Major | NN/g Efficiency vs. Expectations; NN/g Ten Usability Heuristics |
| PT07 | For relationship-maintenance use cases (not pure logistics), route users toward higher-bandwidth channels (voice, video, in-person) rather than positioning low-bandwidth async text/reactions as a full substitute for conversation. | Strong | Moderate | Sherry Turkle (MIT), *Reclaiming Conversation* |

## How the psychology skill should apply these

PT01, PT02, and PT06 describe how trust is built or eroded gradually through many small, consistent interactions — check them across a whole flow, not one screen. PT03-PT05 are ethics-adjacent guardrails: asymmetric friction (PT04) and confirmshaming (PT05) are deceptive-pattern findings the moment they're observed, not context-dependent judgment calls — run them through `ethics.md`'s agency-and-truthfulness gate, but expect them to fail it whenever present. When a finding here overlaps with a permission-timing or destructive-action-confirmation observation, prefer reporting the mechanics under `ux-crux/trust` and only the psychological-signal angle here, to avoid duplicate findings across the two skills. PT07 applies only when the feature's own framing is relationship maintenance ("stay in touch," "keep up with friends") — a purely functional exchange of information (an address, a meeting time) is not a PT07 finding just because it happens over text; if the evidence does not indicate which kind of use case is intended, report PT07 as `NOT ASSESSABLE` rather than assuming either answer.
