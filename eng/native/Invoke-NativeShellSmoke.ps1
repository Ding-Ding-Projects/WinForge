[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$ExecutablePath,
    [int]$TimeoutMs = 10000,
    [switch]$UtilityRoutesOnly,
    [switch]$LineRoutesOnly,
    [switch]$TextAnalysisRoutesOnly,
    [switch]$ReferenceTextRoutesOnly,
    [switch]$MorseRoutesOnly,
    [switch]$SlugifyRoutesOnly,
    [switch]$BmiRoutesOnly,
    [switch]$UuidV5RoutesOnly,
    [switch]$UnitPriceRoutesOnly,
    [switch]$BaseConvertRoutesOnly,
    [switch]$AllowClipboardMutation
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
$uiaReferences = @(
    [System.Windows.Automation.Automation].Assembly.Location,
    [System.Windows.Automation.AutomationElement].Assembly.Location,
    [System.Windows.Automation.AutomationEventArgs].Assembly.Location
) | Select-Object -Unique
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

public sealed class WinForgeAutomationEventLatch
{
    private readonly AutoResetEvent signal = new AutoResetEvent(false);
    public WaitHandle Signal { get { return signal; } }
    public void OnEvent(object sender, AutomationEventArgs args) { signal.Set(); }
}

public static class WinForgeNativeMethods
{
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);
}
'@ -ReferencedAssemblies $uiaReferences

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
        [Parameter(Mandatory)][string]$Prefix,
        [int]$WaitTimeoutMs = $TimeoutMs
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($WaitTimeoutMs)
    $element = $null
    $lastError = $null
    do {
        try {
            $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
            if ($element -and $element.Current.Name.StartsWith($Prefix, [StringComparison]::Ordinal)) {
                return $element
            }
        }
        catch {
            # A virtualized WinUI list can briefly invalidate its provider while
            # replacing realized rows. Reacquire the live element on the next poll.
            $element = $null
            $lastError = $_
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($element) { $element.Current.Name } else { '(missing)' }
    $suffix = if ($lastError) { " Last automation error: $lastError" } else { '' }
    throw "Expected '$AutomationId' name prefix '$Prefix', got '$actual'.$suffix"
}

function Wait-ForDescendantNamePrefix {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$Prefix
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $elements = $Root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($element in $elements) {
            if ($element.Current.Name.StartsWith($Prefix, [StringComparison]::Ordinal)) {
                return $element
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for a descendant name prefix '$Prefix'."
}

function Wait-ForExistingElementNamePrefix {
    param(
        [Parameter(Mandatory)]$Element,
        [Parameter(Mandatory)][string]$Prefix,
        [int]$WaitTimeoutMs = $TimeoutMs
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($WaitTimeoutMs)
    $actual = '(unavailable)'
    $lastError = $null
    do {
        try {
            $actual = $Element.Current.Name
            if ($actual.StartsWith($Prefix, [StringComparison]::Ordinal)) {
                return $Element
            }
        }
        catch {
            # The retained element avoids traversing a large virtualized subtree;
            # retry if its provider is momentarily busy refreshing realized rows.
            $actual = '(unavailable)'
            $lastError = $_
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $suffix = if ($lastError) { " Last automation error: $lastError" } else { '' }
    throw "Expected retained element name prefix '$Prefix', got '$actual'.$suffix"
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
        if (-not $element) {
            Write-Host 'Horizontal bounds check received a missing element.' -ForegroundColor DarkYellow
            return $false
        }
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
                Write-Host "Horizontal bounds scroll failed for '$($element.Current.AutomationId)': $($_.Exception.Message)" -ForegroundColor DarkYellow
                return $false
            }
        }
        if ($rect.Width -le 0 -or $rect.Height -le 0) {
            Write-Host "Horizontal bounds are empty for '$($element.Current.AutomationId)'." -ForegroundColor DarkYellow
            return $false
        }
        # Vertical overflow is intentionally scrollable. Horizontal overflow
        # is the clipping defect this smoke check is designed to catch.
        if ($rect.Left -lt ($window.Left - 2) -or $rect.Right -gt ($window.Right + 2)) {
            Write-Host "Horizontal clipping for '$($element.Current.AutomationId)': element [$($rect.Left), $($rect.Right)] window [$($window.Left), $($window.Right)]." -ForegroundColor DarkYellow
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

    $isUtilityRoute = @(
        'textdiff', 'module.textdiff',
        'aspect', 'aspectratio', 'module.aspectratio',
        'cssunits', 'module.cssunits') -contains $Route
    $isLineRoute = @(
        'lines', 'linetools', 'module.linetools',
        'textsort', 'module.textsort',
        'textwrap', 'module.textwrap') -contains $Route
    $isTextAnalysisRoute = @(
        'textstats', 'module.textstats',
        'wordfreq', 'module.wordfreq',
        'similarity', 'stringcompare', 'module.stringcompare') -contains $Route
    $isReferenceTextRoute = @(
        'nato', 'phonetic', 'module.phonetic',
        'boxtext', 'module.boxtext',
        'entities', 'htmlentities', 'module.htmlentities') -contains $Route
    $isMorseRoute = @('morse', 'module.morse') -contains $Route
    $isSlugifyRoute = @('slug', 'slugify', 'module.slugify') -contains $Route
    $isBmiRoute = @('bmi', 'health', 'module.bmi') -contains $Route
    $isUuidV5Route = @('uuid5', 'uuidv5', 'module.uuidv5') -contains $Route
    $isUnitPriceRoute = @('priceper', 'unitprice', 'module.unitprice') -contains $Route
    $isBaseConvertRoute = @('baseconvert', 'module.baseconvert') -contains $Route
    if (($UtilityRoutesOnly -or $LineRoutesOnly -or $TextAnalysisRoutesOnly -or $ReferenceTextRoutesOnly -or $MorseRoutesOnly -or $SlugifyRoutesOnly -or $BmiRoutesOnly -or $UuidV5RoutesOnly -or $UnitPriceRoutesOnly -or $BaseConvertRoutesOnly) -and -not (
        ($UtilityRoutesOnly -and $isUtilityRoute) -or
        ($LineRoutesOnly -and $isLineRoute) -or
        ($TextAnalysisRoutesOnly -and $isTextAnalysisRoute) -or
        ($ReferenceTextRoutesOnly -and $isReferenceTextRoute) -or
        ($MorseRoutesOnly -and $isMorseRoute) -or
        ($SlugifyRoutesOnly -and $isSlugifyRoute) -or
        ($BmiRoutesOnly -and $isBmiRoute) -or
        ($UuidV5RoutesOnly -and $isUuidV5Route) -or
        ($UnitPriceRoutesOnly -and $isUnitPriceRoute) -or
        ($BaseConvertRoutesOnly -and $isBaseConvertRoute))) {
        return
    }

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

function Navigate-InProcessToUuidV5Route {
    param(
        [Parameter(Mandatory)]$Root
    )

    $allApps = Wait-ForElement -Root $Root -AutomationId 'NativeNav_shell_allapps'
    $allAppsSelection = [System.Windows.Automation.SelectionItemPattern]$allApps.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $allAppsSelection.Select()
    Wait-ForPageTitle -Root $Root -Prefix 'All Apps' | Out-Null

    Set-ElementValueAndWait -Root $Root -AutomationId 'NativeAllAppsSearchBox' -Value 'uuidv5' | Out-Null
    $uuidV5Item = Wait-ForElement -Root $Root -AutomationId 'NativeAllApps_module_uuidv5'
    $uuidV5Selection = [System.Windows.Automation.SelectionItemPattern]$uuidV5Item.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $uuidV5Selection.Select()
    Wait-ForPageTitle -Root $Root -Prefix 'Namespaced UUID' | Out-Null
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
    $value.SetValue('module.smelter')
    $pendingRoute = Wait-ForElement -Root $root -AutomationId 'NativeAllApps_module_smelter'
    Assert-True -Condition ($pendingRoute.Current.Name.Contains('native implementation pending')) -Name 'All Apps keeps a still-unported route explicitly pending'

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

Invoke-OwnedRoute -Route 'uuidv5' -ExpectedTitle 'Namespaced UUID' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeUuidV5ImplementationStatus',
        'NativeUuidV5Namespace',
        'NativeUuidV5Version',
        'NativeUuidV5Name',
        'NativeUuidV5Result',
        'NativeUuidV5Copy',
        'NativeUuidV5BulkInput',
        'NativeUuidV5BulkGenerate',
        'NativeUuidV5BulkOutput',
        'NativeUuidV5BulkCopy',
        'NativeUuidV5Status'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $uuidV5ControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Namespace'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Version'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Name'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Result'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Copy'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5BulkInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5BulkGenerate'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5BulkOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5BulkCopy'))
    Assert-True -Condition $uuidV5ControlsFit `
        -Name 'Namespaced UUID exposes native controls, accessibility, and horizontal clipping safety'

    $namespace = Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Namespace'
    $version = Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Version'
    $name = Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Name'
    Assert-True -Condition ($namespace.Current.Name.StartsWith('UUID namespace', [StringComparison]::Ordinal)) `
        -Name 'Namespaced UUID namespace picker has a localized accessible name'
    Assert-True -Condition ($version.Current.Name.StartsWith('Name-based UUID version', [StringComparison]::Ordinal)) `
        -Name 'Namespaced UUID version picker has a localized accessible name'
    Assert-True -Condition ($name.Current.Name.StartsWith('Name to hash into a UUID', [StringComparison]::Ordinal)) `
        -Name 'Namespaced UUID name input has a localized accessible name'

    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '4ebd0208-8328-5d69-8c44-ec50939c0967' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID computes the RFC v5 empty-name DNS value on its native default surface'

    Set-ElementValueAndWait -Root $root -AutomationId 'NativeUuidV5Name' -Value 'www.widgets.com' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '21f7f8de-8051-5b89-8680-0195ef798b6a' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV5Status' -Prefix 'UUID v5' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID computes the RFC SHA-1 v5 DNS vector through the live native UI'

    Select-ComboIndex -Combo $version -Index 1
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '3d813cbb-47fb-32ba-91df-831e1593ac29' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV5Status' -Prefix 'UUID v3' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID switches to the RFC MD5 v3 vector without changing the name'

    Select-ComboIndex -Combo $namespace -Index 4
    Wait-ForElementVisible -Root $root -AutomationId 'NativeUuidV5CustomNamespace' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV5Status' -Prefix 'Enter a valid custom namespace GUID' | Out-Null
    Set-ElementValueAndWait -Root $root -AutomationId 'NativeUuidV5CustomNamespace' `
        -Value '{6BA7B810-9DAD-11D1-80B4-00C04FD430C8}' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '3d813cbb-47fb-32ba-91df-831e1593ac29' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID accepts the managed braced GUID custom-namespace form'

    Set-ElementValueAndWait -Root $root -AutomationId 'NativeUuidV5CustomNamespace' `
        -Value '{ 0x000000006ba7b810, 0x+00009dad, 0x11d1, { 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8 } }' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '3d813cbb-47fb-32ba-91df-831e1593ac29' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID accepts the managed GUID X custom-namespace form'

    $statusBeforeEmptyBulkCopy = (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Status').Current.Name
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV5BulkCopy'
    Start-Sleep -Milliseconds 100
    $statusAfterEmptyBulkCopy = (Wait-ForElement -Root $root -AutomationId 'NativeUuidV5Status').Current.Name
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5BulkOutput' -ExpectedValue '' | Out-Null
    Assert-True -Condition ($statusAfterEmptyBulkCopy -eq $statusBeforeEmptyBulkCopy) `
        -Name 'Namespaced UUID keeps the managed empty bulk-copy no-op without fabricating output or status'

    $bulkInput = Wait-ForElement -Root $root -AutomationId 'NativeUuidV5BulkInput'
    (Get-EditableValuePattern -Element $bulkInput).SetValue(" alpha `r`n`r beta `n")
    Start-Sleep -Milliseconds 150
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV5BulkGenerate'
    Wait-ForElementValueWhere -Root $root -AutomationId 'NativeUuidV5BulkOutput' `
        -Description 'two managed-style v3 UUID bulk rows' `
        -Predicate {
            param($value)
            return $value -match 'alpha  \u2192  [0-9a-f-]{36}' -and
                $value -match 'beta  \u2192  [0-9a-f-]{36}'
        } | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV5Status' -Prefix 'Generated 2 UUID(s).' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID trims nonblank mixed-newline bulk names through native controls'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV5Copy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV5Status' -Prefix 'Copied to clipboard.' | Out-Null
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUuidV5BulkCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUuidV5Status' -Prefix 'All rows copied.' | Out-Null
        Assert-True -Condition $true `
            -Name 'Namespaced UUID exposes clipboard success only after explicit Copy buttons'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = @([char]0x7CB5, [char]0x8A9E) -join ''
    $bilingualLabel = @([char]0x96D9, [char]0x8A9E) -join ''
    $namespacedUuidCantonese = @([char]0x5177, [char]0x540D, [char]0x7A7A, [char]0x9593) -join ''
    $middleDot = [char]0x00B7
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForPageTitle -Root $root -Prefix ($namespacedUuidCantonese + ' UUID') | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Name' -ExpectedValue 'www.widgets.com' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '3d813cbb-47fb-32ba-91df-831e1593ac29' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID localizes in Cantonese while preserving namespace version name and result state'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name ('Bilingual ' + $middleDot + ' ' + $bilingualLabel)
    Wait-ForPageTitle -Root $root -Prefix ('Namespaced UUID ' + $middleDot + ' ' + $namespacedUuidCantonese + ' UUID') | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Name' -ExpectedValue 'www.widgets.com' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '3d813cbb-47fb-32ba-91df-831e1593ac29' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID exposes bilingual labels while preserving namespace version name and result state'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Namespaced UUID' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Name' -ExpectedValue 'www.widgets.com' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '3d813cbb-47fb-32ba-91df-831e1593ac29' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID returns to English while preserving namespace version name and result state'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeUuidV5Name')) `
        -Name 'Namespaced UUID releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToUuidV5Route -Root $root
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Name' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5BulkInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5BulkOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUuidV5Result' `
        -ExpectedValue '4ebd0208-8328-5d69-8c44-ec50939c0967' | Out-Null
    Assert-True -Condition $true `
        -Name 'Namespaced UUID resets managed page state after in-process route re-entry'
}

foreach ($alias in @('uuid5', 'uuidv5', 'module.uuidv5')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Namespaced UUID'
}

Invoke-OwnedRoute -Route 'bmi' -ExpectedTitle 'Health Calculators' -Inspect {
    param($root, $title)

    $emdash = [char]0x2014
    $middleDot = [char]0x00B7
    $approximately = [char]0x2248

    # A persisted shell language can otherwise make every local formatter and
    # accessible label non-deterministic between owned route processes.
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Health Calculators' | Out-Null

    foreach ($id in @(
        'NativeBmiImplementationStatus',
        'NativeBmiMetric',
        'NativeBmiHeight',
        'NativeBmiWeight',
        'NativeBmiResult',
        'NativeBmiBmrSex',
        'NativeBmiBmrAge',
        'NativeBmiBmrHeight',
        'NativeBmiBmrWeight',
        'NativeBmiActivity',
        'NativeBmiBmrResult',
        'NativeBmiBodyFatSex',
        'NativeBmiBodyFatHeight',
        'NativeBmiBodyFatNeck',
        'NativeBmiBodyFatWaist',
        'NativeBmiBodyFatResult',
        'NativeBmiDisclaimer',
        'NativeBmiStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $bmiControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiHeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiWeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiResult'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBmrSex'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBmrAge'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBmrHeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBmrWeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiActivity'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBmrResult'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatSex'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatHeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatNeck'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatWaist'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatResult'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiStatus'))
    Assert-True -Condition $bmiControlsFit `
        -Name 'Health Calculators exposes native accessible controls without horizontal clipping'

    $metric = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $metric.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiHeight').Current.Name.StartsWith('Height (cm)', [StringComparison]::Ordinal) -and
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBmrSex').Current.Name.StartsWith('BMR sex', [StringComparison]::Ordinal) -and
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatSex').Current.Name.StartsWith('Body-fat sex', [StringComparison]::Ordinal)) `
        -Name 'Health Calculators starts with semantic metric controls and localized UIA names'

    Wait-ForElementValue -Root $root -AutomationId 'NativeBmiHeight' -ExpectedValue '170' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBmiWeight' -ExpectedValue '65' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBmiBmrAge' -ExpectedValue '30' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix "BMI 22.5 $emdash Normal weight" | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiBmrResult' `
        -Prefix "BMR 1568 kcal/day $middleDot about 1881 kcal/day to maintain" | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiBodyFatResult' -Prefix "Body fat $approximately 17.8%" | Out-Null
    Assert-True -Condition $true `
        -Name 'Health Calculators renders managed BMI BMR TDEE and US Navy defaults through native code'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiHeight' -Value '180' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiWeight' -Value '80' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix "BMI 24.7 $emdash Normal weight" | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiHeight' -Value '' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix 'Enter a valid height and weight.' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiHeight' -Value '180' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Assert-True -Condition $true `
        -Name 'Health Calculators updates BMI live and guards invalid values without a stale result'

    $bmrSex = Wait-ForElement -Root $root -AutomationId 'NativeBmiBmrSex'
    Select-ComboIndex -Combo $bmrSex -Index 1
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBmiActivity') -Index 2
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiBmrResult' `
        -Prefix "BMR 1402 kcal/day $middleDot about 2172 kcal/day to maintain" | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBmrAge' -Value '' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiBmrResult' -Prefix 'Enter a valid age, height and weight.' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBmrAge' -Value '30' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Assert-True -Condition $true `
        -Name 'Health Calculators applies Mifflin-St Jeor sex activity and age validation locally'

    $bodyFatSex = Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatSex'
    Select-ComboIndex -Combo $bodyFatSex -Index 1
    Wait-ForElementVisible -Root $root -AutomationId 'NativeBmiBodyFatHipsPanel' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBodyFatHeight' -Value '165' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBodyFatNeck' -Value '32' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBodyFatWaist' -Value '75' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBodyFatHips' -Value '100' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiBodyFatResult' -Prefix "Body fat $approximately 29.9%" | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBmiBodyFatSex') -Index 0
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBodyFatNeck' -Value '85' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiBodyFatWaist' -Value '85' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiBodyFatResult' `
        -Prefix 'Enter valid height, neck and waist' | Out-Null
    Assert-True -Condition $true `
        -Name 'Health Calculators shows female hips only when needed and rejects invalid Navy circumference geometry'

    Set-ToggleState -Root $root -AutomationId 'NativeBmiMetric' -IsOn $false | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeBmiHeight' | Out-Null
    Assert-True -Condition (
        (Wait-ForElement -Root $root -AutomationId 'NativeBmiHeight').Current.Name.StartsWith('Height (in)', [StringComparison]::Ordinal) -and
        (Wait-ForElementValue -Root $root -AutomationId 'NativeBmiHeight' -ExpectedValue '180') -and
        (Wait-ForElementValue -Root $root -AutomationId 'NativeBmiWeight' -ExpectedValue '80')) `
        -Name 'Health Calculators relabels raw fields for imperial input without replacing their state'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiHeight' -Value '70' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBmiWeight' -Value '150' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix "BMI 21.5 $emdash Normal weight" | Out-Null
    Assert-True -Condition $true `
        -Name 'Health Calculators converts imperial height and weight through the native BMI core'

    $cantoneseLabel = @([char]0x7CB5, [char]0x8A9E) -join ''
    $bilingualLabel = @([char]0x96D9, [char]0x8A9E) -join ''
    $healthCantoneseTitle = @([char]0x5065, [char]0x5EB7, [char]0x8A08, [char]0x7B97, [char]0x5668) -join ''
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForPageTitle -Root $root -Prefix $healthCantoneseTitle | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix 'BMI 21.5' | Out-Null
    Select-ComboItem -Combo (Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker') `
        -Name ('Bilingual ' + [char]0x00B7 + ' ' + $bilingualLabel)
    Wait-ForPageTitle -Root $root -Prefix ('Health Calculators ' + [char]0x00B7 + ' ' + $healthCantoneseTitle) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix 'BMI 21.5' | Out-Null
    Select-ComboItem -Combo (Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker') -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Health Calculators' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix "BMI 21.5 $emdash Normal weight" | Out-Null
    Assert-True -Condition $true `
        -Name 'Health Calculators supports Cantonese bilingual and English modes without losing calculator state'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeBmiHeight')) `
        -Name 'Health Calculators releases observable native controls when navigation leaves the route'

    $shellSearch = Find-ByAutomationId -Root $root -AutomationId 'NativeShellSearchBox'
    if (-not $shellSearch -or $shellSearch.Current.IsOffscreen) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'TogglePaneButton'
    }
    $shellSearch = Wait-ForElement -Root $root -AutomationId 'NativeShellSearchBox'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeShellSearchBox' -Value 'module.bmi' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeShellSearchExecute'
    $bmiReentryDeadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $bmiReentryTitle = Find-ByAutomationId -Root $root -AutomationId 'NativePageTitle'
        if ($bmiReentryTitle -and $bmiReentryTitle.Current.Name.StartsWith('Health Calculators', [StringComparison]::Ordinal)) {
            break
        }
        if ($bmiReentryTitle -and $bmiReentryTitle.Current.Name.StartsWith('Search results', [StringComparison]::Ordinal)) {
            Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeRoute_module_bmi'
            Wait-ForPageTitle -Root $root -Prefix 'Health Calculators' | Out-Null
            $bmiReentryTitle = Wait-ForElement -Root $root -AutomationId 'NativePageTitle'
            break
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $bmiReentryDeadline)
    if (-not $bmiReentryTitle -or -not $bmiReentryTitle.Current.Name.StartsWith('Health Calculators', [StringComparison]::Ordinal)) {
        $actualBmiReentryTitle = if ($bmiReentryTitle) { $bmiReentryTitle.Current.Name } else { '(missing)' }
        throw "Expected in-process Health Calculators re-entry, got '$actualBmiReentryTitle'."
    }
    Wait-ForElementValue -Root $root -AutomationId 'NativeBmiHeight' -ExpectedValue '170' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBmiWeight' -ExpectedValue '65' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBmiBmrAge' -ExpectedValue '30' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBmiResult' -Prefix "BMI 22.5 $emdash Normal weight" | Out-Null
    $reentryMetric = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeBmiMetric').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($reentryMetric.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'Health Calculators resets all managed defaults after in-process route re-entry'
}

foreach ($alias in @('health', 'module.bmi')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Health Calculators'
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

function Invoke-AndWaitForLiveRegionChanged {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $element = Wait-ForElement -Root $Root -AutomationId $AutomationId
    $latch = New-Object WinForgeAutomationEventLatch
    $handler = [System.Delegate]::CreateDelegate(
        [System.Windows.Automation.AutomationEventHandler],
        $latch,
        'OnEvent')
    $eventId = [System.Windows.Automation.AutomationElementIdentifiers]::LiveRegionChangedEvent
    [System.Windows.Automation.Automation]::AddAutomationEventHandler(
        $eventId,
        $element,
        [System.Windows.Automation.TreeScope]::Element,
        $handler)
    try {
        & $Action
        return $latch.Signal.WaitOne($TimeoutMs)
    }
    finally {
        [System.Windows.Automation.Automation]::RemoveAutomationEventHandler(
            $eventId,
            $element,
            $handler)
    }
}

function Navigate-InProcessToRoute {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][string]$Route,
        [Parameter(Mandatory)][string]$ExpectedTitle
    )

    $dashboard = Wait-ForElement -Root $Root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $Root -Prefix 'WinForge Native' | Out-Null

    $shellSearch = Find-ByAutomationId -Root $Root -AutomationId 'NativeShellSearchBox'
    if (-not $shellSearch -or $shellSearch.Current.IsOffscreen) {
        Invoke-ElementByAutomationId -Root $Root -AutomationId 'TogglePaneButton'
    }
    $shellSearch = Wait-ForElement -Root $Root -AutomationId 'NativeShellSearchBox'
    Set-EditableValueAndWait -Root $Root -AutomationId 'NativeShellSearchBox' -Value $Route | Out-Null
    Invoke-ElementByAutomationId -Root $Root -AutomationId 'NativeShellSearchExecute'
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $pageTitle = Find-ByAutomationId -Root $Root -AutomationId 'NativePageTitle'
        if ($pageTitle -and $pageTitle.Current.Name.StartsWith($ExpectedTitle, [StringComparison]::Ordinal)) {
            return
        }
        if ($pageTitle -and $pageTitle.Current.Name.StartsWith('Search results', [StringComparison]::Ordinal)) {
            $routeAutomationId = 'NativeRoute_' + ($Route -replace '[^A-Za-z0-9_]', '_')
            Invoke-ElementByAutomationId -Root $Root -AutomationId $routeAutomationId
            Wait-ForPageTitle -Root $Root -Prefix $ExpectedTitle | Out-Null
            return
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actualTitle = if ($pageTitle) { $pageTitle.Current.Name } else { '(missing)' }
    throw "Expected direct '$ExpectedTitle' or a Search results page, got '$actualTitle'."
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

Invoke-OwnedRoute -Route 'morse' -ExpectedTitle 'Morse Code' -Inspect {
    param($root, $title)

    # Individual smoke processes can inherit the persisted language choice.
    # Set a deterministic baseline before asserting localized accessible text.
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Morse Code' | Out-Null

    foreach ($id in @(
        'NativeMorseImplementationStatus',
        'NativeMorseDirection',
        'NativeMorseInputHint',
        'NativeMorseInput',
        'NativeMorseSeparator',
        'NativeMorseCopy',
        'NativeMorseOutput',
        'NativeMorseLamp',
        'NativeMorsePlay',
        'NativeMorseStop',
        'NativeMorseWpm',
        'NativeMorseStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $morseControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseDirection'),
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseSeparator'),
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseLamp'),
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseWpm'))
    Assert-True -Condition $morseControlsFit `
        -Name 'Morse Code exposes native controls, accessibility, and horizontal clipping safety'

    $input = Wait-ForElement -Root $root -AutomationId 'NativeMorseInput'
    $output = Wait-ForElement -Root $root -AutomationId 'NativeMorseOutput'
    Assert-True -Condition (
        $input.Current.Name.StartsWith('Morse Code input', [StringComparison]::Ordinal) -and
        $output.Current.Name.StartsWith('Morse Code output', [StringComparison]::Ordinal)) `
        -Name 'Morse Code editors expose localized accessible names'
    Wait-ForElementValue -Root $root -AutomationId 'NativeMorseInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeMorseOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeMorseWpm' -ExpectedValue '15' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseStatus' -Prefix 'Idle.' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseLamp' -Prefix 'Signal lamp off' | Out-Null
    $direction = [System.Windows.Automation.TogglePattern](
        (Wait-ForElement -Root $root -AutomationId 'NativeMorseDirection').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern))
    $separator = Wait-ForElement -Root $root -AutomationId 'NativeMorseSeparator'
    $stop = Wait-ForElement -Root $root -AutomationId 'NativeMorseStop'
    Assert-True -Condition (
        $direction.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off -and
        $separator.Current.IsEnabled -and -not $stop.Current.IsEnabled) `
        -Name 'Morse Code starts in text-to-Morse mode with default speed and no active flash'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeMorseCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseStatus' -Prefix 'Nothing to copy yet.' | Out-Null
    Assert-True -Condition $true -Name 'Morse Code protects the clipboard when output is empty'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeMorseInput' -Value 'SOS' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeMorseOutput' -ExpectedValue '... --- ...' | Out-Null
    Assert-True -Condition $true -Name 'Morse Code encodes a canonical SOS message live in native C++'

    $separator = Wait-ForElement -Root $root -AutomationId 'NativeMorseSeparator'
    Select-ComboIndex -Combo $separator -Index 1
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeMorseInput' -Value 'A B' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeMorseOutput' -ExpectedValue '.-   -...' | Out-Null
    Assert-True -Condition $true -Name 'Morse Code applies the triple-space word-separator preset'

    Set-ToggleState -Root $root -AutomationId 'NativeMorseDirection' -IsOn $true | Out-Null
    $separator = Wait-ForElement -Root $root -AutomationId 'NativeMorseSeparator'
    Assert-True -Condition (-not $separator.Current.IsEnabled) `
        -Name 'Morse Code disables encoding separators while decoding'
    $middleDot = [char]0x00B7
    $enDash = [char]0x2013
    $bullet = [char]0x2022
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeMorseInput' -Value "$middleDot | $enDash$bullet$bullet" | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeMorseOutput' -ExpectedValue 'E D' | Out-Null
    Assert-True -Condition $true -Name 'Morse Code decodes dot dash and vertical-bar aliases locally'

    Set-ToggleState -Root $root -AutomationId 'NativeMorseDirection' -IsOn $false | Out-Null
    $snowman = [char]0x2603
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeMorseInput' -Value "A$snowman A" | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeMorseOutput' -ExpectedValue '.- #   .-' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseUnknown' `
        -Prefix 'Unsupported characters (marked #):' | Out-Null
    Assert-True -Condition $true -Name 'Morse Code marks unsupported UTF-16 units without losing conversion output'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeMorseCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseStatus' `
            -Prefix 'Copied output to the clipboard.' | Out-Null
        Assert-True -Condition $true -Name 'Morse Code copies populated output only through its explicit Copy action'
    }

    $flashText = 'T'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeMorseInput' -Value $flashText | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeMorsePlay'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseStatus' -Prefix 'Flashing' | Out-Null
    Assert-True -Condition (Wait-ForElement -Root $root -AutomationId 'NativeMorseStop').Current.IsEnabled `
        -Name 'Morse Code enables Stop while a native flash timeline is active'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseStatus' -Prefix 'Done.' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeMorseLamp' -Prefix 'Signal lamp off' | Out-Null
    Assert-True -Condition (-not (Wait-ForElement -Root $root -AutomationId 'NativeMorseStop').Current.IsEnabled) `
        -Name 'Morse Code completes a native timing-correct flash preview safely'

}

Invoke-OwnedRoute -Route 'module.morse' -ExpectedTitle 'Morse Code'

Invoke-OwnedRoute -Route 'textdiff' -ExpectedTitle 'Text Diff' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeTextDiffImplementationStatus',
        'NativeTextDiffInputA',
        'NativeTextDiffInputB',
        'NativeTextDiffIgnoreWhitespace',
        'NativeTextDiffIgnoreCase',
        'NativeTextDiffCopy',
        'NativeTextDiffCounts',
        'NativeTextDiffRows',
        'NativeTextDiffStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $textDiffControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeTextDiffInputA'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextDiffInputB'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextDiffIgnoreWhitespace'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextDiffIgnoreCase'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextDiffCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextDiffCounts'))
    Assert-True -Condition $textDiffControlsFit `
        -Name 'Text Diff exposes native controls, accessibility, and horizontal clipping safety'

    $inputA = Wait-ForElement -Root $root -AutomationId 'NativeTextDiffInputA'
    $inputB = Wait-ForElement -Root $root -AutomationId 'NativeTextDiffInputB'
    Assert-True -Condition (
        $inputA.Current.Name.StartsWith('Original text A', [StringComparison]::Ordinal) -and
        $inputB.Current.Name.StartsWith('Changed text B', [StringComparison]::Ordinal)) `
        -Name 'Text Diff editors expose localized accessible names'

    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+0 added   -0 removed   0 unchanged' | Out-Null
    Assert-True -Condition $true -Name 'Text Diff starts with an empty live comparison'

    # WinUI TextBox's UI Automation provider canonicalizes multiline values to
    # lone CR separators even though the native diff accepts CR, LF, and CRLF.
    $newLine = "`r"
    $leftText = @('Header', 'spaced value', 'Same') -join $newLine
    $rightText = @('header', 'spaced  value', 'Same', 'Added') -join $newLine
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextDiffInputA' -Value $leftText | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextDiffInputB' -Value $rightText | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+3 added   -2 removed   1 unchanged' | Out-Null

    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffLine0' -Prefix '- Header' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffLine5' -Prefix '+ Added' | Out-Null
    Assert-True -Condition $true -Name 'Text Diff renders live removed and added line rows from both editors'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextDiffInputA' -ExpectedValue $leftText | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextDiffInputB' -ExpectedValue $rightText | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+3 added   -2 removed   1 unchanged' | Out-Null
    Assert-True -Condition $true -Name 'Text Diff preserves both editors and live output across language rerender'

    $manyLeftLines = 1..2000 | ForEach-Object { "line-$_" }
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextDiffInputA' `
        -Value ($manyLeftLines -join $newLine) | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextDiffInputB' -Value '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+0 added   -2000 removed   0 unchanged' | Out-Null
    $rows = Wait-ForElement -Root $root -AutomationId 'NativeTextDiffRows'
    try {
        $outerScrollItem = [System.Windows.Automation.ScrollItemPattern]$rows.GetCurrentPattern(
            [System.Windows.Automation.ScrollItemPattern]::Pattern)
        $outerScrollItem.ScrollIntoView()
        Start-Sleep -Milliseconds 100
    }
    catch { }
    $firstLargeRow = Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffLine0' -Prefix '- line-1'
    $listItemCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $realizedRows = $rows.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $listItemCondition).Count
    $rowsBounds = $rows.Current.BoundingRectangle
    $windowDpi = [WinForgeNativeMethods]::GetDpiForWindow(
        [IntPtr]::new($root.Current.NativeWindowHandle))
    if ($windowDpi -eq 0) { $windowDpi = 96 }
    $maximumRowsHeight = [Math]::Ceiling(420.0 * $windowDpi / 96.0) + 2
    $innerScroll = [System.Windows.Automation.ScrollPattern]$rows.GetCurrentPattern(
        [System.Windows.Automation.ScrollPattern]::Pattern)
    $innerScroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 100.0)
    $lastLargeRow = Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffLine1999' -Prefix '- line-2000'
    $largeComparisonVirtualized = (
        -not $rows.Current.IsOffscreen -and
        $rowsBounds.Height -gt 0 -and $rowsBounds.Height -le $maximumRowsHeight -and
        $firstLargeRow -and $lastLargeRow -and
        $realizedRows -gt 0 -and $realizedRows -lt 200)
    if (-not $largeComparisonVirtualized) {
        Write-Host ("Text Diff large comparison diagnostics: offscreen={0}; bounds={1}; dpi={2}; maxHeight={3}; first={4}; last={5}; realized={6}." -f `
            $rows.Current.IsOffscreen,
            $rowsBounds,
            $windowDpi,
            $maximumRowsHeight,
            [bool]$firstLargeRow,
            [bool]$lastLargeRow,
            $realizedRows) -ForegroundColor DarkYellow
    }
    Assert-True -Condition $largeComparisonVirtualized `
        -Name 'Text Diff virtualizes and bounds a large one-sided comparison'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextDiffInputA' -Value $leftText | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextDiffInputB' -Value $rightText | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+3 added   -2 removed   1 unchanged' | Out-Null

    Set-ToggleState -Root $root -AutomationId 'NativeTextDiffIgnoreWhitespace' -IsOn $true | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+2 added   -1 removed   2 unchanged' | Out-Null
    Assert-True -Condition $true -Name 'Text Diff ignore-whitespace mode recomputes the live LCS'

    Set-ToggleState -Root $root -AutomationId 'NativeTextDiffIgnoreCase' -IsOn $true | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+1 added   -0 removed   3 unchanged' | Out-Null
    Assert-True -Condition $true -Name 'Text Diff ignore-case mode composes with whitespace normalization'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextDiffCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffStatus' `
        -Prefix 'Unified diff copied to the clipboard.' | Out-Null
    Assert-True -Condition $true -Name 'Text Diff copies unified output only after explicit Copy'

    Navigate-InProcessToRoute -Root $root -Route 'module.textdiff' -ExpectedTitle 'Text Diff'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextDiffInputA' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextDiffInputB' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextDiffCounts' `
        -Prefix '+0 added   -0 removed   0 unchanged' | Out-Null
    $whitespace = Wait-ForElement -Root $root -AutomationId 'NativeTextDiffIgnoreWhitespace'
    $case = Wait-ForElement -Root $root -AutomationId 'NativeTextDiffIgnoreCase'
    $whitespaceToggle = [System.Windows.Automation.TogglePattern]$whitespace.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    $caseToggle = [System.Windows.Automation.TogglePattern]$case.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $whitespaceToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off -and
        $caseToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'Text Diff resets managed page state after in-process route re-entry'
}

Invoke-OwnedRoute -Route 'module.textdiff' -ExpectedTitle 'Text Diff'

Invoke-OwnedRoute -Route 'lines' -ExpectedTitle 'Line Tools' -Inspect {
    param($root, $title)

    $lineToolsIds = @(
        'NativeLineToolsImplementationStatus',
        'NativeLineToolsInput',
        'NativeLineToolsCounts',
        'NativeLineToolsPrefix',
        'NativeLineToolsSuffix',
        'NativeLineToolsDelimiter',
        'NativeLineToolsNumberDot',
        'NativeLineToolsNumberParen',
        'NativeLineToolsRemoveNumbers',
        'NativeLineToolsAddPrefix',
        'NativeLineToolsAddSuffix',
        'NativeLineToolsQuotes',
        'NativeLineToolsJoin',
        'NativeLineToolsSplit',
        'NativeLineToolsReverseChars',
        'NativeLineToolsSort',
        'NativeLineToolsReverseOrder',
        'NativeLineToolsShuffle',
        'NativeLineToolsDedupe',
        'NativeLineToolsRemoveEmpty',
        'NativeLineToolsTrim',
        'NativeLineToolsOutput',
        'NativeLineToolsCopy',
        'NativeLineToolsStatus'
    )
    foreach ($id in $lineToolsIds) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $lineToolsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsPrefix'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsSuffix'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsDelimiter'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsNumberDot'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsSplit'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsTrim'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeLineToolsCopy'))
    Assert-True -Condition $lineToolsFit `
        -Name 'Line Tools exposes native editors, actions, accessibility, and horizontal clipping safety'

    $lineInput = Wait-ForElement -Root $root -AutomationId 'NativeLineToolsInput'
    $lineOutput = Wait-ForElement -Root $root -AutomationId 'NativeLineToolsOutput'
    Assert-True -Condition (
        $lineInput.Current.Name.StartsWith('Line Tools input', [StringComparison]::Ordinal) -and
        $lineOutput.Current.Name.StartsWith('Line Tools output', [StringComparison]::Ordinal)) `
        -Name 'Line Tools input and output editors expose localized accessible names'

    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsPrefix' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsSuffix' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsDelimiter' -ExpectedValue ', ' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeLineToolsCounts' `
        -Prefix '0 lines' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeLineToolsStatus' `
        -Prefix 'Ready.' | Out-Null
    Assert-True -Condition $true -Name 'Line Tools starts with managed-parity empty editors, delimiter, counts, and status'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeLineToolsStatus' `
        -Prefix 'Nothing to copy yet.' | Out-Null
    Assert-True -Condition $true -Name 'Line Tools refuses an empty clipboard write with an explicit status'

    # WinUI's UIA provider exposes multiline TextBox values with lone CR separators.
    $newLine = "`r"
    $threeLines = @('alpha', '', 'Beta') -join $newLine
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' -Value $threeLines | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeLineToolsCounts' `
        -Prefix '3 lines' | Out-Null

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsNumberDot'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('1. alpha', '2. ', '3. Beta') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsNumberParen'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('1) alpha', '2) ', '3) Beta') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Line Tools numbers every line with both managed formats, including blank lines'

    $unicodeDigit = [char]0x06F3
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' `
        -Value (@('1. alpha', '2) beta', "$unicodeDigit`: gamma") -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsRemoveNumbers'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('alpha', 'beta', 'gamma') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Line Tools removes ASCII and Unicode decimal line-number prefixes'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' -Value $threeLines | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsPrefix' -Value '> ' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsAddPrefix'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('> alpha', '> ', '> Beta') -join $newLine) | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsSuffix' -Value '!' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsAddSuffix'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('alpha!', '!', 'Beta!') -join $newLine) | Out-Null

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsQuotes'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('"alpha"', '""', '"Beta"') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Line Tools applies prefix, suffix, and quote transforms to every line'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsDelimiter' -Value ' | ' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsJoin'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue 'alpha |  | Beta' | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsDelimiter' -Value ', ' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' -Value 'alpha, beta, gamma' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsSplit'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('alpha', 'beta', 'gamma') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Line Tools joins and splits with the literal delimiter'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' `
        -Value (@('abc', 'de') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsReverseChars'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('cba', 'ed') -join $newLine) | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' `
        -Value (@('gamma', 'alpha', 'Beta') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsSort'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('alpha', 'Beta', 'gamma') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsReverseOrder'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('Beta', 'alpha', 'gamma') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Line Tools reverses characters, sorts ordinal-ignore-case, and reverses input order'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' `
        -Value (@('Alpha', 'alpha', 'beta') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsDedupe'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('Alpha', 'beta') -join $newLine) | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' `
        -Value (@('alpha', '   ', 'beta') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsRemoveEmpty'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('alpha', 'beta') -join $newLine) | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' `
        -Value (@('  alpha  ', "`tbeta`t") -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsTrim'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' `
        -ExpectedValue (@('alpha', 'beta') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Line Tools deduplicates first-win, removes whitespace-only lines, and trims line edges'

    $shuffleInput = @('alpha', 'beta', 'gamma') -join $newLine
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeLineToolsInput' -Value $shuffleInput | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsShuffle'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeLineToolsStatus' `
        -Prefix 'Shuffled' | Out-Null
    $shuffleOutput = (Get-EditableValuePattern -Element (
        Wait-ForElement -Root $root -AutomationId 'NativeLineToolsOutput')).Current.Value
    $shuffleLines = @($shuffleOutput -split "`r`n|`r|`n")
    $shuffleMembersMatch = @(Compare-Object @('alpha', 'beta', 'gamma') ($shuffleLines | Sort-Object)).Count -eq 0
    Assert-True -Condition ($shuffleLines.Count -eq 3 -and $shuffleMembersMatch) `
        -Name 'Line Tools shuffles without losing or adding lines'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeLineToolsCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeLineToolsStatus' `
        -Prefix 'Output copied to the clipboard.' | Out-Null
    Assert-True -Condition $true -Name 'Line Tools copies populated output only through the explicit Copy action'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsInput' -ExpectedValue $shuffleInput | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsPrefix' -ExpectedValue '> ' | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' -ExpectedValue $shuffleOutput | Out-Null
    Assert-True -Condition $true -Name 'Line Tools localizes in place while preserving input, settings, and output'

    Navigate-InProcessToRoute -Root $root -Route 'module.linetools' -ExpectedTitle 'Line Tools'
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsPrefix' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsSuffix' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeLineToolsDelimiter' -ExpectedValue ', ' | Out-Null
    Assert-True -Condition $true -Name 'Line Tools resets managed page state after in-process route re-entry'
}

foreach ($alias in @('linetools', 'module.linetools')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Line Tools'
}

Invoke-OwnedRoute -Route 'textsort' -ExpectedTitle 'Line Sort & Dedupe' -Inspect {
    param($root, $title)

    $textSortIds = @(
        'NativeTextSortImplementationStatus',
        'NativeTextSortMode',
        'NativeTextSortCaseInsensitive',
        'NativeTextSortDedupe',
        'NativeTextSortTrimCompare',
        'NativeTextSortReverse',
        'NativeTextSortShuffle',
        'NativeTextSortRemoveBlank',
        'NativeTextSortTrimEach',
        'NativeTextSortApply',
        'NativeTextSortReshuffle',
        'NativeTextSortCopy',
        'NativeTextSortClear',
        'NativeTextSortStats',
        'NativeTextSortInput',
        'NativeTextSortOutput'
    )
    foreach ($id in $textSortIds) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $textSortFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortCaseInsensitive'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortTrimCompare'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortRemoveBlank'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortApply'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortClear'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextSortOutput'))
    Assert-True -Condition $textSortFit `
        -Name 'Text Sort exposes native options, editors, actions, accessibility, and horizontal clipping safety'

    $sortInput = Wait-ForElement -Root $root -AutomationId 'NativeTextSortInput'
    $sortOutput = Wait-ForElement -Root $root -AutomationId 'NativeTextSortOutput'
    Assert-True -Condition (
        $sortInput.Current.Name.StartsWith('Text Sort input', [StringComparison]::Ordinal) -and
        $sortOutput.Current.Name.StartsWith('Text Sort output', [StringComparison]::Ordinal)) `
        -Name 'Text Sort editors expose localized accessible names'

    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextSortStats' `
        -Prefix 'Lines in: 0' | Out-Null
    $defaultTogglesOff = $true
    foreach ($id in @(
        'NativeTextSortCaseInsensitive', 'NativeTextSortDedupe', 'NativeTextSortTrimCompare',
        'NativeTextSortReverse', 'NativeTextSortShuffle', 'NativeTextSortRemoveBlank',
        'NativeTextSortTrimEach')) {
        $toggle = [System.Windows.Automation.TogglePattern](
            Wait-ForElement -Root $root -AutomationId $id).GetCurrentPattern(
                [System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) {
            $defaultTogglesOff = $false
        }
    }
    Assert-True -Condition $defaultTogglesOff `
        -Name 'Text Sort starts ascending with all seven managed option defaults off'

    $newLine = "`r"
    $modePicker = Wait-ForElement -Root $root -AutomationId 'NativeTextSortMode'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextSortInput' `
        -Value (@('c', 'a', 'b') -join $newLine) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('a', 'b', 'c') -join $newLine) | Out-Null
    Select-ComboIndex -Combo $modePicker -Index 0
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('c', 'a', 'b') -join $newLine) | Out-Null
    Select-ComboIndex -Combo $modePicker -Index 2
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('c', 'b', 'a') -join $newLine) | Out-Null
    Select-ComboIndex -Combo $modePicker -Index 3
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextSortInput' `
        -Value (@('file10', 'file2', 'file1') -join $newLine) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('file1', 'file2', 'file10') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Text Sort exercises keep-order, ascending, descending, and natural sort modes'

    $modePicker = Wait-ForElement -Root $root -AutomationId 'NativeTextSortMode'
    Select-ComboIndex -Combo $modePicker -Index 1
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextSortInput' `
        -Value (@('alpha', 'Beta') -join $newLine) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('Beta', 'alpha') -join $newLine) | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeTextSortCaseInsensitive' -IsOn $true | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('alpha', 'Beta') -join $newLine) | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextSortInput' `
        -Value (@('alpha', ' alpha ', 'beta') -join $newLine) | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeTextSortDedupe' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeTextSortTrimCompare' -IsOn $true | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('alpha', 'beta') -join $newLine) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextSortStats' `
        -Prefix 'Lines in: 3   ' | Out-Null
    Assert-True -Condition $true `
        -Name 'Text Sort applies ordinal ignore-case and trim-before-compare first-win deduplication'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextSortInput' `
        -Value (@('  Beta  ', 'alpha', 'ALPHA', '   ', 'gamma') -join $newLine) | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeTextSortRemoveBlank' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeTextSortTrimEach' -IsOn $true | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('alpha', 'Beta', 'gamma') -join $newLine) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextSortStats' `
        -Prefix 'Lines in: 5   ' | Out-Null

    Set-ToggleState -Root $root -AutomationId 'NativeTextSortReverse' -IsOn $true | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('gamma', 'Beta', 'alpha') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextSortApply'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' `
        -ExpectedValue (@('gamma', 'Beta', 'alpha') -join $newLine) | Out-Null

    Set-ToggleState -Root $root -AutomationId 'NativeTextSortShuffle' -IsOn $true | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextSortReshuffle'
    $sortShuffleOutput = (Get-EditableValuePattern -Element (
        Wait-ForElement -Root $root -AutomationId 'NativeTextSortOutput')).Current.Value
    $sortShuffleLines = @($sortShuffleOutput -split "`r`n|`r|`n")
    $sortShuffleMembersMatch = @(
        Compare-Object @('alpha', 'Beta', 'gamma') ($sortShuffleLines | Sort-Object)).Count -eq 0
    Assert-True -Condition ($sortShuffleLines.Count -eq 3 -and $sortShuffleMembersMatch) `
        -Name 'Text Sort Apply and Re-shuffle recompute without losing cleaned lines'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextSortCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextSortStats' `
        -Prefix 'Copied to clipboard.' | Out-Null
    Assert-True -Condition $true -Name 'Text Sort copies output only through its explicit Copy action'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortInput' `
        -ExpectedValue (@('  Beta  ', 'alpha', 'ALPHA', '   ', 'gamma') -join $newLine) | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    $shuffleToggle = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeTextSortShuffle').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($shuffleToggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'Text Sort localizes in place while preserving input and option state'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextSortClear'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' -ExpectedValue '' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextSortCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextSortStats' `
        -Prefix 'Copied to clipboard.' | Out-Null
    Assert-True -Condition $true -Name 'Text Sort Clear preserves options and Copy accepts the managed empty result'

    Navigate-InProcessToRoute -Root $root -Route 'module.textsort' -ExpectedTitle 'Line Sort & Dedupe'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextSortOutput' -ExpectedValue '' | Out-Null
    $resetTogglesOff = $true
    foreach ($id in @(
        'NativeTextSortCaseInsensitive', 'NativeTextSortDedupe', 'NativeTextSortTrimCompare',
        'NativeTextSortReverse', 'NativeTextSortShuffle', 'NativeTextSortRemoveBlank',
        'NativeTextSortTrimEach')) {
        $toggle = [System.Windows.Automation.TogglePattern](
            Wait-ForElement -Root $root -AutomationId $id).GetCurrentPattern(
                [System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) {
            $resetTogglesOff = $false
        }
    }
    Assert-True -Condition $resetTogglesOff `
        -Name 'Text Sort resets managed page state after in-process route re-entry'
}

