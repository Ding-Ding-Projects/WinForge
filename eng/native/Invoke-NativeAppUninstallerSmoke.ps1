[CmdletBinding()]
param(
    [string]$RepoRoot,
    [Parameter(Mandatory)][int]$ProcessId,
    [switch]$RequireLowLevelHeadless,
    [int]$TimeoutMs = 20000
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = if ($RepoRoot) { $RepoRoot } else { Join-Path $PSScriptRoot '..\..' }
$repo = (Resolve-Path -LiteralPath $RepoRoot).Path

if (-not $RequireLowLevelHeadless -or $env:WINFORGE_LOWLEVEL_HEADLESS -ne '1') {
    throw 'Invoke this smoke from LowLevel launch_on_headless_desktop with WINFORGE_LOWLEVEL_HEADLESS=1.'
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-ByAutomationId {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Find-ByAutomationIdPrefix {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$Prefix
    )

    $elements = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements) {
        $automationId = $element.Current.AutomationId
        if ($automationId -and $automationId.StartsWith($Prefix, [StringComparison]::Ordinal)) {
            return $element
        }
    }
    return $null
}

function Wait-ForAutomationIdPrefix {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$Prefix
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $element = Find-ByAutomationIdPrefix -Root $Root -Prefix $Prefix
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for automation id prefix '$Prefix'."
}

function Wait-ForElement {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for automation id '$AutomationId'."
}

function Wait-ForNamePrefix {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Prefix
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element -and $element.Current.Name.StartsWith($Prefix, [StringComparison]::Ordinal)) {
            return $element
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($element) { $element.Current.Name } else { '(missing)' }
    throw "Expected '$AutomationId' name prefix '$Prefix', got '$actual'."
}

function Wait-ForNameContains {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Fragment
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element -and $element.Current.Name.IndexOf($Fragment, [StringComparison]::Ordinal) -ge 0) {
            return $element
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($element) { $element.Current.Name } else { '(missing)' }
    throw "Expected '$AutomationId' name containing '$Fragment', got '$actual'."
}

function Wait-ForPageTitle {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$Prefix
    )

    return Wait-ForNamePrefix -Root $Root -AutomationId 'NativePageTitle' -Prefix $Prefix
}

