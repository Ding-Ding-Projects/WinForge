[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$ExecutablePath,
    [int]$TimeoutMs = 10000
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = if ($RepoRoot) { $RepoRoot } else { Join-Path $PSScriptRoot '..\..' }
$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
if (-not $ExecutablePath) {
    $ExecutablePath = Join-Path $repo 'src\WinForge.App\bin\x64\Debug\WinForge.exe'
}
$exe = (Resolve-Path -LiteralPath $ExecutablePath).Path

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$script:Passed = 0
$script:Failed = 0

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

function Wait-ForElementByAutomationIdPrefix {
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

function Wait-ForElementVisible {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element) {
            try {
                $bounds = $element.Current.BoundingRectangle
                if (-not $element.Current.IsOffscreen -and $bounds.Width -gt 0 -and $bounds.Height -gt 0) {
                    return $element
                }
            }
            catch {
                # WinUI may replace an element while a visibility transition is in flight.
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for visible automation id '$AutomationId'."
}

function Wait-ForWindow {
    param([Parameter(Mandatory)][int]$ProcessId)

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if (-not $process) { throw "Native WinForge process $ProcessId exited before its window appeared." }
        if ($process.MainWindowHandle -ne 0) {
            return [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for native WinForge process $ProcessId to create a window."
}

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Name
    )

    if ($Condition) {
        $script:Passed++
        Write-Host "PASS $Name"
    }
    else {
        $script:Failed++
        Write-Host "FAIL $Name" -ForegroundColor Red
    }
}

function Wait-ForPageTitle {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$Prefix
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $title = Find-ByAutomationId -Root $Root -AutomationId 'NativePageTitle'
        if ($title -and $title.Current.Name.StartsWith($Prefix, [StringComparison]::Ordinal)) {
            return $title
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($title) { $title.Current.Name } else { '(missing)' }
    throw "Expected page title prefix '$Prefix', got '$actual'."
}

function Wait-ForElementNamePrefix {
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

function Wait-ForElementValue {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][AllowEmptyString()][string]$ExpectedValue
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    $element = $null
    $lastError = $null
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element) {
            try {
                $value = Get-EditableValuePattern -Element $element
                if ($value.Current.Value -eq $ExpectedValue) {
                    return $element
                }
            }
            catch {
                # WinUI can temporarily detach a provider during a template or
                # language refresh. Keep polling the live tree instead of
                # reporting that short-lived state as a feature failure.
                $lastError = $_
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($element) {
        try { (Get-EditableValuePattern -Element $element).Current.Value }
        catch { "(value provider unavailable: $($_.Exception.Message))" }
    }
    else { '(missing)' }
    $suffix = if ($lastError) { " Last automation error: $lastError" } else { '' }
    throw "Expected '$AutomationId' value '$ExpectedValue', got '$actual'.$suffix"
}

function Wait-ForElementValueWhere {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][scriptblock]$Predicate
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    $element = $null
    $lastError = $null
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element) {
            try {
                $value = Get-EditableValuePattern -Element $element
                if (& $Predicate $value.Current.Value) {
                    return $element
                }
            }
            catch {
                $lastError = $_
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($element) {
        try { (Get-EditableValuePattern -Element $element).Current.Value }
        catch { "(value provider unavailable: $($_.Exception.Message))" }
    }
    else { '(missing)' }
    $suffix = if ($lastError) { " Last automation error: $lastError" } else { '' }
    throw "Expected '$AutomationId' value matching '$Description', got '$actual'.$suffix"
}

function Set-ElementValueAndWait {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )

    $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
    $pattern = [System.Windows.Automation.ValuePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Value)
    Wait-ForElementValue -Root $Root -AutomationId $AutomationId -ExpectedValue $Value | Out-Null
    return $element
}

function Get-EditableValuePattern {
    param(
        [Parameter(Mandatory)]$Element
    )

    # Prefer a live editable child. WinUI can expose an outer ValuePattern
    # whose value is stale while an Edit is transitioning from collapsed to
    # visible; the child provider is the one assistive technology operates.
    # TryGetCurrentPattern also prevents an in-flight XAML template refresh
    # from turning a transiently unavailable provider into a smoke failure.
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)

    $edits = @()
    try {
        if ($Element.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit) {
            $edits += $Element
        }
        foreach ($edit in $Element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
            $edits += $edit
        }
    }
    catch {
        # Continue with the outer provider; a template can change while UIA walks it.
    }

    foreach ($edit in $edits) {
        if (-not $edit.Current.IsEnabled) { continue }
        $raw = $null
        if ($edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$raw)) {
            $value = [System.Windows.Automation.ValuePattern]$raw
            if (-not $value.Current.IsReadOnly) { return $value }
        }
    }

    $outerRaw = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$outerRaw)) {
        return [System.Windows.Automation.ValuePattern]$outerRaw
    }

    foreach ($edit in $edits) {
        $raw = $null
        if ($edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$raw)) {
            return [System.Windows.Automation.ValuePattern]$raw
        }
    }

    throw "No ValuePattern is currently available for '$($Element.Current.AutomationId)'."
}

function Set-EditableValueAndWait {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    $lastError = $null
    do {
        try {
            $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
            $pattern = Get-EditableValuePattern -Element $element
            $pattern.SetValue($Value)
            $actual = (Get-EditableValuePattern -Element $element).Current.Value
            if ($actual -eq $Value) { return $element }
        }
        catch {
            $lastError = $_
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $suffix = if ($lastError) { " Last automation error: $lastError" } else { '' }
    throw "Expected editable '$AutomationId' value '$Value', got '$actual'.$suffix"
}

function Set-ToggleState {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][bool]$IsOn
    )

    $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
    $toggle = [System.Windows.Automation.TogglePattern]$element.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $expected = if ($IsOn) {
        [System.Windows.Automation.ToggleState]::On
    }
    else {
        [System.Windows.Automation.ToggleState]::Off
    }
    if ($toggle.Current.ToggleState -ne $expected) {
        $toggle.Toggle()
        $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
        do {
            $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
            $toggle = [System.Windows.Automation.TogglePattern]$element.GetCurrentPattern(
                [System.Windows.Automation.TogglePattern]::Pattern)
            if ($toggle.Current.ToggleState -eq $expected) { break }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $deadline)
    }
    if ($toggle.Current.ToggleState -ne $expected) {
        throw "Toggle '$AutomationId' did not reach the expected state."
    }
    return $element
}

function Invoke-ElementByAutomationId {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $lastError = $null
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        try {
            $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
            $invoke = [System.Windows.Automation.InvokePattern]$element.GetCurrentPattern(
                [System.Windows.Automation.InvokePattern]::Pattern)
            $invoke.Invoke()
            return
        }
        catch {
            $lastError = $_
            Start-Sleep -Milliseconds 150
        }
    }

    throw $lastError
}

function Select-ComboItem {
    param(
        [Parameter(Mandatory)]$Combo,
        [Parameter(Mandatory)][string]$Name
    )

    $expand = [System.Windows.Automation.ExpandCollapsePattern]$Combo.GetCurrentPattern(
        [System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    $item = $Combo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (-not $item) { throw "Combo item '$Name' was not found." }
    $selection = [System.Windows.Automation.SelectionItemPattern]$item.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selection.Select()
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
    $items = $null
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

    if ($Index -lt 0 -or $Index -ge $items.Count) {
        throw "Combo index $Index was not found (item count: $($items.Count))."
    }
}

function Test-HorizontalBoundsWithinWindow {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][System.Collections.IEnumerable]$Elements
    )

    $window = $Root.Current.BoundingRectangle
    foreach ($element in $Elements) {
        if (-not $element) { return $false }
        $rect = $element.Current.BoundingRectangle
        # A vertically off-screen item is not clipped: the shared page host
        # intentionally exposes it through its vertical ScrollViewer. Ask UIA
        # to bring it into view before taking the horizontal measurement.
        if ($rect.Width -le 0 -or $rect.Height -le 0) {
            try {
                $scrollItem = [System.Windows.Automation.ScrollItemPattern]$element.GetCurrentPattern(
                    [System.Windows.Automation.ScrollItemPattern]::Pattern)
                $scrollItem.ScrollIntoView()
                Start-Sleep -Milliseconds 100
                $rect = $element.Current.BoundingRectangle
            }
            catch {
                return $false
            }
        }
        if ($rect.Width -le 0 -or $rect.Height -le 0) { return $false }
        # Vertical overflow is intentionally scrollable. Horizontal overflow
        # is the clipping defect this smoke check is designed to catch.
        if ($rect.Left -lt ($window.Left - 2) -or $rect.Right -gt ($window.Right + 2)) {
            return $false
        }
    }

    return $true
}

function Invoke-OwnedRoute {
    param(
        [Parameter(Mandatory)][string]$Route,
        [Parameter(Mandatory)][string]$ExpectedTitle,
        [scriptblock]$Inspect
    )

    $process = $null
    $root = $null
    $title = $null
    $routeReady = $false
    $lastError = $null

    # A new WinUI process can briefly inherit a still-closing previous window on a
    # private desktop. Retry only the launch/title handshake; this preserves the
    # actual route assertion while eliminating a cross-process teardown race.
    for ($attempt = 1; $attempt -le 3 -and -not $routeReady; $attempt++) {
        $process = Start-Process -FilePath $exe -ArgumentList '--page', $Route -PassThru
        try {
            $root = Wait-ForWindow -ProcessId $process.Id
            $title = Wait-ForPageTitle -Root $root -Prefix $ExpectedTitle
            $routeReady = $true
        }
        catch {
            $lastError = $_
        }
        finally {
            if (-not $routeReady -and $process) {
                Get-Process -Id $process.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                try { Wait-Process -Id $process.Id -Timeout 3 -ErrorAction Stop } catch { }
            }
        }

        if (-not $routeReady) {
            Start-Sleep -Milliseconds 350
        }
    }

    if (-not $routeReady) {
        Assert-True -Condition $false -Name "route '$Route' renders '$ExpectedTitle' ($($lastError.Exception.Message))"
        return
    }

    try {
        Assert-True -Condition $true -Name "route '$Route' renders '$ExpectedTitle'"
        if ($Inspect) {
            & $Inspect $root $title
        }
    }
    catch {
        Assert-True -Condition $false -Name "route '$Route' renders '$ExpectedTitle' ($($_.Exception.Message))"
    }
    finally {
        Get-Process -Id $process.Id -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        try { Wait-Process -Id $process.Id -Timeout 3 -ErrorAction Stop } catch { }
    }
}

Invoke-OwnedRoute -Route 'dashboard' -ExpectedTitle 'WinForge Native' -Inspect {
    param($root, $title)
    Assert-True -Condition ($title.Current.Name -match '[\u3400-\u9fff]') -Name 'dashboard title contains a Cantonese label'

    foreach ($case in @(
        @{ Id = 'NativeNav_apps'; Title = 'Apps & Startup' },
        @{ Id = 'NativeNav_launcher'; Title = 'Launcher & Elevation' },
        @{ Id = 'NativeNav_taskbar'; Title = 'Taskbar & Start' },
        @{ Id = 'NativeNav_vault'; Title = 'Encryption & Vault' }
    )) {
        $item = Wait-ForElement -Root $root -AutomationId $case.Id
        $selection = [System.Windows.Automation.SelectionItemPattern]$item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $selection.Select()
        Wait-ForPageTitle -Root $root -Prefix $case.Title | Out-Null
        Assert-True -Condition $true -Name "in-app category '$($case.Id)' keeps canonical routing"
    }
}

