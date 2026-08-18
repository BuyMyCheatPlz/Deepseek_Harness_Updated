# Build the DeepSeek Harness Windows installer (Setup.exe).
#
# Produces a single self-contained installer that bundles:
#   - apps/windows/build/DeepSeek Harness.exe + the three WebView2 DLLs
#   - the bundled web runtime (build\dsh, already rebundled with the fork's
#     Plan/Act routing and Auto/Manual toggle via apps/windows/rebundle.ps1)
#   - a portable Node.js runtime (build\runtime\node) so the user needs no
#     system Node.js or web toolchain
#
# This script EXPECTS apps/windows/build to already be built and rebundled:
#   npx --yes pnpm@11.7.0 run build:lib:host
#   npx --yes pnpm@11.7.0 run build:lib:client
#   powershell -ExecutionPolicy Bypass -File apps/windows/build.ps1 -BundleDsh -DshVersion <version>
#   powershell -ExecutionPolicy Bypass -File apps/windows/rebundle.ps1
# It only supplies the portable Node runtime and compiles installer.iss, so the
# fork overlay baked into build\dsh is preserved (never lost to a re-install).
#
# Usage: build-installer.ps1 [-DshVersion <version>] [-NodeVersion <version>]
#   -NodeVersion  portable Node.js version (default 22.19.0; must satisfy the
#                 project's engines ^22.19.0 || >=24.0.0).
#
# Prereqs: Inno Setup 6 (ISCC.exe) on PATH, or set ISCC_PATH to the compiler.
param(
  [string]$NodeVersion = '22.19.0'
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $scriptDir 'build'

# Sanity: the web runtime must already be built + rebundled.
$binJs = Join-Path $outDir 'dsh\node_modules\@deepseek-ai\dsh\lib\bin.js'
if (-not (Test-Path $binJs)) {
  throw "build\dsh is missing; run apps/windows/build.ps1 -BundleDsh first (see the header comment)."
}
$exe = Join-Path $outDir 'DeepSeek Harness.exe'
if (-not (Test-Path $exe)) { throw "build\DeepSeek Harness.exe is missing; run apps/windows/build.ps1 first." }

# (Re)generate the fork overlay stash (overlay\dsh) from the built packages, so
# the installer ships the self-healing overlay the launcher re-applies after
# every upstream update. Requires the fork packages built (build:lib:host/client).
& (Join-Path $scriptDir 'rebundle.ps1')
if (-not (Test-Path (Join-Path $outDir 'app.ico'))) {
  throw "build\app.ico is missing; build.ps1 copies it from the repo-root icon."
}

$displayVersion = ""
try {
  # Prefer the installed dsh version stamped in the bundled web runtime.
  $pkg = Get-Content (Join-Path $outDir 'dsh\node_modules\@deepseek-ai\dsh\package.json') -Raw | ConvertFrom-Json
  $displayVersion = $pkg.version
} catch { }
if (-not $displayVersion) { $displayVersion = '0.1.0-rc.6' }
$displayVersion = $displayVersion -replace '^dsh-v', ''

# --- Portable Node.js runtime -------------------------------------------------
$nodeDir = Join-Path $outDir 'runtime\node'
$nodeZipCache = Join-Path $scriptDir "node-v$NodeVersion-win-x64.zip"
$nodeUrl = "https://nodejs.org/dist/v$NodeVersion/node-v$NodeVersion-win-x64.zip"

if (-not (Test-Path (Join-Path $nodeDir 'node.exe'))) {
  Write-Host "==> Preparing portable Node v$NodeVersion"
  if (-not (Test-Path $nodeZipCache)) {
    Write-Host "==> Downloading $nodeUrl"
    Invoke-WebRequest $nodeUrl -OutFile $nodeZipCache
  }
  $extract = Join-Path $env:TEMP "node-v$NodeVersion-win-x64"
  if (Test-Path $extract) { Remove-Item -Recurse -Force $extract }
  Expand-Archive $nodeZipCache $extract
  New-Item -ItemType Directory -Force $nodeDir | Out-Null
  $src = Join-Path $extract "node-v$NodeVersion-win-x64"
  Copy-Item (Join-Path $src '*') $nodeDir -Recurse -Force
}

# --- Compile with Inno Setup --------------------------------------------------
$iscc = $env:ISCC_PATH
if (-not $iscc) {
  $iscc = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
  $iscc = if ($iscc) { $iscc.Source } else { $null }
}
if (-not $iscc) {
  throw 'Inno Setup ISCC.exe not found. Install Inno Setup 6 (or set ISCC_PATH).'
}

Write-Host "==> Compiling the installer (ISCC) for version $displayVersion"
# Run ISCC from the script directory so the installer.iss relative paths
# (`Source: build\...`, `OutputDir=build`) resolve against apps\windows
# regardless of the caller's working directory.
Push-Location $scriptDir
try {
  & $iscc "/DMyAppVersion=$displayVersion" 'installer.iss'
  if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }
} finally {
  Pop-Location
}

Write-Host "==> Done: $outDir\DeepSeek-Harness-Setup-$displayVersion.exe"