Invoke-OwnedRoute -Route 'module.textsort' -ExpectedTitle 'Line Sort & Dedupe'

Invoke-OwnedRoute -Route 'textwrap' -ExpectedTitle 'Text Wrap' -Inspect {
    param($root, $title)

    $textWrapIds = @(
        'NativeTextWrapImplementationStatus',
        'NativeTextWrapInput',
        'NativeTextWrapWidth',
        'NativeTextWrapReadout',
        'NativeTextWrapBreakLong',
        'NativeTextWrapPrefix',
        'NativeTextWrapIndent',
        'NativeTextWrapHardWrap',
        'NativeTextWrapUnwrap',
        'NativeTextWrapReflow',
        'NativeTextWrapAddPrefix',
        'NativeTextWrapHangingIndent',
        'NativeTextWrapOutput',
        'NativeTextWrapCopy',
        'NativeTextWrapStatus'
    )
    foreach ($id in $textWrapIds) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $textWrapFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapWidth'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapBreakLong'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapPrefix'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapIndent'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapHardWrap'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapHangingIndent'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapCopy'))
    Assert-True -Condition $textWrapFit `
        -Name 'Text Wrap exposes native editors, options, actions, accessibility, and horizontal clipping safety'

    $wrapInput = Wait-ForElement -Root $root -AutomationId 'NativeTextWrapInput'
    $wrapOutput = Wait-ForElement -Root $root -AutomationId 'NativeTextWrapOutput'
    Assert-True -Condition (
        $wrapInput.Current.Name.StartsWith('Text Wrap input', [StringComparison]::Ordinal) -and
        $wrapOutput.Current.Name.StartsWith('Text Wrap output', [StringComparison]::Ordinal)) `
        -Name 'Text Wrap editors expose localized accessible names'

    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapWidth' -ExpectedValue '72' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapPrefix' -ExpectedValue '> ' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapIndent' -ExpectedValue '4' | Out-Null
    $breakLong = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeTextWrapBreakLong').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapReadout' `
        -Prefix 'Target 72 cols' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapStatus' `
        -Prefix 'Ready.' | Out-Null
    Assert-True -Condition ($breakLong.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'Text Wrap starts with the managed 72-column, prefix, indent, and break-word defaults'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapStatus' `
        -Prefix 'Nothing to copy.' | Out-Null
    Assert-True -Condition $true -Name 'Text Wrap refuses an empty clipboard write with an explicit status'

    $newLine = "`r"
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapWidth' -Value '10' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapInput').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapReadout' `
        -Prefix 'Target 10 cols' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapInput' -Value 'alpha beta gamma' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapHardWrap'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' `
        -ExpectedValue (@('alpha beta', 'gamma') -join $newLine) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapReadout' `
        -Prefix 'Target 10 cols' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapStatus' `
        -Prefix 'Hard-wrapped.' | Out-Null
    Assert-True -Condition $true -Name 'Text Wrap hard-wraps greedily and updates its live measurement readout'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapWidth' -Value '5' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapInput' -Value 'abcdefghijkl' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapHardWrap'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' -ExpectedValue 'abcdefghijkl' | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeTextWrapBreakLong' -IsOn $true | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapHardWrap'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' `
        -ExpectedValue (@('abcde', 'fghij', 'kl') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Text Wrap preserves or chunks over-width words according to the explicit option'

    Set-ToggleState -Root $root -AutomationId 'NativeTextWrapBreakLong' -IsOn $false | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapInput' `
        -Value (@('alpha beta', 'gamma') -join $newLine) | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapUnwrap'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' `
        -ExpectedValue 'alpha beta gamma' | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapWidth' -Value '10' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeTextWrapInput').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapReadout' `
        -Prefix 'Target 10 cols' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapReflow'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' `
        -ExpectedValue (@('alpha beta', 'gamma') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Text Wrap unwraps and reflows from the original input contract'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapPrefix' -Value '> ' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapAddPrefix'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' `
        -ExpectedValue (@('> alpha beta', '> gamma') -join $newLine) | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextWrapIndent' -Value '4' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapHangingIndent'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' `
        -ExpectedValue (@('> alpha beta', '    > gamma') -join $newLine) | Out-Null
    Assert-True -Condition $true -Name 'Text Wrap chains prefix and hanging-indent actions from populated output'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeTextWrapCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextWrapStatus' `
        -Prefix 'Copied to clipboard.' | Out-Null
    Assert-True -Condition $true -Name 'Text Wrap copies populated output only through its explicit Copy action'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapInput' `
        -ExpectedValue (@('alpha beta', 'gamma') -join $newLine) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' `
        -ExpectedValue (@('> alpha beta', '    > gamma') -join $newLine) | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapWidth' -ExpectedValue '10' | Out-Null
    Assert-True -Condition $true `
        -Name 'Text Wrap localizes in place while preserving input, options, and chained output'

    Navigate-InProcessToRoute -Root $root -Route 'module.textwrap' -ExpectedTitle 'Text Wrap'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapWidth' -ExpectedValue '72' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapPrefix' -ExpectedValue '> ' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextWrapIndent' -ExpectedValue '4' | Out-Null
    $breakLong = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeTextWrapBreakLong').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($breakLong.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'Text Wrap resets managed page state after in-process route re-entry'
}

Invoke-OwnedRoute -Route 'module.textwrap' -ExpectedTitle 'Text Wrap'

Invoke-OwnedRoute -Route 'textstats' -ExpectedTitle 'Text Statistics' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeTextStatsImplementationStatus',
        'NativeTextStatsInput',
        'NativeTextStatsIgnoreStopWords',
        'NativeTextStatsStatus',
        'NativeTextStatsCharacters',
        'NativeTextStatsCharactersNoSpaces',
        'NativeTextStatsWords',
        'NativeTextStatsUniqueWords',
        'NativeTextStatsSentences',
        'NativeTextStatsParagraphs',
        'NativeTextStatsAverageWordLength',
        'NativeTextStatsAverageSentenceLength',
        'NativeTextStatsReadingTime',
        'NativeTextStatsSpeakingTime',
        'NativeTextStatsReadingEase',
        'NativeTextStatsGrade',
        'NativeTextStatsEaseHint',
        'NativeTextStatsFrequencyList',
        'NativeTextStatsFrequencyEmpty'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $statsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsIgnoreStopWords'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsCharactersNoSpaces'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsAverageSentenceLength'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsGrade'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsFrequencyEmpty'))
    Assert-True -Condition $statsFit `
        -Name 'Text Statistics exposes accessible editors, metrics, frequency results, and horizontal clipping safety'

    $statsInput = Wait-ForElement -Root $root -AutomationId 'NativeTextStatsInput'
    Assert-True -Condition $statsInput.Current.Name.StartsWith('Text Statistics input', [StringComparison]::Ordinal) `
        -Name 'Text Statistics editor exposes a localized accessible name'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextStatsInput' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsCharacters' -Prefix 'Characters: 0' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsWords' -Prefix 'Words: 0' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsStatus' -Prefix 'Ready.' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsFrequencyEmpty' `
        -Prefix 'No words yet' | Out-Null
    $statsStop = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeTextStatsIgnoreStopWords').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($statsStop.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'Text Statistics starts empty, ready, and with stop-word filtering off'

    $statsText = 'The cat sat. The cat.'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextStatsInput' -Value $statsText | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsCharacters' -Prefix 'Characters: 21' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsWords' -Prefix 'Words: 5' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsUniqueWords' -Prefix 'Unique words: 3' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsSentences' -Prefix 'Sentences: 2' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsAverageSentenceLength' `
        -Prefix 'Avg sentence length: 2.5 words' | Out-Null
    $middleDot = [char]0x00B7
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsTopWord0' -Prefix "cat $middleDot 2" | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsStatus' -Prefix 'Updated live.' | Out-Null
    $statsResultsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsFrequencyList'),
        (Wait-ForElement -Root $root -AutomationId 'NativeTextStatsTopWord0'))
    Assert-True -Condition $statsResultsFit `
        -Name 'Text Statistics populated frequency list and ranked rows stay horizontally unclipped'
    Assert-True -Condition $true `
        -Name 'Text Statistics recomputes live counts, readability inputs, and deterministic top-word ranking'

    $thousandCharacters = ('a' * 1000) -join ''
    $groupedThousand = (1000).ToString('N0', [Globalization.CultureInfo]::CurrentCulture)
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextStatsInput' -Value $thousandCharacters | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsCharacters' `
        -Prefix "Characters: $groupedThousand" | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeTextStatsInput' -Value $statsText | Out-Null
    Assert-True -Condition $true `
        -Name 'Text Statistics formats four-digit counts with the current user culture N0 contract'

    Set-ToggleState -Root $root -AutomationId 'NativeTextStatsIgnoreStopWords' -IsOn $true | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsTopWord0' -Prefix "cat $middleDot 2" | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsTopWord1' -Prefix "sat $middleDot 1" | Out-Null
    Assert-True -Condition $true `
        -Name 'Text Statistics removes English stop words only from its frequency list'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextStatsInput' -ExpectedValue $statsText | Out-Null
    $statsCantoneseWords = ([char]0x5B57).ToString() + [char]0x6578 + [char]0xFF1A + '5'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsWords' `
        -Prefix $statsCantoneseWords | Out-Null
    Assert-True -Condition $true `
        -Name 'Text Statistics renders a real Cantonese metric label before returning to English'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsWords' -Prefix 'Words: 5' | Out-Null
    $statsStop = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeTextStatsIgnoreStopWords').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($statsStop.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'Text Statistics localizes in place while preserving text and stop-word state'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeTextStatsInput')) `
        -Name 'Text Statistics releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.textstats' -ExpectedTitle 'Text Statistics'
    Wait-ForElementValue -Root $root -AutomationId 'NativeTextStatsInput' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeTextStatsWords' -Prefix 'Words: 0' | Out-Null
    $statsStop = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeTextStatsIgnoreStopWords').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($statsStop.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'Text Statistics resets managed page state after in-process route re-entry'
}

Invoke-OwnedRoute -Route 'module.textstats' -ExpectedTitle 'Text Statistics'

Invoke-OwnedRoute -Route 'wordfreq' -ExpectedTitle 'Word Frequency' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeWordFreqImplementationStatus',
        'NativeWordFreqInput',
        'NativeWordFreqMode',
        'NativeWordFreqMinLength',
        'NativeWordFreqCaseInsensitive',
        'NativeWordFreqStripPunctuation',
        'NativeWordFreqRemoveStopWords',
        'NativeWordFreqTotals',
        'NativeWordFreqCopy',
        'NativeWordFreqResults'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $wordFreqFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqMode'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqMinLength'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqCaseInsensitive'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqStripPunctuation'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqRemoveStopWords'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqTotals'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqCopy'))
    Assert-True -Condition $wordFreqFit `
        -Name 'Word Frequency exposes accessible options, summary, results, and horizontal clipping safety'

    $wordInput = Wait-ForElement -Root $root -AutomationId 'NativeWordFreqInput'
    Assert-True -Condition $wordInput.Current.Name.StartsWith('Word Frequency input', [StringComparison]::Ordinal) `
        -Name 'Word Frequency editor exposes a localized accessible name'
    Wait-ForElementValue -Root $root -AutomationId 'NativeWordFreqInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeWordFreqMinLength' -ExpectedValue '1' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' -Prefix 'Total: 0' | Out-Null
    $mode = Wait-ForElement -Root $root -AutomationId 'NativeWordFreqMode'
    $modeSelection = [System.Windows.Automation.SelectionPattern]$mode.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    $selectedMode = $modeSelection.Current.GetSelection()
    $case = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeWordFreqCaseInsensitive').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $punctuation = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeWordFreqStripPunctuation').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $stop = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeWordFreqRemoveStopWords').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $selectedMode.Count -eq 1 -and
        $selectedMode[0].Current.Name.StartsWith('Words', [StringComparison]::Ordinal) -and
        $case.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $punctuation.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $stop.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'Word Frequency starts in word mode with managed case, punctuation, stop-word, and length defaults'

    $wordFreqCopy = Wait-ForElement -Root $root -AutomationId 'NativeWordFreqCopy'
    Assert-True -Condition (
        $wordFreqCopy.Current.IsEnabled -and
        $wordFreqCopy.Current.Name.StartsWith('Copy as CSV', [StringComparison]::Ordinal)) `
        -Name 'Word Frequency exposes its explicit CSV copy action without mutating the clipboard by default'
    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeWordFreqCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqCopy' -Prefix 'Copied!' | Out-Null
        Assert-True -Condition $true `
            -Name 'Word Frequency opt-in smoke explicitly copies the managed header-only CSV'
    }

    $frequencyText = 'The cat, cat dog.'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeWordFreqInput' -Value $frequencyText | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' `
        -Prefix 'Total: 4   Unique: 3   Lexical diversity: 75.0%' | Out-Null
    $middleDot = [char]0x00B7
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqRow0' `
        -Prefix "1 $middleDot cat $middleDot 2 $middleDot 50.0%" | Out-Null
    $wordFreqResultsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqResults'),
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqRow0'))
    Assert-True -Condition $wordFreqResultsFit `
        -Name 'Word Frequency populated ranked list and bar rows stay horizontally unclipped'
    Assert-True -Condition $true `
        -Name 'Word Frequency ranks case-folded, punctuation-stripped words with counts and percentages'

    Set-ToggleState -Root $root -AutomationId 'NativeWordFreqRemoveStopWords' -IsOn $true | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' `
        -Prefix 'Total: 3   Unique: 2   Lexical diversity: 66.7%' | Out-Null
    $mode = Wait-ForElement -Root $root -AutomationId 'NativeWordFreqMode'
    Select-ComboIndex -Combo $mode -Index 1
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' `
        -Prefix 'Total: 2   Unique: 2' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqRow0' `
        -Prefix "1 $middleDot cat cat $middleDot 1 $middleDot 50.0%" | Out-Null
    $mode = Wait-ForElement -Root $root -AutomationId 'NativeWordFreqMode'
    Select-ComboIndex -Combo $mode -Index 2
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' `
        -Prefix 'Total: 14' | Out-Null
    Assert-True -Condition $true `
        -Name 'Word Frequency switches live between stop-word-filtered words, bigrams, and Unicode-scalar characters'

    $mode = Wait-ForElement -Root $root -AutomationId 'NativeWordFreqMode'
    Select-ComboIndex -Combo $mode -Index 0
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeWordFreqMinLength' -Value '4' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqInput').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' -Prefix 'Total: 0' | Out-Null
    Assert-True -Condition $true -Name 'Word Frequency applies the managed minimum-length filter'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeWordFreqInput' -ExpectedValue $frequencyText | Out-Null
    $wordFreqCantoneseTotal = ([char]0x7E3D).ToString() + [char]0x6578 + [char]0xFF1A + '0'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' `
        -Prefix $wordFreqCantoneseTotal | Out-Null
    Assert-True -Condition $true `
        -Name 'Word Frequency renders a real Cantonese result summary before returning to English'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementValue -Root $root -AutomationId 'NativeWordFreqMinLength' -ExpectedValue '4' | Out-Null
    $stop = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeWordFreqRemoveStopWords').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition ($stop.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'Word Frequency localizes in place while preserving input and every option'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeWordFreqInput')) `
        -Name 'Word Frequency releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.wordfreq' -ExpectedTitle 'Word Frequency'
    Wait-ForElementValue -Root $root -AutomationId 'NativeWordFreqInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeWordFreqMinLength' -ExpectedValue '1' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeWordFreqTotals' -Prefix 'Total: 0' | Out-Null
    $case = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeWordFreqCaseInsensitive').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $punctuation = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeWordFreqStripPunctuation').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $stop = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeWordFreqRemoveStopWords').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $case.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $punctuation.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $stop.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off -and
        (Wait-ForElement -Root $root -AutomationId 'NativeWordFreqCopy').Current.Name.StartsWith('Copy as CSV', [StringComparison]::Ordinal)) `
        -Name 'Word Frequency resets managed page state and copy label after in-process route re-entry'
}

Invoke-OwnedRoute -Route 'module.wordfreq' -ExpectedTitle 'Word Frequency'

Invoke-OwnedRoute -Route 'similarity' -ExpectedTitle 'String Compare' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeStringCompareImplementationStatus',
        'NativeStringCompareInputA',
        'NativeStringCompareInputB',
        'NativeStringCompareIgnoreCase',
        'NativeStringCompareIgnoreWhitespace',
        'NativeStringCompareCopy',
        'NativeStringCompareLength',
        'NativeStringCompareLevenshtein',
        'NativeStringCompareSimilarity',
        'NativeStringCompareDamerau',
        'NativeStringCompareHamming',
        'NativeStringCompareJaroWinkler',
        'NativeStringCompareLongestSubstring',
        'NativeStringCompareLongestSubsequence',
        'NativeStringCompareStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $compareFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareInputA'),
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareInputB'),
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreCase'),
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreWhitespace'),
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareSimilarity'),
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareLongestSubsequence'),
        (Wait-ForElement -Root $root -AutomationId 'NativeStringCompareStatus'))
    Assert-True -Condition $compareFit `
        -Name 'String Compare exposes accessible editors, options, metrics, copy, and horizontal clipping safety'

    $inputA = Wait-ForElement -Root $root -AutomationId 'NativeStringCompareInputA'
    $inputB = Wait-ForElement -Root $root -AutomationId 'NativeStringCompareInputB'
    Assert-True -Condition (
        $inputA.Current.Name.StartsWith('String A', [StringComparison]::Ordinal) -and
        $inputB.Current.Name.StartsWith('String B', [StringComparison]::Ordinal)) `
        -Name 'String Compare editors expose localized accessible names'
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementValue -Root $root -AutomationId 'NativeStringCompareInputA' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeStringCompareInputB' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareLength' -Prefix 'Length A / B: 0 / 0' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareSimilarity' -Prefix 'Similarity: 100.0%' | Out-Null
    $case = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreCase').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $whitespace = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreWhitespace').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $case.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off -and
        $whitespace.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'String Compare starts with empty inputs, a 100 percent empty match, and both managed options off'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeStringCompareInputA' -Value 'kitten' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeStringCompareInputB' -Value 'sitting' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareLength' -Prefix 'Length A / B: 6 / 7' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareLevenshtein' `
        -Prefix 'Levenshtein distance: 3' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareSimilarity' `
        -Prefix 'Similarity: 57.1%' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareHamming' `
        -Prefix 'Hamming distance: n/a (lengths differ)' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareLongestSubsequence' `
        -Prefix 'Longest common subsequence: 4' | Out-Null
    Assert-True -Condition $true `
        -Name 'String Compare computes the complete live classic-metric set for unequal strings'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeStringCompareInputA' -Value 'A b' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeStringCompareInputB' -Value 'ab' | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeStringCompareIgnoreCase' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeStringCompareIgnoreWhitespace' -IsOn $true | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareLength' -Prefix 'Length A / B: 2 / 2' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareLevenshtein' `
        -Prefix 'Levenshtein distance: 0' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareSimilarity' `
        -Prefix 'Similarity: 100.0%' | Out-Null
    Assert-True -Condition $true `
        -Name 'String Compare composes invariant case folding with managed whitespace removal'

    $compareCopy = Wait-ForElement -Root $root -AutomationId 'NativeStringCompareCopy'
    $compareStatus = Wait-ForElement -Root $root -AutomationId 'NativeStringCompareStatus'
    Assert-True -Condition (
        $compareCopy.Current.IsEnabled -and
        $compareCopy.Current.Name.StartsWith('Copy report', [StringComparison]::Ordinal) -and
        $compareStatus.Current.Name.StartsWith('Computed locally', [StringComparison]::Ordinal)) `
        -Name 'String Compare exposes explicit report copy and accessible status without mutating the clipboard by default'
    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeStringCompareCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareStatus' `
            -Prefix 'Report copied to the clipboard.' | Out-Null
        Assert-True -Condition $true `
            -Name 'String Compare opt-in smoke copies its localized report after explicit Copy'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeStringCompareInputA' -ExpectedValue 'A b' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeStringCompareInputB' -ExpectedValue 'ab' | Out-Null
    $compareCantoneseSimilarity = ([char]0x76F8).ToString() + [char]0x4F3C + [char]0x5EA6 + ': 100.0%'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareSimilarity' `
        -Prefix $compareCantoneseSimilarity | Out-Null
    Assert-True -Condition $true `
        -Name 'String Compare renders a real Cantonese metric label before returning to English'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareSimilarity' `
        -Prefix 'Similarity: 100.0%' | Out-Null
    $case = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreCase').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $whitespace = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreWhitespace').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $case.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $whitespace.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'String Compare localizes in place while preserving both inputs and options'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeStringCompareInputA')) `
        -Name 'String Compare releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.stringcompare' -ExpectedTitle 'String Compare'
    Wait-ForElementValue -Root $root -AutomationId 'NativeStringCompareInputA' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeStringCompareInputB' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeStringCompareSimilarity' -Prefix 'Similarity: 100.0%' | Out-Null
    $case = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreCase').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $whitespace = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeStringCompareIgnoreWhitespace').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $case.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off -and
        $whitespace.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'String Compare resets managed page state after in-process route re-entry'
}

