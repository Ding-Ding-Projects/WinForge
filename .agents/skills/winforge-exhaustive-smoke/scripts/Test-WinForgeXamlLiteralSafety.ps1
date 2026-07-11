<#
  Fails when a source XAML page uses a proven-unsafe typed literal.

  The self-contained WinUI runtime has reproducibly thrown XamlParseException
  while assigning ToggleSwitch.IsOn from XAML. Assign initial Boolean defaults
  in code-behind after InitializeComponent instead, using the page's existing
  suppression/loading guard when the Toggled handler needs one.

  It also protects the one page-local CheckBox.IsChecked failure and six
  page-local NumberBox.Value failures that were
  reproduced through fresh --page launches. Other NumberBox literals remain
  allowed until a route proves them unsafe; this is deliberately not a global
  policy for numeric controls.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RepoRoot).Path
$sourceRoots = @('Pages', 'Controls') |
    ForEach-Object { Join-Path $root $_ } |
    Where-Object { Test-Path -LiteralPath $_ }

if ($sourceRoots.Count -eq 0) {
    throw "Could not find Pages or Controls below '$root'. Pass -RepoRoot with the WinForge repository root."
}

$booleanLiteralPattern = '\bIsOn\s*=\s*"(?:True|False)"'
$matches = Get-ChildItem -LiteralPath $sourceRoots -Recurse -File -Filter '*.xaml' |
    Select-String -Pattern $booleanLiteralPattern

if ($matches) {
    Write-Host 'FAIL: direct XAML IsOn Boolean literals are unsafe in the self-contained runtime.' -ForegroundColor Red
    $matches | ForEach-Object {
        Write-Host ("  {0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim())
    }
    Write-Host 'Move the default to code-behind after InitializeComponent, then deep-link smoke-test the page.' -ForegroundColor Yellow
    exit 1
}


$expectedBooleanDefaults = [ordered]@{
    'Pages/ApiClientModule.xaml.cs' = @('PrettyToggle.IsOn = true;')
    'Pages/ConnectionsModule.xaml.cs' = @('AutoSwitch.IsOn = false;')
    'Pages/GitHubDesktopProfilesModule.xaml.cs' = @('StartMenuShortcutsToggle.IsOn = true;', 'DesktopShortcutsToggle.IsOn = true;')
    'Pages/HexDumpModule.xaml.cs' = @('OffsetSwitch.IsOn = true;')
    'Pages/HomeAssistantModule.xaml.cs' = @('AcDryRunToggle.IsOn = true;')
    'Pages/HtmlTableModule.xaml.cs' = @('HeaderSwitch.IsOn = true;', 'EscapeSwitch.IsOn = true;')
    'Pages/HttpHeadersModule.xaml.cs' = @('RedirectSwitch.IsOn = true;')
    'Pages/LoremTextModule.xaml.cs' = @('ClassicSwitch.IsOn = true;')
    'Pages/MdTableModule.xaml.cs' = @('HeaderSwitch.IsOn = true;', 'PadSwitch.IsOn = true;')
    'Pages/MinecraftServerModule.xaml.cs' = @('OnlineToggle.IsOn = true;')
    'Pages/NativeUtilitiesModule.xaml.cs' = @('CountersSwitch.IsOn = true;')
    'Pages/ProxmoxModule.xaml.cs' = @('AutoRefreshToggle.IsOn = true;')
}

$missingDefaults = [System.Collections.Generic.List[string]]::new()
foreach ($relativePath in $expectedBooleanDefaults.Keys) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $missingDefaults.Add("$relativePath is missing.")
        continue
    }

    $source = Get-Content -Raw -LiteralPath $path
    foreach ($assignment in $expectedBooleanDefaults[$relativePath]) {
        if (-not $source.Contains($assignment)) {
            $missingDefaults.Add("$relativePath is missing '$assignment'.")
        }
    }
}

if ($missingDefaults.Count -gt 0) {
    Write-Host 'FAIL: a protected managed ToggleSwitch default is missing.' -ForegroundColor Red
    $missingDefaults | ForEach-Object { Write-Host "  $_" }
    exit 1
}

$protectedCheckBoxDefaults = [ordered]@{
    'Pages/MarkdownTocModule.xaml' = [pscustomobject]@{
        CodeBehind = 'Pages/MarkdownTocModule.xaml.cs'
        Control = 'IncludeH1Chk'
        Assignment = 'IncludeH1Chk.IsChecked = true;'
    }
}

