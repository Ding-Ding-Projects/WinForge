[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,

    [Parameter()]
    [int]$WaitSeconds = 25
)

$ErrorActionPreference = 'Stop'

if ($WaitSeconds -lt 1) {
    throw 'WaitSeconds must be at least 1.'
}

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$exe = Join-Path $repo 'bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\publish\WinForge.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Self-contained WinForge publish was not found: $exe. Run the run-winforge driver with -Publish first."
}

Add-Type -AssemblyName UIAutomationClient

function Find-DescendantByAutomationId {
    param(
        [Parameter(Mandatory = $true)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory = $true)][string]$AutomationId
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

$process = Start-Process -FilePath $exe -ArgumentList @('--page', 'shell.allapps') -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
    $root = $null
    $dialog = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        $live = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($live -and $live.MainWindowHandle -ne 0) {
            $root = [System.Windows.Automation.AutomationElement]::FromHandle($live.MainWindowHandle)
            $dialog = Find-DescendantByAutomationId -Root $root -AutomationId 'NewTabPickerDialog'
            if ($dialog) { break }
        }
        Start-Sleep -Milliseconds 250
    }

    if (-not $dialog) {
        throw "NewTabPickerDialog was not found under WinForge pid $($process.Id) within $WaitSeconds seconds."
    }

    $search = Find-DescendantByAutomationId -Root $dialog -AutomationId 'NewTabPickerSearchBox'
    if (-not $search) {
        throw 'The All Apps dialog opened but its search box was not found.'
    }

    $allAppsNav = Find-DescendantByAutomationId -Root $root -AutomationId 'ShellNavItem_shell_allapps'
    if (-not $allAppsNav) {
        throw 'The All Apps navigation item was not found.'
    }

    $selection = [System.Windows.Automation.SelectionItemPattern]$allAppsNav.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    if (-not $selection.Current.IsSelected) {
        throw 'The All Apps dialog opened but its navigation item was not selected.'
    }

    [pscustomobject]@{
        status = 'pass'
        route = 'shell.allapps'
        dialogAutomationId = $dialog.Current.AutomationId
        dialogName = $dialog.Current.Name
        searchAutomationId = $search.Current.AutomationId
        allAppsNavigationSelected = $selection.Current.IsSelected
    }
}
finally {
    Get-Process -Id $process.Id -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}
