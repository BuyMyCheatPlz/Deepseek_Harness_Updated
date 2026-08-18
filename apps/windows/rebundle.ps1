# Re-apply the local fork additions to the exe's bundled dsh.
#
# Run this AFTER the two build steps:
#   1. pnpm run build:lib:host                     (builds the fork's packages)
#   2. apps/windows/build.ps1 -BundleDsh            (compiles the exe + npm-installs @deepseek-ai/dsh)
#
# build.ps1 -BundleDsh installs the UPSTREAM npm package, which does not contain
# this fork's Plan/Act model router, the Auto/Manual model toggle, or the
# plan-mode tool guards. This script overlays the locally-built outputs of the
# changed packages AND stashes them under overlay\dsh beside the exe. The
# launcher re-applies overlay\dsh into dsh\ on every start and after every
# self-update, so an upstream update can never wipe the fork's additions.
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$buildDsh = Join-Path $repo 'apps\windows\build\dsh\node_modules\@deepseek-ai'
$overlay = Join-Path $repo 'apps\windows\build\overlay\dsh\node_modules\@deepseek-ai'
$builtModelRouter = Join-Path $repo 'packages\core\model-router\lib\index.js'
if (-not (Test-Path $builtModelRouter)) {
  throw "packages/core/model-router is not built; run `pnpm run build:lib:host` first"
}

# Copy one fork build output into BOTH the live bundled dsh\ and the persistent
# overlay\dsh stash. `src` is the source path; `rel` is 'dsh-<pkg>\<subpath>'.
function Stage-OverlayFile([string]$src, [string]$rel) {
  Copy-Item $src (Join-Path $buildDsh $rel) -Force
  $dst = Join-Path $overlay $rel
  New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
  Copy-Item $src $dst -Force
  Write-Host "overlaid $rel"
}

# 1. Overlay the built runtime outputs of the changed host packages.
Stage-OverlayFile (Join-Path $repo 'packages\core\agent\lib\index.js')            'dsh-agent\lib\index.js'
Stage-OverlayFile (Join-Path $repo 'packages\host\apiproxy\lib\index.js')         'dsh-host-apiproxy\lib\index.js'
Stage-OverlayFile (Join-Path $repo 'packages\bundle\headless\lib\index.js')       'dsh-headless\lib\index.js'
Stage-OverlayFile (Join-Path $repo 'packages\bundle\web-app\cordis.patch.yml')    'dsh-web-app\cordis.patch.yml'
# The web-app manifest declares @deepseek-ai/dsh-model-router as a dependency,
# which is what makes the boot-time module-fallback heal link it into any
# $DSH_HOME/profiles/node_modules. Stash it so a fresh/scratch DSH_HOME (CI
# lifecycle test) resolves model-router and `dsh web` boots.
Stage-OverlayFile (Join-Path $repo 'packages\bundle\web-app\package.json')        'dsh-web-app\package.json'

# Plan-mode enforcement lives in the mutating tool packages (write / edit /
# pwsh). Overlay their fork builds too, or the installed exe keeps the upstream
# tool code that only suggests (never enforces) plan mode.
Stage-OverlayFile (Join-Path $repo 'packages\fs\tool-fs\lib\index.js')                    'dsh-tool-fs\lib\index.js'
Stage-OverlayFile (Join-Path $repo 'packages\fs\tool-str-replace-editor\lib\index.js')    'dsh-tool-str-replace-editor\lib\index.js'
Stage-OverlayFile (Join-Path $repo 'packages\shell\tool-pwsh\lib\index.js')               'dsh-tool-pwsh\lib\index.js'

# 1b. Overlay the fork's browser bundles. The wire client (dsh-client-connection)
#     inlines the fetch carrier + the sessions value schemas, so it MUST be the
#     fork build to expose session.setAutoRouting / the autoRouting response
#     field. The two UI bundles carry the Auto/Manual toggle UI.
Stage-OverlayFile (Join-Path $repo 'packages\client\connection\lib\client.js')   'dsh-client-connection\lib\client.js'
if (Test-Path (Join-Path $repo 'packages\client\connection\lib\client.js.map')) {
  Stage-OverlayFile (Join-Path $repo 'packages\client\connection\lib\client.js.map') 'dsh-client-connection\lib\client.js.map'
}
Stage-OverlayFile (Join-Path $repo 'packages\client\ui-model-selection\lib\client.js') 'dsh-client-ui-model-selection\lib\client.js'
if (Test-Path (Join-Path $repo 'packages\client\ui-model-selection\lib\client.js.map')) {
  Stage-OverlayFile (Join-Path $repo 'packages\client\ui-model-selection\lib\client.js.map') 'dsh-client-ui-model-selection\lib\client.js.map'
}
Stage-OverlayFile (Join-Path $repo 'packages\client\ui-plan\lib\client.js')       'dsh-client-ui-plan\lib\client.js'
if (Test-Path (Join-Path $repo 'packages\client\ui-plan\lib\client.js.map')) {
  Stage-OverlayFile (Join-Path $repo 'packages\client\ui-plan\lib\client.js.map') 'dsh-client-ui-plan\lib\client.js.map'
}

# 2. Install the new model-router package beside its siblings (full package),
#    into BOTH destinations.
$mrSrc = Join-Path $repo 'packages\core\model-router'
foreach ($dst in @($buildDsh, $overlay)) {
  $mrDst = Join-Path $dst 'dsh-model-router'
  New-Item -ItemType Directory -Force $mrDst | Out-Null
  Copy-Item (Join-Path $mrSrc 'package.json') $mrDst -Force
  Copy-Item (Join-Path $mrSrc 'lib') (Join-Path $mrDst 'lib') -Recurse -Force
}

# 3. Link model-router into the profile's flat module fallback so the loader
#    resolves it. Symbolic links need Developer Mode on Windows; a junction
#    (which Node resolves the same way) needs no privilege, so fall back to it.
$link = Join-Path $env:USERPROFILE '.dsh\profiles\node_modules\@deepseek-ai\dsh-model-router'
if (-not (Test-Path $link)) {
  try {
    New-Item -ItemType SymbolicLink -Path $link -Target (Join-Path $buildDsh 'dsh-model-router') -ErrorAction Stop | Out-Null
    Write-Host "linked model-router (symbolic link)"
  } catch {
    New-Item -ItemType Junction -Path $link -Target (Join-Path $buildDsh 'dsh-model-router') -ErrorAction Stop | Out-Null
    Write-Host "linked model-router (junction)"
  }
}

Write-Host "rebundle complete (live dsh + overlay stash)"
