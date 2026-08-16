# @deepseek-ai/dsh-client-ui-plan

[English](README.md) | 中文

Plan/Act 模式切换，纯浏览器 surface 插件。浏览器侧占用会话声明的 `conversation.input.plan` 单实例 seat（位于 access 模式控件右侧）；node 侧是空 apply（roster 行）。plan 行为本身——`/plan` 命令、边界或空闲即时提交的 `plan/mode` 状态、`plan` 投影单元与 policy 段——归 [`@deepseek-ai/dsh-plan-mode`](../../plan/plan-mode/README.md) 所有，由 host roster 独立组合。

chip 始终可见，读取 host 计算的 `plan` 投影有效目标（`pending ? !active : active`——折叠的 host 值而非客户端乐观态，帧到达即自动纠正）。当 plan mode **关闭**时 chip 显示 "Act"（中性样式），点击执行 `/plan` 进入 plan mode；当 plan mode **开启**时 chip 显示 "Plan"（warn 样式并带关闭图标），点击执行 `/plan off` 退出。两条路径都走 `command.execute`，用户也可以从 composer 的 `+` Command 菜单或手动输入 `/plan` 进入。plan mode 为有效目标期间，composer 文本框的 placeholder 切换为 plan 任务提示——"describe your task to generate plan"（中文「描述你的任务以生成计划」），经 ui-conversation 的 `conversation` locale 命名空间（`placeholder.plan` / `hint.plan` 键）本地化，并与已认领 `/plan` 命令的提示逐字共用同一份文案（由 composer 从同一投影渲染；owner 提供的 placeholder 优先）。

chip 在激活时携带无障碍描述 "Plan mode on, press to turn off"，未激活时 "Plan mode off, press to turn on"。准入失败（`matched: false`、业务错误、传输故障）以内联错误呈现，chip 保持显示直至投影确认切换完成。

模型通过稳定的 `exit_plan_mode` 工具退出 plan mode；其 plan 评审走已组合的 Web question 通道。

## 模型体验

间接地，通过 chip 派发的 `/plan` / `/plan off` 命令行：`@deepseek-ai/dsh-plan-mode` 拥有这些命令行驱动的模型可见 policy 段、退出工具 schema 与已记录状态，本包只渲染投影并发送用户同样可以手敲的内容。模型路由（`@deepseek-ai/dsh-model-router`）随后在 plan mode 内提供推理槽位、退出后提供执行槽位。

#### KV Cache 影响

进入或离开 plan mode 会改变活跃的 `plan:policy` 系统提示词段，因此改变请求前缀（以及所用的模型槽位）；chip 本身不添加任何提示词内容。

## 已知局限与延后工作

- **Plan mode 是引导而非执行沙箱**：需要强制只读规划的部署必须组合独立的沙箱与审批策略。
- **chip 属于默认编辑器**：待处理的整编辑器交互（如 plan 评审）会临时取代 InputBar 及其 chip。
- **切换只针对模式而非具体模型**——它切换 plan/execute 模式；每个模式用哪个模型由 `agent-model-router` 配置。
