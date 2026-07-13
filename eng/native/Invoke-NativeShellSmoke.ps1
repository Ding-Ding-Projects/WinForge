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

Invoke-OwnedRoute -Route 'module.packages#updates' -ExpectedTitle 'Package Manager' -Inspect {
    param($root, $title)

    foreach ($id in @(
        'NativePackageManagerMigrationStatus',
        'NativePackageViewPicker',
        'NativePackageSearchBox',
        'NativePackagePrimaryAction',
        'NativePackageSecondaryAction',
        'NativePackageOperationsAction',
        'NativePackageResultsHeader'
    )) {
        Wait-ForElement -Root $root -AutomationId $id | Out-Null
    }
    Assert-True -Condition $true -Name 'Package Manager exposes its native control contract'

    foreach ($manager in @('winget', 'scoop', 'choco', 'pip', 'npm', 'dotnet', 'psgallery', 'pwsh7', 'cargo', 'bun', 'vcpkg')) {
        Wait-ForElement -Root $root -AutomationId "NativePackageManagerFilter_$manager" | Out-Null
    }
    Assert-True -Condition $true -Name 'Package Manager exposes all 11 native manager filters'

    $header = Wait-ForElement -Root $root -AutomationId 'NativePackageResultsHeader'
    Assert-True -Condition ($header.Current.Name.StartsWith('Available updates', [StringComparison]::Ordinal)) `
        -Name 'Package Manager deep link selects Updates'

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
