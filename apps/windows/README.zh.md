# DeepSeek Harness Windows 启动器

[English](README.md) | 中文

`apps/windows` 下围绕 `dsh web` 的一个轻量原生 Windows 包装，与 [macOS 壳](../macos/README.md) 对应。Web 运行器本身完全不变：应用启动同一个端口上的同一个服务，并在内嵌的 WebView2 控件里显示界面——与浏览器加载该地址所见的页面一致。启动器额外提供的是标准 Windows 形态与进程所有权——带内嵌界面的窗口、任务栏图标，以及有保障的清理：退出（关闭窗口或 Quit 按钮）会终止服务端的进程树，并在应用退出前确认端口已释放。系统浏览器永远不会被自动打开；「Open in Browser」按钮才是显式选择。

启动器是单个 C# 文件，用 Windows 自带的 .NET Framework `csc.exe` 编译；WebView2 SDK 程序集与 loader 在构建时从 NuGet 下载，因此除了可选的 `dsh` 内嵌所需的 Node.js 外，不需要任何 SDK 或工具链。

## 环境要求

- Windows 10 或更高（.NET Framework 4.x 运行时随系统自带）
- Microsoft Edge WebView2 运行时（Windows 11 与大多数 Windows 10 自带；缺失时应用会明确报错）
- `node`（22.19+ 或 24+），可在 `PATH`、`%ProgramFiles%\nodejs`、`%LOCALAPPDATA%\Programs\nodejs`、nvm-windows、Volta 或 bun 安装中找到——**仅**在你手打文件夹时需要。**安装版**（见下文「安装版」节）内置了一套便携 Node.js 运行时，用户完全无需系统 Node。
- 已安装的 `dsh`（npm/pnpm 全局，或 npx 缓存），或使用 `-BundleDsh` 将其内嵌到 exe 旁

## 安装版

