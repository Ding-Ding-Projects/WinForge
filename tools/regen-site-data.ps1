<#
.SYNOPSIS
  Regenerate design/winforge-data.js from the REAL WinForge app — no hand-written data.

.DESCRIPTION
  The GitHub Pages documentation site is data-driven by design/winforge-data.js
  (module list, categories, feature counts). This script gets that data straight from
  the live app instead of pretend/hardcoded numbers:

    1. publishes WinForge self-contained (unless -SkipBuild),
    2. runs `WinForge.exe --export-site-data <tmp.json>` — the app dumps its real
       ModuleRegistry + Categories + TweakCatalog (meta / categories / modules),
    3. merges the authored wiki sections (wikiIndex / wiki) from the existing
       design/winforge-data.js (wiki content is docs, not app data),
    4. writes `window.WINFORGE_DATA = {…};` to design/winforge-data.js.

  Simulators (Reactor / FuelFactory / CakeFarm) are untouched — they generate their
  own valid .fuel / .cake files and don't read this data.

.EXAMPLE
  pwsh -File tools/regen-site-data.ps1
#>
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tfm  = 'net11.0-windows10.0.26100.0'
$exe  = Join-Path $root "bin/Debug/$tfm/win-x64/publish/WinForge.exe"
$data = Join-Path $root 'design/winforge-data.js'
$tmp  = Join-Path $env:TEMP 'winforge-sitedata.json'

if (-not $SkipBuild -or -not (Test-Path $exe)) {
  Write-Host 'Publishing WinForge (self-contained)…'
  & dotnet publish (Join-Path $root 'WinForge.csproj') -c Debug -r win-x64 --self-contained true `
      -p:Platform=x64 -p:WindowsAppSDKSelfContained=true | Out-Null
}
if (-not (Test-Path $exe)) { throw "WinForge.exe not found at $exe" }

Write-Host 'Exporting real app data…'
& $exe --export-site-data $tmp | Out-Null
$deadline = (Get-Date).AddSeconds(30)
while (-not (Test-Path $tmp) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
if (-not (Test-Path $tmp)) { throw 'Export did not produce the data file.' }

$real = Get-Content $tmp -Raw | ConvertFrom-Json

# Preserve the authored wiki sections from the existing data file.
$wikiIndex = @(); $wiki = [pscustomobject]@{}; $wikiCount = 0
if (Test-Path $data) {
  $txt = Get-Content $data -Raw
  $json = $txt.Substring($txt.IndexOf('{'))
  $json = $json.Substring(0, $json.LastIndexOf('}') + 1)
  $old = $json | ConvertFrom-Json
  if ($old.wikiIndex) { $wikiIndex = $old.wikiIndex; $wikiCount = $old.wikiIndex.Count }
  if ($old.wiki) { $wiki = $old.wiki }
}

$merged = [ordered]@{
  meta       = [ordered]@{
    totalFeatures = $real.meta.totalFeatures
    categoryCount = $real.meta.categoryCount
    moduleCount   = $real.meta.moduleCount
    wikiCount     = $wikiCount
    generatedUtc  = $real.meta.generatedUtc
  }
  categories = $real.categories
  modules    = $real.modules
  wikiIndex  = $wikiIndex
  wiki       = $wiki
}

$out = 'window.WINFORGE_DATA = ' + ($merged | ConvertTo-Json -Depth 40 -Compress) + ';' + "`n"
Set-Content -Path $data -Value $out -Encoding UTF8 -NoNewline
Write-Host "Wrote $data — $($real.meta.moduleCount) modules, $($real.meta.categoryCount) categories, $($real.meta.totalFeatures) features, $wikiCount wiki pages."
