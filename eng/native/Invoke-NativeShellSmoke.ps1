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
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element) {
            $value = [System.Windows.Automation.ValuePattern]$element.GetCurrentPattern(
                [System.Windows.Automation.ValuePattern]::Pattern)
            if ($value.Current.Value -eq $ExpectedValue) {
                return $element
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($element) {
        ([System.Windows.Automation.ValuePattern]$element.GetCurrentPattern(
            [System.Windows.Automation.ValuePattern]::Pattern)).Current.Value
    }
    else { '(missing)' }
    throw "Expected '$AutomationId' value '$ExpectedValue', got '$actual'."
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
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element) {
            $value = [System.Windows.Automation.ValuePattern]$element.GetCurrentPattern(
                [System.Windows.Automation.ValuePattern]::Pattern)
            if (& $Predicate $value.Current.Value) {
                return $element
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $actual = if ($element) {
        ([System.Windows.Automation.ValuePattern]$element.GetCurrentPattern(
            [System.Windows.Automation.ValuePattern]::Pattern)).Current.Value
    }
    else { '(missing)' }
    throw "Expected '$AutomationId' value matching '$Description', got '$actual'."
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
    $items = $Combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($Index -lt 0 -or $Index -ge $items.Count) {
        throw "Combo index $Index was not found (item count: $($items.Count))."
    }
    $selection = [System.Windows.Automation.SelectionItemPattern]$items[$Index].GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selection.Select()
}

function Invoke-OwnedRoute {
    param(
        [Parameter(Mandatory)][string]$Route,
        [Parameter(Mandatory)][string]$ExpectedTitle,
        [scriptblock]$Inspect
    )

    $process = Start-Process -FilePath $exe -ArgumentList '--page', $Route -PassThru
    try {
        $root = Wait-ForWindow -ProcessId $process.Id
        $title = Wait-ForPageTitle -Root $root -Prefix $ExpectedTitle
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

Invoke-OwnedRoute -Route 'shell.allapps' -ExpectedTitle 'All Apps' -Inspect {
    param($root, $title)
    $filter = Wait-ForElement -Root $root -AutomationId 'NativeAllAppsSearchBox'
    $value = [System.Windows.Automation.ValuePattern]$filter.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $value.SetValue('reactor')
    Wait-ForElement -Root $root -AutomationId 'NativeAllApps_module_reactor' | Out-Null
    Assert-True -Condition $true -Name 'All Apps filter updates its live native list'

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
        # intentionally exposes it through its vertical ScrollViewer.  Ask UIA
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
        # Vertical overflow is intentionally scrollable.  Horizontal overflow
        # is the clipping defect this smoke check is designed to catch.
        if ($rect.Left -lt ($window.Left - 2) -or $rect.Right -gt ($window.Right + 2)) {
            return $false
        }
    }

    return $true
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
    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeCaseConvertInput' -Value 'helloWorld42API'
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)

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

    $input = Set-ElementValueAndWait -Root $root -AutomationId 'NativeCaseConvertInput' -Value ''
    $inputValue = [System.Windows.Automation.ValuePattern]$input.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern)
    Invoke-ElementByAutomationId -Root $root -AutomationId 'NativeCaseConvertCopyCamel'
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativeCaseConvertStatus' -Prefix 'Nothing to copy' | Out-Null
    Wait-ForElementValue -Root $root -AutomationId 'NativeCaseConvertOutputCamel' -ExpectedValue '' | Out-Null
    Assert-True -Condition $true -Name 'Case Converter clears stale values and copies empty rows explicitly'

    Set-ElementValueAndWait -Root $root -AutomationId 'NativeCaseConvertInput' -Value 'helloWorld42API' | Out-Null
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

    foreach ($manager in @('winget', 'scoop', 'choco', 'pip', 'npm', 'dotnet', 'psgallery', 'pwsh7', 'cargo', 'bun', 'vcpkg')) {
        Wait-ForElement -Root $root -AutomationId "NativePackageManagerFilter_$manager" | Out-Null
    }
    Assert-True -Condition $true -Name 'Package Manager exposes all 11 native manager filters'

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

    $operations = Wait-ForElement -Root $root -AutomationId 'NativePackageOperationsAction'
    $invoke = [System.Windows.Automation.InvokePattern]$operations.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
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
    Assert-True -Condition ($queueSummary.Current.Name.StartsWith('Preview queue policy', [StringComparison]::Ordinal)) `
        -Name 'Package Manager exposes durable preview queue policy'
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
        'NativePackageSearchIgnoreSpecial'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    $discoverFilterControlsFit = Test-HorizontalBoundsWithinWindow -Root $root -Elements @(
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchModePicker'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchCaseSensitive'),
        (Wait-ForElement -Root $root -AutomationId 'NativePackageSearchIgnoreSpecial')
    )
    Assert-True -Condition $discoverFilterControlsFit `
        -Name 'Package Manager Discover filter controls are accessible and horizontally unclipped'

    $mode = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchModePicker'
    Select-ComboIndex -Combo $mode -Index 3
    $case = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchCaseSensitive'
    $caseToggle = [System.Windows.Automation.TogglePattern]$case.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    if ($caseToggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { $caseToggle.Toggle() }
    $ignoreSpecial = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchIgnoreSpecial'
    $ignoreSpecialToggle = [System.Windows.Automation.TogglePattern]$ignoreSpecial.GetCurrentPattern(
        [System.Windows.Automation.TogglePattern]::Pattern)
    if ($ignoreSpecialToggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { $ignoreSpecialToggle.Toggle() }
    Wait-ForElementNamePrefix -Root $root -AutomationId 'NativePackageLiveStatus' `
        -Prefix 'Package Manager status: Discover filters applied locally' | Out-Null
    Assert-True -Condition $true `
        -Name 'Package Manager re-filters cached Discover results without starting another package query'

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
    Assert-True -Condition ($queueSummary.Current.Name.StartsWith('Preview queue policy', [StringComparison]::Ordinal)) `
        -Name 'Package Manager package-operations exposes preview queue policy'
}

Invoke-OwnedRoute -Route 'packages-operations' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Operation queue and history', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-operations alias selects Operations'
    $queueSummary = Wait-ForElement -Root $root -AutomationId 'NativePackageQueueSummary'
    Assert-True -Condition ($queueSummary.Current.Name.StartsWith('Preview queue policy', [StringComparison]::Ordinal)) `
        -Name 'Package Manager packages-operations exposes preview queue policy'
}

foreach ($case in @(
    @{ Route = 'about'; Title = 'About WinForge Native' },
    @{ Route = 'settings'; Title = 'Settings' },
    @{ Route = 'search:reactor'; Title = 'Search results' },
    @{ Route = 'manual:reactor-safety'; Title = 'Manual' },
    @{ Route = 'weblogin?url=https://example.test'; Title = 'In-App Login' },
    @{ Route = 'apps'; Title = 'App Uninstaller' },
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
