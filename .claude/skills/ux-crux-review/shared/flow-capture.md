# Flow capture

How a ux-crux skill collects its own evidence from a live web page or a running build before it evaluates rules. Flow capture makes cross-step rules (step count, back navigation, data kept between steps, consistent labels across steps) testable, and gives the report real screenshots.

## When it runs

Run flow capture without the user asking for it when all three hold:

1. The evidence is a live web page (a URL) or a running build the agent can open.
2. The request names a task or flow — registration, sign-up, login, checkout, onboarding, booking, search to result, settings change — or the URL is the entry page of such a task.
3. The host provides a tool that can open, drive, and capture the page.

For a single screen with no named task, capture that one screen and its reachable states, and skip the flow map.

## Find a tool

Use the first one available:

1. A Playwright MCP server (browser navigate, click, type, snapshot, screenshot tools).
2. A Chrome DevTools MCP server.
3. A Playwright command line (`playwright-cli`, or `npx playwright` for screenshots).
4. Any other browser or device automation tool the host lists.

If none is available, ask the user to choose: provide screenshots of each step, or continue without captures, in which case report pictures are schematic only. Never claim that you observed the live page. List the steps you did not capture in Missing states / missing context.

## Walk the flow

For each step, in order:

1. Record the URL, the page title or main heading, and the visible primary action.
2. Capture the step at the size the user would see: 390px wide for a mobile flow, 1280px wide for a desktop flow; JPEG at quality about 70. Save it under the report's `img/` folder when a report is requested.
3. Reach the states that change no data: empty-field validation, inline errors after leaving a field, invalid formats, loading and empty states, focus order, back navigation to the previous step. Capture each state that has a finding.
4. Go to the next step with the primary action, entering test input where needed (`test+uxcrux@example.com`, an obviously fake name).

Limits:

- Walk at most 12 steps. Say in Missing context where you stopped and why.
- When the flow branches (account type, payment method, "I already have an account"), ask the user which branch to walk.
- When a step needs something you do not have — a code sent by email or SMS, a CAPTCHA, a real card, a signed-in account — stop there. Mark that step and every later step `not_reached` with the reason, report the findings that depend on them as `NOT_ASSESSABLE`, and name the evidence that would resolve them (a test account, a staging inbox, a test card).

## Ask before any data change

Before any action that creates, changes, sends, or pays for something — submitting a form, creating an account, placing an order, sending an email or message, saving settings, deleting — stop and ask the user. Offer three choices: submit this once, use a test account or staging environment, or stop the walk at this step. The user's yes covers only the action you named. Client-side validation that runs without a request is not a data change.

## Use the walked flow

- Build the flow map: each step with its number, title, URL, capture, and status (`captured`, `observed`, `not_reached`).
- Evaluate each step's rules against its capture, and the cross-step rules against the transitions between steps.
- Locate every finding: a step (`step: <n>`) or a transition (`from: <n>`, `to: <n>`), as in `findings-contract.md`.
- In the text report, name the step in each finding ("Step 2 → Step 3: going back clears the email field").