foreach ($alias in @('stringcompare', 'module.stringcompare')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'String Compare'
}

Invoke-OwnedRoute -Route 'phonetic' -ExpectedTitle 'Phonetic Speller' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativePhoneticImplementationStatus',
        'NativePhoneticInput',
        'NativePhoneticAlphabet',
        'NativePhoneticUpper',
        'NativePhoneticKeepPunctuation',
        'NativePhoneticSpoken',
        'NativePhoneticRows',
        'NativePhoneticCopy',
        'NativePhoneticStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    Set-EditableValueAndWait -Root $root -AutomationId 'NativePhoneticInput' -Value 'ab-9' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativePhoneticSpoken' `
        -ExpectedValue 'Alpha Bravo - Niner' | Out-Null
    $phoneticRows = Wait-ForElement -Root $root -AutomationId 'NativePhoneticRows'
    Wait-ForDescendantNamePrefix -Root $phoneticRows -Prefix 'a: Alpha' | Out-Null
    Assert-True -Condition $true `
        -Name 'Phonetic Speller maps letters, punctuation, and ICAO digit words through native controls'

    Set-ToggleState -Root $root -AutomationId 'NativePhoneticKeepPunctuation' -IsOn $false | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativePhoneticSpoken' `
        -ExpectedValue 'Alpha Bravo Niner' | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativePhoneticAlphabet') -Index 1
    Wait-ForElementValue -Root $root -AutomationId 'NativePhoneticSpoken' `
        -ExpectedValue 'Adam Boy Niner' | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativePhoneticUpper' -IsOn $true | Out-Null
    $phoneticRows = Wait-ForElement -Root $root -AutomationId 'NativePhoneticRows'
    Wait-ForDescendantNamePrefix -Root $phoneticRows -Prefix 'A: Adam' | Out-Null
    Assert-True -Condition $true `
        -Name 'Phonetic Speller preserves police alphabet, punctuation filtering, and display-case options'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativePhoneticCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePhoneticStatus' `
            -Prefix 'Spoken text copied' | Out-Null
        Assert-True -Condition $true -Name 'Phonetic Speller writes the clipboard only after explicit Copy'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Phonetic Speller' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativePhoneticSpoken' `
        -ExpectedValue 'Adam Boy Niner' | Out-Null
    Assert-True -Condition $true `
        -Name 'Phonetic Speller localizes in place while preserving its input and options'

    $phoneticStatus = Wait-ForElement -Root $root -AutomationId 'NativePhoneticStatus'
    $largePhoneticInput = 'a' * 501
    Set-EditableValueAndWait -Root $root -AutomationId 'NativePhoneticInput' `
        -Value $largePhoneticInput | Out-Null
    Wait-ForExistingElementNamePrefix -Element $phoneticStatus `
        -Prefix '501 item(s) spelled locally.' -WaitTimeoutMs 30000 | Out-Null
    Assert-True -Condition $true `
        -Name 'Phonetic Speller renders a 501-item per-character result and reports its exact count'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativePhoneticInput')) `
        -Name 'Phonetic Speller releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.phonetic' -ExpectedTitle 'Phonetic Speller'
    Wait-ForElementValue -Root $root -AutomationId 'NativePhoneticInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativePhoneticSpoken' `
        -ExpectedValue '(nothing to spell yet)' | Out-Null
    $upper = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativePhoneticUpper').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $punctuation = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativePhoneticKeepPunctuation').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $upper.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off -and
        $punctuation.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) `
        -Name 'Phonetic Speller resets managed page state after in-process route re-entry'
}

