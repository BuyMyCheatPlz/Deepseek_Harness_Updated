# @deepseek-ai/dsh-model-router

English | [中文](README.zh.md)

Per-step model routing for the harness Agent: a reasoning model serves a turn's first step, and an execution model serves its later tool-continuation steps.

The router is opt-in. The plugin mounts `ctx.modelRouter`, but `route({ step })` returns `undefined` until the user-owned `agent-model-router` settings section names both slots — so an unconfigured router changes nothing and the single `agent-default-model` behavior is unchanged. Entry points that install `@deepseek-ai/dsh-agent`'s `installModelSelection` read `ctx.modelRouter.route({ step })` per request and apply the returned slot through the existing model-selection seam; an explicit composer model pick wins over routing.

`step` is 1-based within a turn: step 1 is the fresh reasoning about user intent, and every later step processes tool results and issues the next call.

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
| `reasoning.provider` / `reasoning.model` | Provider route and model for a turn's first step |
| `reasoning.reasoningEffort` | `off` / `high` / `max`, or omitted for the provider default |
| `execution.*` | The same fields for the tool-continuation steps |

Both slots must be present for routing to activate; removing either (or the whole section) restores single-model behavior. Edits hot-publish through the settings provider and reach the next step without a restart.

## Composition

```yaml
- id: model-router
  name: '@deepseek-ai/dsh-model-router'
```

The web-app bundle (`packages/bundle/web-app/cordis.patch.yml`) supplies the deployment-default slots (`reasoning` → `deepseek-v4-pro`, `execution` → `deepseek-v4-flash`); a `agent-model-router:` settings section overrides them without a restart.

## Known Limitations

- Routing keys on `step === 1` within a turn; a steering message that starts new reasoning inside the same turn still routes its continuation steps to the execution slot.
- The router is host-plane: it applies to every agent that reads it, with no per-session override other than an explicit composer pick.

## Model Experience

Indirect, through the model selection each step receives: the reasoning model sees the turn's first request and the execution model sees the tool-result continuations, both over the same system prompt and history; only provider/model (and per-slot reasoning effort) differ.

#### KV Cache effect

A route change between steps selects a different cache domain, so the execution model's first request cannot reuse the reasoning model's prefix.
