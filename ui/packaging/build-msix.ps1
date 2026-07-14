<#
.SYNOPSIS
  Local/CI Package Stage (ui-build-pipeline.md §2.4, §3.5): stages
  LimelightX.exe + llx.exe + AppxManifest.xml into a layout directory and
  packs it into an unsigned .msix via the Windows SDK's makeappx.exe.

.DESCRIPTION
  Assumes both binaries are already built:
    - dotnet build/publish for /ui (LimelightX.UI)
    - cargo build --release for /src (llx.exe)
  This script only stages and packs; it does not build either component,
  keeping it usable identically from a developer machine or CI.

.PARAMETER Rid
  Runtime identifier to package: "win-x64" or "win-arm64". Determines which
  publish output folder is staged and which architecture's makeappx.exe is
  used to pack it.

.PARAMETER LlxExePath
  Path to the built llx.exe (default: cargo's release output).

.PARAMETER OutputMsixPath
  Where to write the resulting .msix.
#>
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot/../..").Path,
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Rid = "win-x64",
    [string]$LlxExePath = "$RepoRoot/target/release/llx.exe",
    [string]$OutputMsixPath = "$RepoRoot/ui/packaging/out/LimelightX.msix"
)

$ErrorActionPreference = "Stop"

$packagingDir = "$RepoRoot/ui/packaging"
$layoutDir = "$packagingDir/layout"
# ui-build-pipeline.md §7.1: publishing with "-r <rid>" inserts a RID segment
# into the publish output path. CI runs this once per matrix leg (-Rid
# win-x64 and -Rid win-arm64); the default here is for ad hoc local use.
$publishDir = "$RepoRoot/ui/bin/$Configuration/net8.0-windows/$Rid/publish"

if (-not (Test-Path $LlxExePath)) {
    throw "llx.exe not found at '$LlxExePath' - build /src first (cargo build --release)."
}
if (-not (Test-Path $publishDir)) {
    throw "LimelightX.UI publish output not found at '$publishDir' - run 'dotnet publish -c $Configuration' in /ui first."
}

if (Test-Path $layoutDir) {
    Remove-Item $layoutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $layoutDir | Out-Null
New-Item -ItemType Directory -Path "$layoutDir/assets" | Out-Null

Copy-Item "$publishDir/*" $layoutDir -Recurse -Force
Copy-Item $LlxExePath "$layoutDir/llx.exe" -Force
Copy-Item "$packagingDir/AppxManifest.xml" "$layoutDir/AppxManifest.xml" -Force
Copy-Item "$packagingDir/assets/*.png" "$layoutDir/assets/" -Force

# ui-build-pipeline.md §4.1: LimelightX.UI.csproj's <Version> is the single
# source of truth - stamp it into the *staged* manifest copy (never the
# committed source file) so the shipped MSIX's version always matches the
# assembly version baked into LimelightX.UI.dll by the publish that already
# ran. Read via a plain string match rather than a full MSBuild-project XML
# parse, since the csproj is not itself well-formed against the AppxManifest
# schema and doesn't need a heavier parser for one element.
$csprojPath = "$RepoRoot/ui/LimelightX.UI.csproj"
$versionMatch = Select-String -Path $csprojPath -Pattern "<Version>(.*?)</Version>" | Select-Object -First 1
if (-not $versionMatch) {
    throw "LimelightX.UI.csproj has no <Version> element - cannot stamp AppxManifest.xml."
}
$version = $versionMatch.Matches[0].Groups[1].Value

$manifestPath = "$layoutDir/AppxManifest.xml"
[xml]$manifestXml = Get-Content $manifestPath
$manifestXml.Package.Identity.Version = $version
$manifestXml.Save($manifestPath)

# ui-architecture.md §3 requires the assembly name to stay "LimelightX.UI"
# (so avares:// resource URIs baked into the compiled XAML keep resolving),
# but the shipped executable must be named "LimelightX.exe". Renaming the
# published apphost file (not rebuilding it) is safe: the apphost's reference
# to its companion .dll is a fixed relative path embedded at publish time,
# independent of the exe's own file name - verified by launching a renamed
# copy and confirming the window still opens correctly.
if (-not (Test-Path "$layoutDir/LimelightX.UI.exe")) {
    throw "Staged layout is missing LimelightX.UI.exe - check the publish output."
}
Move-Item "$layoutDir/LimelightX.UI.exe" "$layoutDir/LimelightX.exe" -Force

# Use the makeappx.exe matching -Rid's architecture (native tool, not x64
# emulation) so a native ARM64 machine packages with its own SDK tool.
$msixArch = if ($Rid -eq "win-arm64") { "arm64" } else { "x64" }
$makeAppx = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\$msixArch\\" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $makeAppx) {
    throw "makeappx.exe not found under Windows Kits 10 - install the Windows SDK."
}

$outputDir = Split-Path $OutputMsixPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}
if (Test-Path $OutputMsixPath) {
    Remove-Item $OutputMsixPath -Force
}

& $makeAppx.FullName pack /d $layoutDir /p $OutputMsixPath /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed with exit code $LASTEXITCODE."
}

Write-Host "MSIX package created at $OutputMsixPath"