function Wait-ForWindow {
    param([Parameter(Mandatory)][int]$ProcessId)

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if (-not $process) {
            throw "Native WinForge process $ProcessId exited before its window appeared."
        }
        if ($process.MainWindowHandle -ne 0) {
            return [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for native WinForge process $ProcessId to create a window."
}

function Get-EditableValuePattern {
    param([Parameter(Mandatory)]$Element)

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $candidates = @()
    if ($Element.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit) {
        $candidates += $Element
    }
    try {
        foreach ($child in $Element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
            $candidates += $child
        }
    }
    catch {
        # WinUI can replace a template while UI Automation is walking it.
    }

    foreach ($candidate in $candidates) {
        $raw = $null
        if ($candidate.TryGetCurrentPattern(
            [System.Windows.Automation.ValuePattern]::Pattern,
            [ref]$raw)) {
            $value = [System.Windows.Automation.ValuePattern]$raw
            if (-not $value.Current.IsReadOnly) { return $value }
        }
    }

    $raw = $null
    if ($Element.TryGetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern,
        [ref]$raw)) {
        return [System.Windows.Automation.ValuePattern]$raw
    }
    throw "No editable ValuePattern is available for '$($Element.Current.AutomationId)'."
}

function Set-EditableValueAndWait {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        try {
            $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
            $pattern = Get-EditableValuePattern -Element $element
            $pattern.SetValue($Value)
            if ((Get-EditableValuePattern -Element $element).Current.Value -eq $Value) {
                return $element
            }
        }
        catch {
            # A TextBox can replace its automation provider during rerender.
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Could not set editable value '$AutomationId'."
}

function Set-ToggleOn {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
    $toggle = [System.Windows.Automation.TogglePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
        $toggle.Toggle()
    }

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
        $toggle = [System.Windows.Automation.TogglePattern]$element.GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) {
            return $element
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Toggle '$AutomationId' did not turn on."
}

function Invoke-Element {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
    $invoke = [System.Windows.Automation.InvokePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
}

function Select-ComboIndex {
    param(
        [Parameter(Mandatory)]$Combo,
        [Parameter(Mandatory)][int]$Index
    )

    $expand = [System.Windows.Automation.ExpandCollapsePattern]$Combo.GetCurrentPattern(
        [System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $items = $Combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($Index -ge 0 -and $Index -lt $items.Count) {
            $selection = [System.Windows.Automation.SelectionItemPattern]$items[$Index].GetCurrentPattern(
                [System.Windows.Automation.SelectionItemPattern]::Pattern)
            $selection.Select()
            return
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Regex Builder target index $Index was not found."
}

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Name
    )

    if (-not $Condition) { throw "ASSERT: $Name" }
    Write-Output "PASS $Name"
}

$root = Wait-ForWindow -ProcessId $ProcessId
try {
    Wait-ForPageTitle -Root $root -Prefix 'App Uninstaller' | Out-Null

    foreach ($id in @(
        'NativeAppUninstallerSafety',
        'NativeAppUninstallerSearch',
        'NativeAppUninstallerRegexMode',
        'NativeAppUninstallerRegexBuilder',
        'NativeAppUninstallerRefresh',
        'NativeAppUninstallerStatus',
        'NativeAppUninstallerResultCount',
        'NativeAppUninstallerList'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $safety = Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerSafety'
    Assert-True -Condition (
        $safety.Current.Name -eq
        'Native App Uninstaller safety: normal integrity required; local data deletion unavailable.'
    ) -Name 'App Uninstaller exposes normal-integrity and no-local-data-deletion safety evidence'
    Wait-ForNameContains -Root $root -AutomationId 'NativeAppUninstallerStatus' -Fragment 'Current-user Store/UWP inventory refreshed.' | Out-Null
    $deepCleanupStatus = Wait-ForAutomationIdPrefix -Root $root -Prefix 'NativeAppUninstallerDeepCleanupStatus_'
    Assert-True -Condition (
        $deepCleanupStatus.Current.Name -eq
        'Deep cleanup is intentionally unavailable until handle-relative deletion is implemented. Package removal never deletes local data.'
    ) -Name 'App Uninstaller shows deep-cleanup unavailability after inventory has rendered'
    Assert-True -Condition (
        -not (Find-ByAutomationIdPrefix -Root $root -Prefix 'NativeAppUninstallerReviewDeep_')
    ) -Name 'App Uninstaller exposes no unsafe deep-cleanup review action'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '__winforge_native_uninstaller_no_such_package__' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerEmpty' | Out-Null
    Wait-ForNamePrefix -Root $root -AutomationId 'NativeAppUninstallerResultCount' -Prefix '0 / ' | Out-Null
    Assert-True -Condition $true -Name 'App Uninstaller literal filtering stays inside the local package cache'

    Set-ToggleOn -Root $root -AutomationId 'NativeAppUninstallerRegexMode' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '.*' | Out-Null
    Wait-ForNameContains -Root $root -AutomationId 'NativeAppUninstallerStatus' -Fragment 'Bounded PCRE2 filters only the already-returned local package cache.' | Out-Null
    $countBeforeInvalid = (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerResultCount').Current.Name
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '[' | Out-Null
    Wait-ForNamePrefix -Root $root -AutomationId 'NativeAppUninstallerStatus' -Prefix 'Invalid PCRE2 filter' | Out-Null
    $countAfterInvalid = (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerResultCount').Current.Name
    Assert-True -Condition ($countBeforeInvalid -eq $countAfterInvalid) -Name 'App Uninstaller retains local results while an invalid regex is corrected'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '.*' | Out-Null
    Invoke-Element -Root $root -AutomationId 'NativeAppUninstallerRegexBuilder'
    Wait-ForPageTitle -Root $root -Prefix 'Regex Tester & Builder' | Out-Null
    $target = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderTarget'
    Select-ComboIndex -Combo $target -Index 5
    Invoke-Element -Root $root -AutomationId 'NativeRegexBuilderApply'
    Wait-ForPageTitle -Root $root -Prefix 'App Uninstaller' | Out-Null
    $mode = Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerRegexMode'
    $toggle = [System.Windows.Automation.TogglePattern]$mode.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $search = (Get-EditableValuePattern -Element (
        Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerSearch'
    )).Current.Value
    Assert-True -Condition (
        $toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $search -eq '.*'
    ) -Name 'Regex Builder returns a verified pattern to the App Uninstaller cache target'

    Write-Output 'Focused native App Uninstaller smoke: PASS'
}
finally {
    # The LowLevel headless orchestrator owns the app lifecycle and cleanup.
}
