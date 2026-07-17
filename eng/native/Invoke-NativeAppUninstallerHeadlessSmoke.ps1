[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$ExecutablePath,
    [Parameter(Mandatory)][string]$LowLevelRunner,
    [int]$TimeoutMs = 30000
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = if ($RepoRoot) { $RepoRoot } else { Join-Path $PSScriptRoot '..\..' }
$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$ExecutablePath = if ($ExecutablePath) {
    (Resolve-Path -LiteralPath $ExecutablePath).Path
}
else {
    (Resolve-Path -LiteralPath (Join-Path $repo 'src\WinForge.App\bin\x64\Debug\WinForge.exe')).Path
}
$runner = (Resolve-Path -LiteralPath $LowLevelRunner).Path
$smoke = Join-Path $repo 'eng\native\Invoke-NativeAppUninstallerSmoke.ps1'

function Invoke-LowLevel {
    param([Parameter(Mandatory)][string]$Tool, [Parameter(Mandatory)][hashtable]$Payload)

    $arguments = @($Tool)
    foreach ($entry in $Payload.GetEnumerator()) {
        $arguments += "--$($entry.Key)"
        if ($entry.Value -is [bool]) {
            if (-not $entry.Value) {
                $arguments += 'false'
            }
        }
        else {
            $arguments += [string]$entry.Value
        }
    }

    $raw = & $runner @arguments
    $value = $raw | ConvertFrom-Json
    if (-not $value.ok) {
        throw "LowLevel $Tool failed: $raw"
    }
    return $value
}

$desktop = "WinForgeUninstallerSmoke-$PID"
$log = Join-Path $env:TEMP "$desktop.log"
$result = Join-Path $env:TEMP "$desktop.result"
$app = $null
$test = $null

Remove-Item -LiteralPath $log,$result -Force -ErrorAction SilentlyContinue
try {
    Invoke-LowLevel -Tool 'create_headless_desktop' -Payload @{ name = $desktop } | Out-Null
    $app = Invoke-LowLevel -Tool 'launch_on_headless_desktop' -Payload @{
        name = $desktop
        command = ('"{0}" --page module.uninstall' -f $ExecutablePath)
    }

    $innerTemplate = @'
$ErrorActionPreference = 'Stop'
$env:WINFORGE_LOWLEVEL_HEADLESS = '1'
try {
    & '__SMOKE__' -RepoRoot '__REPO__' -ProcessId __PID__ -RequireLowLevelHeadless -TimeoutMs __TIMEOUT__ *>&1 |
        Out-File -LiteralPath '__LOG__' -Encoding utf8
    '0' | Set-Content -LiteralPath '__RESULT__' -Encoding ascii
    exit 0
}
catch {
    $_ | Out-String | Add-Content -LiteralPath '__LOG__' -Encoding utf8
    '1' | Set-Content -LiteralPath '__RESULT__' -Encoding ascii
    exit 1
}
'@
    $inner = $innerTemplate.
        Replace('__SMOKE__', $smoke).
        Replace('__REPO__', $repo).
        Replace('__PID__', [string]$app.pid).
        Replace('__TIMEOUT__', [string]$TimeoutMs).
        Replace('__LOG__', $log).
        Replace('__RESULT__', $result)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($inner))
    $test = Invoke-LowLevel -Tool 'launch_on_headless_desktop' -Payload @{
        name = $desktop
        command = "powershell.exe -NoProfile -EncodedCommand $encoded"
    }

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs + 30000)
    while ((Get-Process -Id $test.pid -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (Get-Process -Id $test.pid -ErrorAction SilentlyContinue) {
        throw "Headless smoke process $($test.pid) did not finish within the timeout."
    }
    if (-not (Test-Path -LiteralPath $result)) {
        throw 'Headless smoke did not write a result file.'
    }

    Get-Content -LiteralPath $log -Encoding utf8
    if ((Get-Content -LiteralPath $result -Raw).Trim() -ne '0') {
        throw 'Focused native App Uninstaller smoke failed.'
    }

    $windows = Invoke-LowLevel -Tool 'list_headless_windows' -Payload @{ name = $desktop }
    Write-Output "HEADLESS_SMOKE=PASS desktop=$desktop appPid=$($app.pid) smokePid=$($test.pid) windowCount=$($windows.count)"
}
finally {
    if ($test -and (Get-Process -Id $test.pid -ErrorAction SilentlyContinue)) {
        try { Invoke-LowLevel -Tool 'kill_process' -Payload @{ pid = [int]$test.pid; force = $true } | Out-Null } catch { }
    }
    if ($app -and (Get-Process -Id $app.pid -ErrorAction SilentlyContinue)) {
        try { Invoke-LowLevel -Tool 'kill_process' -Payload @{ pid = [int]$app.pid; force = $true } | Out-Null } catch { }
    }
    try { Invoke-LowLevel -Tool 'close_headless_desktop' -Payload @{ name = $desktop } | Out-Null } catch { }
    Remove-Item -LiteralPath $log,$result -Force -ErrorAction SilentlyContinue
}
