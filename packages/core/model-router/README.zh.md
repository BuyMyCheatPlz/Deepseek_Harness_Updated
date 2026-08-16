# @deepseek-ai/dsh-model-router

[English](README.md) | 中文

为 harness Agent 提供基于模式的模型分流：plan 模式开启时（Cline 风格的「Plan」）**推理**槽位服务每一步，plan 模式关闭后（Cline 风格的「Act」）**执行**槽位服务每一步——在计划/执行的分界上完成一次干净的 pro→flash 交接。

该路由器是「按需启用」的。插件会挂载 `ctx.modelRouter`，但在用户自有的 `agent-model-router` 设置段同时给出两个槽位之前，`route()` 返回 `undefined`——因此未配置的路由器不改变任何行为，单一的 `agent-default-model` 行为保持不变。安装了 `@deepseek-ai/dsh-agent` 的 `installModelSelection` 的入口点，会在每次请求时读取 `ctx.modelRouter.route(payload)`，把实时的 `foldPlanMode(agent.session.events)` 状态喂给它，并通过既有的模型选择缝隙应用返回的槽位。在 Web 入口点，显式选择模型会把会话切到手动模式（host 端 `autoRouting: false`），此后路由器不再生效，直到用户切回 Auto——见 [Windows 应用 README](../../../apps/windows/README.md) 的 Auto/Manual 开关。

## 模式切换如何工作

plan 状态是 harness 记录的 `plan/mode` 会话状态（来自 `/plan` 命令、composer 的 Plan 芯片，或 `exit_plan_mode` 复核）。`@deepseek-ai/dsh-host-apiproxy` 通过 `foldPlanMode` 在每次请求中折出该状态：

| plan 模式 | 槽位 | 模型 |
|---|---|---|
| 开启（Plan） | `reasoning` | `deepseek-v4-pro`，`reasoningEffort: high` |
| 关闭（Act） | `execution` | `deepseek-v4-flash`，`reasoningEffort: off` |

即：先用 `deepseek-v4-pro` 读懂请求并制定计划，再让经过批准的计划由 `deepseek-v4-flash` 执行。当 plan 状态未知（某个路由没传 plan 标记）时，路由器回退到步骤规则：一个回合的第 1 步推理，之后的工具续作步骤执行。

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
| `reasoning.provider` / `reasoning.model` | plan 模式下所用的提供方路由与模型 |
| `reasoning.reasoningEffort` | `off` / `high` / `max`，省略时取提供方默认 |
| `execution.*` | plan 模式外所用的同名字段 |

两个槽位必须都存在才会启用分流；删除任意一个（或整个段）即恢复单模型行为。编辑会通过设置提供方热发布，无需重启即可作用于下一个请求。

## 组合

```yaml
- id: model-router
  name: '@deepseek-ai/dsh-model-router'
```

Web-app bundle（`packages/bundle/web-app/cordis.patch.yml`）提供了部署默认槽位（`reasoning` → `deepseek-v4-pro`，`execution` → `deepseek-v4-flash`）；`agent-model-router:` 设置段可覆盖它们，无需重启。

## 已知局限

- 分流以 plan 模式为键且位于宿主平面：作用于读取它的每一个 agent，除 composer 显式选择外没有按会话的覆盖。
- 从未进入 plan 模式的会话始终使用 `execution` 槽位（或在无 plan 时按步骤回退到第 1 步推理），所以「Act 恒 flash」是默认行为，除非你进入 plan 模式。
- `@deepseek-ai/dsh-host-apiproxy` 依赖 `@deepseek-ai/dsh-plan-mode` 来做 fold；未挂载 plan-mode 的组合只回退到步骤规则。

## 模型体验

间接地通过每个请求收到的模型选择生效：plan 模式下推理模型看到每个请求（在高 `high` 强度下做强规划）；plan 模式外执行模型看到它们（关闭推理、更省成本）。二者使用同一份系统提示与历史；仅 provider/model（以及各自槽位的推理强度）不同。

#### KV 缓存影响

两次请求之间的模式切换会进入不同的缓存域，因此离开 plan 模式后的第一个执行请求无法复用推理模型的前缀。
