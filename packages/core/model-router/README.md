# @deepseek-ai/dsh-model-router

English | [中文](README.zh.md)

Mode-based model routing for the harness Agent: while plan mode is active (Cline-style "Plan") the **reasoning** slot serves every step, and once plan mode is off (Cline-style "Act") the **execution** slot does — a clean pro-to-flash handoff across the plan/execute boundary.

The router is opt-in. The plugin mounts `ctx.modelRouter`, but `route()` returns `undefined` until the user-owned `agent-model-router` settings section names both slots — so an unconfigured router changes nothing and the single `agent-default-model` behavior is unchanged. Entry points that install `@deepseek-ai/dsh-agent`'s `installModelSelection` read `ctx.modelRouter.route(payload)` per request, feeding it the live `foldPlanMode(agent.session.events)` state, and apply the returned slot through the existing model-selection seam; an explicit composer model pick wins over routing.

## How the mode switch works

Plan state is the harness's logged `plan/mode` session state (from the `/plan` command, the composer Plan chip, or the `exit_plan_mode` review). `@deepseek-ai/dsh-host-apiproxy` folds it per request via `foldPlanMode`:

| Plan mode | Slot | Model |
|---|---|---|
| on (Plan) | `reasoning` | `deepseek-v4-pro`, `reasoningEffort: high` |
| off (Act) | `execution` | `deepseek-v4-flash`, `reasoningEffort: off` |

So the model reads your request and plans with `deepseek-v4-pro`, then the approved plan is executed with `deepseek-v4-flash`. When plan state is unknown (a route that passes no plan flag), the router falls back to the step rule: a turn's first step reasons, later tool-continuation steps execute.

## Settings

```yaml
agent-model-router:
  reasoning:
    provider: deepseek-official
    model: deepseek-v4-pro
    reasoningEffort: high
  execution:
    provider: deepseek-official
    model: deepseek-v4-flash
    reasoningEffort: off
```

| Field | Meaning |
|---|---|
| `reasoning.provider` / `reasoning.model` | Provider route and model used in plan mode |
| `reasoning.reasoningEffort` | `off` / `high` / `max`, or omitted for the provider default |
| `execution.*` | The same fields used outside plan mode |

Both slots must be present for routing to activate; removing either (or the whole section) restores single-model behavior. Edits hot-publish through the settings provider and reach the next request without a restart.

## Composition

```yaml
- id: model-router
  name: '@deepseek-ai/dsh-model-router'
```

The web-app bundle (`packages/bundle/web-app/cordis.patch.yml`) supplies the deployment-default slots (`reasoning` → `deepseek-v4-pro`, `execution` → `deepseek-v4-flash`); a `agent-model-router:` settings section overrides them without a restart.

## Known Limitations

- Routing is plan-mode keyed and host-plane: it applies to every agent that reads it, with no per-session override other than an explicit composer pick.
- A session that never enters plan mode always uses the `execution` slot (or the step fallback first step), so "Act always flash" is the default unless you enter plan mode.
- `@deepseek-ai/dsh-host-apiproxy` depends on `@deepseek-ai/dsh-plan-mode` for the fold; a composition without plan-mode mounts only the step fallback.

## Model Experience

Indirect, through the model selection each request receives: in plan mode the reasoning model sees every request (strong planning over the effortful `high` setting); outside plan mode the execution model sees them (reasoning disabled, cheaper execution). Both run over the same system prompt and history; only provider/model (and per-slot reasoning effort) differ.

#### KV Cache effect

A mode transition between requests selects a different cache domain, so the first execution request after leaving plan mode cannot reuse the reasoning model's prefix.
