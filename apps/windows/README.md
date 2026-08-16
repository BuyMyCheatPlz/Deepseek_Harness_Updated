# DeepSeek Harness Windows launcher

English | [中文](README.zh.md)

A thin native Windows wrapper around `dsh web` for `apps/windows`, mirroring the [macOS shell](../macos/README.md). The web runner itself is unchanged: the app starts the same server on the same port and shows the UI in an embedded WebView2 control, exactly like the page a browser would load at the served URL. What the launcher adds is a standard Windows presence and process ownership — a window with the embedded UI, a taskbar icon, and guaranteed cleanup: quitting (closing the window or the Quit button) terminates the server's process tree and verifies the port is released before the app exits. The system browser is never opened automatically; the "Open in Browser" button is the explicit opt-in.

The launcher is a single C# file compiled with the .NET Framework `csc.exe` that ships with Windows; the WebView2 SDK assemblies and loader are downloaded from NuGet at build time, so no SDK or toolchain is needed beyond Node.js (for the optional bundled `dsh`).

## Requirements

- Windows 10 or newer (the .NET Framework 4.x runtime ships with the OS)
- the Microsoft Edge WebView2 runtime (ships with Windows 11 and most Windows 10 installs; the app reports a clear error when it is missing)
- `node` (22.19+ or 24+), reachable on `PATH` or at `%ProgramFiles%\nodejs`, `%LOCALAPPDATA%\Programs\nodejs`, nvm-windows, Volta, or bun installs
- an installed `dsh` (npm/pnpm global, or an npx cache), or `-BundleDsh` to embed one beside the exe

## Build

```powershell
apps/windows/build.ps1                      # -> apps/windows/build/DeepSeek Harness.exe
apps/windows/build.ps1 -BundleDsh           # self-contained: also installs @deepseek-ai/dsh
apps/windows/build.ps1 -BundleDsh -DshVersion 0.1.0-rc.6
```

`-BundleDsh` installs `@deepseek-ai/dsh@<version>` into `build\dsh` (version from `-DshVersion` or the `DSH_BUNDLE_VERSION` environment variable, default `latest`). `-AppVersion` stamps the launcher's own version into the exe (defaults to `-DshVersion`, or `0.0.0` when the bundled dsh is `latest`); `-UpdateRepos` bakes in the semicolon-separated `owner/repo` list the startup update check queries (default `deepseek-ai/deepseek-harness`). The build also downloads the WebView2 SDK and places `Microsoft.Web.WebView2.Core.dll`, `Microsoft.Web.WebView2.WinForms.dll`, and the x64 `WebView2Loader.dll` beside the exe (pin the version with the `WEBVIEW2_SDK_VERSION` environment variable). The result is a folder with `DeepSeek Harness.exe`, the three WebView2 DLLs, and (when bundled) `dsh\` — zip that folder to distribute.

### Rebuild and update the fork bundle

