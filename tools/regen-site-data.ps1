<#
.SYNOPSIS
  Regenerate design/winforge-data.js from the REAL WinForge app - no hand-written data.

.DESCRIPTION
  The GitHub Pages documentation site is data-driven by design/winforge-data.js
  (module list, categories, feature counts). This script gets that data straight from
  the live app instead of pretend/hardcoded numbers:

    1. publishes WinForge self-contained (unless -SkipBuild),
    2. runs `WinForge.exe --export-site-data <tmp.json>` - the app dumps its real
       ModuleRegistry + Categories + TweakCatalog (meta / categories / modules),
    3. reads the canonical authored wiki sections (wikiIndex / wiki) from
       docs/wiki (wiki content is docs, not app data),
    4. writes `window.WINFORGE_DATA = {...};` to design/winforge-data.js.

  Simulators (Reactor / FuelFactory / CakeFarm) are untouched - they generate their
  own valid .fuel / .cake files and don't read this data.

.EXAMPLE
  pwsh -File tools/regen-site-data.ps1
#>
param(
  [switch]$SkipBuild,
  [string]$ExePath    # use a prebuilt WinForge.exe (CI passes the published Release exe)
)

function ConvertTo-PlainJsonValue {
  <#
  Convert PowerShell wrapper objects to ordinary dictionaries/lists before the
  framework JSON serializer sees them. This avoids PSObject method graphs and
  keeps the large generated wiki payload practical in Windows PowerShell 5.1.
  #>
  param([object]$Value)

  if ($null -eq $Value) {
    return $Value
  }

  # Get-Content -Raw can carry PowerShell-added members on the string wrapper.
  # Materialize a plain string so JavaScriptSerializer never reflects that wrapper.
  if ($Value -is [string]) {
    return $Value.Substring(0, $Value.Length)
  }

  if ($Value -is [ValueType]) {
    return $Value
  }

  if ($Value -is [System.Collections.IDictionary]) {
    $map = [ordered]@{}
    foreach ($key in $Value.Keys) {
      $map[[string]$key] = ConvertTo-PlainJsonValue -Value $($Value[$key])
    }
    return $map
  }

  if ($Value -is [System.Management.Automation.PSCustomObject]) {
    $map = [ordered]@{}
    foreach ($property in $Value.PSObject.Properties) {
      $map[$property.Name] = ConvertTo-PlainJsonValue -Value $property.Value
    }
    return $map
  }

  if ($Value -is [System.Collections.IEnumerable]) {
    $items = New-Object System.Collections.ArrayList
    foreach ($item in $Value) {
      [void]$items.Add((ConvertTo-PlainJsonValue -Value $item))
    }
    return $items
  }

  return $Value
}

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tfm  = 'net11.0-windows10.0.26100.0'
$exe  = if ($ExePath) { $ExePath } else { Join-Path $root "bin/x64/Debug/$tfm/win-x64/publish/WinForge.exe" }
$data = Join-Path $root 'design/winforge-data.js'
$tmp  = Join-Path $env:TEMP 'winforge-sitedata.json'

if (-not $ExePath -and (-not $SkipBuild -or -not (Test-Path $exe))) {
  Write-Host 'Publishing WinForge (self-contained)...'
  & dotnet publish (Join-Path $root 'WinForge.csproj') -c Debug -r win-x64 --self-contained true `
      -p:Platform=x64 -p:WindowsAppSDKSelfContained=true | Out-Null
}
if (-not (Test-Path $exe)) { throw "WinForge.exe not found at $exe" }

if (Test-Path $tmp) { Remove-Item $tmp -Force }
Write-Host 'Exporting real app data...'
# Run headless with a hard timeout so a stuck UI start can never hang CI.
$p = Start-Process -FilePath $exe -ArgumentList '--export-site-data', $tmp -PassThru
if (-not $p.WaitForExit(60000)) { try { $p.Kill($true) } catch {} }
$deadline = (Get-Date).AddSeconds(20)
while (-not (Test-Path $tmp) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
if (-not (Test-Path $tmp)) { throw 'Export did not produce the data file.' }

$real = Get-Content $tmp -Raw | ConvertFrom-Json

# Read the canonical wiki Markdown from docs/wiki so GitHub Pages never preserves stale
# embedded wiki text from an older winforge-data.js.
$wikiDir = Join-Path $root 'docs/wiki'
$wikiPrefix = ([System.IO.Path]::GetFullPath($wikiDir)).TrimEnd([char[]]'\/') + [System.IO.Path]::DirectorySeparatorChar
$wikiIndex = @()
$wikiMap = [ordered]@{}
if (Test-Path $wikiDir) {
  $wikiFiles = Get-ChildItem -LiteralPath $wikiDir -Filter '*.md' -File -Recurse |
    Where-Object { $_.Name -ne 'README.md' } |
    Sort-Object FullName
  foreach ($file in $wikiFiles) {
    if (-not $file.FullName.StartsWith($wikiPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
      throw "Wiki file is outside docs/wiki: $($file.FullName)"
    }
    $relative = $file.FullName.Substring($wikiPrefix.Length).Replace('\', '/')
    $relativeNoExt = [System.IO.Path]::ChangeExtension($relative, $null).TrimEnd('.')
    $slug = $relativeNoExt -replace '/', '--'
    $body = Get-Content -LiteralPath $file.FullName -Raw
    $title = $slug
    if ($body -match '(?m)^#\s+(.+?)\s*$') { $title = $Matches[1].Trim() }
    $entry = [ordered]@{ slug = $slug; title = $title; path = $relative }
    if ($body -match '(?m)^\| Tag .*?\| <code>([^<]+)</code> \|') { $entry.moduleTag = $Matches[1] }
    $wikiIndex += $entry
    $wikiMap[$slug] = $body
  }
}
$wikiCount = $wikiIndex.Count

$merged = [ordered]@{
  meta       = [ordered]@{
    totalFeatures = $real.meta.totalFeatures
    tweakFeatureCount = $real.meta.tweakFeatureCount
    categoryCount = $real.meta.categoryCount
    moduleCount   = $real.meta.moduleCount
    wikiCount     = $wikiCount
  }
  categories = $real.categories
  modules    = $real.modules
  wikiIndex  = $wikiIndex
  wiki       = $wikiMap
}

$plainMerged = ConvertTo-PlainJsonValue -Value $merged
Add-Type -AssemblyName System.Web.Extensions
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength = [int]::MaxValue
$serializer.RecursionLimit = 1000
$out = 'window.WINFORGE_DATA = ' + $serializer.Serialize($plainMerged) + ';' + "`n"
Set-Content -Path $data -Value $out -Encoding UTF8 -NoNewline
Write-Host "Wrote $data - $($real.meta.moduleCount) modules, $($real.meta.categoryCount) categories, $($real.meta.totalFeatures) features, $wikiCount wiki pages."