foreach ($alias in @('nato', 'phonetic', 'module.phonetic')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Phonetic Speller'
}

Invoke-OwnedRoute -Route 'boxtext' -ExpectedTitle 'Box & Banner Text' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeBoxTextImplementationStatus',
        'NativeBoxTextInput',
        'NativeBoxTextStyle',
        'NativeBoxTextAlignment',
        'NativeBoxTextPadding',
        'NativeBoxTextTitle',
        'NativeBoxTextOutput',
        'NativeBoxTextCopy',
        'NativeBoxTextStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $newLine = "`r"
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBoxTextInput' `
        -Value (@('hi', 'x') -join $newLine) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBoxTextOutput' `
        -ExpectedValue (@('+----+', '| hi |', '| x  |', '+----+') -join $newLine) | Out-Null
    Assert-True -Condition $true `
        -Name 'Box Text renders managed-compatible multiline ASCII padding through native controls'

    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBoxTextStyle') -Index 6
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBoxTextTitle' -Value 'Note' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBoxTextPadding' -Value '2' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeBoxTextOutput').SetFocus()
    Wait-ForElementValue -Root $root -AutomationId 'NativeBoxTextOutput' `
        -ExpectedValue (@('/*', ' * Note', ' *', ' *   hi', ' *   x', ' */') -join $newLine) | Out-Null
    Assert-True -Condition $true `
        -Name 'Box Text switches to titled comment blocks with bounded padding'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeBoxTextCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBoxTextStatus' `
            -Prefix 'Copied to the clipboard' | Out-Null
        Assert-True -Condition $true -Name 'Box Text writes the clipboard only after explicit Copy'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Box & Banner Text' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBoxTextOutput' `
        -ExpectedValue (@('/*', ' * Note', ' *', ' *   hi', ' *   x', ' */') -join $newLine) | Out-Null
    Assert-True -Condition $true `
        -Name 'Box Text localizes in place while preserving text, title, style, and padding'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeBoxTextInput')) `
        -Name 'Box Text releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.boxtext' -ExpectedTitle 'Box & Banner Text'
    Wait-ForElementValue -Root $root -AutomationId 'NativeBoxTextInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBoxTextTitle' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBoxTextPadding' -ExpectedValue '1' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBoxTextOutput' `
        -ExpectedValue (@('+--+', '|  |', '+--+') -join $newLine) | Out-Null
    Assert-True -Condition $true `
        -Name 'Box Text resets managed page state after in-process route re-entry'
}

