[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,

    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter()]
    [string]$DotnetPath
)

$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$testsRoot = Join-Path $repo 'tests'
if (-not (Test-Path -LiteralPath $testsRoot -PathType Container)) {
    throw "WinForge tests directory was not found: $testsRoot"
}

if ([string]::IsNullOrWhiteSpace($DotnetPath)) {
    # Prefer the system x64 host when present. A workspace-local preview host can compile
    # net8.0 fixtures but may not contain the net8 runtime needed to execute their apphosts.
    $systemHost = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $systemHost -PathType Leaf) {
        $DotnetPath = $systemHost
    }
    else {
        $command = Get-Command dotnet -ErrorAction Stop
        $DotnetPath = $command.Source
    }
}

$DotnetPath = (Resolve-Path -LiteralPath $DotnetPath).Path
$oldDotnetRoot = $env:DOTNET_ROOT
$env:DOTNET_ROOT = Split-Path -Parent $DotnetPath

try {
    $projects = @(Get-ChildItem -LiteralPath $testsRoot -Directory |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File } |
        Sort-Object FullName)
    if ($projects.Count -eq 0) { throw "No test projects were found under $testsRoot" }

    $results = [System.Collections.Generic.List[object]]::new()
    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($project in $projects) {
        $output = @(& $DotnetPath run --project $project.FullName -c $Configuration 2>&1)
        $exitCode = $LASTEXITCODE
        $evidence = ($output | Select-String -Pattern 'PASS \d+/\d+|\d+/\d+ scenarios passed' |
            Select-Object -Last 1 | ForEach-Object { $_.Line.Trim() })
        $results.Add([pscustomobject]@{
            Project = $project.Directory.Name
            ExitCode = $exitCode
            Evidence = $evidence
        })
        if ($exitCode -ne 0) {
            Write-Output "===== FAILED $($project.FullName) ====="
            $output | ForEach-Object { $_ }
            $failures.Add($project.FullName)
        }
    }

    $results | Format-Table -AutoSize
    if ($failures.Count -gt 0) {
        throw ('WinForge test projects failed: ' + ($failures -join '; '))
    }
    Write-Output "ALL TEST PROJECTS PASSED: $($projects.Count)"
}
finally {
    $env:DOTNET_ROOT = $oldDotnetRoot
}