Invoke-OwnedRoute -Route 'dashboard' -ExpectedTitle 'WinForge Native' -Inspect {
    param($root, $title)

    $shellSearch = Find-ByAutomationId -Root $root -AutomationId 'NativeShellSearchBox'
    if (-not $shellSearch -or $shellSearch.Current.IsOffscreen) {
        # In compact NavigationView mode the shell search lives behind the
        # built-in, keyboard-accessible navigation-pane toggle. Exercise that
        # real accessibility path before asserting its controls.
        Invoke-ElementByAutomationId -Root $root -AutomationId 'TogglePaneButton'
    }

    foreach ($id in @(
        'NativeShellSearchBox',
        'NativeShellRegexMode',
        'NativeShellRegexBuilder',
        'NativeShellSearchExecute',
        'NativeShellRegexStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Set-ToggleState -Root $root -AutomationId 'NativeShellRegexMode' -IsOn $true | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeShellSearchBox' -Value '^module\.reactor$' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeShellSearchExecute'
    Wait-ForPageTitle -Root $root -Prefix 'Search results' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeRoute_module_reactor' | Out-Null
    Assert-True -Condition $true -Name 'shell catalog regex matches a native route through the explicit search action'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeShellSearchBox' -Value '[' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeShellSearchExecute'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSearchRegexStatus' -Prefix 'Regex needs correction' | Out-Null
    Assert-True -Condition $true -Name 'shell catalog regex reports invalid syntax without crashing or routing a literal alias'

    $shellControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeShellRegexMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeShellRegexBuilder'),
        (Wait-ForElement -Root $root -AutomationId 'NativeShellSearchExecute'),
        (Wait-ForElement -Root $root -AutomationId 'NativeShellRegexStatus'))
    Assert-True -Condition $shellControlsFit -Name 'shell regex controls are horizontally unclipped'
}

Invoke-OwnedRoute -Route 'shell.allapps' -ExpectedTitle 'All Apps' -Inspect {
    param($root, $title)
    $filter = Wait-ForElement -Root $root -AutomationId 'NativeAllAppsSearchBox'
    $value = [System.Windows.Automation.ValuePattern]$filter.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $value.SetValue('reactor')
    Wait-ForElement -Root $root -AutomationId 'NativeAllApps_module_reactor' | Out-Null
    Assert-True -Condition $true -Name 'All Apps filter updates its live native list'

    $value.SetValue('module.unixperm')
    $implementedRoute = Wait-ForElement -Root $root -AutomationId 'NativeAllApps_module_unixperm'
    Assert-True -Condition ($implementedRoute.Current.Name.Contains('native implementation available')) -Name 'All Apps labels a dedicated native renderer as available'
    $value.SetValue('module.slugify')
    $pendingRoute = Wait-ForElement -Root $root -AutomationId 'NativeAllApps_module_slugify'
    Assert-True -Condition ($pendingRoute.Current.Name.Contains('native implementation pending')) -Name 'All Apps keeps an unported route explicitly pending'

    foreach ($id in @(
        'NativeAllAppsRegexMode',
        'NativeAllAppsRegexBuilder',
        'NativeAllAppsRegexStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Set-ToggleState -Root $root -AutomationId 'NativeAllAppsRegexMode' -IsOn $true | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAllAppsSearchBox' -Value '^module\.reactor$' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeAllApps_module_reactor' | Out-Null
    Assert-True -Condition $true -Name 'All Apps PCRE2 regex matches an anchored individual route id'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAllAppsSearchBox' -Value '[' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAllAppsRegexStatus' -Prefix 'Regex syntax needs correction' | Out-Null
    Assert-True -Condition $true -Name 'All Apps retains responsiveness and reports an invalid regex locally'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAllAppsSearchBox' -Value '^module\.reactor$' | Out-Null
    $allAppsRegexControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeAllAppsSearchBox'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAllAppsRegexMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAllAppsRegexBuilder'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAllAppsRegexStatus'))
    Assert-True -Condition $allAppsRegexControlsFit -Name 'All Apps regex controls are horizontally unclipped'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $expand = [System.Windows.Automation.ExpandCollapsePattern]$language.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        'English')
    $english = $language.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
    if ($english) {
        $selection = [System.Windows.Automation.SelectionItemPattern]$english.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $selection.Select()
    }
    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    Assert-True -Condition ($english -and $dashboard.Current.Name -eq 'Dashboard') -Name 'language picker rerenders navigation in English'
}

Invoke-OwnedRoute -Route 'regextester' -ExpectedTitle 'Regex Tester & Builder' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeRegexBuilderSafety',
        'NativeRegexBuilderTarget',
        'NativeRegexPattern',
        'NativeRegexStatus',
        'NativeRegexBuilderPreview',
        'NativeRegexBuilderCaseSensitive',
        'NativeRegexBuilderMultiline',
        'NativeRegexBuilderDotAll',
        'NativeRegexBuilderIgnorePatternWhitespace',
        'NativeRegexBuilderExplicitCapture',
        'NativeRegexBuilderBack',
        'NativeRegexBuilderNext',
        'NativeRegexBuilderApply'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Set-ToggleState -Root $root -AutomationId 'NativeRegexBuilderIgnorePatternWhitespace' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeRegexBuilderExplicitCapture' -IsOn $true | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexPattern' -Value '(?<suite> WinForge ) \s+ Native # extended whitespace' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexStatus' -Prefix 'PCRE2 pattern is valid' | Out-Null
    $stepOneFits = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexPattern'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderIgnorePatternWhitespace'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderExplicitCapture'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderBack'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderNext'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderApply'))
    Assert-True -Condition $stepOneFits -Name 'Regex builder step one is horizontally unclipped'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderNext'
    foreach ($id in @(
        'NativeRegexBuilderLiteral',
        'NativeRegexBuilderRecipe',
        'NativeRegexBuilderApplyRecipe',
        'NativeRegexBuilderAppendLiteral'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexBuilderLiteral' -Value 'module.reactor' | Out-Null
    $recipe = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderRecipe'
    Select-ComboIndex -Combo $recipe -Index 1
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderApplyRecipe'
    $recipePattern = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeRegexPattern')).Current.Value
    Assert-True -Condition ($recipePattern -eq '^(?:module\.reactor)$') `
        -Name 'Regex builder recipe escapes a literal exact-match pattern'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexPattern' -Value '(?<suite>WinForge)\s+Native' | Out-Null
    $stepTwoFits = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderLiteral'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderRecipe'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderApplyRecipe'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderAppendLiteral'))
    Assert-True -Condition $stepTwoFits -Name 'Regex builder recipe-and-token step is horizontally unclipped'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderNext'
    foreach ($id in @(
        'NativeRegexBuilderCaptureName',
        'NativeRegexBuilderAssertion',
        'NativeRegexBuilderAssertionFragment',
        'NativeRegexBuilderAppendAssertion',
        'NativeRegexBuilderQuantifier',
        'NativeRegexBuilderApplyQuantifier'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    $assertion = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderAssertion'
    Select-ComboIndex -Combo $assertion -Index 0
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderAppendAssertion'
    $assertionPattern = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeRegexPattern')).Current.Value
    Assert-True -Condition $assertionPattern.EndsWith('\b', [StringComparison]::Ordinal) `
        -Name 'Regex builder appends a bounded word-boundary assertion'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexPattern' -Value '(?<suite>WinForge)\s+Native' | Out-Null
    $stepThreeFits = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderCaptureName'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderAssertion'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderAssertionFragment'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderAppendAssertion'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderQuantifier'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderApplyQuantifier'))
    Assert-True -Condition $stepThreeFits -Name 'Regex builder grouping-assertion-and-quantifier step is horizontally unclipped'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderNext'
    foreach ($id in @(
        'NativeRegexTestInput',
        'NativeRegexReplacementInput',
        'NativeRegexMatchSummary',
        'NativeRegexReplacementPreview',
        'NativeRegexReplacementStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexTestInput' -Value 'WinForge Native WinForge Native' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexMatchSummary' -Prefix '2 non-overlapping' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexMatch_1' -Prefix 'Match 1: index 0' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexReplacementInput' -Value '${suite}-$0-$$-$1' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexReplacementStatus' -Prefix '2 replacement substitution(s)' | Out-Null
    $replacementPreview = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeRegexReplacementPreview')).Current.Value
    Assert-True -Condition ($replacementPreview -eq 'WinForge-WinForge Native-$-WinForge WinForge-WinForge Native-$-WinForge') `
        -Name 'Regex tester lists all named-capture matches and previews bounded replacements'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexReplacementInput' -Value '$&' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexReplacementStatus' -Prefix 'Replacement preview was not generated: Replacement supports only' | Out-Null
    $invalidReplacementPreview = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeRegexReplacementPreview')).Current.Value
    Assert-True -Condition ([string]::IsNullOrEmpty($invalidReplacementPreview)) `
        -Name 'Regex tester rejects unsupported replacement syntax without applying it'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexPattern' -Value 'a+' | Out-Null
    $longRegexInput = ('a' * 8192) -join ''
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexTestInput' -Value $longRegexInput | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexReplacementInput' -Value '$0$0$0$0$0' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexReplacementStatus' -Prefix 'Replacement preview was not generated: Replacement preview exceeded' | Out-Null
    Assert-True -Condition $true -Name 'Regex tester enforces the replacement-output safety cap'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexPattern' -Value '[' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexStatus' -Prefix 'Regex syntax needs correction' | Out-Null
    $invalidApply = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderApply'
    Assert-True -Condition (-not $invalidApply.Current.IsEnabled) `
        -Name 'Regex builder rejects invalid live patterns and blocks target application'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexPattern' -Value '^module\.reactor$' | Out-Null

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderBack'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexBuilderStep' -Prefix 'Step 3' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderNext'
    Wait-ForElement -Root $root -AutomationId 'NativeRegexTestInput' | Out-Null

    $target = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderTarget'
    Select-ComboIndex -Combo $target -Index 0
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderApply'
    Wait-ForPageTitle -Root $root -Prefix 'Search results' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeRoute_module_reactor' | Out-Null
    Assert-True -Condition $true -Name 'Regex builder applies a validated pattern to the native Shell catalog target'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeShellRegexBuilder'
    Wait-ForPageTitle -Root $root -Prefix 'Regex Tester & Builder' | Out-Null
    $target = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderTarget'
    Select-ComboIndex -Combo $target -Index 1
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderApply'
    Wait-ForPageTitle -Root $root -Prefix 'All Apps' | Out-Null
    $allAppsMode = Wait-ForElement -Root $root -AutomationId 'NativeAllAppsRegexMode'
    $allAppsToggle = [System.Windows.Automation.TogglePattern]$allAppsMode.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $allAppsValue = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeAllAppsSearchBox')).Current.Value
    Assert-True -Condition ($allAppsToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and $allAppsValue -eq '^module\.reactor$') `
        -Name 'Regex builder applies its pattern and flags to the selected native All Apps target'
}

Invoke-OwnedRoute -Route 'regexcheat' -ExpectedTitle 'Regex Cheatsheet' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeRegexCheatSafety',
        'NativeRegexCheatSearchBox',
        'NativeRegexCheatRegexMode',
        'NativeRegexCheatCategory',
        'NativeRegexCheatRegexBuilder',
        'NativeRegexCheatRegexStatus',
        'NativeRegexCheatResultCount',
        'NativeRegexCheatEntryList',
        'NativeRegexCheatRecipeList',
        'NativeRegexCheatCopyRecipe_email',
        'NativeRegexCheatCopyStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexCheatResultCount' -Prefix '67 of 67' | Out-Null
    Assert-True -Condition $true -Name 'Regex Cheatsheet exposes the complete local native reference and copy-only recipes'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexCheatSearchBox' -Value 'atomic' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatCopyEntry_atomic' | Out-Null
    $category = Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatCategory'
    Select-ComboIndex -Combo $category -Index 3
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexCheatResultCount' -Prefix '1 of 67' | Out-Null
    Assert-True -Condition $true -Name 'Regex Cheatsheet literal filtering intersects the Quantifiers category without executing a reference token'

    Set-ToggleState -Root $root -AutomationId 'NativeRegexCheatRegexMode' -IsOn $true | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexCheatSearchBox' -Value '^\(\?>a\*\)$' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatCopyEntry_atomic' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexCheatRegexStatus' -Prefix 'PCRE2 local reference filter is active' | Out-Null
    Assert-True -Condition $true -Name 'Regex Cheatsheet evaluates an explicit PCRE2 filter only against static local reference fields'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexCheatSearchBox' -Value '[' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexCheatRegexStatus' -Prefix 'Invalid PCRE2 filter' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatCopyEntry_atomic' | Out-Null
    Assert-True -Condition $true -Name 'Regex Cheatsheet retains the previous visible rows while an invalid PCRE2 filter is corrected'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexCheatSearchBox' -Value '^\(\?>a\*\)$' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexCheatCopyEntry_atomic'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexCheatCopyStatus' -Prefix 'Reference token copied' | Out-Null
    Assert-True -Condition $true -Name 'Regex Cheatsheet writes to the clipboard only after an explicit Copy token action'

    $cheatControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatSearchBox'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatRegexMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatCategory'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatRegexBuilder'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatRegexStatus'))
    Assert-True -Condition $cheatControlsFit -Name 'Regex Cheatsheet search and builder controls are horizontally unclipped'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexCheatRegexBuilder'
    Wait-ForPageTitle -Root $root -Prefix 'Regex Tester & Builder' | Out-Null
    $target = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderTarget'
    Select-ComboIndex -Combo $target -Index 3
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderApply'
    Wait-ForPageTitle -Root $root -Prefix 'Regex Cheatsheet' | Out-Null
    $cheatMode = Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatRegexMode'
    $cheatToggle = [System.Windows.Automation.TogglePattern]$cheatMode.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $cheatValue = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeRegexCheatSearchBox')).Current.Value
    Assert-True -Condition ($cheatToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and $cheatValue -eq '^\(\?>a\*\)$') `
        -Name 'Regex builder applies a verified pattern and flags to the native Cheatsheet local-only target'
}

Invoke-OwnedRoute -Route 'symbols' -ExpectedTitle 'Symbols Palette' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeSymbolsSafety',
        'NativeSymbolsSearch',
        'NativeSymbolsRegexMode',
        'NativeSymbolsCategory',
        'NativeSymbolsRegexBuilder',
        'NativeSymbolsStatus',
        'NativeSymbolsResultCount',
        'NativeSymbolsList',
        'NativeSymbolsCopyStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSymbolsResultCount' -Prefix '226 of 226' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeSymbolsCopy_8592_arrows' | Out-Null
    Assert-True -Condition $true -Name 'Symbols Palette exposes all 226 static local glyphs and explicit-copy controls'

    $category = Wait-ForElement -Root $root -AutomationId 'NativeSymbolsCategory'
    Select-ComboIndex -Combo $category -Index 2
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSymbolsResultCount' -Prefix '32 of 226' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSymbolsSearch' -Value '  nOt EqUaL  ' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeSymbolsEntry_8800_math' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSymbolsResultCount' -Prefix '1 of 226' | Out-Null
    Assert-True -Condition $true -Name 'Symbols Palette keeps category intersection and trimmed literal filtering local'

    Set-ToggleState -Root $root -AutomationId 'NativeSymbolsRegexMode' -IsOn $true | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSymbolsSearch' -Value '^Not equal$' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeSymbolsCopy_8800_math' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSymbolsStatus' -Prefix 'PCRE2 local symbol filter is active' | Out-Null
    Assert-True -Condition $true -Name 'Symbols Palette runs bounded PCRE2 only over static local entries'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSymbolsSearch' -Value '[' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSymbolsStatus' -Prefix 'Invalid PCRE2 filter' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeSymbolsCopy_8800_math' | Out-Null
    Assert-True -Condition $true -Name 'Symbols Palette retains prior results while an invalid regex is corrected'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSymbolsSearch' -Value '^Not equal$' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeSymbolsCopy_8800_math'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSymbolsCopyStatus' -Prefix 'Copied' | Out-Null
    Assert-True -Condition $true -Name 'Symbols Palette copies only after an explicit action'

    $symbolsControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeSymbolsSearch'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSymbolsRegexMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSymbolsCategory'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSymbolsRegexBuilder'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSymbolsStatus'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSymbolsCopy_8800_math'))
    Assert-True -Condition $symbolsControlsFit -Name 'Symbols Palette controls and matching row are horizontally unclipped'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeSymbolsRegexBuilder'
    Wait-ForPageTitle -Root $root -Prefix 'Regex Tester & Builder' | Out-Null
    $target = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderTarget'
    Select-ComboIndex -Combo $target -Index 4
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderApply'
    Wait-ForPageTitle -Root $root -Prefix 'Symbols Palette' | Out-Null
    $symbolsMode = Wait-ForElement -Root $root -AutomationId 'NativeSymbolsRegexMode'
    $symbolsToggle = [System.Windows.Automation.TogglePattern]$symbolsMode.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $symbolsValue = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeSymbolsSearch')).Current.Value
    Assert-True -Condition ($symbolsToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and $symbolsValue -eq '^Not equal$') `
        -Name 'Regex Builder returns verified Symbols state to its native local-only target'
}

foreach ($symbolsAlias in @('glyphs', 'module.symbols')) {
    Invoke-OwnedRoute -Route $symbolsAlias -ExpectedTitle 'Symbols Palette' -Inspect {
        param($root, $title)
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSymbolsResultCount' -Prefix '226 of 226' | Out-Null
        Assert-True -Condition $true -Name 'Symbols Palette aliases resolve through the native route index'
    }
}

Invoke-OwnedRoute -Route 'package-updates' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $updatesHeader = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    $updatesHeaderName = $updatesHeader.Current.Name
    $queryAudit = Wait-ForElement -Root $root -AutomationId 'NativePackageQueryAudit'
    $auditBefore = $queryAudit.Current.Name
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeShellRegexBuilder'
    Wait-ForPageTitle -Root $root -Prefix 'Regex Tester & Builder' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeRegexPattern' -Value '^module\.reactor$' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRegexStatus' -Prefix 'PCRE2 pattern is valid' | Out-Null
    $target = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderTarget'
    Select-ComboIndex -Combo $target -Index 2
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderApply'
    Wait-ForPageTitle -Root $root -Prefix 'Package Manager' | Out-Null

    $discoverHeader = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    $packageMode = Wait-ForElement -Root $root -AutomationId 'NativePackageRegexMode'
    $packageToggle = [System.Windows.Automation.TogglePattern]$packageMode.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $packageValue = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchBox')).Current.Value
    $primary = Wait-ForElement -Root $root -AutomationId 'NativePackagePrimaryAction'
    $queryAudit = Wait-ForElement -Root $root -AutomationId 'NativePackageQueryAudit'
    Assert-True -Condition ($updatesHeaderName.StartsWith('Available updates', [StringComparison]::Ordinal) `
        -and $discoverHeader.Current.Name.StartsWith('Discover packages', [StringComparison]::Ordinal) `
        -and $packageToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On `
        -and $packageValue -eq '^module\.reactor$' `
        -and -not $primary.Current.IsEnabled `
        -and $queryAudit.Current.Name -eq $auditBefore) `
        -Name 'Regex builder deterministically targets Discover, clears stale Update rows, and never starts a package query'
}

Invoke-OwnedRoute -Route 'checkdigit' -ExpectedTitle 'Check Digit Validator' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeCheckDigitImplementationStatus',
        'NativeCheckDigitScheme',
        'NativeCheckDigitInput',
        'NativeCheckDigitBadge',
        'NativeCheckDigitDetail',
        'NativeCheckDigitStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Assert-True -Condition $true -Name 'Check Digit exposes its native control and accessibility contract'

    $scheme = Wait-ForElement -Root $root -AutomationId 'NativeCheckDigitScheme'
    $input = Wait-ForElement -Root $root -AutomationId 'NativeCheckDigitInput'
    Assert-True -Condition ($scheme.Current.Name.StartsWith('Check digit scheme', [StringComparison]::Ordinal)) `
        -Name 'Check Digit scheme picker has a localized accessible name'
    Assert-True -Condition ($input.Current.Name.StartsWith('Value to check', [StringComparison]::Ordinal)) `
        -Name 'Check Digit input has a localized accessible name'
    $value = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)

    $cases = @(
        @{ Index = 0; Label = 'Luhn'; Value = '4111 1111 1111 1111'; Detail = 'Expected last digit: 1' },
        @{ Index = 1; Label = 'ISBN-10'; Value = '0-306-40615-2'; Detail = 'Expected check: 2' },
        @{ Index = 2; Label = 'ISBN-13'; Value = '978-0-306-40615-7'; Detail = 'Expected check digit: 7' },
        @{ Index = 3; Label = 'EAN-13'; Value = '4006381333931'; Detail = 'Expected check digit: 1' },
        @{ Index = 4; Label = 'UPC-A'; Value = '036000291452'; Detail = 'Expected check digit: 2' },
        @{ Index = 5; Label = 'IBAN'; Value = 'GB82 WEST 1234 5698 7654 32'; Detail = 'mod-97 = 1' }
    )
    foreach ($case in $cases) {
        Select-ComboIndex -Combo $scheme -Index $case.Index
        $value.SetValue($case.Value)
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCheckDigitBadge' -Prefix 'VALID' | Out-Null
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCheckDigitDetail' -Prefix $case.Detail | Out-Null
        Assert-True -Condition $true -Name "Check Digit validates $($case.Label) through the live native UI"
    }

    Select-ComboIndex -Combo $scheme -Index 0
    $value.SetValue('4111111111111112')
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCheckDigitBadge' -Prefix 'INVALID' | Out-Null
    Assert-True -Condition $true -Name 'Check Digit surfaces an invalid checksum without treating it as a parse failure'

    $value.SetValue('4111A')
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCheckDigitStatus' -Prefix 'Digits only for Luhn' | Out-Null
    $detail = Find-ByAutomationId -Root $root -AutomationId 'NativeCheckDigitDetail'
    Assert-True -Condition ($detail -and [string]::IsNullOrEmpty($detail.Current.Name)) `
        -Name 'Check Digit clears stale accessible detail after malformed input'

    Select-ComboIndex -Combo $scheme -Index 5
    $value.SetValue('GB82 WEST 1234 5698 7654 32')
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Check Digit Validator' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCheckDigitBadge' -Prefix 'VALID' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCheckDigitDetail' -Prefix 'mod-97 = 1' | Out-Null
    Assert-True -Condition $true -Name 'Check Digit preserves scheme, input, and validation across language rerender'
}

foreach ($alias in @('luhn', 'module.checkdigit')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Check Digit Validator'
}

Invoke-OwnedRoute -Route 'binarytext' -ExpectedTitle 'Text to Binary' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeBinaryTextImplementationStatus',
        'NativeBinaryTextBase',
        'NativeBinaryTextInput',
        'NativeBinaryTextEncode',
        'NativeBinaryTextDecode',
        'NativeBinaryTextSwap',
        'NativeBinaryTextCopy',
        'NativeBinaryTextOutput',
        'NativeBinaryTextStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Assert-True -Condition $true -Name 'Binary Text exposes its native control and accessibility contract'

    $base = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextBase'
    $input = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextInput'
    $output = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextOutput'
    Assert-True -Condition ($base.Current.Name.StartsWith('Numeric base', [StringComparison]::Ordinal)) `
        -Name 'Binary Text base picker has a localized accessible name'
    Assert-True -Condition ($input.Current.Name.StartsWith('Binary Text input', [StringComparison]::Ordinal)) `
        -Name 'Binary Text input has a localized accessible name'

    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $encode = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextEncode'
    $encodeInvoke = [System.Windows.Automation.InvokePattern]$encode.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeBinaryTextInput' -Value 'Hi'
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $encodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextOutput' -ExpectedValue '01001000 01101001' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBinaryTextStatus' -Prefix 'Encoded to numeric codes' | Out-Null
    Assert-True -Condition $true -Name 'Binary Text encodes padded binary bytes through the live native UI'

    Select-ComboIndex -Combo $base -Index 3
    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeBinaryTextInput' -Value 'Hi'
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $encodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextOutput' -ExpectedValue '48 69' | Out-Null
    Assert-True -Condition $true -Name 'Binary Text switches to hexadecimal byte encoding through the live native UI'

    $swap = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextSwap'
    ([System.Windows.Automation.InvokePattern]$swap.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextInput' -ExpectedValue '48 69' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextOutput' -ExpectedValue '' | Out-Null
    Assert-True -Condition $true -Name 'Binary Text moves output to input and clears stale output'

    $input = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextInput'
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $decode = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextDecode'
    $decodeInvoke = [System.Windows.Automation.InvokePattern]$decode.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeBinaryTextInput' -Value '0x48 0X69'
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $decodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextOutput' -ExpectedValue 'Hi' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBinaryTextStatus' -Prefix 'Decoded back to text' | Out-Null
    Assert-True -Condition $true -Name 'Binary Text decodes prefixed hexadecimal bytes through the live native UI'

    $copy = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextCopy'
    ([System.Windows.Automation.InvokePattern]$copy.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBinaryTextStatus' -Prefix 'Output copied to clipboard' | Out-Null
    Assert-True -Condition $true -Name 'Binary Text copies output only after the explicit live native action'

    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeBinaryTextInput' -Value 'GG'
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $decodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBinaryTextStatus' -Prefix 'Some codes are not valid' | Out-Null
    Assert-True -Condition $true -Name 'Binary Text clears stale output after malformed codes'

    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeBinaryTextInput' -Value '0x48 0X69'
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $decodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextOutput' -ExpectedValue 'Hi' | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Text to Binary' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextInput' -ExpectedValue '0x48 0X69' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBinaryTextOutput' -ExpectedValue 'Hi' | Out-Null
    $base = Wait-ForElement -Root $root -AutomationId 'NativeBinaryTextBase'
    $selection = [System.Windows.Automation.SelectionPattern]$base.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    $selected = $selection.Current.GetSelection()
    Assert-True -Condition ($selected.Count -eq 1 -and $selected[0].Current.Name -eq 'Hex (base 16)') `
        -Name 'Binary Text preserves selected base, input, and output across language rerender'
}