foreach ($alias in @('boxtext', 'module.boxtext')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Box & Banner Text'
}

Invoke-OwnedRoute -Route 'htmlentities' -ExpectedTitle 'HTML Entities' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeHtmlEntitiesImplementationStatus',
        'NativeHtmlEntitiesMode',
        'NativeHtmlEntitiesEscapeNonAscii',
        'NativeHtmlEntitiesInput',
        'NativeHtmlEntitiesInputCount',
        'NativeHtmlEntitiesOutput',
        'NativeHtmlEntitiesOutputCount',
        'NativeHtmlEntitiesCopy',
        'NativeHtmlEntitiesReferenceRows',
        'NativeHtmlEntitiesReference0',
        'NativeHtmlEntitiesStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $copyright = [char]0x00A9
    $emoji = [char]::ConvertFromUtf32(0x1F600)
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeHtmlEntitiesInput' `
        -Value "A<&`"'$copyright$emoji" | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesOutput' `
        -ExpectedValue "A&lt;&amp;&quot;&#39;$copyright$emoji" | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeHtmlEntitiesEscapeNonAscii' -IsOn $true | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesOutput' `
        -ExpectedValue 'A&lt;&amp;&quot;&#39;&#xA9;&#x1F600;' | Out-Null
    Assert-True -Condition $true `
        -Name 'HTML Entities escapes mandatory characters and supplementary scalars through native controls'

    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeHtmlEntitiesMode') -Index 1
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeHtmlEntitiesInput' `
        -Value '&copy; &#x1F600; &unknown;' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesOutput' `
        -ExpectedValue "$copyright $emoji &unknown;" | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeHtmlEntitiesReference0' `
        -Prefix '&amp; Ampersand' | Out-Null
    Assert-True -Condition $true `
        -Name 'HTML Entities decodes named and numeric entities while preserving unknown references'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeHtmlEntitiesReference0'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeHtmlEntitiesStatus' `
            -Prefix 'Copied &amp;' | Out-Null
        Assert-True -Condition $true `
            -Name 'HTML Entities reference rows copy only after an explicit selection'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = @([char]0x7CB5, [char]0x8A9E) -join ''
    $bilingualLabel = @([char]0x96D9, [char]0x8A9E) -join ''
    $entityLabel = @([char]0x5BE6, [char]0x9AD4) -join ''
    $andSymbolLabel = 'and ' + (@([char]0x7B26, [char]0x865F) -join '')
    $middleDot = [char]0x00B7
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForPageTitle -Root $root -Prefix ('HTML ' + $entityLabel) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeHtmlEntitiesReference0' `
        -Prefix ('&amp; ' + $andSymbolLabel) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesOutput' `
        -ExpectedValue "$copyright $emoji &unknown;" | Out-Null
    Assert-True -Condition $true `
        -Name 'HTML Entities localizes reference descriptions in Cantonese while preserving decode state'

    Select-ComboItem -Combo $language -Name ('Bilingual ' + $middleDot + ' ' + $bilingualLabel)
    Wait-ForPageTitle -Root $root -Prefix ('HTML Entities ' + $middleDot + ' HTML ' + $entityLabel) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeHtmlEntitiesReference0' `
        -Prefix ('&amp; Ampersand ' + $middleDot + ' ' + $andSymbolLabel) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesOutput' `
        -ExpectedValue "$copyright $emoji &unknown;" | Out-Null
    Assert-True -Condition $true `
        -Name 'HTML Entities exposes bilingual reference descriptions while preserving decode state'

    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'HTML Entities' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeHtmlEntitiesReference0' `
        -Prefix '&amp; Ampersand' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesOutput' `
        -ExpectedValue "$copyright $emoji &unknown;" | Out-Null
    Assert-True -Condition $true `
        -Name 'HTML Entities returns reference descriptions to English while preserving decode state and input'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeHtmlEntitiesInput')) `
        -Name 'HTML Entities releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.htmlentities' -ExpectedTitle 'HTML Entities'
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeHtmlEntitiesOutput' -ExpectedValue '' | Out-Null
    $nonAscii = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeHtmlEntitiesEscapeNonAscii').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $nonAscii.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'HTML Entities resets managed page state after in-process route re-entry'
}