$checkBoxFailures = [System.Collections.Generic.List[string]]::new()
foreach ($relativeXamlPath in $protectedCheckBoxDefaults.Keys) {
    $entry = $protectedCheckBoxDefaults[$relativeXamlPath]
    $xamlPath = Join-Path $root $relativeXamlPath
    $codeBehindPath = Join-Path $root $entry.CodeBehind
    if (-not (Test-Path -LiteralPath $xamlPath)) {
        $checkBoxFailures.Add("$relativeXamlPath is missing.")
        continue
    }
    if (-not (Test-Path -LiteralPath $codeBehindPath)) {
        $checkBoxFailures.Add("$($entry.CodeBehind) is missing.")
        continue
    }

    $xaml = Get-Content -Raw -LiteralPath $xamlPath
    $checkBoxTags = [regex]::Matches($xaml, '(?s)<CheckBox\b[^>]*>')
    $namePattern = '\bx:Name\s*=\s*"' + [regex]::Escape($entry.Control) + '"'
    $tags = @($checkBoxTags | Where-Object { $_.Value -match $namePattern })
    if ($tags.Count -ne 1) {
        $checkBoxFailures.Add("$relativeXamlPath must retain exactly one CheckBox named '$($entry.Control)'.")
    }
    elseif ($tags[0].Value -match '\bIsChecked\s*=\s*"(?!\{)[^"]+"') {
        $checkBoxFailures.Add("$relativeXamlPath still assigns direct XAML CheckBox.IsChecked for '$($entry.Control)'.")
    }

    $codeBehind = Get-Content -Raw -LiteralPath $codeBehindPath
    if (-not $codeBehind.Contains($entry.Assignment)) {
        $checkBoxFailures.Add("$($entry.CodeBehind) is missing '$($entry.Assignment)'.")
    }
}

if ($checkBoxFailures.Count -gt 0) {
    Write-Host 'FAIL: a reproduced CheckBox.IsChecked XAML regression is not safely initialized.' -ForegroundColor Red
    $checkBoxFailures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

$protectedNumberBoxDefaults = [ordered]@{
    'Pages/MarkdownTocModule.xaml' = [pscustomobject]@{
        CodeBehind = 'Pages/MarkdownTocModule.xaml.cs'
        Controls = @('MinBox', 'MaxBox')
        Assignments = @('MinBox.Value = 1;', 'MaxBox.Value = 6;')
    }
    'Pages/NameGenModule.xaml' = [pscustomobject]@{
        CodeBehind = 'Pages/NameGenModule.xaml.cs'
        Controls = @('CountBox')
        Assignments = @('CountBox.Value = 10;')
    }
    'Pages/NumberFormatModule.xaml' = [pscustomobject]@{
        CodeBehind = 'Pages/NumberFormatModule.xaml.cs'
        Controls = @('DecimalsBox', 'PadBox')
        Assignments = @('DecimalsBox.Value = 2;', 'PadBox.Value = 8;')
    }
    'Pages/SciNotationModule.xaml' = [pscustomobject]@{
        CodeBehind = 'Pages/SciNotationModule.xaml.cs'
        Controls = @('SigBox')
        Assignments = @('_suppress = true;', 'SigBox.Value = 6;', '_suppress = false;', 'if (!_suppress) Recompute();')
    }
    'Pages/SubnetCalcModule.xaml' = [pscustomobject]@{
        CodeBehind = 'Pages/SubnetCalcModule.xaml.cs'
        Controls = @('CidrBox', 'NewPrefixBox', 'CountBox')
        Assignments = @('CidrBox.Value = 24;', 'NewPrefixBox.Value = 26;', 'CountBox.Value = 0;')
    }
    'Pages/UnitConvertModule.xaml' = [pscustomobject]@{
        CodeBehind = 'Pages/UnitConvertModule.xaml.cs'
        Controls = @('ValueBox')
        Assignments = @('ValueBox.Value = 1;')
    }
}

$numberBoxFailures = [System.Collections.Generic.List[string]]::new()
foreach ($relativeXamlPath in $protectedNumberBoxDefaults.Keys) {
    $entry = $protectedNumberBoxDefaults[$relativeXamlPath]
    $xamlPath = Join-Path $root $relativeXamlPath
    $codeBehindPath = Join-Path $root $entry.CodeBehind
    if (-not (Test-Path -LiteralPath $xamlPath)) {
        $numberBoxFailures.Add("$relativeXamlPath is missing.")
        continue
    }
    if (-not (Test-Path -LiteralPath $codeBehindPath)) {
        $numberBoxFailures.Add("$($entry.CodeBehind) is missing.")
        continue
    }

    $xaml = Get-Content -Raw -LiteralPath $xamlPath
    $numberBoxTags = [regex]::Matches($xaml, '(?s)<NumberBox\b[^>]*>')
    foreach ($control in $entry.Controls) {
        $namePattern = '\bx:Name\s*=\s*"' + [regex]::Escape($control) + '"'
        $tags = @($numberBoxTags | Where-Object { $_.Value -match $namePattern })
        if ($tags.Count -ne 1) {
            $numberBoxFailures.Add("$relativeXamlPath must retain exactly one NumberBox named '$control'.")
            continue
        }
        if ($tags[0].Value -match '\bValue\s*=\s*"(?!\{)[^"]+"') {
            $numberBoxFailures.Add("$relativeXamlPath still assigns direct XAML NumberBox.Value for '$control'.")
        }
    }

    $codeBehind = Get-Content -Raw -LiteralPath $codeBehindPath
    foreach ($assignment in $entry.Assignments) {
        if (-not $codeBehind.Contains($assignment)) {
            $numberBoxFailures.Add("$($entry.CodeBehind) is missing '$assignment'.")
        }
    }
}

if ($numberBoxFailures.Count -gt 0) {
    Write-Host 'FAIL: a reproduced NumberBox.Value XAML regression is not safely initialized.' -ForegroundColor Red
    $numberBoxFailures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host 'PASS: no direct XAML IsOn Boolean literals remain, 16 protected ToggleSwitch defaults, one protected CheckBox default, and 10 reproduced NumberBox defaults use managed initialization.' -ForegroundColor Green
