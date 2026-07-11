<#
  Fails when a source XAML page uses a direct Boolean literal for IsOn.

  The self-contained WinUI runtime has reproducibly thrown XamlParseException
  while assigning ToggleSwitch.IsOn from XAML. Assign initial Boolean defaults
  in code-behind after InitializeComponent instead, using the page's existing
  suppression/loading guard when the Toggled handler needs one.
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

$literalPattern = '\bIsOn\s*=\s*"(?:True|False)"'
$matches = Get-ChildItem -LiteralPath $sourceRoots -Recurse -File -Filter '*.xaml' |
    Select-String -Pattern $literalPattern

if ($matches) {
    Write-Host 'FAIL: direct XAML IsOn Boolean literals are unsafe in the self-contained runtime.' -ForegroundColor Red
    $matches | ForEach-Object {
        Write-Host ("  {0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim())
    }
    Write-Host 'Move the default to code-behind after InitializeComponent, then deep-link smoke-test the page.' -ForegroundColor Yellow
    exit 1
}


$expectedDefaults = [ordered]@{
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
foreach ($relativePath in $expectedDefaults.Keys) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $missingDefaults.Add("$relativePath is missing.")
        continue
    }

    $source = Get-Content -Raw -LiteralPath $path
    foreach ($assignment in $expectedDefaults[$relativePath]) {
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

Write-Host 'PASS: no direct XAML IsOn Boolean literals remain, and all 16 protected managed defaults are present.' -ForegroundColor Green
