# @deepseek-ai/dsh-model-router

[English](README.md) | 中文

为 harness Agent 提供按步骤的模型分流：一个回合的第一步用推理（reasoning）模型，之后的工具续作步骤用执行（execution）模型。

该路由器是「按需启用」的。插件会挂载 `ctx.modelRouter`，但在用户自有的 `agent-model-router` 设置段同时给出两个槽位之前，`route({ step })` 返回 `undefined`——因此未配置的路由器不改变任何行为，单一的 `agent-default-model` 行为保持不变。安装了 `@deepseek-ai/dsh-agent` 的 `installModelSelection` 的入口点，会在每次请求时读取 `ctx.modelRouter.route({ step })`，并通过既有的模型选择缝隙应用返回的槽位；用户在 composer 中显式选择的模型优先于路由。

`step` 在一个回合内从 1 开始：第 1 步是对用户意图的新一轮推理，之后每一步都是处理工具结果并发出下一次调用。

## 设置

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

| 字段 | 含义 |
|---|---|
| `reasoning.provider` / `reasoning.model` | 回合第一步所用的提供方路由与模型 |
| `reasoning.reasoningEffort` | `off` / `high` / `max`，省略时取提供方默认 |
| `execution.*` | 工具续作步骤的同名字段 |

两个槽位必须都存在才会启用分流；删除任意一个（或整个段）即恢复单模型行为。编辑会通过设置提供方热发布，无需重启即可作用于下一步。

## 组合

```yaml
- id: model-router
  name: '@deepseek-ai/dsh-model-router'
```

Web-app bundle（`packages/bundle/web-app/cordis.patch.yml`）提供了部署默认槽位（`reasoning` → `deepseek-v4-pro`，`execution` → `deepseek-v4-flash`）；`agent-model-router:` 设置段可覆盖它们，无需重启。

## 已知局限

- 分流以「回合内 `step === 1`」为键；同回合内一条开始新一轮推理的 steering 消息，其续作步骤仍会走执行槽位。
- 路由器位于宿主平面：作用于读取它的每一个 agent，除 composer 显式选择外没有按会话的覆盖。

## 模型体验

间接地通过每一步收到的模型选择生效：推理模型看到回合的第一次请求，执行模型看到工具结果的续作，二者使用同一份系统提示与历史；仅 provider/model（以及各自槽位的推理强度）不同。

#### KV 缓存影响

步骤间切换路由会进入不同的缓存域，因此执行模型的首次请求无法复用推理模型的前缀。
