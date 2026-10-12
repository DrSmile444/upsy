# Psychology: evaluating psychological findings (required reading)

Read this file before applying any mechanism from `cognitive.md`, `attention.md`, `motivation.md`, `emotion.md`, `behavioral-economics.md`, `social.md`, `habits.md`, or `gamification.md`. It is the meta-layer that keeps this skill from turning a descriptive psychological fact into a manipulative recommendation.

See `../../shared/evidence-model.md` and `severity-model.md` for how to report findings from these rules.

## The meta-rules

| ID | Rule | Evidence | Default severity | Sources |
|---|---|---|---|---|
| PX01 | Separate descriptive psychology from prescriptive UX: a bias/effect explains behavior but does not automatically justify exploiting it. | Research + ethical principle | Major | NN/g Psychology for UX: Study Guide; NN/g Deceptive Patterns in UX |
| PX02 | For each persuasive mechanism, run a dual-goal test: does it materially help the user's own stated/likely goal as well as the business goal? | Ethical guardrail | Major | NN/g Deceptive Patterns in UX; NN/g Hierarchy of Trust |
| PX03 | Rate psychological findings by evidence strength and context. Do not make Zeigarnik/Von Restorff/curiosity/Peak-End effects hard failures without task-specific evidence. | Research guardrail | Moderate | NN/g Psychology for UX: Study Guide; NN/g The Peak-End Rule |
| PX04 | When an effect predicts behavior but ethical/user-benefit implications are ambiguous, output "needs context / needs user test" rather than a recommendation to manipulate. | Research + ethical principle | Major | NN/g Psychology for UX: Study Guide; NN/g How to Conduct a Heuristic Evaluation |
| PX05 | Distinguish deliberate friction that protects a shared resource, another user, or a legitimate collective/enterprise policy from the acting user's own action (Slanty Design) from a dark pattern that imposes friction to extract a business advantage from the acting user at their own expense. Friction whose primary beneficiary is a shared resource or third party is not automatically a manipulative finding under this requirement — but it still passes through the four-gate test below for its own proportionality and clarity, it is not exempted from all scrutiny. Distinct from `trust`'s E08, which covers friction as a trust *signal* on the acting user's own high-stakes action, not friction that protects someone else. | Ethical guardrail | Major | Hartson & Pyla, *The UX Book* (Slanty Design; library reading-desk case) |

## The four-gate ethical test

Before this skill lets any psychological or persuasive mechanism become a positive recommendation, it must pass all four gates. Failing any gate turns the finding into an ethical-guardrail concern (per `../../shared/severity-model.md`, typically major or blocker), not a suggestion to use the mechanism.

| Gate | Question | Fail result |
|---|---|---|
| **Evidence** | Is the effect well-supported, or merely a popular UX-law retelling? | Downgrade to heuristic/contextual; report with low confidence. |
| **Applicability** | Does it match this user, task, frequency, risk, and product category? | Report as `NOT ASSESSABLE` / `needs_context`. |
| **User benefit** | Does the mechanism help the user achieve a goal they plausibly value? | Flag as a business-only optimization, not a recommendation. |
| **Agency & truthfulness** | Is the choice informed, truthful, non-coercive, and reasonably reversible? | Major/blocker ethical finding. |

A mechanism that passes all four gates can be described as a legitimate, user-aligned use of that psychological effect. A mechanism that fails even one gate is reported as a concern, using the language of the failed gate, never endorsed as "good use of X effect."

## Evidence-strength corrections for popular psychology claims

Use this table to resist treating a famous-sounding effect as an automatic best practice.

| Claim/effect | How this project treats it |
|---|---|
| Self-Determination Theory (autonomy, competence, relatedness) | Strong psychological foundation; translate cautiously into product context — see `motivation.md`. |
| Processing fluency | Supported psychological mechanism; useful for clarity/aesthetics, not proof of task usability — see `emotion.md`. |
| Default effect | Strong behavioral influence; requires a safe/user-benefiting default and transparency — see `behavioral-economics.md` PB01/PB02. |
| Loss aversion / anchoring | Descriptive decision biases; never encode as "best practice" without ethical review — see `behavioral-economics.md` PB03/PB04. Loss aversion is real but its size varies with domain knowledge, experience, and age (Mrkva, Johnson, Gächter & Herrmann, "Moderating Loss Aversion: Loss Aversion Has Moderators, But Reports of its Death are Greatly Exaggerated", *Journal of Consumer Psychology* 30(3), 2020, 407-428), so do not treat loss-framed or "threat" copy as stronger than gain framing by default. |
| Peak-End | Useful for journey diagnosis and closure; contextual, not permission to ignore the rest of the flow — see `emotion.md` PE02-PE04. |
| Information-gap curiosity | Supported theory, but use contextually and never hide decision-critical information — see `behavioral-economics.md` PB10. |
| Social proof | Can reduce uncertainty; must be truthful, representative, and tested for distraction — see `behavioral-economics.md` PB06. |
| Commitment/consistency | Can support user-owned goals; can also create pressure and sunk-cost traps — see `motivation.md` PM06, `behavioral-economics.md` PB11. |
| Zeigarnik/open-loop claims | Keep as a low-confidence/contextual heuristic unless stronger task-specific evidence is available. |
| Von Restorff/isolation | Useful attention heuristic, not a rule that every screen needs a visually unique CTA — see `attention.md` PA04. |
| Variable rewards | Not a general UX best practice; require an explicit wellbeing/ethics review — see `behavioral-economics.md` PB12, `habits.md` PH03. |
| Deliberate friction / Slanty Design | Legitimate when it protects a shared resource or a third party from the acting user's own action, not the acting user's own choice on their own behalf; distinguish from `trust`'s E08 (friction as a trust signal to the acting user themselves) and from dark-pattern friction that serves the business alone at the user's expense — see PX05. |
| Choice overload / the "jam study" (Iyengar & Lepper) | Contested replication: Scheibehenne, Greifeneder & Todd's meta-analysis found the choice-overload effect is inconsistent and often small or absent depending on context. Treat "limit initial choices to roughly 3-4 options" as a low-confidence, contextual heuristic, not a hard rule — do not fail a design solely for offering more options without task-specific evidence that overload is actually occurring. |

## How the psychology skill should apply these

This file is the gate every other psychology reference file must pass through, not an independent set of findings. When drafting a finding from any other file in this domain, explicitly state which of the four gates it passed or failed before deciding whether the output is a recommendation or a concern — do not skip straight to "this is good/bad UX" without naming the gate. When a reviewed friction point could be read either as protecting a third party/shared resource (PX05, Slanty Design) or as a dark pattern serving the business at the acting user's expense, determine which it is from the evidence before classifying it — do not default to the legitimizing reading when the evidence does not make the beneficiary clear; report `NOT ASSESSABLE`/`RISK` instead. A finding classified as Slanty Design under PX05 still passes through the four-gate test above for its own proportionality and clarity — it is evaluated, not exempted.
