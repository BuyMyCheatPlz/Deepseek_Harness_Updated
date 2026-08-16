# @deepseek-ai/dsh-client-ui-plan

English | [中文](README.zh.md)

Plan/Act mode toggle, a pure browser surface plugin. The browser half occupies the conversation-declared `conversation.input.plan` single seat (to the right of the access-mode control); the node half is an empty apply (the roster row). Plan behavior itself — the `/plan` command, the boundary-or-idle-committed `plan/mode` state, the `plan` projection unit, and the policy section — is owned by [`@deepseek-ai/dsh-plan-mode`](../../plan/plan-mode/README.md), composed independently on the host roster.

The chip is always visible and reads the host-computed `plan` projection's effective target (`pending ? !active : active` — a folded host value, not client optimism, so an arriving frame corrects the chip either way). When plan mode is **off** the chip reads "Act" (neutral styling) and clicking it runs `/plan` to enter plan mode; when plan mode is **on** the chip reads "Plan" (warn styling with a remove glyph) and clicking it runs `/plan off` to leave. Both go through `command.execute`, so a user can also enter from the composer's `+` Command menu or by typing `/plan`. While plan mode is the effective target, the composer textarea's placeholder switches to the plan-task hint — "describe your task to generate plan", localized through ui-conversation's `conversation` locale namespace (the `placeholder.plan` / `hint.plan` keys) and shared verbatim with the claimed `/plan` command hint (rendered by the composer from the same projection; owner-supplied placeholders win).

The chip carries the accessible description "Plan mode on, press to turn off" when active and "Plan mode off, press to turn on" when not. Admission failures (`matched: false`, business errors, transport faults) surface as an inline error and the chip stays until the projection confirms the transition.

The model exits plan mode through the stable `exit_plan_mode` tool; its plan review uses the composed Web question channel.

## Model Experience

Indirectly, through the `/plan` / `/plan off` command lines the chip dispatches: `@deepseek-ai/dsh-plan-mode` owns the model-visible policy section, the exit-tool schema, and the logged state those lines drive, while this package only renders the projection and sends what a user could equally type. Model routing (`@deepseek-ai/dsh-model-router`) then serves the reasoning slot in plan mode and the execution slot out of it.

#### KV Cache effect

Entering or leaving plan mode changes the active `plan:policy` system-prompt section and therefore the request prefix (and the served model slot); the chip itself adds no prompt content.

## Known Limitations and Deferred Work

- **Plan mode is guidance, not an execution sandbox** — deployments that require enforced read-only planning must compose the independent sandbox and approval policies.
- **The chip belongs to the default composer** — a pending whole-composer interaction such as plan review temporarily replaces the InputBar and its chip.
- **The toggle is mode-only, not model-specific** — it switches the plan/execute mode; which model each mode uses is configured in `agent-model-router`.
