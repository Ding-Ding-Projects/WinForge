<#
.SYNOPSIS
  Regenerate design/winforge-data.js from the REAL WinForge app - no hand-written data.

.DESCRIPTION
  The GitHub Pages documentation site is data-driven by design/winforge-data.js
  (module list, categories, feature counts). This script gets that data straight from
  the live app instead of pretend/hardcoded numbers:

    1. publishes WinForge self-contained (unless -SkipBuild or -DocsOnly),
    2. runs `WinForge.exe --export-site-data <tmp.json>` - the app dumps its real
       ModuleRegistry + Categories + TweakCatalog (meta / categories / modules),
    3. reads the canonical authored wiki sections (wikiIndex / wiki) from
       docs/wiki (wiki content is docs, not app data),
    4. writes `window.WINFORGE_DATA = {...};` to design/winforge-data.js.

  -DocsOnly preserves the committed managed-app export and refreshes only the
  authored wiki. It is intended for documentation-only changes, where publishing
  the managed application adds no new data.

  Simulators (Reactor / FuelFactory / CakeFarm) are untouched - they generate their
  own valid .fuel / .cake files and don't read this data.

.EXAMPLE
  pwsh -File tools/regen-site-data.ps1

.EXAMPLE
  pwsh -File tools/regen-site-data.ps1 -DocsOnly
#>
param(
  [switch]$SkipBuild,
  [switch]$DocsOnly,
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

$jsonSerializer = $null
if ($PSVersionTable.PSEdition -eq 'Desktop') {
  try {
    # Windows PowerShell 5's ConvertTo-Json is extremely slow and memory-hungry for
    # the thousands of wiki pages below. JavaScriptSerializer is a .NET Framework
    # dependency, so PowerShell 7 intentionally uses its portable native JSON cmdlets.
    Add-Type -AssemblyName System.Web.Extensions -ErrorAction Stop
    $jsonSerializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $jsonSerializer.MaxJsonLength = [int]::MaxValue
    $jsonSerializer.RecursionLimit = 100
  } catch { }
}

function ConvertFrom-PortableJson {
  param([Parameter(Mandatory)][string]$Json)
  if ($jsonSerializer) { return $jsonSerializer.DeserializeObject($Json) }
  return $Json | ConvertFrom-Json
}

if ($DocsOnly -and $ExePath) {
  throw '-DocsOnly and -ExePath are mutually exclusive.'
}

if ($DocsOnly) {
  if (-not (Test-Path -LiteralPath $data)) {
    throw "Committed site data is required for -DocsOnly: $data"
  }
  Write-Host 'Preserving committed managed-app export (docs-only refresh)...'
  $existingScript = [System.IO.File]::ReadAllText($data, [System.Text.Encoding]::UTF8).TrimStart([char]0xFEFF)
  $prefix = 'window.WINFORGE_DATA = '
  if (-not $existingScript.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
    throw "Unexpected site-data prefix in $data"
  }
  $existingPayload = $existingScript.Substring($prefix.Length).Trim()
  if (-not $existingPayload.EndsWith(';', [System.StringComparison]::Ordinal)) {
    throw "Unexpected site-data suffix in $data"
  }
  $realJson = $existingPayload.Substring(0, $existingPayload.Length - 1)
} else {
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
  $realJson = [System.IO.File]::ReadAllText($tmp, [System.Text.Encoding]::UTF8)
}

$real = ConvertFrom-PortableJson -Json $realJson
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
$wikiPrefix = ([System.IO.Path]::GetFullPath($wikiDir)).TrimEnd([char[]]'\/') + [System.IO.Path]::DirectorySeparatorChar
$wikiIndex = New-Object 'System.Collections.Generic.List[object]'
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

$plainMerged = ConvertTo-PlainJsonValue -Value $merged
$json = if ($jsonSerializer) {
  $jsonSerializer.RecursionLimit = 1000
  $jsonSerializer.Serialize($plainMerged)
} else {
  $plainMerged | ConvertTo-Json -Depth 40 -Compress
}
$out = 'window.WINFORGE_DATA = ' + $json + ';' + "`n"
Set-Content -Path $data -Value $out -Encoding UTF8 -NoNewline
$moduleCount = Get-JsonField $realMeta 'moduleCount'
$categoryCount = Get-JsonField $realMeta 'categoryCount'
$featureCount = Get-JsonField $realMeta 'totalFeatures'
Write-Host ('Wrote {0} - {1} modules, {2} categories, {3} features, {4} wiki pages.' -f $data, $moduleCount, $categoryCount, $featureCount, $wikiCount)