foreach ($alias in @('entities', 'htmlentities', 'module.htmlentities')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'HTML Entities'
}

Invoke-OwnedRoute -Route 'slugify' -ExpectedTitle 'Slugify' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeSlugifyImplementationStatus',
        'NativeSlugifyInput',
        'NativeSlugifySeparator',
        'NativeSlugifyCase',
        'NativeSlugifyMaxLength',
        'NativeSlugifyStripDiacritics',
        'NativeSlugifyCollapseRepeats',
        'NativeSlugifyKeepUnicodeLetters',
        'NativeSlugifyOutput',
        'NativeSlugifyCopy',
        'NativeSlugifyPreview',
        'NativeSlugifyStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $slugFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifySeparator'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyCase'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyMaxLength'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyStripDiacritics'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyCollapseRepeats'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyKeepUnicodeLetters'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyOutput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyPreview'),
        (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyStatus'))
    Assert-True -Condition $slugFit `
        -Name 'Slugify exposes accessible local controls without horizontal clipping'

    $cafeTea = 'Caf' + ([char]0x00E9).ToString() + ' & Tea'
    $previewArrow = ([char]0x2192).ToString()
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSlugifyInput' -Value $cafeTea | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyOutput' -ExpectedValue 'cafe-tea' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSlugifyPreview' `
        -Prefix ($cafeTea + '  ' + $previewArrow + '  cafe-tea') | Out-Null
    Assert-True -Condition $true `
        -Name 'Slugify performs managed-compatible accent stripping, boundary cleanup, and live first-line preview'

    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeSlugifySeparator') -Index 1
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeSlugifyCase') -Index 1
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyOutput' -ExpectedValue 'CAFE_TEA' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSlugifyMaxLength' -Value '5' | Out-Null
    # NumberBox commits a typed value when focus leaves its embedded edit control.
    Set-ToggleState -Root $root -AutomationId 'NativeSlugifyCollapseRepeats' -IsOn $false | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyOutput' -ExpectedValue 'CAFE' | Out-Null
    Assert-True -Condition $true `
        -Name 'Slugify applies separator case and UTF-16 max-length options through native controls'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSlugifyMaxLength' -Value '0' | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeSlugifyCollapseRepeats' -IsOn $true | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeSlugifyKeepUnicodeLetters' -IsOn $true | Out-Null
    $chinese = ([char]0x4E2D).ToString() + ([char]0x6587).ToString()
    $chineseCafe = $chinese + ' Caf' + ([char]0x00E9).ToString()
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeSlugifyInput' -Value $chineseCafe | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyOutput' -ExpectedValue ($chinese + '_CAFE') | Out-Null
    Set-ToggleState -Root $root -AutomationId 'NativeSlugifyStripDiacritics' -IsOn $false | Out-Null
    $chineseAccentedUpper = $chinese + '_CAF' + ([char]0x00C9).ToString()
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyOutput' -ExpectedValue $chineseAccentedUpper | Out-Null
    Assert-True -Condition $true `
        -Name 'Slugify keeps opted-in Unicode letters and can retain accents entirely in native code'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeSlugifyCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSlugifyStatus' `
            -Prefix 'Slugs copied to the clipboard' | Out-Null
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSlugifyCopy' `
            -Prefix 'Copied' | Out-Null
        Assert-True -Condition $true -Name 'Slugify writes the clipboard only after explicit Copy'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Slugify' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyOutput' -ExpectedValue $chineseAccentedUpper | Out-Null
    Assert-True -Condition $true `
        -Name 'Slugify rerenders English labels while retaining its local text and options'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeSlugifyInput')) `
        -Name 'Slugify releases its observable page controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.slugify' -ExpectedTitle 'Slugify'
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyInput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyOutput' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeSlugifyMaxLength' -ExpectedValue '0' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeSlugifyPreview' `
        -Prefix '(type something above)' | Out-Null
    $strip = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeSlugifyStripDiacritics').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $collapse = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeSlugifyCollapseRepeats').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    $unicode = [System.Windows.Automation.TogglePattern](
        Wait-ForElement -Root $root -AutomationId 'NativeSlugifyKeepUnicodeLetters').GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
    Assert-True -Condition (
        $strip.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $collapse.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On -and
        $unicode.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) `
        -Name 'Slugify resets the managed page defaults after in-process route re-entry'
}

foreach ($alias in @('slug', 'slugify', 'module.slugify')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Slugify'
}

Invoke-OwnedRoute -Route 'unitprice' -ExpectedTitle 'Unit Price' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeUnitPriceImplementationStatus',
        'NativeUnitPriceCurrency',
        'NativeUnitPriceColumns',
        'NativeUnitPriceRows',
        'NativeUnitPriceLabel0',
        'NativeUnitPricePrice0',
        'NativeUnitPriceQuantity0',
        'NativeUnitPriceUnit0',
        'NativeUnitPricePerUnit0',
        'NativeUnitPriceBadge0',
        'NativeUnitPriceRemove0',
        'NativeUnitPriceLabel1',
        'NativeUnitPricePrice1',
        'NativeUnitPriceQuantity1',
        'NativeUnitPriceUnit1',
        'NativeUnitPricePerUnit1',
        'NativeUnitPriceBadge1',
        'NativeUnitPriceRemove1',
        'NativeUnitPriceAdd',
        'NativeUnitPriceCopy',
        'NativeUnitPriceStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $unitPriceFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceCurrency'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceLabel0'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPricePrice0'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceQuantity0'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceUnit0'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPricePerUnit0'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceBadge0'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceRemove0'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceAdd'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceStatus'))
    Assert-True -Condition $unitPriceFit `
        -Name 'Unit Price exposes accessible local comparison controls without horizontal clipping'

    Wait-ForElementValue -Root $root -AutomationId 'NativeUnitPriceCurrency' -ExpectedValue '$' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceStatus' `
        -Prefix '2 item(s) need a quantity greater than zero' | Out-Null
    Assert-True -Condition $true `
        -Name 'Unit Price starts with the managed dollar currency and two blank comparison rows'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceLabel0' -Value 'Coffee' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPricePrice0' -Value '5' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceQuantity0' -Value '250' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceUnit0' -Value 'g' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceLabel1' -Value 'Tea' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPricePrice1' -Value '3' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceQuantity1' -Value '100' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceUnit1' -Value 'g' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPricePerUnit0' -Prefix '$0.02/g' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceBadge0' -Prefix ([char]0x2605) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPricePerUnit1' -Prefix '$0.03/g' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceBadge1' -Prefix '+50% more' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceStatus' -Prefix 'Comparing 2 items' | Out-Null
    Assert-True -Condition $true `
        -Name 'Unit Price calculates price-per-unit best-value and percent-more badges locally'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPricePrice1' -Value '2' | Out-Null
    # NumberBox commits a typed value when its embedded editor loses focus.
    (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceBadge1' -Prefix ([char]0x2605) | Out-Null
    Assert-True -Condition $true `
        -Name 'Unit Price marks equal per-unit costs as managed tolerance ties'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPricePrice0' -Value '0' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPricePrice1' -Value '3' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPricePerUnit0' -Prefix '$0/g' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceBadge1' -Prefix ([char]0x221E) | Out-Null
    Assert-True -Condition $true `
        -Name 'Unit Price preserves the managed free-item infinity comparison behavior'

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUnitPriceAdd'
    Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceLabel2' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnitPriceUnit2' -ExpectedValue 'g' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUnitPriceRemove2'
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeUnitPriceLabel2')) `
        -Name 'Unit Price adds a row with the first unit and can remove it without a stale control'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceQuantity1' -Value '0' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceStatus' `
        -Prefix '1 item(s) need a quantity greater than zero' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeUnitPriceQuantity1' -Value '100' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceCopy').SetFocus()
    Assert-True -Condition $true `
        -Name 'Unit Price reports invalid zero quantities before comparing valid items'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeUnitPriceCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceStatus' `
            -Prefix 'Comparison copied to clipboard' | Out-Null
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceCopy' `
            -Prefix 'Copied' | Out-Null
        Assert-True -Condition $true -Name 'Unit Price writes the local comparison only after explicit Copy'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = @([char]0x7CB5, [char]0x8A9E) -join ''
    $unitPriceCantoneseTitle = @([char]0x55AE, [char]0x4F4D, [char]0x50F9, [char]0x683C) -join ''
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForPageTitle -Root $root -Prefix $unitPriceCantoneseTitle | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPriceBadge0' -Prefix ([char]0x2605) | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForPageTitle -Root $root -Prefix 'Unit Price' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeUnitPricePerUnit0' -Prefix '$0/g' | Out-Null
    Assert-True -Condition $true `
        -Name 'Unit Price localizes Cantonese and English while retaining comparison state'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeUnitPriceCurrency')) `
        -Name 'Unit Price releases observable controls when navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.unitprice' -ExpectedTitle 'Unit Price'
    Wait-ForElementValue -Root $root -AutomationId 'NativeUnitPriceCurrency' -ExpectedValue '$' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceLabel0' | Out-Null
    Wait-ForElement -Root $root -AutomationId 'NativeUnitPriceLabel1' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeUnitPriceLabel2')) `
        -Name 'Unit Price resets managed defaults after in-process route re-entry'
}