Release 发布 `DeepSeek-Harness-Setup-<版本>.exe`，一个基于 [Inno Setup](https://jrsoftware.org/isinfo.php) 的安装程序，把所有东西打成一个自包含的 `Setup.exe`：

- 启动器 exe + 三个 WebView2 DLL
- 内嵌的 web 运行时（`dsh\`），已套用 fork 的 Plan/Act 分流与 Auto/Manual 开关
- 一套**便携 Node.js 运行时**（`runtime\node`）——无需系统 Node.js 或任何 web 工具链

运行安装程序后，会装到 `%ProgramFiles%\DeepSeek Harness`、建开始菜单快捷方式（可选桌面图标）、并注册卸载项。卸载：**设置 → 应用 → DeepSeek Harness → 卸载**。

本地构建（需先按上文「重新打包与更新 fork 产物」把 `build\` 构建好并 rebundle）：

```powershell
npx --yes pnpm@11.7.0 run build:lib:host      # build the fork's packages
npx --yes pnpm@11.7.0 run build:lib:client     # build the fork's browser bundles
powershell -ExecutionPolicy Bypass -File apps/windows/build.ps1 -BundleDsh -DshVersion 0.1.0-rc.6
powershell -ExecutionPolicy Bypass -File apps/windows/rebundle.ps1
powershell -ExecutionPolicy Bypass -File apps/windows/build-installer.ps1   # -> build/DeepSeek-Harness-Setup-0.1.0-rc.6.exe
```

`build-installer.ps1` 会下载并暂存便携 Node（`-NodeVersion`，默认 22.19.0）到 `build\runtime\node`，再用 Inno Setup（`PATH` 里的 `ISCC.exe`，或 `ISCC_PATH`）编译 `installer.iss`。

## 构建

```powershell
apps/windows/build.ps1                      # -> apps/windows/build/DeepSeek Harness.exe
apps/windows/build.ps1 -BundleDsh           # self-contained: also installs @deepseek-ai/dsh
apps/windows/build.ps1 -BundleDsh -DshVersion 0.1.0-rc.6
```

`-BundleDsh` 会把 `@deepseek-ai/dsh@<版本>` 装进 `build\dsh`（版本来自 `-DshVersion` 或 `DSH_BUNDLE_VERSION` 环境变量，默认 `latest`）。`-AppVersion` 会把启动器自身版本写入 exe（默认取 `-DshVersion`，内嵌 dsh 为 `latest` 时取 `0.0.0`）；`-UpdateRepos` 会固化启动时更新检查所查询的、以分号分隔的 `owner/repo` 列表（默认 `deepseek-ai/deepseek-harness`）。构建还会下载 WebView2 SDK，把 `Microsoft.Web.WebView2.Core.dll`、`Microsoft.Web.WebView2.WinForms.dll` 与 x64 的 `WebView2Loader.dll` 放到 exe 旁（可用 `WEBVIEW2_SDK_VERSION` 环境变量固定版本）。产物是一个包含 `DeepSeek Harness.exe`、三个 WebView2 DLL 与（内嵌时）`dsh\` 的文件夹——打包该文件夹即可分发。

### 重新打包与更新 fork 产物

`build.ps1 -BundleDsh` 装的是 **上游** `@deepseek-ai/dsh`（来自 npm），其中**不包含**本 fork 的改动——Plan/Act 双模型分流、模型选择器的 **Auto / Manual** 开关、以及该开关驱动的 `session.setAutoRouting` RPC，都只存在于本 fork。

**用户无需在每次上游更新后手动操作。** 安装目录里带有一份持久的 fork 叠加层 `overlay\dsh`，启动器在**每次启动**和**每次应用内自更新后**都会把它重新铺到内嵌的 `dsh\` 里——所以升级到更新的上游版本（如 rc.7）后，Plan/Act 分流、模型开关、以及 plan 模式工具拦截都会自动保留。这是 `Program.cs` 里的 `ForkOverlay.Ensure()`。

**开发者**重新构建 fork 包并重铺叠加层（同时写入 `dsh\` 与 `overlay\dsh\`）：

```powershell
npx --yes pnpm@11.7.0 run build:lib:host    # build the fork's packages
npx --yes pnpm@11.7.0 run build:lib:client   # build the fork's browser bundles
powershell -ExecutionPolicy Bypass -File apps/windows/build.ps1 -BundleDsh -DshVersion 0.1.0-rc.7
powershell -ExecutionPolicy Bypass -File apps/windows/rebundle.ps1
```

`rebundle.ps1` 会把 fork 构建的 host 包（`dsh-agent`、`dsh-host-apiproxy`、`dsh-headless`、`dsh-web-app`）**以及**浏览器 bundle——`dsh-client-connection`（承载 `setAutoRouting` 方法与 `autoRouting` 响应字段）、`dsh-client-ui-model-selection`、`dsh-client-ui-plan`——的本地构建产物覆盖回去，把 `dsh-model-router` 装到旁边、链接进 profile 的模块回退目录，并把同样文件写入 `overlay\dsh\` 用于自修复。

由于浏览器 bundle 在启动时会被哈希并注入页面，**自己调试重新打包后需要重启应用**（退出 `DeepSeek Harness.exe` 再重新打开），新 bundle 才会真正加载。

### 应用图标

启动器与安装包使用仓库根目录的 `7x4nf-prdx8-001.ico`。`build.ps1` 把它作为 `app.ico` 复制到 exe 旁、用 `/win32icon:` 内嵌为 exe 图标；`installer.iss` 把它用作安装程序图标并装进 `{app}`；`Program.cs` 运行时加载它为窗口图标。安装后 exe、开始菜单/桌面快捷方式、安装程序、窗口都会显示该图标。

## 工作方式

1. 启动时应用解析 `dsh`（其 `lib/bin.js`）与 `node` 可执行文件——`dshPath`/`nodePath` 注册表值，其次是 exe 旁的内嵌安装，最后是按 PATH 风格的搜索，包括 `%APPDATA%\npm`、Program Files、nvm-windows、Volta、bun 与 npx 缓存。
2. 服务端启动前，它会检查是否有新版本（见下文「更新检查与自更新」）；服务端会等待检查与任何已确认的更新完成后再启动。
3. 它以无控制台窗口的子进程形式拉起 `node <dsh> web`，服务端输出追加到 server.log。
4. 一旦 `127.0.0.1:<端口>` 接受连接，内嵌的 WebView2 控件就加载服务地址。系统浏览器只会在点「Open in Browser」按钮或启用 `openBrowserOnLaunch` 设置时打开。
5. 退出——关闭窗口、Quit 按钮，或被 `taskkill /PID`（WM_CLOSE）关闭窗口——会杀掉服务端的进程树（`taskkill /PID <pid> /T /F`），等待端口释放（最多 6 秒），并且只触碰应用自己拉起的进程树。Windows 没有 SIGTERM，因此这是硬终止而不是 macOS 的优雅收尾；端口仍然会释放。
6. 如果应用被强杀（任务管理器、崩溃），孤儿的服务端会继续服务；下次启动会读取记录的 pid，确认其命令行与解析出的 `dsh` 匹配后将其终止，再全新启动。
7. 第二个实例拒绝启动（命名互斥体）。

## 更新检查与自更新

服务端启动前，应用会检查是否有更新的 DeepSeek Harness 版本，有的话提供一键就地更新。它取以下来源中最新的一个：

- `updateRepos` 中每个仓库的最新 GitHub Release（默认 `deepseek-ai/deepseek-harness`）；
- npm 注册表（`@deepseek-ai/dsh`），因为上游把 webui 发布到 npm 而不是 GitHub Release。

「当前」版本从解析出的 `dsh` 的 `package.json` 读取。发现更新版本时，会弹窗提供**立即更新**：把 `@deepseek-ai/dsh@<版本>` 重新安装到暂存目录，再原子地替换内嵌的 `dsh\`（未内嵌的全局安装则用 `npm install -g` 刷新）。exe 从不替换自身，因此后续每一次 webui 发布都能沿用此机制，无需重装应用。检查在后台线程上运行且带超时；若检查失败、超时或无法确定当前版本，应用就直接启动服务端。

无界面诊断（打印后退出，不打开 UI）：

- `DeepSeek Harness.exe --check-update` —— 打印 `current`、`latest`、`source` 后退出。
- `DeepSeek Harness.exe --update [版本]` —— 无界面执行自更新（未给版本时取最新），然后退出。

## Plan/Act 双模型分流（Cline 风格）

Web profile 内置了 `@deepseek-ai/dsh-model-router` 插件，它根据会话的 **plan 模式**从两个模型中选一个：plan 模式开启时（Cline 风格的「Plan」）每个请求都用**推理**模型；plan 模式关闭后（Cline 风格的「Act」）每个请求都用**执行**模型。它适用于每个会话——除非你在 composer 里显式选了模型（显式选择在该会话中优先）。

### 默认槽位

出厂默认值在 `packages/bundle/web-app/cordis.patch.yml`：

| 模式 | 槽位 | 模型 |
|---|---|---|
| plan 开启 | reasoning | `deepseek-v4-pro`，`reasoningEffort: high` |
| plan 关闭 | execution | `deepseek-v4-flash`，`reasoningEffort: off` |

即：在 plan 模式下先用 `deepseek-v4-pro` 读懂请求、制定计划；离开 plan 模式后，让经过批准的计划由 `deepseek-v4-flash` 执行——每个模式内模型固定，在计划/执行的分界上干净地切换。

### 怎么用

- **plan 模式开启**（`/plan`、composer 的 Plan 芯片，或让 agent 调用 `exit_plan_mode` 提交计划）→ 请求走 `deepseek-v4-pro`。
- **plan 模式关闭**（默认；或 `exit_plan_mode` 被批准后 / `/plan off` / Plan 芯片）→ 请求走 `deepseek-v4-flash`。

启动时无需任何操作：打开 `DeepSeek Harness.exe`，分流就已生效。可以从会话记录里确认（每个模型请求都会在它的 `request/header` 事件中记录 `provider` / `model` / `reasoningEffort`）。

### 自动分流 vs 手动模型

composer 的模型座位在会话处于 Plan/Act 自动分流时是**只读**的：它显示当前模式实际生效的模型（Plan → `deepseek-v4-pro`，Act → `deepseek-v4-flash`）并置灰。旁边有一个小的 **Auto / Manual** 开关控制：

- **Auto**（默认）：会话按 plan 模式自动分流（Plan/Act 双模型），模型列表不可选。
- **Manual**：会话使用你在列表里手动选择的模型，Plan/Act 分流暂时关闭，直到你切回 Auto。

在 Manual 下选择模型也会把该会话切换到手动模式。这由 host 侧的 `autoRouting` 每会话状态驱动，无需重启即可作用于每个工作区。

### 怎么自定义

两个槽位是一个用户自有的设置段，热发布生效（编辑后下一个请求就生效，无需重启）。编辑 `%USERPROFILE%\.dsh\settings.yaml`：

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

- 改任意模型或其 `reasoningEffort`（`off` / `high` / `max`，省略时取提供方默认）。
- 要关闭分流、回到单一模型：删除整个 `agent-model-router` 段（回退到单一的 `agent-default-model`），或把两个槽位设成同一个模型。
- 每个槽位的 `provider` / `model` 必填；`provider` 必须是你已配置的 adapter 路由（如 DeepSeek adapter 的 `deepseek-official`）。
- 在 composer 里手动选择的模型会优先于分流。
- 从未进入 plan 模式的会话始终用 `execution` 槽位，所以「Act 恒 flash」是默认行为。

### 代码位置

插件在 `packages/core/model-router`，接入点在 `packages/host/apiproxy/src/api-proxy.ts` 的按请求模型选择处，通过 `@deepseek-ai/dsh-plan-mode` 的 `foldPlanMode` 折出 plan 状态（`headless` 直接驱动入口则回退到步骤规则）。这是**本地 fork 新增**，尚未进入 npm 上游 `@deepseek-ai/dsh` 包——见上文「重新打包注意」。

## 配置

`HKCU\Software\DeepSeek Harness` 下的注册表值（对应 macOS 的 `defaults` 域），可用命令行参数覆盖：

| 设置 | 注册表值 / 参数 | 默认值 | 含义 |
|---|---|---|---|
| `port` | `port` / `-port` | `3080` | 以 `--port` 传给 `dsh web` 的端口；已被占用时应用自动改用下一个空闲端口 |
| `dshPath` | `dshPath` / `-dshPath` | 自动 | dsh `lib/bin.js` 的显式路径 |
| `nodePath` | `nodePath` / `-nodePath` | 自动 | `node.exe` 的显式路径 |
| `openBrowserOnLaunch` | `openBrowserOnLaunch` / `-openBrowserOnLaunch` | `0` | 服务就绪时是否额外打开系统浏览器（内嵌视图始终显示） |
| `stateDir` | `stateDir` / `-stateDir` | `%LOCALAPPDATA%\DeepSeek Harness` | 启动器存放 `server.pid`/`app.pid` 锁与 server.log 的位置 |
| `checkUpdates` | `checkUpdates` / `-checkUpdates` | `1` | 服务端启动前是否检查新版本 |
| `checkNpm` | `checkNpm` / `-checkNpm` | `1` | 是否同时把 npm 注册表作为版本来源 |
| `updateRepos` | `updateRepos` / `-updateRepos` | `deepseek-ai/deepseek-harness` | 以分号分隔、用于查询最新 GitHub Release 的 `owner/repo` 列表 |

示例——换一个端口提供服务：

```powershell
reg add HKCU\Software\DeepSeek Harness /v port /t REG_DWORD /d 8080
```

## 日志与状态

- `%LOCALAPPDATA%\DeepSeek Harness\server.log` —— 服务端的 stdout/stderr
- `%LOCALAPPDATA%\DeepSeek Harness\server.pid` / `app.pid` —— 锁文件
- `%USERPROFILE%\.dsh` —— 服务端自己的 profile、会话与设置，启动器不触碰

卸载：退出应用，删除文件夹，并移除注册表键 `HKCU\Software\DeepSeek Harness` 与 `%LOCALAPPDATA%\DeepSeek Harness`。

## 测试

`apps/windows/scripts/test-lifecycle.ps1` 在测试端口上以临时 `DSH_HOME` 启动 exe，端到端验证两个阶段：正常退出释放端口并杀掉服务端进程树；强杀崩溃留下孤儿服务端，由下次启动回收。

## 发布

本 fork 只发布**Windows**应用，产物是一个自包含安装程序。`.github/workflows/app-release.yml` 在 CI 里构建 exe（先内嵌上游 `@deepseek-ai/dsh`，再构建 fork 工作区并执行 `apps/windows/rebundle.ps1`，把 fork 的 Plan/Act 分流与 Auto/Manual 开关一并打进产物），然后用 `apps/windows/build-installer.ps1` 编译出 `DeepSeek-Harness-Setup-<版本>.exe` 并挂到 GitHub Release；fork 中 macOS 任务已跳过。

要发布 release，推送一个 `dsh-v*` 标签即可——标签名去掉 `dsh-v` 前缀即为版本号，且该版本必须已发布到 npm 的 `@deepseek-ai/dsh`（`-BundleDsh` 才能安装）：

```bash
git push origin <branch> --no-verify
git tag dsh-v0.1.0-rc.6
git push origin dsh-v0.1.0-rc.6
```

推送标签会触发 workflow 的 `publish` 任务（使用 Actions 的 `GITHUB_TOKEN`），自动创建 Release 并挂上 `DeepSeek-Harness-Setup-<版本>.exe`。也可以在 Actions 页面手动触发（`workflow_dispatch`，`publish=true`，且需从 `dsh-v*` 标签运行）。