foreach ($alias in @('textbinary', 'module.binarytext')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Text to Binary'
}

Invoke-OwnedRoute -Route 'base32' -ExpectedTitle 'Base32 / 58 / 85' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeCodecImplementationStatus',
        'NativeCodecPicker',
        'NativeCodecInput',
        'NativeCodecEncode',
        'NativeCodecDecode',
        'NativeCodecSwap',
        'NativeCodecCopy',
        'NativeCodecOutput',
        'NativeCodecStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    $picker = Wait-ForElement -Root $root -AutomationId 'NativeCodecPicker'
    $input = Wait-ForElement -Root $root -AutomationId 'NativeCodecInput'
    $output = Wait-ForElement -Root $root -AutomationId 'NativeCodecOutput'
    $codecControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        $picker,
        $input,
        $output,
        (Wait-ForElement -Root $root -AutomationId 'NativeCodecEncode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCodecDecode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCodecSwap'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCodecCopy'))
    Assert-True -Condition $codecControlsFit -Name 'Base32 / 58 / 85 exposes native controls, accessibility, and horizontal clipping safety'

    Assert-True -Condition ($picker.Current.Name.StartsWith('Text codec', [StringComparison]::Ordinal)) `
        -Name 'Base32 / 58 / 85 picker has a localized accessible name'
    Assert-True -Condition ($input.Current.Name.StartsWith('Codec input', [StringComparison]::Ordinal)) `
        -Name 'Base32 / 58 / 85 input has a localized accessible name'

    $encode = Wait-ForElement -Root $root -AutomationId 'NativeCodecEncode'
    $encodeInvoke = [System.Windows.Automation.InvokePattern]$encode.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeCodecInput' -Value 'foo'
    $encodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue 'MZXW6===' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCodecStatus' -Prefix 'Encoded successfully' | Out-Null
    Assert-True -Condition $true -Name 'Base32 / 58 / 85 encodes padded RFC 4648 Base32 through the live native UI'

    Select-ComboItem -Combo $picker -Name 'Base58 (Bitcoin)'
    Set-ElementValueAndWait -Root $root -AutomationId 'NativeCodecInput' -Value 'Hello World!' | Out-Null
    $encodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue '2NEpo7TZRRrLZSi2U' | Out-Null
    Assert-True -Condition $true -Name 'Base32 / 58 / 85 encodes Bitcoin Base58 through the live native UI'

    Select-ComboItem -Combo $picker -Name 'Ascii85 (Adobe)'
    Set-ElementValueAndWait -Root $root -AutomationId 'NativeCodecInput' -Value 'Hello' | Out-Null
    $encodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue '<~87cURDZ~>' | Out-Null
    Assert-True -Condition $true -Name 'Base32 / 58 / 85 encodes Adobe Ascii85 through the live native UI'

    $decode = Wait-ForElement -Root $root -AutomationId 'NativeCodecDecode'
    $decodeInvoke = [System.Windows.Automation.InvokePattern]$decode.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    Set-ElementValueAndWait -Root $root -AutomationId 'NativeCodecInput' -Value '<~87cURDZ~>' | Out-Null
    $decodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue 'Hello' | Out-Null
    Assert-True -Condition $true -Name 'Base32 / 58 / 85 decodes Adobe Ascii85 through the live native UI'

    $swap = Wait-ForElement -Root $root -AutomationId 'NativeCodecSwap'
    ([System.Windows.Automation.InvokePattern]$swap.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecInput' -ExpectedValue 'Hello' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue '' | Out-Null
    Assert-True -Condition $true -Name 'Base32 / 58 / 85 moves output to input and clears stale output'

    Select-ComboItem -Combo $picker -Name 'Base58 (Bitcoin)'
    Set-ElementValueAndWait -Root $root -AutomationId 'NativeCodecInput' -Value 'not-valid!' | Out-Null
    $decodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCodecStatus' -Prefix 'Input is not valid' | Out-Null
    Assert-True -Condition $true -Name 'Base32 / 58 / 85 clears stale output after malformed codec input'

    Select-ComboItem -Combo $picker -Name 'Base32 (no padding)'
    Set-ElementValueAndWait -Root $root -AutomationId 'NativeCodecInput' -Value 'foo' | Out-Null
    $encodeInvoke.Invoke()
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue 'MZXW6' | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Base32 / 58 / 85' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecInput' -ExpectedValue 'foo' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCodecOutput' -ExpectedValue 'MZXW6' | Out-Null
    $picker = Wait-ForElement -Root $root -AutomationId 'NativeCodecPicker'
    $selection = [System.Windows.Automation.SelectionPattern]$picker.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    $selected = $selection.Current.GetSelection()
    Assert-True -Condition ($selected.Count -eq 1 -and $selected[0].Current.Name -eq 'Base32 (no padding)') `
        -Name 'Base32 / 58 / 85 preserves codec, input, and output across language rerender'
}

foreach ($alias in @('base58', 'module.base32')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Base32 / 58 / 85'
}

Invoke-OwnedRoute -Route 'caseconvert' -ExpectedTitle 'Case Converter' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeCaseConvertImplementationStatus',
        'NativeCaseConvertInput',
        'NativeCaseConvertStatus',
        'NativeCaseConvertOutputCamel',
        'NativeCaseConvertOutputPascal',
        'NativeCaseConvertOutputSnake',
        'NativeCaseConvertOutputKebab',
        'NativeCaseConvertOutputConstant',
        'NativeCaseConvertOutputTitle',
        'NativeCaseConvertOutputSentence',
        'NativeCaseConvertOutputDot',
        'NativeCaseConvertOutputPath',
        'NativeCaseConvertOutputTrain',
        'NativeCaseConvertCopyCamel'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Assert-True -Condition $true -Name 'Case Converter exposes its native control and accessibility contract'

    $input = Wait-ForElement -Root $root -AutomationId 'NativeCaseConvertInput'
    Assert-True -Condition ($input.Current.Name.StartsWith('Input text', [StringComparison]::Ordinal)) `
        -Name 'Case Converter input has a localized accessible name'
    $input = Set-EditableValueAndWait -Root $root -AutomationId 'NativeCaseConvertInput' -Value 'helloWorld42API'

    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputCamel' -ExpectedValue 'helloWorld42Api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputPascal' -ExpectedValue 'HelloWorld42Api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputSnake' -ExpectedValue 'hello_world_42_api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputKebab' -ExpectedValue 'hello-world-42-api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputConstant' -ExpectedValue 'HELLO_WORLD_42_API' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputTitle' -ExpectedValue 'Hello World 42 Api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputSentence' -ExpectedValue 'Hello world 42 api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputDot' -ExpectedValue 'hello.world.42.api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputPath' -ExpectedValue 'hello/world/42/api' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputTrain' -ExpectedValue 'Hello-World-42-Api' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCaseConvertStatus' -Prefix '4 word(s) detected.' | Out-Null
    Assert-True -Condition $true -Name 'Case Converter renders all ten forms through the live native UI'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeCaseConvertCopyCamel'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCaseConvertStatus' -Prefix 'Output copied to clipboard' | Out-Null
    Assert-True -Condition $true -Name 'Case Converter copies a populated row through the live native UI'

    $input = Set-EditableValueAndWait -Root $root -AutomationId 'NativeCaseConvertInput' -Value ''
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeCaseConvertCopyCamel'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCaseConvertStatus' -Prefix 'Nothing to copy' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputCamel' -ExpectedValue '' | Out-Null
    Assert-True -Condition $true -Name 'Case Converter clears stale values and copies empty rows explicitly'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCaseConvertInput' -Value 'helloWorld42API' | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Case Converter' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputCamel' -ExpectedValue 'helloWorld42Api' | Out-Null
    Assert-True -Condition $true -Name 'Case Converter preserves input and outputs across language rerender'
}

foreach ($alias in @('caseconvert', 'module.caseconvert')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Case Converter'
}

Invoke-OwnedRoute -Route 'guidgen' -ExpectedTitle 'GUID & ID Generator' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeGuidGenImplementationStatus',
        'NativeGuidGenFormatPicker',
        'NativeGuidGenUpperSwitch',
        'NativeGuidGenGuidOutput',
        'NativeGuidGenGenerateGuid',
        'NativeGuidGenCopyGuid',
        'NativeGuidGenBulkCount',
        'NativeGuidGenGenerateBulk',
        'NativeGuidGenBulkOutput',
        'NativeGuidGenUlidOutput',
        'NativeGuidGenGenerateUlid',
        'NativeGuidGenNanoLength',
        'NativeGuidGenNanoOutput',
        'NativeGuidGenGenerateNano',
        'NativeGuidGenInspectInput',
        'NativeGuidGenInspectHex',
        'NativeGuidGenInspectMeta',
        'NativeGuidGenStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Assert-True -Condition $true -Name 'GUID Generator exposes its native controls and accessibility contract'

    $format = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenFormatPicker'
    $upper = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenUpperSwitch'
    $guid = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenGuidOutput'
    Assert-True -Condition ($format.Current.Name.StartsWith('GUID format', [StringComparison]::Ordinal)) `
        -Name 'GUID Generator format picker has a localized accessible name'
    Assert-True -Condition ($upper.Current.Name.StartsWith('Uppercase GUID output', [StringComparison]::Ordinal)) `
        -Name 'GUID Generator uppercase switch has a localized accessible name'

    $guidValue = [System.Windows.Automation.ValuePattern]$guid.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Assert-True -Condition ($guidValue.Current.Value -match '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') `
        -Name 'GUID Generator creates an initial GUID through the live native UI'

    Select-ComboIndex -Combo $format -Index 1
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeGuidGenGenerateGuid'
    $guid = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenGuidOutput'
    $guidValue = [System.Windows.Automation.ValuePattern]$guid.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Assert-True -Condition ($guidValue.Current.Value -match '^[0-9a-f]{32}$') `
        -Name 'GUID Generator switches to N format through the live native UI'

    $toggle = [System.Windows.Automation.TogglePattern]$upper.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $toggle.Toggle()
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeGuidGenGenerateGuid'
    $guid = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenGuidOutput'
    $guidValue = [System.Windows.Automation.ValuePattern]$guid.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Assert-True -Condition ($guidValue.Current.Value -match '^[0-9A-F]{32}$') `
        -Name 'GUID Generator uppercases generated GUIDs through the live native UI'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeGuidGenGenerateBulk'
    Wait-ForElementValueWhere -Root $root -AutomationId 'NativeGuidGenBulkOutput' `
        -Description 'ten uppercase N-format GUID lines' `
        -Predicate {
            param($value)
            return ([regex]::Matches($value, '(^|[\r\n])[0-9A-F]{32}(?=($|[\r\n]))').Count -eq 10)
        } | Out-Null
    Assert-True -Condition $true `
        -Name 'GUID Generator bulk-generates the default count through the live native UI'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeGuidGenGenerateUlid'
    $ulid = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenUlidOutput'
    $ulidValue = [System.Windows.Automation.ValuePattern]$ulid.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Assert-True -Condition ($ulidValue.Current.Value -match '^[0-9A-HJKMNP-TV-Z]{26}$') `
        -Name 'GUID Generator creates a ULID through the live native UI'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeGuidGenGenerateNano'
    $nano = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenNanoOutput'
    $nanoValue = [System.Windows.Automation.ValuePattern]$nano.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Assert-True -Condition ($nanoValue.Current.Value -match '^[0-9A-Za-z_-]{21}$') `
        -Name 'GUID Generator creates a nano-ID through the live native UI'

    $inspect = Wait-ForElement -Root $root -AutomationId 'NativeGuidGenInspectInput'
    $inspectValue = [System.Windows.Automation.ValuePattern]$inspect.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    $inspect = Set-ElementValueAndWait -Root $root -AutomationId 'NativeGuidGenInspectInput' -Value '00112233-4455-6677-8899-aabbccddeeff'
    $inspectValue = [System.Windows.Automation.ValuePattern]$inspect.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Wait-ForElementValue -Root $root -AutomationId 'NativeGuidGenInspectHex' -ExpectedValue '00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeGuidGenInspectMeta' -Prefix 'Version: 6' | Out-Null
    Assert-True -Condition $true -Name 'GUID Generator inspects GUID bytes and version through the live native UI'

    $inspect = Set-ElementValueAndWait -Root $root -AutomationId 'NativeGuidGenInspectInput' -Value 'not-a-guid'
    $inspectValue = [System.Windows.Automation.ValuePattern]$inspect.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeGuidGenStatus' -Prefix 'That is not a valid GUID' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeGuidGenInspectHex' -ExpectedValue '' | Out-Null
    Assert-True -Condition $true -Name 'GUID Generator clears stale inspector output after invalid input'

    Set-ElementValueAndWait -Root $root -AutomationId 'NativeGuidGenInspectInput' -Value '00112233-4455-6677-8899-aabbccddeeff' | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'GUID & ID Generator' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeGuidGenInspectHex' -ExpectedValue '00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF' | Out-Null
    Assert-True -Condition $true -Name 'GUID Generator preserves inspector state across language rerender'
}