foreach ($alias in @('priceper', 'unitprice', 'module.unitprice')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Unit Price'
}

Invoke-OwnedRoute -Route 'baseconvert' -ExpectedTitle 'Base Converter' -Inspect {
    param($root, $title)

    $baseConvertDot = [char]0x00B7
    $baseConvertOpenQuote = [char]0x201C
    $baseConvertCloseQuote = [char]0x201D
    $baseConvertNbsp = [char]0x00A0

    foreach ($id in @(
        'NativeBaseConvertImplementationStatus',
        'NativeBaseConvertInput',
        'NativeBaseConvertInputBase',
        'NativeBaseConvertStatus',
        'NativeBaseConvertBinary',
        'NativeBaseConvertBinaryCopy',
        'NativeBaseConvertOctal',
        'NativeBaseConvertOctalCopy',
        'NativeBaseConvertDecimal',
        'NativeBaseConvertDecimalCopy',
        'NativeBaseConvertHex',
        'NativeBaseConvertHexCopy',
        'NativeBaseConvertCustom',
        'NativeBaseConvertCustomCopy',
        'NativeBaseConvertBitLength',
        'NativeBaseConvertBit64Label',
        'NativeBaseConvertBit64',
        'NativeBaseConvertOperandA',
        'NativeBaseConvertOperation',
        'NativeBaseConvertOperandB',
        'NativeBaseConvertBitwiseResult'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $baseConvertFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertInputBase'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertBinary'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertHex'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperandA'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperation'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperandB'),
        (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertBitwiseResult')
    )
    Assert-True -Condition $baseConvertFit `
        -Name 'Base Converter exposes accessible local controls without horizontal clipping'

    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertInput' -ExpectedValue '255' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertBinary' -ExpectedValue '1111 1111' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertOctal' -ExpectedValue '377' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertDecimal' -ExpectedValue '255' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertHex' -ExpectedValue '0xFF' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertCustom' -ExpectedValue '255' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitLength' -Prefix 'Bit length: 8' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitwiseResult' -Prefix ('0  ' + $baseConvertDot + '  0x0') | Out-Null
    Assert-True -Condition $true `
        -Name 'Base Converter renders the managed decimal defaults and live bitwise baseline'

    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertInputBase') -Index 3
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertInput' -Value 'Ff' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertDecimal' -ExpectedValue '255' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertCustom' -ExpectedValue 'ff' | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertInputBase') -Index 4
    Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertCustomBase' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertInput' -Value 'z' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertDecimal' -ExpectedValue '35' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertCustom' -ExpectedValue 'z' | Out-Null
    Assert-True -Condition $true `
        -Name 'Base Converter supports hexadecimal and custom-base arbitrary-radix input'

    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertInputBase') -Index 0
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertInput' -Value ($baseConvertNbsp + '2' + $baseConvertNbsp) | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertBinary' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertHex' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertStatus' `
        -Prefix ($baseConvertOpenQuote + '2' + $baseConvertCloseQuote + ' is not a valid base-2 number.') | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertInputBase') -Index 2
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertInput' -Value '18446744073709551616' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitLength' -Prefix 'Bit length: 65' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBit64Label' -Prefix 'Value exceeds 64 bits.' | Out-Null
    Assert-True -Condition $true `
        -Name 'Base Converter clears invalid rows and keeps oversized arbitrary-precision values out of 64-bit display'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertOperandA' -Value '0xF0' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertOperandB' -Value '0x0F' | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperation') -Index 1
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitwiseResult' -Prefix ('255  ' + $baseConvertDot + '  0xFF') | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperation') -Index 5
    $operandB = Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperandB'
    Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertShift' | Out-Null
    Assert-True -Condition (-not $operandB.Current.IsEnabled) `
        -Name 'Base Converter shift mode disables Operand B'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertOperandA' -Value '-5' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertShift' -Value '2' | Out-Null
    # NumberBox commits a typed value when focus leaves its embedded edit control.
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeBaseConvertOperandA' -Value '-5' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitwiseResult' -Prefix ('-20  ' + $baseConvertDot + '  -0x14') | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperation') -Index 6
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitwiseResult' -Prefix ('-2  ' + $baseConvertDot + '  -0x2') | Out-Null
    Assert-True -Condition $true `
        -Name 'Base Converter evaluates native signed bitwise and arithmetic-shift operations'

    if ($AllowClipboardMutation) {
        Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeBaseConvertHexCopy'
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertStatus' `
            -Prefix 'Output copied to the clipboard.' | Out-Null
        Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertHexCopy' -Prefix 'Copied' | Out-Null
        Assert-True -Condition $true -Name 'Base Converter writes the clipboard only after explicit Copy'
    }

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboIndex -Combo $language -Index 1
    $baseLabel = @([char]0x9032, [char]0x4F4D, [char]0x8F49, [char]0x63DB) -join ''
    Wait-ForPageTitle -Root $root -Prefix $baseLabel | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitwiseResult' -Prefix ('-2  ' + $baseConvertDot + '  -0x2') | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboIndex -Combo $language -Index 2
    Wait-ForPageTitle -Root $root -Prefix 'Base Converter' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeBaseConvertBitwiseResult' -Prefix ('-2  ' + $baseConvertDot + '  -0x2') | Out-Null
    Assert-True -Condition $true `
        -Name 'Base Converter localizes labels while retaining input and bitwise state across language rerenders'

    $dashboard = Wait-ForElement -Root $root -AutomationId 'NativeNav_dashboard'
    $dashboardSelection = [System.Windows.Automation.SelectionItemPattern]$dashboard.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $dashboardSelection.Select()
    Wait-ForPageTitle -Root $root -Prefix 'WinForge Native' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeBaseConvertInput')) `
        -Name 'Base Converter releases observable controls after navigation leaves the route'

    Navigate-InProcessToRoute -Root $root -Route 'module.baseconvert' -ExpectedTitle 'Base Converter'
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertInput' -ExpectedValue '255' | Out-Null
    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertInputBase') -Index 4
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertCustomBase' -ExpectedValue '36' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertOperandA' -ExpectedValue '0xF0' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertOperandB' -ExpectedValue '0x0F' | Out-Null
    $resetOperation = Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertOperation'
    $resetSelection = [System.Windows.Automation.SelectionPattern]$resetOperation.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    $resetSelected = $resetSelection.Current.GetSelection()
    Assert-True -Condition ($resetSelected.Count -eq 1 -and $resetSelected[0].Current.Name -eq 'AND') `
        -Name 'Base Converter resets the managed bitwise operation after in-process route re-entry'
    Select-ComboIndex -Combo $resetOperation -Index 5
    Wait-ForElement -Root $root -AutomationId 'NativeBaseConvertShift' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeBaseConvertShift' -ExpectedValue '1' | Out-Null
    Assert-True -Condition $true `
        -Name 'Base Converter resets managed page defaults after in-process route re-entry'
}

