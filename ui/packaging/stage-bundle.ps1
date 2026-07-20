<#
.SYNOPSIS
  Local/CI Package Stage (ui-build-pipeline.md §2.4, §3.5): stages
  LimelightX.exe + llx.exe + all managed/native dependencies into a portable
  layout directory - the exact contents shipped as the per-architecture zip
  bundle (ui-deployment.md §2).

.DESCRIPTION
  Assumes both binaries are already built:
    - dotnet build/publish for /ui (LimelightX.UI)
    - cargo build --release for /src (llx.exe)
  This script only stages; it does not build, zip, or sign anything, keeping
  it usable identically from a developer machine or CI (CI zips the staged
  output itself when assembling GitHub Release assets).

  There is no installer, manifest, or packing step - the staged directory
  *is* the distribution artifact. The app is deployed by extracting the zip
  built from this directory's contents to any folder and running
  LimelightX.exe from there; it is removed by deleting that folder.

.PARAMETER Rid
  Runtime identifier to stage: "win-x64" or "win-arm64". Determines which
  publish output folder is staged.

.PARAMETER LlxExePath
  Path to the built llx.exe (default: cargo's release output).

.PARAMETER OutputDir
  Where to stage the bundle contents.
#>
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot/../..").Path,
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Rid = "win-x64",
    [string]$LlxExePath = "$RepoRoot/target/release/llx.exe",
    [string]$OutputDir = "$RepoRoot/ui/packaging/layout"
)

$ErrorActionPreference = "Stop"

# ui-build-pipeline.md §7.1: publishing with "-r <rid>" inserts a RID segment
# into the publish output path. CI runs this once per matrix leg (-Rid
# win-x64 and -Rid win-arm64); the default here is for ad hoc local use.
$publishDir = "$RepoRoot/ui/bin/$Configuration/net8.0-windows/$Rid/publish"

if (-not (Test-Path $LlxExePath)) {
    throw "llx.exe not found at '$LlxExePath' - build /src first (cargo build --release)."
}
if (-not (Test-Path $publishDir)) {
    throw "LimelightX.UI publish output not found at '$publishDir' - run 'dotnet publish -c $Configuration -r $Rid --self-contained false' in /ui first."
}

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

Copy-Item "$publishDir/*" $OutputDir -Recurse -Force
Copy-Item $LlxExePath "$OutputDir/llx.exe" -Force

# ui-architecture.md §3 requires the assembly name to stay "LimelightX.UI"
# (so avares:// resource URIs baked into the compiled XAML keep resolving),
# but the shipped executable must be named "LimelightX.exe". Renaming the
# published apphost file (not rebuilding it) is safe: the apphost's reference
# to its companion .dll is a fixed relative path embedded at publish time,
# independent of the exe's own file name - verified by launching a renamed
# copy and confirming the window still opens correctly.
if (-not (Test-Path "$OutputDir/LimelightX.UI.exe")) {
    throw "Staged layout is missing LimelightX.UI.exe - check the publish output."
}
Move-Item "$OutputDir/LimelightX.UI.exe" "$OutputDir/LimelightX.exe" -Force

Write-Host "Portable bundle staged at $OutputDir"
Write-Host "  UI:  $OutputDir\LimelightX.exe"
Write-Host "  CLI: $OutputDir\llx.exe"
