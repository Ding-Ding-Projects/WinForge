[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,

    [Parameter()]
    [switch]$Detailed
)

$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$pages = Join-Path $repo 'Pages'
$services = Join-Path $repo 'Services'
$controls = Join-Path $repo 'Controls'
$mainWindow = Join-Path $repo 'MainWindow.xaml'

foreach ($path in @($pages, $services, $controls, $mainWindow)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Expected WinForge source path was not found: $path"
    }
}

# Keep the list explicit: matching an arbitrary XAML attribute would turn properties such as
# IsItemClickEnabled and QueryIcon into false handler declarations.
$eventNames = @(
    'Click', 'Loaded', 'Unloaded', 'SelectionChanged', 'ItemInvoked',
    'TabCloseRequested', 'TabDroppedOutside', 'TextChanged', 'Toggled',
    'ValueChanged', 'QuerySubmitted', 'Invoked', 'PointerPressed',
    'PointerReleased', 'PointerMoved', 'DragStarting', 'Drop', 'KeyDown',
    'KeyUp', 'LostFocus', 'GotFocus', 'Tapped', 'DoubleTapped', 'RightTapped',
    'Holding', 'ManipulationStarting', 'ManipulationStarted',
    'ManipulationDelta', 'ManipulationCompleted', 'Checked', 'Unchecked',
    'Opened', 'Closed', 'SizeChanged', 'LayoutUpdated', 'Navigated',
    'NavigationFailed', 'BackRequested', 'TimeChanged', 'DateChanged',
    'PasswordChanged', 'SuggestionChosen', 'ItemClick', 'CalendarViewDayItemChanging'
)
$eventAttributePattern = '(?<![A-Za-z0-9_])(?:' +
    (($eventNames | ForEach-Object { [regex]::Escape($_) }) -join '|') +
    ')\s*=\s*"(?<handler>[A-Za-z_][A-Za-z0-9_]*)"'
$actionControlPattern = '<(?<type>Button|AppBarButton|ToggleButton|HyperlinkButton|SplitButton|DropDownButton|ToggleSplitButton|MenuFlyoutItem)\b(?<attrs>[^>]*)>'
$directActionPattern = '(?<![A-Za-z0-9_])(?:Click|Tapped)\s*=\s*"(?<handler>[A-Za-z_][A-Za-z0-9_]*)"'
$commandPattern = '(?<![A-Za-z0-9_])Command\s*='

function Get-PartialSources {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$Xaml)

    $base = [System.IO.Path]::GetFileNameWithoutExtension($Xaml.Name)
    if ($base -eq 'MainWindow') {
        return @(Get-ChildItem -LiteralPath $repo -Filter 'MainWindow*.cs' -File)
    }
    return @(Get-ChildItem -LiteralPath $pages -Filter "$base*.cs" -File)
}

$xamls = @(
    Get-ChildItem -LiteralPath $pages -Filter '*.xaml' -File
) + @(
    Get-Item -LiteralPath $mainWindow
)