foreach ($alias in @('module.guidgen')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'GUID & ID Generator'
}

Invoke-OwnedRoute -Route 'passgen' -ExpectedTitle 'Password Generator' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativePassGenImplementationStatus',
        'NativePassGenMode',
        'NativePassGenLength',
        'NativePassGenLower',
        'NativePassGenUpper',
        'NativePassGenDigits',
        'NativePassGenSymbols',
        'NativePassGenAvoidAmbiguous',
        'NativePassGenNoRepeats',
        'NativePassGenCount',
        'NativePassGenGenerate',
        'NativePassGenCopy',
        'NativePassGenEntropy',
        'NativePassGenEntropyBar',
        'NativePassGenOutput',
        'NativePassGenStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $passwordControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenLength'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenLower'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenUpper'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenDigits'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenSymbols'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenAvoidAmbiguous'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenNoRepeats'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenCount'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenGenerate'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenOutput'))
    Assert-True -Condition $passwordControlsFit `
        -Name 'Password Generator exposes native controls, accessibility, and horizontal clipping safety'

    $mode = Wait-ForElement -Root $root -AutomationId 'NativePassGenMode'
    $length = Wait-ForElement -Root $root -AutomationId 'NativePassGenLength'
    $output = Wait-ForElement -Root $root -AutomationId 'NativePassGenOutput'
    Assert-True -Condition ($mode.Current.Name.StartsWith('Generator mode', [StringComparison]::Ordinal)) `
        -Name 'Password Generator mode picker has a localized accessible name'
    Assert-True -Condition ($length.Current.Name.StartsWith('Password length', [StringComparison]::Ordinal)) `
        -Name 'Password Generator length control has a localized accessible name'
    Assert-True -Condition ($output.Current.Name.StartsWith('Generated password', [StringComparison]::Ordinal)) `
        -Name 'Password Generator output has a localized accessible name'

    Wait-ForElementValueWhere -Root $root -AutomationId 'NativePassGenOutput' `
        -Description 'a default sixteen-character password covering every selected class' `
        -Predicate {
            param($value)
            return $value -match '^(?=.{16}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()\-_=+\[\]{};:,.?/]).+$'
        } | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePassGenStatus' -Prefix 'Generated 1' | Out-Null
    Assert-True -Condition $true -Name 'Password Generator emits a secure default password through the live native UI'

    Set-ToggleState -Root $root -AutomationId 'NativePassGenLower' -IsOn $false | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenUpper' -IsOn $false | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenDigits' -IsOn $false | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenSymbols' -IsOn $false | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePassGenStatus' -Prefix "Can't generate: select at least one character set." | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativePassGenOutput' -ExpectedValue '' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativePassGenCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePassGenStatus' -Prefix 'Nothing to copy yet.' | Out-Null
    Assert-True -Condition $true -Name 'Password Generator fails closed and leaves the clipboard untouched for invalid empty output'

    Set-ToggleState -Root $root -AutomationId 'NativePassGenLower' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenUpper' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenDigits' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenSymbols' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenAvoidAmbiguous' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenNoRepeats' -IsOn $true | Out-Null
    Wait-ForElementValueWhere -Root $root -AutomationId 'NativePassGenOutput' `
        -Description 'a unique password without ambiguous glyphs' `
        -Predicate {
            param($value)
            if ($value.Length -ne 16 -or $value -cmatch '[O0Il1|]') { return $false }
            return (($value.ToCharArray() | Sort-Object -Unique).Count -eq 16)
        } | Out-Null
    Assert-True -Condition $true -Name 'Password Generator honors no-repeat and avoid-ambiguous controls through the live native UI'

    Select-ComboIndex -Combo $mode -Index 1
    Wait-ForElement -Root $root -AutomationId 'NativePassGenWordCount' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativePassGenSeparator' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativePassGenCapitalize' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativePassGenAppendDigit' | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenCapitalize' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePassGenAppendDigit' -IsOn $true | Out-Null
    $separator = Wait-ForElement -Root $root -AutomationId 'NativePassGenSeparator'
    Select-ComboIndex -Combo $separator -Index 3
    Wait-ForElementValueWhere -Root $root -AutomationId 'NativePassGenOutput' `
        -Description 'a four-word capitalized underscore passphrase with an appended digit' `
        -Predicate {
            param($value)
            return $value -match '^(?:[A-Z][a-z]{3}_){3}[A-Z][a-z]{3}\d$'
        } | Out-Null
    $passphraseControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenWordCount'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenSeparator'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenCapitalize'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenAppendDigit'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenCount'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenGenerate'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativePassGenOutput'))
    Assert-True -Condition $passphraseControlsFit `
        -Name 'Password Generator keeps passphrase controls inside horizontal bounds'
    Assert-True -Condition $true -Name 'Password Generator builds a configured passphrase through the live native UI'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativePassGenCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePassGenStatus' -Prefix 'Copied to clipboard.' | Out-Null
    Assert-True -Condition $true -Name 'Password Generator writes to the clipboard only after explicit Copy'

    $beforeLanguage = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativePassGenOutput')).Current.Value
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Password Generator' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativePassGenOutput' -ExpectedValue $beforeLanguage | Out-Null
    Assert-True -Condition $true -Name 'Password Generator preserves configured output across language rerender'
}

foreach ($alias in @('passgen', 'password', 'module.passgen')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Password Generator'
}

Invoke-OwnedRoute -Route 'passwordstrength' -ExpectedTitle 'Password Strength' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativePasswordStrengthImplementationStatus',
        'NativePasswordStrengthHiddenInput',
        'NativePasswordStrengthReveal',
        'NativePasswordStrengthBar',
        'NativePasswordStrengthBand',
        'NativePasswordStrengthStatus',
        'NativePasswordStrengthLength',
        'NativePasswordStrengthPool',
        'NativePasswordStrengthEntropy',
        'NativePasswordStrengthOnline',
        'NativePasswordStrengthGpu',
        'NativePasswordStrengthFast'
    )) {
        if ($id -eq 'NativeShellSearchBox') {
            Wait-ForElementVisible -Root $root -AutomationId $id | Out-Null
        }
        else {
            Wait-ForElement -Root $root -AutomationId $id | Out-Null
        }
    }

    foreach ($index in 0..9) {
        Wait-ForElement -Root $root -AutomationId "NativePasswordStrengthCheck$index" | Out-Null
    }

    $hidden = Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthHiddenInput'
    $reveal = Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthReveal'
    $status = Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthStatus'
    Assert-True -Condition ($hidden.Current.Name.StartsWith('Masked password to test', [StringComparison]::Ordinal)) `
        -Name 'Password Strength masks its initial input with an accessible local-only name'
    Assert-True -Condition ($reveal.Current.Name.StartsWith('Show the password', [StringComparison]::Ordinal)) `
        -Name 'Password Strength reveal control has a localized accessible name'
    Assert-True -Condition ($status.Current.Name.StartsWith('Start typing to analyze a password.', [StringComparison]::Ordinal)) `
        -Name 'Password Strength starts with the managed empty-input prompt'

    Set-ToggleState -Root $root -AutomationId 'NativePasswordStrengthReveal' -IsOn $true | Out-Null
    Wait-ForElementVisible -Root $root -AutomationId 'NativePasswordStrengthShownInput' | Out-Null
    $sample = 'Ab1!Ab1!Ab1!Ab1!'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativePasswordStrengthShownInput' -Value $sample | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthBand' -Prefix 'Very strong' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthStatus' -Prefix 'Very strong' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthLength' -Prefix 'Length:  16 characters' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthPool' -Prefix 'Character pool:  95 symbols' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthEntropy' -Prefix 'Entropy:  105.1 bits' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthCheck2' -Prefix 'Pass: At least 16 characters' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthCheck8' -Prefix 'Pass: No simple sequences' | Out-Null
    $status = Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthStatus'
    Assert-True -Condition ($status.Current.Name.IndexOf($sample, [StringComparison]::Ordinal) -lt 0) `
        -Name 'Password Strength keeps the typed value out of its live status surface'

    $strengthControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthShownInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthReveal'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthBar'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthBand'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthLength'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthPool'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthEntropy'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthOnline'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthGpu'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthFast'),
        (Wait-ForElement -Root $root -AutomationId 'NativePasswordStrengthCheck9'))
    Assert-True -Condition $strengthControlsFit `
        -Name 'Password Strength exposes native controls, accessibility, and horizontal clipping safety'

    Set-ToggleState -Root $root -AutomationId 'NativePasswordStrengthReveal' -IsOn $false | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePasswordStrengthReveal' -IsOn $true | Out-Null
    # Do not assert a revealed secret through a freshly re-created WinUI UIA
    # TextBox provider: after a collapsed-to-visible transition it may expose an
    # empty wrapper value even though the page model is intact. Verify the
    # corresponding non-secret analysis instead, preserving the no-secret-log
    # contract while proving the in-memory value survived both toggles.
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthBand' -Prefix 'Very strong' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthLength' -Prefix 'Length:  16 characters' | Out-Null
    Assert-True -Condition $true -Name 'Password Strength preserves its in-memory strength state across reveal toggles'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativePasswordStrengthShownInput' -Value 'password' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthCommonWarning' -Prefix 'Known common password' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthCheck9' -Prefix 'Needs work: Not a known common password' | Out-Null
    Assert-True -Condition $true -Name 'Password Strength flags its embedded local common-password blocklist'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativePasswordStrengthShownInput' -Value $sample | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Password Strength' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthBand' -Prefix 'Very strong' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePasswordStrengthLength' -Prefix 'Length:  16 characters' | Out-Null
    Assert-True -Condition $true -Name 'Password Strength preserves its in-memory strength state across language rerender'
}