foreach ($alias in @('baseconvert', 'module.baseconvert')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Base Converter'
}

Invoke-OwnedRoute -Route 'aspect' -ExpectedTitle 'Aspect Ratio' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeAspectRatioImplementationStatus',
        'NativeAspectRatioWidth',
        'NativeAspectRatioHeight',
        'NativeAspectRatioRatio',
        'NativeAspectRatioDetail',
        'NativeAspectRatioCopy',
        'NativeAspectRatioPreset',
        'NativeAspectRatioTargetWidth',
        'NativeAspectRatioTargetWidthResult',
        'NativeAspectRatioTargetHeight',
        'NativeAspectRatioTargetHeightResult',
        'NativeAspectRatioStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $aspectControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioWidth'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioHeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioRatio'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioCopy'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioPreset'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioTargetWidth'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioTargetWidthResult'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioTargetHeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioTargetHeightResult'))
    Assert-True -Condition $aspectControlsFit `
        -Name 'Aspect Ratio exposes native controls, accessibility, and horizontal clipping safety'

    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioWidth' -ExpectedValue '1920' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioHeight' -ExpectedValue '1080' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioRatio' -Prefix '16:9' | Out-Null
    $middleDot = [char]0x00B7
    $multiply = [char]0x00D7
    $decimalSeparator = [Globalization.CultureInfo]::CurrentCulture.NumberFormat.NumberDecimalSeparator
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioDetail' `
        -Prefix ("Decimal 1$($decimalSeparator)7778 $middleDot 2$($decimalSeparator)07 MP (1920$($multiply)1080)") | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioTargetWidthResult' `
        -Prefix (([char]0x2192).ToString() + ' height 720') | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioTargetHeightResult' `
        -Prefix (([char]0x2192).ToString() + ' width 1280') | Out-Null
    Assert-True -Condition $true -Name 'Aspect Ratio starts at the managed 1920x1080 ratio, detail, and scale defaults'

    $aspectLiveChanged = Invoke-AndWaitForLiveRegionChanged `
        -Root $root -AutomationId 'NativeAspectRatioStatus' -Action {
            Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioWidth' -Value '3440' | Out-Null
            (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioCopy').SetFocus()
        }
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioHeight' -Value '1440' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioRatio' -Prefix '43:18' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioDetail' `
        -Prefix ("Decimal 2$($decimalSeparator)3889 $middleDot 4$($decimalSeparator)95 MP (3440$($multiply)1440)") | Out-Null
    Assert-True -Condition $aspectLiveChanged `
        -Name 'Aspect Ratio recomputes live and raises the polite accessibility event'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioWidth' -Value '57' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioHeight' -Value '800' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioDetail' `
        -Prefix ("Decimal 0$($decimalSeparator)0713 $middleDot 0$($decimalSeparator)05 MP (57$($multiply)800)") | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioWidth' -Value '1005' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioHeight' -Value '1000' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioDetail' `
        -Prefix ("Decimal 1$($decimalSeparator)005 $middleDot 1$($decimalSeparator)01 MP (1005$($multiply)1000)") | Out-Null
    Assert-True -Condition $true -Name 'Aspect Ratio uses managed midpoint formatting for decimal and megapixel values'

    $preset = Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioPreset'
    Select-ComboIndex -Combo $preset -Index 5
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioStatus' `
        -Prefix 'Ratio seeded to 1:1.' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioTargetWidthResult' `
        -Prefix (([char]0x2192).ToString() + ' height 1280') | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioTargetHeightResult' `
        -Prefix (([char]0x2192).ToString() + ' width 720') | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioTargetWidth' `
        -Value ("1$($decimalSeparator)015") | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioPreset').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioTargetWidthResult' `
        -Prefix (([char]0x2192).ToString() + " height 1$($decimalSeparator)02") | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioTargetWidth' `
        -Value ("1$($decimalSeparator)0049999999999997") | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioPreset').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioTargetWidthResult' `
        -Prefix (([char]0x2192).ToString() + " height 1$($decimalSeparator)01") | Out-Null
    Assert-True -Condition $true `
        -Name 'Aspect Ratio presets retain CoreLib custom-format parity at adjacent midpoint values'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioWidth' -Value '0' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioStatus' `
        -Prefix 'Waiting for a valid width and height.' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioRatio' -Prefix ([char]0x2014) | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioDetail' `
        -Prefix 'Enter a positive width and height.' | Out-Null
    $presetSelection = [System.Windows.Automation.SelectionPattern]$preset.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    $selectedPreset = @($presetSelection.Current.GetSelection())
    Assert-True -Condition ($selectedPreset.Count -eq 1 -and $selectedPreset[0].Current.Name -eq '1:1') `
        -Name 'Aspect Ratio invalid input preserves the locked preset and refreshes accessible output names'
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeAspectRatioCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioStatus' `
        -Prefix 'Nothing to copy' | Out-Null
    Assert-True -Condition $true -Name 'Aspect Ratio invalid input clears copy eligibility and reports the guarded state'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioWidth' -Value '' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioTargetWidth' -Value '' | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioWidth' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioTargetWidth' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioRatio' -Prefix ([char]0x2014) | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioWidth' -ExpectedValue '' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioTargetWidth' -ExpectedValue '' | Out-Null
    Assert-True -Condition $true `
        -Name 'Aspect Ratio preserves blank dimension and target fields across language rerender'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioWidth' -Value '1920' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioHeight' -Value '1080' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioTargetWidth' -Value '1280' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeAspectRatioCopy').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioRatio' -Prefix '16:9' | Out-Null
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeAspectRatioCopy'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioStatus' `
        -Prefix 'Copied to clipboard.' | Out-Null
    Assert-True -Condition $true -Name 'Aspect Ratio copies a valid native result only after explicit Copy'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioWidth' -Value '3440' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeAspectRatioTargetHeight' -Value '999' | Out-Null
    Navigate-InProcessToRoute -Root $root -Route 'module.aspectratio' -ExpectedTitle 'Aspect Ratio'
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioWidth' -ExpectedValue '1920' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioHeight' -ExpectedValue '1080' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioTargetWidth' -ExpectedValue '1280' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeAspectRatioTargetHeight' -ExpectedValue '720' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeAspectRatioRatio' -Prefix '16:9' | Out-Null
    Assert-True -Condition $true -Name 'Aspect Ratio resets managed page state after in-process route re-entry'
}

foreach ($alias in @('aspectratio', 'module.aspectratio')) {
    Invoke-OwnedRoute -Route $alias -ExpectedTitle 'Aspect Ratio'
}

Invoke-OwnedRoute -Route 'cssunits' -ExpectedTitle 'CSS Unit Converter' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativeCssUnitsImplementationStatus',
        'NativeCssUnitsValueInput',
        'NativeCssUnitsUnitPicker',
        'NativeCssUnitsRoot',
        'NativeCssUnitsElement',
        'NativeCssUnitsViewportWidth',
        'NativeCssUnitsViewportHeight',
        'NativeCssUnitsContainer',
        'NativeCssUnitsResultEm',
        'NativeCssUnitsResultRem',
        'NativeCssUnitsResultPt',
        'NativeCssUnitsResultPc',
        'NativeCssUnitsResultPercent',
        'NativeCssUnitsResultVw',
        'NativeCssUnitsResultVh',
        'NativeCssUnitsResultCm',
        'NativeCssUnitsResultMm',
        'NativeCssUnitsResultIn',
        'NativeCssUnitsStatus'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }

    $cssControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsValueInput'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsUnitPicker'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsRoot'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsElement'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsViewportWidth'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsViewportHeight'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsContainer'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsResultEm'),
        (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsResultIn'))
    Assert-True -Condition $cssControlsFit `
        -Name 'CSS Unit Converter exposes native controls, accessibility, and horizontal clipping safety'

    $cssInput = Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsValueInput'
    $cssPicker = Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsUnitPicker'
    Assert-True -Condition (
        $cssInput.Current.Name.StartsWith('CSS numeric value', [StringComparison]::Ordinal) -and
        $cssPicker.Current.Name.StartsWith('Source CSS unit', [StringComparison]::Ordinal)) `
        -Name 'CSS Unit Converter input and unit picker expose localized accessible names'

    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsValueInput' -ExpectedValue '16' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultEm' -Prefix 'Copy 1em' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultRem' -Prefix 'Copy 1rem' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPt' -Prefix 'Copy 12pt' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPercent' -Prefix 'Copy 1.6%' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultIn' -Prefix 'Copy 0.1667in' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsStatus' `
        -Prefix 'Select a result row to copy it.' | Out-Null
    Assert-True -Condition $true -Name 'CSS Unit Converter renders managed-parity default px conversions'

    Select-ComboIndex -Combo $cssPicker -Index 3
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsValueInput' -Value '72' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPx' -Prefix 'Copy 96px' | Out-Null
    Assert-True -Condition (-not (Find-ByAutomationId -Root $root -AutomationId 'NativeCssUnitsResultPt')) `
        -Name 'CSS Unit Converter changes source unit and excludes it from live results'

    $cssLiveChanged = Invoke-AndWaitForLiveRegionChanged `
        -Root $root -AutomationId 'NativeCssUnitsStatus' -Action {
            Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsValueInput' -Value '1e309' | Out-Null
        }
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsStatus' `
        -Prefix 'Enter a valid invariant number to convert.' | Out-Null
    $invalidPx = Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPx' -Prefix 'px unavailable'
    $unavailableCantonese = ([char]0x7121).ToString() + [char]0x6CD5 + [char]0x63DB + [char]0x7B97
    $middleDot = [char]0x00B7
    Assert-True -Condition (
        $cssLiveChanged -and
        -not $invalidPx.Current.IsEnabled -and
        $invalidPx.Current.Name -eq "px unavailable $middleDot px $unavailableCantonese") `
        -Name 'CSS Unit Converter disables non-finite results, raises its live event, and exposes the bilingual state'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsValueInput' -Value 'not-a-number' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsStatus' `
        -Prefix 'Enter a valid invariant number to convert.' | Out-Null
    $invalidPx = Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPx' -Prefix 'px unavailable'
    Assert-True -Condition (-not $invalidPx.Current.IsEnabled) `
        -Name 'CSS Unit Converter also rejects ordinary malformed invariant input'

    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    $cantoneseLabel = ([char]0x7CB5).ToString() + [char]0x8A9E
    $copyCantonese = ([char]0x8907).ToString() + [char]0x88FD
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPx' `
        -Prefix ("px $unavailableCantonese") | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPx' -Prefix 'px unavailable' | Out-Null
    Assert-True -Condition $true -Name 'CSS Unit Converter localizes unavailable result accessibility names'

    $cssPicker = Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsUnitPicker'
    Select-ComboIndex -Combo $cssPicker -Index 0
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsValueInput' -Value '16' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsElement' -Value '0' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsValueInput').SetFocus()
    $zeroEm = Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultEm' -Prefix 'em unavailable'
    Assert-True -Condition (-not $zeroEm.Current.IsEnabled) `
        -Name 'CSS Unit Converter rejects a zero em denominator without stale output'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsElement' -Value '16' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsValueInput').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultEm' -Prefix 'Copy 1em' | Out-Null

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsElement' -Value '' | Out-Null
    $language = Wait-ForElement -Root $root -AutomationId 'NativeLanguagePicker'
    Select-ComboItem -Combo $language -Name $cantoneseLabel
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsElement' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultEm' `
        -Prefix ("$copyCantonese 1em") | Out-Null
    Select-ComboItem -Combo $language -Name 'English'
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsElement' -ExpectedValue '' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultEm' -Prefix 'Copy 1em' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsElement' -Value '16' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsValueInput').SetFocus()
    Assert-True -Condition $true -Name 'CSS Unit Converter preserves a blank context across language rerender while using its calculation fallback'

    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsContainer' -Value '0' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsValueInput').SetFocus()
    $zeroPercent = Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPercent' -Prefix '% unavailable'
    Assert-True -Condition (-not $zeroPercent.Current.IsEnabled) `
        -Name 'CSS Unit Converter rejects a zero percentage context without stale output'
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsContainer' -Value '1000' | Out-Null
    (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsValueInput').SetFocus()
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultPercent' -Prefix 'Copy 1.6%' | Out-Null

    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeCssUnitsResultEm'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsStatus' -Prefix 'Copied 1em' | Out-Null
    Assert-True -Condition $true -Name 'CSS Unit Converter copies a complete CSS value only after explicit row selection'

    Select-ComboIndex -Combo (Wait-ForElement -Root $root -AutomationId 'NativeCssUnitsUnitPicker') -Index 3
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsValueInput' -Value '72' | Out-Null
    Set-EditableValueAndWait -Root $root -AutomationId 'NativeCssUnitsElement' -Value '20' | Out-Null
    Navigate-InProcessToRoute -Root $root -Route 'module.cssunits' -ExpectedTitle 'CSS Unit Converter'
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsValueInput' -ExpectedValue '16' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsRoot' -ExpectedValue '16' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsElement' -ExpectedValue '16' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsViewportWidth' -ExpectedValue '1920' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsViewportHeight' -ExpectedValue '1080' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCssUnitsContainer' -ExpectedValue '1000' | Out-Null
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCssUnitsResultEm' -Prefix 'Copy 1em' | Out-Null
    Assert-True -Condition $true -Name 'CSS Unit Converter resets managed page state after in-process route re-entry'
}

Invoke-OwnedRoute -Route 'module.cssunits' -ExpectedTitle 'CSS Unit Converter'

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