$handlerDeclarations = [System.Collections.Generic.List[object]]::new()
$unresolvedHandlers = [System.Collections.Generic.List[object]]::new()
$actionControls = [System.Collections.Generic.List[object]]::new()
$unresolvedActionControls = [System.Collections.Generic.List[object]]::new()
foreach ($xaml in $xamls) {
    $sourceFiles = Get-PartialSources -Xaml $xaml
    $sourceText = (($sourceFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n")
    $xamlText = Get-Content -LiteralPath $xaml.FullName -Raw

    foreach ($match in [regex]::Matches($xamlText, $eventAttributePattern)) {
        $handler = $match.Groups['handler'].Value
        $row = [pscustomobject]@{
            Xaml = $xaml.Name
            Handler = $handler
            SourceFiles = ($sourceFiles.Name -join ';')
        }
        $handlerDeclarations.Add($row)
        if ($sourceText -notmatch ('(?m)\b' + [regex]::Escape($handler) + '\s*\(')) {
            $unresolvedHandlers.Add($row)
        }
    }

    foreach ($control in [regex]::Matches($xamlText, $actionControlPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $attrs = $control.Groups['attrs'].Value
        $direct = [regex]::Match($attrs, $directActionPattern)
        $handler = if ($direct.Success) { $direct.Groups['handler'].Value } else { '' }
        $kind = if ($direct.Success) { 'direct-handler' } elseif ([regex]::IsMatch($attrs, $commandPattern)) { 'command' } else { 'no-declared-action' }
        $row = [pscustomobject]@{
            Xaml = $xaml.Name
            Type = $control.Groups['type'].Value
            Kind = $kind
            Handler = $handler
            SourceFiles = ($sourceFiles.Name -join ';')
        }
        $actionControls.Add($row)
        if ($direct.Success -and $sourceText -notmatch ('(?m)\b' + [regex]::Escape($handler) + '\s*\(')) {
            $unresolvedActionControls.Add($row)
        }
    }
}

$languageMismatches = [System.Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem -LiteralPath $pages -Filter '*.cs' -File) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $adds = [regex]::Matches($text, 'Loc\.I\.LanguageChanged\s*\+=').Count
    $removes = [regex]::Matches($text, 'Loc\.I\.LanguageChanged\s*-=').Count
    if ($adds -ne $removes) {
        $languageMismatches.Add([pscustomobject]@{
            File = $file.Name
            Adds = $adds
            Removes = $removes
        })
    }
}

# Ignore words inside normal strings; only surface actionable source markers and actual throws.
$implementationFiles = @(Get-ChildItem -LiteralPath $pages, $services, $controls -Recurse -Include '*.cs', '*.xaml' -File)
$markers = @($implementationFiles | Select-String -Pattern '(^\s*//.*\b(?:TODO|FIXME)\b)|\bthrow\s+new\s+NotImplementedException\b')
$featureDocs = @(Get-ChildItem -LiteralPath (Join-Path $repo 'docs\wiki\features') -Recurse -Filter '*.md' -File | Where-Object { $_.Name -ne 'README.md' })
$buttonDocs = @(Get-ChildItem -LiteralPath (Join-Path $repo 'docs\wiki\buttons') -Recurse -Filter '*.md' -File | Where-Object { $_.Name -ne 'README.md' })

$result = [pscustomobject]@{
    RepoRoot = $repo
    XamlFiles = $xamls.Count
    DeclaredEventHandlers = $handlerDeclarations.Count
    ResolvedEventHandlers = $handlerDeclarations.Count - $unresolvedHandlers.Count
    UnresolvedEventHandlers = $unresolvedHandlers.Count
    ActionControls = $actionControls.Count
    DirectHandlerActionControls = @($actionControls | Where-Object Kind -eq 'direct-handler').Count
    ResolvedDirectHandlerActionControls = @($actionControls | Where-Object { $_.Kind -eq 'direct-handler' }).Count - $unresolvedActionControls.Count
    CommandBoundActionControls = @($actionControls | Where-Object Kind -eq 'command').Count
    NoDeclaredActionControls = @($actionControls | Where-Object Kind -eq 'no-declared-action').Count
    UnresolvedDirectHandlerActionControls = $unresolvedActionControls.Count
    GeneratedFeatureDocs = $featureDocs.Count
    GeneratedButtonDocs = $buttonDocs.Count
    PageLanguageSubscriptionMismatches = $languageMismatches.Count
    ActionableImplementationMarkers = $markers.Count
}

$result
if ($Detailed) {
    if ($unresolvedHandlers.Count) {
        Write-Output "`nUnresolved XAML handlers:"
        $unresolvedHandlers | Sort-Object Xaml, Handler | Format-Table -AutoSize
    }
    if ($unresolvedActionControls.Count) {
        Write-Output "`nUnresolved direct action-control handlers:"
        $unresolvedActionControls | Sort-Object Xaml, Handler | Format-Table -AutoSize
    }
    if ($languageMismatches.Count) {
        Write-Output "`nPage language subscription count mismatches:"
        $languageMismatches | Sort-Object File | Format-Table -AutoSize
    }
    if ($markers.Count) {
        Write-Output "`nActionable implementation markers:"
        $markers | ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($repo, $_.Path)
            "$($relative):$($_.LineNumber):$($_.Line.Trim())"
        }
    }
}

if ($unresolvedHandlers.Count -gt 0 -or $unresolvedActionControls.Count -gt 0 -or $languageMismatches.Count -gt 0) {
    exit 1
}