foreach ($alias in @('passwordstrength', 'pwstrength', 'module.passwordstrength')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Password Strength'
}

Invoke-OwnedRoute -Route 'uuidv7' -ExpectedTitle 'UUID v7' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeUuidV7ImplementationStatus',
        'NativeUuidV7Count',
        'NativeUuidV7Monotonic',
        'NativeUuidV7Generate',
        'NativeUuidV7GeneratedOutput',
        'NativeUuidV7CopyGenerated',
        'NativeUuidV7DecodeInput',
        'NativeUuidV7Decode',
        'NativeUuidV7CopyTimestamp',
        'NativeUuidV7Status'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $uuidControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7Count'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7Monotonic'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7Generate'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7GeneratedOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7CopyGenerated'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7DecodeInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7Decode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV7CopyTimestamp'))
    Assert-True -Condition $uuidControlsFit `
        -Name 'UUID v7 exposes native controls, accessibility, and horizontal clipping safety'

    $count = Wait-ForElement -Root $root -AutomationId 'NativeUuidV7Count'
    $monotonic = Wait-ForElement -Root $root -AutomationId 'NativeUuidV7Monotonic'
    $decodeInput = Wait-ForElement -Root $root -AutomationId 'NativeUuidV7DecodeInput'
    Assert-True -Condition ($count.Current.Name.StartsWith('Number of UUID v7 values', [StringComparison]::Ordinal)) `
        -Name 'UUID v7 count has a localized accessible name'
    Assert-True -Condition ($monotonic.Current.Name.StartsWith('Keep UUID v7 values', [StringComparison]::Ordinal)) `
        -Name 'UUID v7 monotonic switch has a localized accessible name'
    Assert-True -Condition ($decodeInput.Current.Name.StartsWith('UUID value to decode', [StringComparison]::Ordinal)) `
        -Name 'UUID v7 decode input has a localized accessible name'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV7CopyGenerated'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV7Status' -Prefix 'Nothing to copy' | Out-Null
    Assert-True -Condition $true -Name 'UUID v7 leaves clipboard untouched until an explicit populated copy action'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV7Generate'
    Wait-ForElementValueWhere -Root $root -AutomationId 'NativeUuidV7GeneratedOutput' `
        -Description 'one canonical RFC 9562 UUIDv7' `
        -Predicate {
            param($value)
            return $value -match '^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
        } | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV7Status' -Prefix 'Generated 1 UUIDv7 value.' | Out-Null
    Assert-True -Condition $true -Name 'UUID v7 generates a canonical RFC 9562 value through the live native UI'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV7CopyGenerated'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV7Status' -Prefix 'Generated UUIDv7 values copied to clipboard' | Out-Null
    Assert-True -Condition $true -Name 'UUID v7 copies generated values only after explicit confirmation'

    Set-ElementValueAndWait -Root $root -AutomationId 'NativeUuidV7DecodeInput' `
        -Value 'urn:uuid:{01234567-89AB-7ABC-9D09-0A0B0C0D0E0F}' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV7Decode'
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV7VersionOutput' -ExpectedValue '7' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV7VariantOutput' -ExpectedValue 'RFC 4122/9562 (10xx)' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV7CanonicalOutput' `
        -ExpectedValue '01234567-89ab-7abc-9d09-0a0b0c0d0e0f' | Out-Null
    Wait-ForElementValueWhere -Root $root -AutomationId 'NativeUuidV7UtcOutput' `
        -Description 'decoded UTC timestamp' `
        -Predicate { param($value) return $value -match '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$' } | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV7Status' -Prefix 'Valid UUIDv7' | Out-Null
    Assert-True -Condition $true -Name 'UUID v7 decodes canonical URN input locally with timestamp and variant metadata'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV7CopyTimestamp'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV7Status' -Prefix 'Timestamp copied to clipboard' | Out-Null
    Assert-True -Condition $true -Name 'UUID v7 copies a decoded timestamp only through an explicit button'

    Set-ElementValueAndWait -Root $root -AutomationId 'NativeUuidV7DecodeInput' -Value 'not-a-uuid' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV7Decode'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV7Status' -Prefix 'That is not a valid UUID' | Out-Null
    $canonicalAfterInvalid = Find-ByAutomationId -Root $root -AutomationId 'NativeUuidV7CanonicalOutput'
    Assert-True -Condition ($null -eq $canonicalAfterInvalid) `
        -Name 'UUID v7 clears and collapses stale decode results after invalid input'

    Set-ElementValueAndWait -Root $root -AutomationId 'NativeUuidV7DecodeInput' `
        -Value '01234567-89ab-7abc-9d09-0a0b0c0d0e0f' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV7Decode'
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'UUID v7' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV7CanonicalOutput' `
        -ExpectedValue '01234567-89ab-7abc-9d09-0a0b0c0d0e0f' | Out-Null
    Assert-True -Condition $true -Name 'UUID v7 preserves decode state across language rerender'
}