`build.ps1 -BundleDsh` installs the **upstream** `@deepseek-ai/dsh` from npm, which does **not** contain this fork's changes — the Plan/Act dual-model router, the model selector's **Auto / Manual** toggle, and the `session.setAutoRouting` RPC that toggle drives all live only in this fork. Run `apps/windows/rebundle.ps1` after **every** rebuild that re-runs `-BundleDsh`, and after the in-app self-update atomically swaps the bundled `dsh\`:

```powershell
npx --yes pnpm@11.7.0 run build:lib:host    # build the fork's packages
npx --yes pnpm@11.7.0 run build:lib:client   # build the fork's browser bundles
powershell -ExecutionPolicy Bypass -File apps/windows/build.ps1 -BundleDsh -DshVersion 0.1.0-rc.6
powershell -ExecutionPolicy Bypass -File apps/windows/rebundle.ps1
```

`rebundle.ps1` overlays the fork-built host packages (`dsh-agent`, `dsh-host-apiproxy`, `dsh-headless`, `dsh-web-app`) **and** the browser bundles — `dsh-client-connection` (carries the `setAutoRouting` wire method and the `autoRouting` response field), `dsh-client-ui-model-selection`, and `dsh-client-ui-plan` — installs `dsh-model-router` beside them, and links it into the profile's module fallback so the loader resolves it.

Because the browser bundles are hashed and injected into the page at startup, **restart the app** (quit `DeepSeek Harness.exe` and reopen it) after rebundling so the new bundles actually load.

## How it works

1. On launch the app resolves the `dsh` (its `lib/bin.js`) and `node` executables — the `dshPath`/`nodePath` registry values, then a bundled install beside the exe, then a PATH-style search including `%APPDATA%\npm`, Program Files, nvm-windows, Volta, bun, and the npx cache.
2. Before the server starts, it checks for a newer version (see Update check and self-update below); the server waits for the check and any confirmed update to finish.
3. It spawns `node <dsh> web` as a child process with no console window, appending the server's output to the server log.
4. Once `127.0.0.1:<port>` accepts connections, the embedded WebView2 control loads the served URL. The system browser opens only through the "Open in Browser" button or the opt-in `openBrowserOnLaunch` setting.
5. Quitting — closing the window, the Quit button, or the window being closed by `taskkill /PID` (WM_CLOSE) — kills the server's process tree (`taskkill /PID <pid> /T /F`), waits for the port to free (up to 6s), and only ever touches the process tree the app itself spawned. Windows has no SIGTERM, so this is a hard terminate rather than the macOS graceful drain; the port is still released.
6. If the app is killed hard (Task Manager, crash), the orphaned server keeps serving; the next launch detects the recorded pid, verifies its command line matches the resolved `dsh`, terminates it, and starts fresh.
7. A second instance refuses to start (named mutex).

## Update check and self-update

Before the server starts, the app checks for a newer DeepSeek Harness version and, when one exists, offers a one-click in-place update. It takes the newest of:

- the latest GitHub Release of each repository in `updateRepos` (default `deepseek-ai/deepseek-harness`); and
- the npm registry (`@deepseek-ai/dsh`), because upstream publishes the webui to npm rather than to GitHub Releases.

The "current" version is read from the resolved `dsh`'s `package.json`. When a newer version is found, a dialog offers **Update now**: it reinstalls `@deepseek-ai/dsh@<version>` into a staging folder and atomically swaps it for the bundled `dsh\` (a non-bundled global install is refreshed with `npm install -g`). The exe never replaces itself, so this keeps working across every future webui release without reinstalling the app. The check runs on a background thread with a bounded timeout; if it fails, times out, or the current version cannot be determined, the app simply starts the server.

Headless diagnostics (exit without opening the UI):

- `DeepSeek Harness.exe --check-update` — print `current`, `latest`, and `source`, then exit.
- `DeepSeek Harness.exe --update [version]` — run the self-update (latest when no version is given), then exit.

## Plan/Act dual-model routing (Cline-style)

The web profile ships the `@deepseek-ai/dsh-model-router` plugin, which picks one of two models based on the session's **plan mode**: while plan mode is active (Cline-style "Plan") every request uses the **reasoning** model; once plan mode is off (Cline-style "Act") every request uses the **execution** model. It applies to every session unless you pick a model explicitly in the composer (an explicit pick wins for that session).

### Default slots

The shipped defaults live in `packages/bundle/web-app/cordis.patch.yml`:

| Mode | Slot | Model |
|---|---|---|
| plan on | reasoning | `deepseek-v4-pro`, `reasoningEffort: high` |
| plan off | execution | `deepseek-v4-flash`, `reasoningEffort: off` |

So the model reads your request and plans the work with `deepseek-v4-pro` while in plan mode, then executes the approved plan with `deepseek-v4-flash` once you leave plan mode — the model stays fixed through each mode and switches cleanly at the plan/execute boundary.

### How to use

- **Plan mode on** (`/plan`, the composer Plan chip, or let the agent call `exit_plan_mode` to present a plan) → requests run on `deepseek-v4-pro`.
- **Plan mode off** (default; or after `exit_plan_mode` is approved / `/plan off` / the Plan chip) → requests run on `deepseek-v4-flash`.

There is nothing to do at launch: start `DeepSeek Harness.exe` and routing is active. You can confirm it from a session's transcript (each model request records `provider` / `model` / `reasoningEffort` in its `request/header` event).

### Auto-routing vs manual model

The composer's model seat is read-only while the session is on Plan/Act auto-routing: it shows the model the router actually serves for the current mode (Plan → `deepseek-v4-pro`, Act → `deepseek-v4-flash`) and is greyed out. A small **Auto / Manual** toggle beside it switches:

- **Auto** (default): the session routes by plan mode (Plan/Act dual model). The model list is disabled.
- **Manual**: the session uses the model you pick from the list; Plan/Act routing is off until you switch back to Auto.

Picking a model while in Manual also turns Auto-routing off for that session. This is driven by host-side `autoRouting` state (per session), so it applies to every workspace without a restart.

### How to customize

Both slots are a user-owned settings section that hot-publishes (an edit reaches the very next request, no restart). Edit `%USERPROFILE%\.dsh\settings.yaml`:

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

- Change either model or its `reasoningEffort` (`off` / `high` / `max`, or omit for the provider default).
- To disable routing and go back to a single model, either delete the whole `agent-model-router` section (falling back to the single `agent-default-model` selection) or set both slots to the same model.
- Per slot the `provider` / `model` pair is required; the route must exist in an adapter you have configured (e.g. `deepseek-official` from the DeepSeek adapter).
- A model you pick in the composer overrides routing for that session.
- A session that never enters plan mode always uses the `execution` slot, so "Act always flash" is the default.

### Where it lives

The plugin is `packages/core/model-router`, wired into the web entry point's per-request model selection in `packages/host/apiproxy/src/api-proxy.ts`, which folds `plan/mode` state via `@deepseek-ai/dsh-plan-mode`. This is a **local fork addition**, not yet in the upstream npm `@deepseek-ai/dsh` package — see the Rebuild note above.

## Configuration

Registry values under `HKCU\Software\DeepSeek Harness` (mirror of the macOS `defaults` domain), overridden by command-line arguments:

| Setting | Registry value / argument | Default | Meaning |
|---|---|---|---|
| `port` | `port` / `-port` | `3080` | Port passed to `dsh web` as `--port` |
| `dshPath` | `dshPath` / `-dshPath` | auto | Explicit path to the dsh `lib/bin.js` |
| `nodePath` | `nodePath` / `-nodePath` | auto | Explicit path to `node.exe` |
| `openBrowserOnLaunch` | `openBrowserOnLaunch` / `-openBrowserOnLaunch` | `0` | Additionally open the system browser when the server is ready (the embedded view always shows) |
| `stateDir` | `stateDir` / `-stateDir` | `%LOCALAPPDATA%\DeepSeek Harness` | Where the launcher keeps its `server.pid`/`app.pid` locks and the server log |
| `checkUpdates` | `checkUpdates` / `-checkUpdates` | `1` | Check for a newer version before starting the server |
| `checkNpm` | `checkNpm` / `-checkNpm` | `1` | Also query the npm registry as a version source |
| `updateRepos` | `updateRepos` / `-updateRepos` | `deepseek-ai/deepseek-harness` | Semicolon-separated `owner/repo` list checked for the latest GitHub Release |

Example — serve on a different port:

```powershell
reg add HKCU\Software\DeepSeek Harness /v port /t REG_DWORD /d 8080
```

## Logs and state

- `%LOCALAPPDATA%\DeepSeek Harness\server.log` — the server's stdout/stderr
- `%LOCALAPPDATA%\DeepSeek Harness\server.pid` / `app.pid` — lock files
- `%USERPROFILE%\.dsh` — the server's own profile, sessions, and settings, untouched by the launcher

Uninstall: quit the app, delete the folder, and remove the registry key `HKCU\Software\DeepSeek Harness` plus `%LOCALAPPDATA%\DeepSeek Harness`.

## Testing

`apps/windows/scripts/test-lifecycle.ps1` launches the exe on a test port with a scratch `DSH_HOME` and verifies both phases end to end: graceful quit frees the port and kills the server's process tree; a hard kill leaves an orphaned server that the next launch reclaims.

## Release

Both platforms are built and attached to the same GitHub Release by `.github/workflows/app-release.yml`; the asset is `DeepSeek-Harness-<version>-windows-x64.zip` (the exe plus the bundled `dsh\` folder when released). See [the macOS README](../macos/README.md#release) for the release flow and the automatic upstream sync.
