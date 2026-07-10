<#
.SYNOPSIS
  Manual Testing (Debug) Build Workflow (ui-build-pipeline.md §2.5): builds
  /src and /ui and stages both binaries plus all required runtime
  dependencies into target/manual-testing/, without running the static
  analysis, dependency audits, tests, or MSIX packaging that the full Local
  (§2.1-§2.4) / CI (§3) build workflows require.

.DESCRIPTION
  This script exists so developers can manually exercise the app after a
  change without going through the complete CI-equivalent build. It only
  compiles and stages output; it does not lint, audit, test, or package.

  Steps:
    - cargo build (Debug by default; -Configuration Release for a release
      build) for /src, producing llx.exe
    - dotnet publish ui/LimelightX.UI.csproj -r <rid> --self-contained false
      (same configuration) for /ui, producing LimelightX.UI.exe plus all
      managed dependencies (DLLs, .deps.json, .runtimeconfig.json). <rid>
      defaults to the host's own architecture (win-arm64 on an ARM64 dev
      machine, win-x64 elsewhere) rather than being hardcoded, since this
      repo's native Tree-sitter DLLs (ui/native/*.dll) are currently
      ARM64-only (CLAUDE.md §3.5) - publishing win-x64 on an ARM64 dev
      machine would ship an architecture mismatch that throws the moment any
      Tree-sitter-backed editor feature runs. Override with -Rid if needed.
    - Both are staged together into target/manual-testing/ so the app can be
      run from a single folder

.PARAMETER Configuration
  Build configuration to use for both components: "Debug" (default) or
  "Release".

.PARAMETER Rid
  .NET RuntimeIdentifier to publish /ui for: "win-x64" or "win-arm64".
  Defaults to the host machine's own architecture.
#>
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Rid = $(
        if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) { "win-arm64" }
        else { "win-x64" }
    )
)

$ErrorActionPreference = "Stop"

$outDir = "$RepoRoot/target/manual-testing"

Write-Host "== Manual testing build ($Configuration, $Rid) =="

# --- /src (Rust) ---
Write-Host "Building /src (cargo build, $Configuration)..."
Push-Location $RepoRoot
try {
    if ($Configuration -eq "Release") {
        cargo build --release
        if ($LASTEXITCODE -ne 0) { throw "cargo build --release failed with exit code $LASTEXITCODE." }
        $cargoOutDir = "$RepoRoot/target/release"
    }
    else {
        cargo build
        if ($LASTEXITCODE -ne 0) { throw "cargo build failed with exit code $LASTEXITCODE." }
        $cargoOutDir = "$RepoRoot/target/debug"
    }
}
finally {
    Pop-Location
}

$llxExePath = "$cargoOutDir/llx.exe"
if (-not (Test-Path $llxExePath)) {
    throw "llx.exe not found at '$llxExePath' after cargo build."
}

# --- /ui (.NET) ---
Write-Host "Building /ui (dotnet publish, $Configuration)..."
if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outDir | Out-Null

dotnet publish "$RepoRoot/ui/LimelightX.UI.csproj" -c $Configuration -r $Rid --self-contained false -o $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

# dotnet publish already staged LimelightX.UI.exe plus every managed
# dependency it needs straight into $outDir; llx.exe is self-contained
# (statically-linked Rust binary, no extra DLLs on Windows) so a plain copy
# alongside it is all /src needs.
Copy-Item $llxExePath "$outDir/llx.exe" -Force

Write-Host ""
Write-Host "Manual testing build complete: $outDir"
Write-Host "  UI:  $outDir\LimelightX.UI.exe"
Write-Host "  CLI: $outDir\llx.exe (e.g. llx.exe run <file>, llx.exe serve)"
