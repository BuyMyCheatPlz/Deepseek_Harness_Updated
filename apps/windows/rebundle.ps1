# Re-apply the local dual-model-router patch to the exe's bundled dsh.
#
# Run this AFTER the two build steps:
#   1. pnpm run build:lib:host                     (builds the fork's packages)
#   2. apps/windows/build.ps1 -BundleDsh            (compiles the exe + npm-installs @deepseek-ai/dsh)
#
# build.ps1 -BundleDsh installs the UPSTREAM npm package, which does not contain
# the model-router changes in this fork. This script overlays the locally-built
# outputs of the changed packages and installs the new model-router package, so
# the exe boots the fork's routing behavior. Re-run it after every rebuild.
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$buildDsh = Join-Path $repo 'apps\windows\build\dsh\node_modules\@deepseek-ai'
$builtModelRouter = Join-Path $repo 'packages\core\model-router\lib\index.js'
if (-not (Test-Path $builtModelRouter)) {
  throw "packages/core/model-router is not built; run `pnpm run build:lib:host` first"
}

# 1. Overlay the built runtime outputs of the changed packages.
Copy-Item (Join-Path $repo 'packages\core\agent\lib\index.js')          (Join-Path $buildDsh 'dsh-agent\lib\index.js') -Force
Copy-Item (Join-Path $repo 'packages\host\apiproxy\lib\index.js')       (Join-Path $buildDsh 'dsh-host-apiproxy\lib\index.js') -Force
Copy-Item (Join-Path $repo 'packages\bundle\headless\lib\index.js')     (Join-Path $buildDsh 'dsh-headless\lib\index.js') -Force
Copy-Item (Join-Path $repo 'packages\bundle\web-app\cordis.patch.yml')  (Join-Path $buildDsh 'dsh-web-app\cordis.patch.yml') -Force

# 1b. Overlay the fork's browser bundles too. The wire client (dsh-client-connection)
#     inlines the fetch carrier + the sessions value schemas, so it MUST be the
#     fork build to expose session.setAutoRouting / the autoRouting response field
#     (a stale upstream bundle drops the method and shelves the toggle into
#     TypeError). The two UI bundles carry the Auto/Manual toggle UI.
Copy-Item (Join-Path $repo 'packages\client\connection\lib\client.js')      (Join-Path $buildDsh 'dsh-client-connection\lib\client.js') -Force
if (Test-Path (Join-Path $repo 'packages\client\connection\lib\client.js.map')) {
  Copy-Item (Join-Path $repo 'packages\client\connection\lib\client.js.map') (Join-Path $buildDsh 'dsh-client-connection\lib\client.js.map') -Force
}
Copy-Item (Join-Path $repo 'packages\client\ui-model-selection\lib\client.js') (Join-Path $buildDsh 'dsh-client-ui-model-selection\lib\client.js') -Force
if (Test-Path (Join-Path $repo 'packages\client\ui-model-selection\lib\client.js.map')) {
  Copy-Item (Join-Path $repo 'packages\client\ui-model-selection\lib\client.js.map') (Join-Path $buildDsh 'dsh-client-ui-model-selection\lib\client.js.map') -Force
}
Copy-Item (Join-Path $repo 'packages\client\ui-plan\lib\client.js')          (Join-Path $buildDsh 'dsh-client-ui-plan\lib\client.js') -Force
if (Test-Path (Join-Path $repo 'packages\client\ui-plan\lib\client.js.map')) {
  Copy-Item (Join-Path $repo 'packages\client\ui-plan\lib\client.js.map')    (Join-Path $buildDsh 'dsh-client-ui-plan\lib\client.js.map') -Force
}

# 2. Install the new model-router package beside its siblings.
$mrSrc = Join-Path $repo 'packages\core\model-router'
$mrDst = Join-Path $buildDsh 'dsh-model-router'
New-Item -ItemType Directory -Force $mrDst | Out-Null
Copy-Item (Join-Path $mrSrc 'package.json') $mrDst -Force
Copy-Item (Join-Path $mrSrc 'lib') (Join-Path $mrDst 'lib') -Recurse -Force

# 3. Link model-router into the profile's flat module fallback so the loader
#    resolves it. Symbolic links need Developer Mode on Windows; a junction
#    (which Node resolves the same way) needs no privilege, so fall back to it.
$link = Join-Path $env:USERPROFILE '.dsh\profiles\node_modules\@deepseek-ai\dsh-model-router'
if (-not (Test-Path $link)) {
  try {
    New-Item -ItemType SymbolicLink -Path $link -Target $mrDst -ErrorAction Stop | Out-Null
    Write-Host "linked model-router (symbolic link)"
  } catch {
    New-Item -ItemType Junction -Path $link -Target $mrDst -ErrorAction Stop | Out-Null
    Write-Host "linked model-router (junction)"
  }
}

Write-Host "rebundle complete"
