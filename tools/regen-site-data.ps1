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
param(
  [switch]$SkipBuild,
  [string]$ExePath    # use a prebuilt WinForge.exe (CI passes the published Release exe)
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tfm  = 'net11.0-windows10.0.26100.0'
$exe  = if ($ExePath) { $ExePath } else { Join-Path $root "bin/x64/Debug/$tfm/win-x64/publish/WinForge.exe" }
$data = Join-Path $root 'design/winforge-data.js'
$tmp  = Join-Path $env:TEMP 'winforge-sitedata.json'

if (-not $ExePath -and (-not $SkipBuild -or -not (Test-Path $exe))) {
  Write-Host 'Publishing WinForge (self-contained)…'
  & dotnet publish (Join-Path $root 'WinForge.csproj') -c Debug -r win-x64 --self-contained true `
      -p:Platform=x64 -p:WindowsAppSDKSelfContained=true | Out-Null
}
if (-not (Test-Path $exe)) { throw "WinForge.exe not found at $exe" }

if (Test-Path $tmp) { Remove-Item $tmp -Force }
Write-Host 'Exporting real app data…'
# Run headless with a hard timeout so a stuck UI start can never hang CI.
$p = Start-Process -FilePath $exe -ArgumentList '--export-site-data', $tmp -PassThru
if (-not $p.WaitForExit(60000)) { try { $p.Kill($true) } catch {} }
$deadline = (Get-Date).AddSeconds(20)
while (-not (Test-Path $tmp) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
if (-not (Test-Path $tmp)) { throw 'Export did not produce the data file.' }

$jsonSerializer = $null
try {
  # Windows PowerShell 5's ConvertTo-Json is extremely slow and memory-hungry for
  # the thousands of wiki pages below. JavaScriptSerializer keeps the Desktop
  # edition fast; PowerShell 7 falls back to its improved native JSON cmdlets.
  Add-Type -AssemblyName System.Web.Extensions -ErrorAction Stop
  $jsonSerializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
  $jsonSerializer.MaxJsonLength = [int]::MaxValue
  $jsonSerializer.RecursionLimit = 100
} catch { }

$realJson = [System.IO.File]::ReadAllText($tmp, [System.Text.Encoding]::UTF8)
$real = if ($jsonSerializer) { $jsonSerializer.DeserializeObject($realJson) } else { $realJson | ConvertFrom-Json }
$realMeta = if ($real -is [System.Collections.IDictionary]) { $real['meta'] } else { $real.meta }
$realCategories = if ($real -is [System.Collections.IDictionary]) { $real['categories'] } else { $real.categories }
$realModules = if ($real -is [System.Collections.IDictionary]) { $real['modules'] } else { $real.modules }
function Get-JsonField($source, [string]$name) {
  if ($source -is [System.Collections.IDictionary]) { return $source[$name] }
  return $source.$name
}

# Read the canonical wiki Markdown from docs/wiki so GitHub Pages never preserves stale
# embedded wiki text from an older winforge-data.js.
$wikiDir = Join-Path $root 'docs/wiki'
$wikiIndex = [System.Collections.Generic.List[object]]::new()
$wikiMap = [ordered]@{}
if (Test-Path $wikiDir) {
  $wikiFiles = Get-ChildItem -LiteralPath $wikiDir -Filter '*.md' -File -Recurse |
    Where-Object { $_.Name -ne 'README.md' } |
    Sort-Object FullName
  foreach ($file in $wikiFiles) {
    $relative = $file.FullName.Substring($wikiDir.TrimEnd('\').Length + 1).Replace('\', '/')
    $relativeNoExt = [System.IO.Path]::ChangeExtension($relative, $null).TrimEnd('.')
    $slug = $relativeNoExt -replace '/', '--'
    # File.ReadAllText returns a plain String. Get-Content adds PowerShell ETS
    # metadata that legacy serializers try to walk as a circular object graph.
    $body = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    $title = $slug
    if ($body -match '(?m)^#\s+(.+?)\s*$') { $title = $Matches[1].Trim() }
    $entry = [ordered]@{ slug = $slug; title = $title; path = $relative }
    if ($body -match '(?m)^\| Tag .*?\| <code>([^<]+)</code> \|') { $entry['moduleTag'] = $Matches[1] }
    [void]$wikiIndex.Add($entry)
    $wikiMap[$slug] = $body
  }
}
$wikiCount = $wikiIndex.Count

$merged = [ordered]@{
  meta       = [ordered]@{
    totalFeatures = Get-JsonField $realMeta 'totalFeatures'
    tweakFeatureCount = Get-JsonField $realMeta 'tweakFeatureCount'
    categoryCount = Get-JsonField $realMeta 'categoryCount'
    moduleCount   = Get-JsonField $realMeta 'moduleCount'
    wikiCount     = $wikiCount
  }
  categories = $realCategories
  modules    = $realModules
  wikiIndex  = $wikiIndex
  wiki       = $wikiMap
}

$json = if ($jsonSerializer) { $jsonSerializer.Serialize($merged) } else { $merged | ConvertTo-Json -Depth 40 -Compress }
$out = 'window.WINFORGE_DATA = ' + $json + ';' + "`n"
Set-Content -Path $data -Value $out -Encoding UTF8 -NoNewline
Write-Host "Wrote $data — $(Get-JsonField $realMeta 'moduleCount') modules, $(Get-JsonField $realMeta 'categoryCount') categories, $(Get-JsonField $realMeta 'totalFeatures') features, $wikiCount wiki pages."