foreach ($alias in @('uuidv7', 'module.uuidv7')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'UUID v7'
}

Invoke-OwnedRoute -Route 'roman' -ExpectedTitle 'Roman Numerals' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeRomanNumImplementationStatus',
        'NativeRomanNumExtendedSwitch',
        'NativeRomanNumExtendedNote',
        'NativeRomanNumNumberInput',
        'NativeRomanNumRomanOutput',
        'NativeRomanNumRomanBreakdown',
        'NativeRomanNumCopyRoman',
        'NativeRomanNumRomanInput',
        'NativeRomanNumNumberOutput',
        'NativeRomanNumNumberBreakdown',
        'NativeRomanNumCopyNumber',
        'NativeRomanNumStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $romanControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumExtendedSwitch'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumExtendedNote'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumNumberInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumRomanOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumCopyRoman'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumRomanInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumNumberOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeRomanNumCopyNumber'))
    Assert-True -Condition $romanControlsFit -Name 'Roman Numerals exposes native controls, accessibility, and horizontal clipping safety'

    $numberInput = Wait-ForElement -Root $root -AutomationId 'NativeRomanNumNumberInput'
    $romanInput = Wait-ForElement -Root $root -AutomationId 'NativeRomanNumRomanInput'
    $extended = Wait-ForElement -Root $root -AutomationId 'NativeRomanNumExtendedSwitch'
    Assert-True -Condition ($numberInput.Current.Name.StartsWith('Whole number input', [StringComparison]::Ordinal)) `
        -Name 'Roman Numerals number input has a localized accessible name'
    Assert-True -Condition ($romanInput.Current.Name.StartsWith('Roman numeral input', [StringComparison]::Ordinal)) `
        -Name 'Roman Numerals Roman input has a localized accessible name'

    $numberInput = Set-ElementValueAndWait -Root $root -AutomationId 'NativeRomanNumNumberInput' -Value '1994'
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumRomanOutput' -ExpectedValue 'MCMXCIV' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRomanNumRomanBreakdown' -Prefix '1,994 = M + CM + XC + IV' | Out-Null
    Assert-True -Condition $true -Name 'Roman Numerals converts a standard number through the live native UI'

    $romanInput = Set-ElementValueAndWait -Root $root -AutomationId 'NativeRomanNumRomanInput' -Value 'MCMXCIV'
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumNumberOutput' -ExpectedValue '1,994' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRomanNumNumberBreakdown' -Prefix '= M + CM + XC + IV' | Out-Null
    Assert-True -Condition $true -Name 'Roman Numerals parses canonical input through the live native UI'

    $toggle = [System.Windows.Automation.TogglePattern]$extended.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { $toggle.Toggle() }
    $numberInput = Set-ElementValueAndWait -Root $root -AutomationId 'NativeRomanNumNumberInput' -Value '4000'
    $fourThousand = "I$([char]0x0305)V$([char]0x0305)"
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumRomanOutput' -ExpectedValue $fourThousand | Out-Null
    Assert-True -Condition $true -Name 'Roman Numerals enables canonical vinculum output through the live native UI'

    $romanInput = Set-ElementValueAndWait -Root $root -AutomationId 'NativeRomanNumRomanInput' -Value '(IV)'
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumNumberOutput' -ExpectedValue '4,000' | Out-Null
    Assert-True -Condition $true -Name 'Roman Numerals accepts canonical parenthetical vinculum input through the live native UI'

    $romanInput = Set-ElementValueAndWait -Root $root -AutomationId 'NativeRomanNumRomanInput' -Value 'IIII'
    $placeholderDash = [string][char]0x2014
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumNumberOutput' -ExpectedValue $placeholderDash | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRomanNumStatus' -Prefix 'Malformed Roman numeral' | Out-Null
    Assert-True -Condition $true -Name 'Roman Numerals clears stale number output after malformed input'

    $numberInput = Set-ElementValueAndWait -Root $root -AutomationId 'NativeRomanNumNumberInput' -Value '1994'
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRomanNumCopyRoman'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeRomanNumStatus' -Prefix 'Copied: MCMXCIV' | Out-Null
    Assert-True -Condition $true -Name 'Roman Numerals copies a populated native result with the managed status contract'

    $romanInput = Set-ElementValueAndWait -Root $root -AutomationId 'NativeRomanNumRomanInput' -Value 'MCMXCIV'
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Roman Numerals' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumNumberInput' -ExpectedValue '1994' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumRomanOutput' -ExpectedValue 'MCMXCIV' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumRomanInput' -ExpectedValue 'MCMXCIV' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeRomanNumNumberOutput' -ExpectedValue '1,994' | Out-Null
    $extended = Wait-ForElement -Root $root -AutomationId 'NativeRomanNumExtendedSwitch'
    $toggle = [System.Windows.Automation.TogglePattern]$extended.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'Roman Numerals preserves range, inputs, and outputs across language rerender'
}

foreach ($alias in @('romannum', 'module.romannum')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Roman Numerals'
}

Invoke-OwnedRoute -Route 'unixperm' -ExpectedTitle 'chmod Calculator' -Inspect {
    param($root, $title)

    $ids = @(
        'NativeUnixPermImplementationStatus',
        'NativeUnixPermOwnerRead',
        'NativeUnixPermOwnerWrite',
        'NativeUnixPermOwnerExecute',
        'NativeUnixPermGroupRead',
        'NativeUnixPermGroupWrite',
        'NativeUnixPermGroupExecute',
        'NativeUnixPermOtherRead',
        'NativeUnixPermOtherWrite',
        'NativeUnixPermOtherExecute',
        'NativeUnixPermSetUid',
        'NativeUnixPermSetGid',
        'NativeUnixPermSticky',
        'NativeUnixPermOctalInput',
        'NativeUnixPermSymbolicInput',
        'NativeUnixPermCommandOutput',
        'NativeUnixPermCopyOctal',
        'NativeUnixPermCopySymbolic',
        'NativeUnixPermCopyCommand',
        'NativeUnixPermStatus'
    )
    foreach ($id in $ids) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $controlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermOwnerRead'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermOwnerWrite'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermOwnerExecute'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermOctalInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermCopyOctal'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermSymbolicInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermCopySymbolic'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermCommandOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnixPermCopyCommand'))
    Assert-True -Condition $controlsFit -Name 'chmod Calculator exposes native controls and horizontal clipping safety'

    $octal = Wait-ForElement -Root $root -AutomationId 'NativeUnixPermOctalInput'
    $symbolic = Wait-ForElement -Root $root -AutomationId 'NativeUnixPermSymbolicInput'
    Assert-True -Condition ($octal.Current.Name.StartsWith('Octal permission mode', [StringComparison]::Ordinal)) -Name 'chmod octal editor has a localized accessible name'
    Assert-True -Condition ($symbolic.Current.Name.StartsWith('Symbolic permission mode', [StringComparison]::Ordinal)) -Name 'chmod symbolic editor has a localized accessible name'

    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermOctalInput' -ExpectedValue '0644' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermSymbolicInput' -ExpectedValue 'rw-r--r--' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermCommandOutput' -ExpectedValue 'chmod 644 file' | Out-Null
    Assert-True -Condition $true -Name 'chmod Calculator starts at managed-parity mode 0644'

    $octal = Set-ElementValueAndWait -Root $root -AutomationId 'NativeUnixPermOctalInput' -Value '6755'
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermSymbolicInput' -ExpectedValue 'rwsr-sr-x' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermCommandOutput' -ExpectedValue 'chmod 6755 file' | Out-Null
    Assert-True -Condition $true -Name 'chmod octal editor updates special bits, symbolic mode, and command preview'

    foreach ($toggleExpectation in @(
        @{ Id = 'NativeUnixPermSetUid'; State = [System.Windows.Automation.ToggleState]::On },
        @{ Id = 'NativeUnixPermSetGid'; State = [System.Windows.Automation.ToggleState]::On },
        @{ Id = 'NativeUnixPermSticky'; State = [System.Windows.Automation.ToggleState]::Off }
    )) {
        $toggleElement = Wait-ForElement -Root $root -AutomationId $toggleExpectation.Id
        $toggle = [System.Windows.Automation.TogglePattern]$toggleElement.GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.Current.ToggleState -ne $toggleExpectation.State) {
            throw "$($toggleExpectation.Id) did not reflect mode 6755."
        }
    }
    Assert-True -Condition $true -Name 'chmod permission matrix reflects parsed setuid and setgid state'

    $octal = Set-ElementValueAndWait -Root $root -AutomationId 'NativeUnixPermOctalInput' -Value '6888'
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermSymbolicInput' -ExpectedValue 'rwsr-sr-x' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermCommandOutput' -ExpectedValue 'chmod 6755 file' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnixPermStatus' -Prefix 'Invalid octal' | Out-Null
    Assert-True -Condition $true -Name 'chmod invalid octal retains the last valid mode atomically'

    $symbolic = Set-ElementValueAndWait -Root $root -AutomationId 'NativeUnixPermSymbolicInput' -Value '--S--S--T'
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermOctalInput' -ExpectedValue '7000' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermCommandOutput' -ExpectedValue 'chmod 7000 file' | Out-Null
    Assert-True -Condition $true -Name 'chmod symbolic editor preserves uppercase special-bit semantics'

    $symbolic = Set-ElementValueAndWait -Root $root -AutomationId 'NativeUnixPermSymbolicInput' -Value 'rwxr-xr-z'
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermOctalInput' -ExpectedValue '7000' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermCommandOutput' -ExpectedValue 'chmod 7000 file' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnixPermStatus' -Prefix 'Invalid symbolic mode' | Out-Null
    Assert-True -Condition $true -Name 'chmod invalid symbolic input retains the last valid permission matrix'

    $octal = Set-ElementValueAndWait -Root $root -AutomationId 'NativeUnixPermOctalInput' -Value '6755'
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUnixPermCopyCommand'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnixPermStatus' -Prefix 'Copied: chmod 6755 file' | Out-Null
    Assert-True -Condition $true -Name 'chmod writes a command to the clipboard only after explicit Copy'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'chmod Calculator' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermOctalInput' -ExpectedValue '6755' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermSymbolicInput' -ExpectedValue 'rwsr-sr-x' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnixPermCommandOutput' -ExpectedValue 'chmod 6755 file' | Out-Null
    Assert-True -Condition $true -Name 'chmod preserves its complete valid mode across language rerender'
}

foreach ($alias in @('chmod', 'module.unixperm')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'chmod Calculator'
}

