# Psychology: gamification and game-design mechanics

See `../../shared/evidence-model.md` and `severity-model.md` for how to report findings from these rules. Read `ethics.md` before treating any mechanism below as a recommendation rather than a description.

This file covers the *legitimate-design* side of game mechanics: when points, badges, collectible sets, and feedback loops genuinely serve the user's own engagement and mastery, as distinct from `behavioral-economics.md`'s `PB12` (variable rewards) and `habits.md`'s `PH01`-`PH04` (streaks, autoplay, FOMO), which remain this project's guardrail/deceptive-use rules for the same broad territory. A mechanism can fail this file's legitimacy checks and simultaneously trigger one of those guardrail rules; the two are complementary lenses on the same reviewed evidence, not alternatives.

## The Elements of Game Design Model

Stephen P. Anderson's model frames a game as concentric layers, core to outer:

| Layer | Question it answers |
|---|---|
| Play & Challenges | Is there a genuine core challenge (mastery, self-improvement, estimation) the user actually cares about? |
| Conflicts & Choices | Do real constraints force meaningful trade-offs? |
| Feedback Loops | Does the user get a timely, interpretable reflection of their own performance? |
| Goals & Rewards | Do points/badges/levels sit on top of the above, or stand alone? |
| Imaginary World | Is there a narrative frame, if any, wrapping the rest? |

The legitimacy test this file applies throughout is `(Play + Challenges) + (Rewards + Goals) = Game`. Stripping a design down to only `Rewards + Goals` — points and badges with no underlying core challenge — is "sugarcoating," not game design, and does not reliably sustain engagement.

## Rules

| ID | Rule | Evidence | Default severity | Sources |
|---|---|---|---|---|
| PG01 | Difficulty or challenge should scale with the user's demonstrated skill level; do not present a static, one-size-fits-all challenge that treats a first-time user and an expert identically. | Strong | Moderate | Mihaly Csikszentmihalyi, Flow Theory |
| PG02 | Before endorsing points, badges, or leaderboards added to a task, confirm a genuine core challenge (mastery, self-improvement, estimation accuracy) has been identified — for example via a "5 Whys"/laddering pass from the surface task down to what the user actually wants to get better at. Extrinsic reward mechanics layered onto a task with no such core challenge produce only temporary novelty and can crowd out intrinsic motivation for qualitative tasks (peer reviews, community comments, creative work). | Strong + ethical | Major | Alfie Kohn, *Punished by Rewards*; Daniel Pink, *Drive* |
| PG03 | Collectible/achievement sets should be small, bounded, and genuinely achievable, with visible empty or locked placeholder slots for items not yet earned. An unbounded, effectively infinite collection bucket does not reliably motivate completion and instead produces helplessness. | Strong | Moderate | Set-completion / collecting-behavior research (via Anderson, *Seductive Interaction Design*) |
| PG04 | A personal feedback loop should passively and periodically reflect the user's own behavior back to them in meaningful, interpretable terms — not raw, uninterpreted numbers — and should support self-correction with an actionable next step. Showing negative performance data with no actionable next step produces anxiety/avoidance rather than self-correction. | Strong | Moderate | Personal-informatics / behavior-reflection research (via Anderson, *Seductive Interaction Design*) |
| PG05 | A real, disclosed, function-serving constraint (a character limit, a submission quota, a time window) that improves the user's own output quality or focus is a legitimate design choice, distinct from `behavioral-economics.md`'s `PB08` (fabricated scarcity/urgency on a resource the user wants). This legitimacy does not exempt the constraint from the four-gate ethical test in `ethics.md` — a constraint whose primary beneficiary is the business at the user's expense, not the user's own stated goal, still fails that test. | Contextual + ethical | Moderate | Robert Cialdini, scarcity/constraint research (via Anderson, *Seductive Interaction Design*) |

## How the psychology skill should apply these

Apply the `(Play+Challenges)+(Rewards+Goals)=Game` test as the first question whenever the reviewed evidence includes game-like mechanics (points, badges, levels, streak-adjacent progress, collectible sets): is there a real core challenge and feedback loop underneath, or only a rewards/goals layer bolted onto an otherwise unchanged task? A "sugarcoating" finding (`PG02`) is the answer when the latter is true. `PG01` (Flow) is a design-quality check for anything with adjustable difficulty — a static challenge is a `PG01` finding whether or not any reward layer is present at all. `PG03` (set completion) and `PG04` (feedback loops) are the two mechanisms most likely to appear together in a single reviewed dashboard or achievement system — check both, since a well-bounded collection with no interpretable feedback (or vice versa) is only half of a legitimate design. `PG05` is this file's one guardrail-adjacent rule: it legitimizes real constraints, but every `PG05` finding must still be run through `ethics.md`'s four gates before being reported as a positive finding, exactly like every other contextual mechanism in this skill.

This file is deliberately the complementary, legitimate-use half of a territory `behavioral-economics.md`'s `PB12` (variable rewards require an explicit wellbeing/ethics review) and `habits.md`'s `PH01`-`PH04` (optimize for healthy engagement, not maximum engagement; scrutinize streaks, autoplay, and FOMO) already cover from the guardrail side. A single reviewed mechanism can trigger both: for example, a streak mechanic can be evaluated here under `PG01`/`PG04` for whether its difficulty and feedback are well-designed, while `PH03` separately asks whether the streak pressure itself is ethically justified. Report both angles rather than treating one file's finding as making the other's check redundant.
