[CmdletBinding()]
param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Root).Path
$tracked = @(& git -C $root ls-files -z | ForEach-Object { $_ })
$raw = [string]::Join('', $tracked)
$paths = @($raw -split "`0" | Where-Object { $_ })
$commitCache = @{}
$lineCache = @{}

function Category([string]$path) {
  $p = $path.Replace('\', '/').ToLowerInvariant()
  if ($p.StartsWith('tests/')) { return 'tests' }
  if ($p -eq 'design/winforge-data.js' -or $p -like 'docs/generated*' -or $p -like 'docs/wiki/generated*') { return 'generated' }
  if ($p.EndsWith('.xaml') -or $p.EndsWith('.html') -or $p.EndsWith('.css') -or $p.EndsWith('.js') -or $p.EndsWith('.scss')) { return 'styles/markup' }
  if ($p.EndsWith('.cs') -or $p.EndsWith('.csproj') -or $p.EndsWith('.ps1') -or $p.EndsWith('.bat')) { return 'source' }
  return 'other'
}

function IsAgentCommit([string]$sha) {
  if ([string]::IsNullOrWhiteSpace($sha) -or $sha -match '^0{40}$') { return $false }
  if (-not $commitCache.ContainsKey($sha)) {
    $body = (& git -C $root show -s --format='%an%n%ae%n%B' $sha | Out-String)
    $commitCache[$sha] = $body -match '(?im)Claude Fable 5|noreply@anthropic\.com|Co-Authored-By:.*agent'
  }
  return [bool]$commitCache[$sha]
}

$rows = [ordered]@{}
foreach ($name in @('source','tests','styles/markup','generated','other')) {
  $rows[$name] = [ordered]@{ Files = 0; Total = 0; NonBlank = 0; AgentNonBlank = 0 }
}

foreach ($relative in $paths) {
  if ([string]::IsNullOrWhiteSpace($relative)) { continue }
  if ($relative.Replace('\','/') -like 'ThirdParty/*' -or $relative.Replace('\','/') -like '*/bin/*' -or $relative.Replace('\','/') -like '*/obj/*') { continue }
  $category = Category $relative
  $full = Join-Path $root $relative
  if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
  $lines = [IO.File]::ReadAllLines($full)
  $lineCache[$relative] = $lines
  $rows[$category].Files++
  $rows[$category].Total += $lines.Count
  $rows[$category].NonBlank += @($lines | Where-Object { $_.Trim().Length -gt 0 }).Count

}

# Incremental blame omits the source text and returns result-line ranges, so it
# keeps the required surviving-line attribution while avoiding porcelain output
# for every line. The cached file content tells us which blamed lines are blank.
# Prose/configuration rows remain in the grand totals, while attribution is
# explicitly measured for source, tests, styles/markup, and generated code.
$blamePaths = @($paths | Where-Object { (Category $_) -ne 'other' })
foreach ($relative in $blamePaths) {
  if ([string]::IsNullOrWhiteSpace($relative) -or -not $lineCache.ContainsKey($relative)) { continue }
  if ($relative.Replace('\','/') -like 'ThirdParty/*' -or $relative.Replace('\','/') -like '*/bin/*' -or $relative.Replace('\','/') -like '*/obj/*') { continue }
  $blame = @(& git -C $root blame --incremental -- $relative 2>$null)
  if ($LASTEXITCODE -ne 0) { throw "git incremental blame failed for $relative" }
  foreach ($line in $blame) {
    if ($line -notmatch '^([0-9a-f]{40})\s+(\d+)\s+(\d+)\s+(\d+)$') { continue }
    $sha = $Matches[1]
    $resultLine = [int]$Matches[3]
    $lineCount = [int]$Matches[4]
    $fileLines = $lineCache[$relative]
    for ($offset = 0; $offset -lt $lineCount; $offset++) {
      $index = $resultLine - 1 + $offset
      if ($index -ge 0 -and $index -lt $fileLines.Count -and $fileLines[$index].Trim().Length -gt 0) {
        if (IsAgentCommit $sha) { $rows[(Category $relative)].AgentNonBlank++ }
      }
    }
  }
}

$grand = [ordered]@{ Files = 0; Total = 0; NonBlank = 0; AgentNonBlank = 0 }
Write-Output '| Category | Files | Total lines | Non-blank lines | Agent-written non-blank lines |'
Write-Output '|---|---:|---:|---:|---:|'
foreach ($pair in $rows.GetEnumerator()) {
  $row = $pair.Value
  $grand.Files += $row.Files; $grand.Total += $row.Total; $grand.NonBlank += $row.NonBlank; $grand.AgentNonBlank += $row.AgentNonBlank
  Write-Output "| $($pair.Key) | $($row.Files) | $($row.Total) | $($row.NonBlank) | $($row.AgentNonBlank) |"
}
Write-Output "| **Grand total (tracked, exclusions applied)** | **$($grand.Files)** | **$($grand.Total)** | **$($grand.NonBlank)** | **$($grand.AgentNonBlank)** |"
Write-Output ''
Write-Output 'Excluded: ThirdParty/, dependency directories, build output, and ignored files. Agent attribution is based on surviving git-blame lines in source, tests, styles/markup, and generated-code rows; prose/configuration rows remain visible in totals but are not attributed.'