Invoke-OwnedRoute -Route 'package-updates' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativePackageManagerMigrationStatus',
        'NativePackageViewPicker',
        'NativePackageSortPicker',
        'NativePackageSearchBox',
        'NativePackagePrimaryAction',
        'NativePackageSecondaryAction',
        'NativePackageOperationsAction',
        'NativePackageResultsHeader'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    $packageControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativePackageViewPicker'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSortPicker'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchBox'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackagePrimaryAction'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSecondaryAction'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageOperationsAction'))
    Assert-True -Condition $packageControlsFit -Name 'Package Manager exposes native controls, accessibility, and horizontal clipping safety'

    $managerFilters = foreach ($manager in @('winget', 'scoop', 'choco', 'pip', 'npm', 'dotnet', 'psgallery', 'pwsh7', 'cargo', 'bun', 'vcpkg')) {
        Wait-ForElement -Root $root -AutomationId "NativePackageManagerFilter_$manager"
    }
    Assert-True -Condition $true -Name 'Package Manager exposes all 11 native manager filters'
    $managerFiltersFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @($managerFilters)
    Assert-True -Condition $managerFiltersFit `
        -Name 'Package Manager exposes every manager filter without horizontal clipping'

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Available updates', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-updates alias selects Updates'

    $sortPicker = Wait-ForElement -Root $root -AutomationId 'NativePackageSortPicker'
    $selection = [System.Windows.Automation.SelectionPattern]$sortPicker.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    Assert-True -Condition ([bool]$selection) -Name 'Package Manager exposes the persistent sort picker'

    $probeDeadline = [DateTime]::UtcNow.AddMilliseconds([Math]::Max($TimeoutMs, 30000))
    do {
        $ready = Find-ByAutomationId -Root $root -AutomationId 'NativePackageReadyState'
        if ($ready) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $probeDeadline)
    Assert-True -Condition ([bool]$ready) -Name 'Package Manager completes its live non-destructive engine probes'

    $availableManager = $null
    $querySucceeded = $false
    foreach ($manager in @('winget', 'scoop', 'choco', 'pip', 'npm', 'dotnet', 'psgallery', 'pwsh7', 'cargo', 'bun', 'vcpkg')) {
        $filter = Find-ByAutomationId -Root $root -AutomationId "NativePackageManagerFilter_$manager"
        if (-not $filter -or -not $filter.Current.IsEnabled) { continue }
        if (-not $availableManager) {
            $availableManager = $manager
            continue
        }
        $toggle = [System.Windows.Automation.TogglePattern]$filter.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) { $toggle.Toggle() }
    }
    if ($availableManager) {
        Assert-True -Condition $true -Name 'Package Manager unlocks a successfully probed native engine'
    }
    else {
        $lockedPrimary = Wait-ForElement -Root $root -AutomationId 'NativePackagePrimaryAction'
        Assert-True -Condition (-not $lockedPrimary.Current.IsEnabled) `
            -Name 'Package Manager keeps every failed or high-integrity engine probe locked'
    }

    if ($availableManager) {
        $primary = Wait-ForElement -Root $root -AutomationId 'NativePackagePrimaryAction'
        $primaryInvoke = [System.Windows.Automation.InvokePattern]$primary.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $primaryInvoke.Invoke()
        $queryDeadline = [DateTime]::UtcNow.AddSeconds(65)
        $queryCompleted = $false
        $querySucceeded = $false
        do {
            $working = Find-ByAutomationId -Root $root -AutomationId 'NativePackageWorkingState'
            $managerState = Find-ByAutomationId -Root $root -AutomationId "NativePackageManagerState_$availableManager"
            if (-not $working -and $managerState) {
                $queryCompleted = $true
                $querySucceeded = $managerState.Current.HelpText.StartsWith(
                    'Query completed successfully', [StringComparison]::Ordinal)
                break
            }
            Start-Sleep -Milliseconds 150
        } while ([DateTime]::UtcNow -lt $queryDeadline)
        Assert-True -Condition $queryCompleted -Name 'Package Manager completes a live read-only updates query'
        Assert-True -Condition $querySucceeded -Name 'Package Manager live updates query reports explicit engine success'
    }
    else {
        $lockedPrimary = Wait-ForElement -Root $root -AutomationId 'NativePackagePrimaryAction'
        Assert-True -Condition (-not $lockedPrimary.Current.IsEnabled) `
            -Name 'Package Manager does not start a live external query when no engine passes its safety probe'
    }

    # A cached Updates row may not exist on a clean machine. When it does,
    # exercise only the review step: it must create a visible batch and leave
    # every command deferred. Do not invoke the confirmation/cancellation UI
    # in a smoke campaign because that would request a real package mutation.
    $reviewedBatch = $false
    if ($querySucceeded) {
        $selectionToggle = Find-ByAutomationIdPrefix -Root $root -Prefix 'NativePackageSelect_'
        if ($selectionToggle) {
            $toggle = [System.Windows.Automation.TogglePattern]$selectionToggle.GetCurrentPattern(
                [System.Windows.Automation.TogglePattern]::Pattern)
            if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
                $toggle.Toggle()
            }
            Wait-ForElement -Root $root -AutomationId 'NativePackageBatchSelectionSummary' | Out-Null
            Invoke-ElementByAutomationId -Root $root -AutomationId 'NativePackageBatchReviewUpdate'

            $batchCard = Wait-ForElementByAutomationIdPrefix -Root $root -Prefix 'NativePackageMutationBatch_'
            $batchPreview = Wait-ForElementByAutomationIdPrefix -Root $root -Prefix 'NativePackageMutationBatchPreview_'
            $batchConfirm = Wait-ForElementByAutomationIdPrefix -Root $root -Prefix 'NativePackageMutationBatchConfirm_'
            $unexpectedWorking = Find-ByAutomationId -Root $root -AutomationId 'NativePackageWorkingState'
            $batchControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
                $batchCard,
                $batchPreview,
                $batchConfirm)
            Assert-True -Condition (-not [bool]$unexpectedWorking) `
                -Name 'Package Manager batch review keeps package commands deferred before explicit confirmation'
            Assert-True -Condition $batchControlsFit `
                -Name 'Package Manager batch review card, argv preview, and confirmation control are horizontally unclipped'
            $reviewedBatch = $true
        }
        else {
            Write-Host 'Package Manager batch review smoke skipped: the successful Updates query returned no selectable cached row.' -ForegroundColor Yellow
        }
    }

    if (-not $reviewedBatch) {
        $operations = Wait-ForElement -Root $root -AutomationId 'NativePackageOperationsAction'
        $invoke = [System.Windows.Automation.InvokePattern]$operations.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
    }
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $header = Find-ByAutomationId -Root $root -AutomationId 'NativePackageResultsHeader'
        if ($header -and $header.Current.Name.StartsWith('Operation queue and history', [StringComparison]::Ordinal)) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    $operationViewSelected = $header -and `
        $header.Current.Name.StartsWith('Operation queue and history', [StringComparison]::Ordinal)
    if (-not $operationViewSelected) {
        $actualHeader = if ($header) { $header.Current.Name } else { '(missing)' }
        Write-Host "Package Manager view switch diagnostic: header='$actualHeader'" -ForegroundColor Yellow
    }
    Assert-True -Condition $operationViewSelected `
        -Name 'Package Manager switches among its nine native views'
    $queueSummary = Wait-ForElement -Root $root -AutomationId 'NativePackageQueueSummary'
    Assert-True -Condition ($queueSummary.Current.Name.StartsWith('Native mutation consent policy', [StringComparison]::Ordinal)) `
        -Name 'Package Manager exposes explicit mutation-consent policy'
    $mutationPolicyFits = Test-HorizontalBoundsWithinWindow -Root $root -Elements @($queueSummary)
    Assert-True -Condition $mutationPolicyFits `
        -Name 'Package Manager mutation-consent policy is accessible and horizontally unclipped'
    $batchPolicy = Wait-ForElement -Root $root -AutomationId 'NativePackageBatchConsentPolicy'
    Assert-True -Condition ($batchPolicy.Current.Name.StartsWith('Native batch consent policy', [StringComparison]::Ordinal)) `
        -Name 'Package Manager exposes atomic batch-consent policy'
    $batchPolicyFits = Test-HorizontalBoundsWithinWindow -Root $root -Elements @($batchPolicy)
    Assert-True -Condition $batchPolicyFits `
        -Name 'Package Manager batch-consent policy is accessible and horizontally unclipped'
    $operationEntry = Find-ByAutomationId -Root $root -AutomationId 'NativePackageOperation_0'
    $runLast = Find-ByAutomationId -Root $root -AutomationId 'NativePackageOperationRunLast_0'
    $retry = Find-ByAutomationId -Root $root -AutomationId 'NativePackageOperationRetry_0'
    Assert-True -Condition ($null -ne $operationEntry -and $null -ne $runLast -and $null -ne $retry) `
        -Name 'Package Manager exposes preview queue reorder and retry controls'
}

Invoke-OwnedRoute -Route 'package-installed' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Installed packages', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-installed alias selects Installed'
}

Invoke-OwnedRoute -Route 'package-discover' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Discover packages', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-discover alias selects Discover'

    foreach ($id in @(
        'NativePackageSearchModePicker',
        'NativePackageSearchCaseSensitive',
        'NativePackageSearchIgnoreSpecial',
        'NativePackageRegexMode',
        'NativePackageRegexBuilder',
        'NativePackageRegexApply',
        'NativePackageRegexStatus',
        'NativePackageQueryAudit'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    $discoverFilterControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchModePicker'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchCaseSensitive'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchIgnoreSpecial'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageRegexMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageRegexBuilder'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageRegexApply'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageRegexStatus'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageQueryAudit')
    )
    Assert-True -Condition $discoverFilterControlsFit `
        -Name 'Package Manager Discover filter controls are accessible and horizontally unclipped'

    # Preferences persist across processes; begin the literal-filter contract
    # from a known state before exercising the local-only regex branch below.
    Set-ToggleState -Root $root -AutomationId 'NativePackageRegexMode' -IsOn $false | Out-Null

    # Start from a known *different* state. The package preferences persist
    # between native processes, so selecting Exact or enabling a toggle only
    # conditionally would skip the local-filter callback on a second smoke run.
    $mode = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchModePicker'
    Select-ComboIndex -Combo $mode -Index 0
    Select-ComboIndex -Combo $mode -Index 3
    $case = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchCaseSensitive'
    $caseToggle = [System.Windows.Automation.TogglePattern]$case.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    if ($caseToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) { $caseToggle.Toggle() }
    $caseToggle.Toggle()
    $ignoreSpecial = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchIgnoreSpecial'
    $ignoreSpecialToggle = [System.Windows.Automation.TogglePattern]$ignoreSpecial.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    if ($ignoreSpecialToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) { $ignoreSpecialToggle.Toggle() }
    $ignoreSpecialToggle.Toggle()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePackageLiveStatus' `
        -Prefix 'Package Manager status: Discover filters applied locally' | Out-Null
    Assert-True -Condition $true `
        -Name 'Package Manager re-filters cached Discover results without starting another package query'

    $queryAudit = Wait-ForElement -Root $root -AutomationId 'NativePackageQueryAudit'
    $auditBefore = $queryAudit.Current.Name
    Set-ToggleState -Root $root -AutomationId 'NativePackageRegexMode' -IsOn $true | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativePackageSearchBox' -Value '.*' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativePackageRegexApply'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePackageRegexStatus' -Prefix 'PCRE2 regex filters cached Discover results only' | Out-Null
    $queryAudit = Wait-ForElement -Root $root -AutomationId 'NativePackageQueryAudit'
    $primaryInRegexMode = Wait-ForElement -Root $root -AutomationId 'NativePackagePrimaryAction'
    $workingInRegexMode = Find-ByAutomationId -Root $root -AutomationId 'NativePackageWorkingState'
    Assert-True -Condition ($queryAudit.Current.Name -eq $auditBefore -and -not $primaryInRegexMode.Current.IsEnabled -and -not $workingInRegexMode) `
        -Name 'Package Manager regex applies only to cached Discover rows and cannot start a remote query'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativePackageSearchBox' -Value '[' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativePackageRegexApply'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePackageRegexStatus' -Prefix 'Regex syntax needs correction' | Out-Null
    $queryAudit = Wait-ForElement -Root $root -AutomationId 'NativePackageQueryAudit'
    Assert-True -Condition ($queryAudit.Current.Name -eq $auditBefore) `
        -Name 'Package Manager invalid regex keeps the remote query epoch unchanged'

    Set-ToggleState -Root $root -AutomationId 'NativePackageRegexMode' -IsOn $false | Out-Null

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Package Manager' | Out-Null
    $mode = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchModePicker'
    $modeSelection = [System.Windows.Automation.SelectionPattern]$mode.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    $selectedMode = $modeSelection.Current.GetSelection()
    $case = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchCaseSensitive'
    $caseToggle = [System.Windows.Automation.TogglePattern]$case.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $ignoreSpecial = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchIgnoreSpecial'
    $ignoreSpecialToggle = [System.Windows.Automation.TogglePattern]$ignoreSpecial.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($selectedMode.Count -eq 1 -and $selectedMode[0].Current.Name -eq 'Exact match' `
        -and $caseToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On `
        -and $ignoreSpecialToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'Package Manager preserves Discover filters across a language rerender'
}

Invoke-OwnedRoute -Route 'package-bundles' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Portable package bundles', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-bundles alias selects Bundles'
    $export = Wait-ForElement -Root $root -AutomationId 'NativePackagePrimaryAction'
    $import = Wait-ForElement -Root $root -AutomationId 'NativePackageSecondaryAction'
    $empty = Wait-ForElement -Root $root -AutomationId 'NativeBundleEmpty'
    Assert-True -Condition ($empty.Current.Name.StartsWith('No native bundle workspace yet', [StringComparison]::Ordinal) `
        -and -not $export.Current.IsEnabled -and $import.Current.IsEnabled) `
        -Name 'Package Manager Bundles exposes an explicit empty workspace, refuses empty export, and leaves inert import available'
    $bundleControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        $header,
        $export,
        $import,
        $empty)
    Assert-True -Condition $bundleControlsFit `
        -Name 'Package Manager Bundles export, import, and workspace guidance are accessible and horizontally unclipped'
}

Invoke-OwnedRoute -Route 'packages-bundles' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Portable package bundles', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-bundles alias selects Bundles'
}

Invoke-OwnedRoute -Route 'package-sources' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Package sources', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-sources alias selects Sources'
}

Invoke-OwnedRoute -Route 'packages-sources' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Package sources', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-sources alias selects Sources'
}

Invoke-OwnedRoute -Route 'package-ignored' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Ignored, pinned, and snoozed updates', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-ignored alias selects Ignored'
    $state = Find-ByAutomationId -Root $root -AutomationId 'NativePackageIgnoredEmpty'
    if (-not $state) {
        $state = Find-ByAutomationIdPrefix -Root $root -Prefix 'NativePackageIgnored_'
    }
    if (-not $state) {
        $state = Find-ByAutomationIdPrefix -Root $root -Prefix 'NativePackagePinned_'
    }
    if (-not $state) {
        $state = Find-ByAutomationIdPrefix -Root $root -Prefix 'NativePackageSnoozed_'
    }
    Assert-True -Condition ($null -ne $state) `
        -Name 'Package Manager package-ignored exposes native update-rule state'
}

Invoke-OwnedRoute -Route 'packages-ignored' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Ignored, pinned, and snoozed updates', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-ignored alias selects Ignored'
    $state = Find-ByAutomationId -Root $root -AutomationId 'NativePackageIgnoredEmpty'
    if (-not $state) {
        $state = Find-ByAutomationIdPrefix -Root $root -Prefix 'NativePackageIgnored_'
    }
    if (-not $state) {
        $state = Find-ByAutomationIdPrefix -Root $root -Prefix 'NativePackagePinned_'
    }
    if (-not $state) {
        $state = Find-ByAutomationIdPrefix -Root $root -Prefix 'NativePackageSnoozed_'
    }
    Assert-True -Condition ($null -ne $state) `
        -Name 'Package Manager packages-ignored exposes native update-rule state'
}

Invoke-OwnedRoute -Route 'package-setup' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Engine setup', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-setup alias selects Setup'
    $policy = Wait-ForElement -Root $root -AutomationId 'NativePackageSetupPolicy'
    $manager = Wait-ForElement -Root $root -AutomationId 'NativePackageSetup_manager_choco'
    $dependency = Wait-ForElement -Root $root -AutomationId 'NativePackageSetup_dependency_ffmpeg'
    $review = Wait-ForElement -Root $root -AutomationId 'NativePackageSetupReview_manager_choco'
    $batch = Wait-ForElement -Root $root -AutomationId 'NativePackageSetupReviewCuratedBatch'
    $provenance = Wait-ForElement -Root $root -AutomationId 'NativePackageSetupUniGetUIProvenance'
    Assert-True -Condition ($policy.Current.Name.StartsWith('Safe engine bootstrap', [StringComparison]::Ordinal) `
        -and $manager.Current.Name.StartsWith('Chocolatey', [StringComparison]::Ordinal) `
        -and $dependency.Current.Name.StartsWith('FFmpeg', [StringComparison]::Ordinal) `
        -and $batch.Current.Name.StartsWith('Review all curated Winget dependency installs', [StringComparison]::Ordinal) `
        -and $provenance.Current.Name.StartsWith('UniGetUI source provenance', [StringComparison]::Ordinal)) `
        -Name 'Package Manager Setup exposes fixed bootstrap, curated dependency, and vendored-UniGetUI provenance surfaces'
    Assert-True -Condition ($review.Current.HelpText.Contains('separate explicit confirmation') `
        -and $batch.Current.HelpText.Contains('later explicit confirmation')) `
        -Name 'Package Manager Setup review controls are deferred and explicitly require later consent'
    $working = Find-ByAutomationId -Root $root -AutomationId 'NativePackageWorkingState'
    Assert-True -Condition ($null -eq $working) `
        -Name 'Package Manager Setup route never starts a package process while proving the review-only surface'
    $setupControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        $header,
        $policy,
        $manager,
        $dependency,
        $review,
        $batch,
        $provenance)
    Assert-True -Condition $setupControlsFit `
        -Name 'Package Manager Setup review and provenance controls are accessible and horizontally unclipped'
}

Invoke-OwnedRoute -Route 'packages-setup' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Engine setup', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-setup alias selects Setup'
}

Invoke-OwnedRoute -Route 'package-settings' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $summary = Wait-ForElement -Root $root -AutomationId 'NativePackageSettingsSummary'
    Assert-True -Condition ($summary.Current.Name.StartsWith('Native package-manager state', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-settings alias selects Settings'
    $snooze = Wait-ForElement -Root $root -AutomationId 'NativePackageSnoozeDays'
    Assert-True -Condition ($snooze.Current.Name.StartsWith('Default update snooze duration', [StringComparison]::Ordinal)) `
        -Name 'Package Manager exposes a localized default snooze-duration picker'
    Select-ComboIndex -Combo $snooze -Index 1
    $snooze = Wait-ForElement -Root $root -AutomationId 'NativePackageSnoozeDays'
    Select-ComboIndex -Combo $snooze -Index 2
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePackageLiveStatus' -Prefix 'Package Manager status: Default update snooze duration saved' | Out-Null
    Assert-True -Condition $true -Name 'Package Manager saves the custom native snooze duration through UI Automation'
}

Invoke-OwnedRoute -Route 'packages-settings' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $summary = Wait-ForElement -Root $root -AutomationId 'NativePackageSettingsSummary'
    Assert-True -Condition ($summary.Current.Name.StartsWith('Native package-manager state', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-settings alias selects Settings'
}

Invoke-OwnedRoute -Route 'package-operations' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Operation queue and history', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-operations alias selects Operations'
    $queueSummary = Wait-ForElement -Root $root -AutomationId 'NativePackageQueueSummary'
    Assert-True -Condition ($queueSummary.Current.Name.StartsWith('Native mutation consent policy', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-operations exposes mutation-consent policy'
}

Invoke-OwnedRoute -Route 'packages-operations' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Operation queue and history', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-operations alias selects Operations'
    $queueSummary = Wait-ForElement -Root $root -AutomationId 'NativePackageQueueSummary'
    Assert-True -Condition ($queueSummary.Current.Name.StartsWith('Native mutation consent policy', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-operations exposes mutation-consent policy'
}

Invoke-OwnedRoute -Route 'uninstall' -ExpectedTitle 'App Uninstaller' -Inspect {
    param($root, $title)

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
    Assert-True -Condition ($safety.Current.Name -eq 'Native App Uninstaller safety: normal integrity required; local data deletion unavailable.') -Name 'App Uninstaller exposes explicit normal-integrity and no-local-data-deletion safety evidence'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAppUninstallerStatus' -Prefix 'Current-user Store/UWP inventory refreshed.' | Out-Null
    $deepCleanupStatus = Wait-ForElementByAutomationIdPrefix -Root $root -Prefix 'NativeAppUninstallerDeepCleanupStatus_'
    Assert-True -Condition ($deepCleanupStatus.Current.Name.StartsWith('Deep cleanup is intentionally unavailable until handle-relative deletion is implemented. Package removal never deletes local data.', [StringComparison]::Ordinal)) -Name 'App Uninstaller renders explicit deep-cleanup unavailability after inventory completion'
    Assert-True -Condition (-not (Find-ByAutomationIdPrefix -Root $root -Prefix 'NativeAppUninstallerReviewDeep_')) -Name 'App Uninstaller does not expose an unsafe deep-cleanup review action'
    $search = Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerSearch'
    $mode = Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerRegexMode'
    Assert-True -Condition ($search.Current.Name.StartsWith('App Uninstaller local cached package search', [StringComparison]::Ordinal) -and $mode.Current.Name.StartsWith('Enable bounded Regex filtering', [StringComparison]::Ordinal)) -Name 'App Uninstaller exposes an accessible native Store/UWP cache search surface'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '__winforge_native_uninstaller_no_such_package__' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerEmpty' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAppUninstallerResultCount' -Prefix '0 / ' | Out-Null
    Assert-True -Condition $true -Name 'App Uninstaller literal filtering stays inside the current local package cache'

    Set-ToggleState -Root $root -AutomationId 'NativeAppUninstallerRegexMode' -IsOn $true | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '.*' | Out-Null
    $status = Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerStatus'
    Assert-True -Condition $status.Current.Name.Contains('Bounded PCRE2 filters only the already-returned local package cache') -Name 'App Uninstaller evaluates bounded PCRE2 only against returned package metadata'

    $countBeforeInvalid = (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerResultCount').Current.Name
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '[' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAppUninstallerStatus' -Prefix 'Invalid PCRE2 filter' | Out-Null
    $countAfterInvalid = (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerResultCount').Current.Name
    Assert-True -Condition ($countBeforeInvalid -eq $countAfterInvalid) -Name 'App Uninstaller retains the prior visible local cache while an invalid regex is corrected'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAppUninstallerSearch' -Value '.*' | Out-Null
    $uninstallerControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerSearch'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerRegexMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerRegexBuilder'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerRefresh'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerStatus')
    )
    Assert-True -Condition $uninstallerControlsFit -Name 'App Uninstaller controls are horizontally unclipped in the native shell'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeAppUninstallerRegexBuilder'
    Wait-ForPageTitle -Root $root -Prefix 'Regex Tester & Builder' | Out-Null
    $target = Wait-ForElement -Root $root -AutomationId 'NativeRegexBuilderTarget'
    Select-ComboIndex -Combo $target -Index 5
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRegexBuilderApply'
    Wait-ForPageTitle -Root $root -Prefix 'App Uninstaller' | Out-Null
    $uninstallerMode = Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerRegexMode'
    $uninstallerToggle = [System.Windows.Automation.TogglePattern]$uninstallerMode.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $uninstallerValue = (Get-EditableValuePattern -Element (Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerSearch')).Current.Value
    Assert-True -Condition ($uninstallerToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and $uninstallerValue -eq '.*') -Name 'Regex Builder returns a verified pattern to the native App Uninstaller cache target'
}

foreach ($uninstallerAlias in @('apps', 'module.uninstall')) {
    Invoke-OwnedRoute -Route $uninstallerAlias -ExpectedTitle 'App Uninstaller' -Inspect {
        param($root, $title)
        Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerSafety' | Out-Null
        Wait-ForElement -Root $root -AutomationId 'NativeAppUninstallerSearch' | Out-Null
        Assert-True -Condition $true -Name 'App Uninstaller aliases resolve to the native reviewed-removal surface'
    }
}

foreach ($case in @(
    @{ Route = 'about'; Title = 'About WinForge Native' },
    @{ Route = 'settings'; Title = 'Settings' },
    @{ Route = 'search:reactor'; Title = 'Search results' },
    @{ Route = 'manual:reactor-safety'; Title = 'Manual' },
    @{ Route = 'weblogin?url=https://example.test'; Title = 'In-App Login' },
    @{ Route = 'launcher'; Title = 'Command Palette' },
    @{ Route = 'taskbar'; Title = 'Taskbar Tweaker' },
    @{ Route = 'vault'; Title = 'WinForge Vault' },
    @{ Route = 'does-not-exist'; Title = 'Unknown native route' }
)) {
    Invoke-OwnedRoute -Route $case.Route -ExpectedTitle $case.Title
}

Write-Host ""
Write-Host "$script:Passed passed, $script:Failed failed"
if ($script:Failed -ne 0) {
    exit 1
}
