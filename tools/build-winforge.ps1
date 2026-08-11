[CmdletBinding()]
param(
  [switch]$Installer,
  [switch]$Silent,
  [switch]$ReusePublish,
  [string]$Version,
  [string]$ReleaseDir
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$workRoot = Join-Path $repo 'artifacts\winforge'
$publishDir = Join-Path $workRoot 'publish'
$packageDir = Join-Path $workRoot 'package'
$squirrelReleaseDir = Join-Path $workRoot 'releases'
$releaseOutput = if ([string]::IsNullOrWhiteSpace($ReleaseDir)) { Join-Path $repo 'release-artifacts' } else { [IO.Path]::GetFullPath($ReleaseDir) }

function Say([string]$message) { Write-Host "[WinForge] $message" }
function Fail([string]$message) { throw "WinForge build failed: $message" }

function Resolve-Dotnet {
  $system = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
  if (Test-Path -LiteralPath $system -PathType Leaf) { return $system }
  $userToolchain = Join-Path $env:LOCALAPPDATA 'WinForge\toolchain\dotnet\dotnet.exe'
  if (Test-Path -LiteralPath $userToolchain -PathType Leaf) { return $userToolchain }
  $found = Get-Command dotnet -ErrorAction SilentlyContinue
  if ($found) { return $found.Source }
  return $null
}

function Install-Dotnet([string]$channel) {
  Say "Installing .NET SDK $channel from the canonical Microsoft source"
  $winget = Get-Command winget -ErrorAction SilentlyContinue
  if ($winget) {
    $id = if ($channel -eq '11') { 'Microsoft.DotNet.SDK.Preview' } else { 'Microsoft.DotNet.SDK.8' }
    & $winget.Source install --id $id -e --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -eq 0) { return }
  }

  $installRoot = Join-Path $env:LOCALAPPDATA 'WinForge\toolchain\dotnet'
  New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
  $scriptPath = Join-Path $env:TEMP "dotnet-install-winforge-$channel.ps1"
  Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $scriptPath
  try {
    $channelArg = if ($channel -eq '11') { '11.0' } else { '8.0' }
    $quality = if ($channel -eq '11') { 'preview' } else { '' }
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath, '-Channel', $channelArg, '-InstallDir', $installRoot, '-NoPath')
    if ($quality) { $args += @('-Quality', $quality) }
    & powershell @args
    if ($LASTEXITCODE -ne 0) { Fail "dotnet-install.ps1 could not install SDK $channel" }
  }
  finally { if (Test-Path -LiteralPath $scriptPath) { Remove-Item -LiteralPath $scriptPath -Force } }
  $candidate = Join-Path $installRoot 'dotnet.exe'
  if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { Fail "SDK $channel is unavailable after both winget and dotnet-install.ps1" }
}

function Ensure-Dotnet {
  $dotnet = Resolve-Dotnet
  if (-not $dotnet) { Install-Dotnet '11'; $dotnet = Resolve-Dotnet }
  if (-not $dotnet) { Fail 'dotnet host was not found' }

  $env:PATH = "$(Split-Path -Parent $dotnet);$env:PATH"
  $sdks = @(& $dotnet --list-sdks 2>$null)
  if (-not ($sdks -match '^11\.')) { Install-Dotnet '11' }
  if (-not ($sdks -match '^8\.')) { Install-Dotnet '8' }
  $dotnet = Resolve-Dotnet
  if (-not $dotnet) { Fail 'dotnet host disappeared after SDK bootstrap' }
  $env:PATH = "$(Split-Path -Parent $dotnet);$env:PATH"
  return $dotnet
}

function Resolve-Version {
  if ([string]::IsNullOrWhiteSpace($Version)) {
    $count = [int]((& git -C $repo rev-list --count HEAD).Trim())
    if ($LASTEXITCODE -ne 0 -or $count -lt 1) { $count = 1 }
    $Version = "1.1.$count"
  }
  if ($Version.StartsWith('v', [StringComparison]::OrdinalIgnoreCase)) { $Version = $Version.Substring(1) }
  if ($Version -notmatch '^1\.1\.[0-9]+$') { Fail "version must match 1.1.<integer>: $Version" }
  $build = [int]$Version.Split('.')[2]
  if ($build -lt 1 -or $build -gt 65535) { Fail "version build component must be 1..65535: $build" }
  return $Version
}

function Ensure-SquirrelTools([string]$dotnet) {
  Say 'Restoring the official Squirrel.Windows packaging tool'
  & $dotnet restore (Join-Path $repo 'tools\SquirrelPackaging\SquirrelPackaging.csproj') --nologo 2>&1 |
    ForEach-Object { Write-Host $_ }
  $restoreCode = $LASTEXITCODE
  if ($restoreCode -ne 0) { Fail 'Squirrel.Windows tool restore failed' }
  $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
  $squirrel = Join-Path $nugetRoot 'squirrel.windows\2.0.1\tools\Squirrel.exe'
  if (-not (Test-Path -LiteralPath $squirrel -PathType Leaf)) { Fail "Squirrel.exe was not restored at $squirrel" }
  return $squirrel
}

function Publish-App([string]$dotnet, [string]$version) {
  if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $publishDir,$packageDir,$squirrelReleaseDir | Out-Null
  Say 'Publishing self-contained x64 WinForge'
  & $dotnet publish (Join-Path $repo 'WinForge.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:Version=$version -p:FileVersion="$version.0" -p:InformationalVersion=$version -o $publishDir
  if ($LASTEXITCODE -ne 0) { Fail 'managed application publish failed' }

  Say 'Publishing the launcher into the release footprint'
  & $dotnet publish (Join-Path $repo 'launcher\WinForgeLauncher.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$version -p:FileVersion="$version.0" -p:InformationalVersion=$version -o $publishDir
  if ($LASTEXITCODE -ne 0) { Fail 'launcher publish failed' }

  $updaterDir = Join-Path $publishDir 'updater-runtime'
  New-Item -ItemType Directory -Force -Path $updaterDir | Out-Null
  Say 'Publishing the updater into the release footprint'
  & $dotnet publish (Join-Path $repo 'updater\WinForgeUpdater\WinForgeUpdater.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None -p:Version=$version -p:FileVersion="$version.0" -p:InformationalVersion=$version -o $updaterDir
  if ($LASTEXITCODE -ne 0) { Fail 'updater publish failed' }
}

function Validate-Footprint([string]$version) {
  foreach ($relative in @('WinForge.exe','WinForgeLauncher.exe','updater-runtime\WinForgeUpdater.exe')) {
    $path = Join-Path $publishDir $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -le 0) { Fail "required runtime file is missing: $relative" }
    $info = (Get-Item -LiteralPath $path).VersionInfo
    if (-not $info.ProductVersion.Trim().StartsWith($version, [StringComparison]::OrdinalIgnoreCase) -or
        $info.FileVersion.Trim() -ne "$version.0") { Fail "$relative version mismatch: product=$($info.ProductVersion); file=$($info.FileVersion)" }
  }
  $sha = ([string]((& git -C $repo rev-parse HEAD).Trim())).ToLowerInvariant()
  $manifest = [ordered]@{
    schemaVersion = 2
    repository = 'Ding-Ding-Projects/WinForge'
    sourceSha = $sha
    version = $version
    tag = "v$version"
    installerAsset = 'Setup.exe'
    squirrelReleasesAsset = 'RELEASES'
    squirrelFullPackageAsset = "WinForge-$version-full.nupkg"
    portableAsset = "WinForge-portable-x64-$version.zip"
    executable = 'WinForge.exe'
    launcher = 'WinForgeLauncher.exe'
    updater = 'updater-runtime/WinForgeUpdater.exe'
    installer = 'Squirrel.Windows 2.0.1; unsigned; no signing inputs or signer invocation'
  }
  $manifestPath = Join-Path $publishDir 'WinForge.release.json'
  [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
  return $manifest
}

function Package-Portable([string]$version) {
  New-Item -ItemType Directory -Force -Path $releaseOutput | Out-Null
  $portable = Join-Path $releaseOutput "WinForge-portable-x64-$version.zip"
  if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Force }
  Say "Creating portable archive $portable"
  Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $portable -Force
  if (-not (Test-Path -LiteralPath $portable -PathType Leaf) -or (Get-Item -LiteralPath $portable).Length -le 0) { Fail 'portable archive was not produced' }
  return $portable
}

function Package-Squirrel([string]$dotnet, [string]$squirrel, [string]$version) {
  $package = Join-Path $packageDir "WinForge.$version.nupkg"
  Say "Creating NuGet package WinForge.$version.nupkg for Squirrel.Windows"
  & $dotnet run --project (Join-Path $repo 'tools\SquirrelPackaging\SquirrelPackaging.csproj') --no-restore -- pack --source $publishDir --output $package --version $version
  if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $package -PathType Leaf)) { Fail 'Squirrel NuGet package was not produced' }
  Say 'Releasifying with Squirrel.Windows (Setup.exe, RELEASES, full package; no MSI or signing)'
  $squirrelProcess = Start-Process -FilePath $squirrel -ArgumentList @(
    '--releasify', $package, '--releaseDir', $squirrelReleaseDir, '--no-msi'
  ) -WindowStyle Hidden -PassThru -Wait
  if ($squirrelProcess.ExitCode -ne 0) { Fail "Squirrel releasify failed with exit code $($squirrelProcess.ExitCode)" }
  $setup = Join-Path $squirrelReleaseDir 'Setup.exe'
  $releases = Join-Path $squirrelReleaseDir 'RELEASES'
  $full = Join-Path $squirrelReleaseDir "WinForge-$version-full.nupkg"
  $deadline = [DateTime]::UtcNow.AddMinutes(3)
  $stable = $false
  while ([DateTime]::UtcNow -lt $deadline) {
    $ready = @($setup, $releases, $full) | ForEach-Object {
      (Test-Path -LiteralPath $_ -PathType Leaf) -and (Get-Item -LiteralPath $_).Length -gt 0
    }
    if ($ready -notcontains $false) {
      $firstSizes = @($setup, $releases, $full) | ForEach-Object { (Get-Item -LiteralPath $_).Length }
      Start-Sleep -Seconds 2
      $secondSizes = @($setup, $releases, $full) | ForEach-Object { (Get-Item -LiteralPath $_).Length }
      if (($firstSizes -join ',') -eq ($secondSizes -join ',')) { $stable = $true; break }
    }
    Start-Sleep -Seconds 1
  }
  if (-not $stable) { Fail 'Squirrel output did not become complete and stable within three minutes' }
  $signature = Get-AuthenticodeSignature -LiteralPath $setup
  if ($signature.Status -ne 'NotSigned') { Fail "Squirrel Setup.exe is not unsigned: $($signature.Status)" }
  New-Item -ItemType Directory -Force -Path $releaseOutput | Out-Null
  foreach ($name in @('Setup.exe','RELEASES',"WinForge-$version-full.nupkg")) { Copy-Item -LiteralPath (Join-Path $squirrelReleaseDir $name) -Destination (Join-Path $releaseOutput $name) -Force }
  Get-ChildItem -LiteralPath $squirrelReleaseDir -File -Filter '*-delta.nupkg' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $releaseOutput $_.Name) -Force }
  return $setup
}

$dotnet = Ensure-Dotnet
$Version = Resolve-Version
$squirrel = if ($Installer) { Ensure-SquirrelTools $dotnet } else { $null }
if (-not $ReusePublish) { Publish-App $dotnet $Version }
elseif (-not (Test-Path -LiteralPath (Join-Path $publishDir 'WinForge.exe') -PathType Leaf)) { Fail "-ReusePublish requested but $publishDir is not a valid publish output" }
$null = Validate-Footprint $Version
$portable = Package-Portable $Version
if ($Installer) { $null = Package-Squirrel $dotnet $squirrel $Version }

Say "Source commit: $((& git -C $repo rev-parse HEAD).Trim())"
Say "Publish directory: $publishDir"
Say "Portable SHA-256: $((Get-FileHash -LiteralPath $portable -Algorithm SHA256).Hash.ToLowerInvariant())"
if ($Installer) {
  foreach ($name in @('Setup.exe','RELEASES',"WinForge-$Version-full.nupkg")) {
    $path = Join-Path $releaseOutput $name
    Say "$name SHA-256: $((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant())"
  }
  Say 'Unsigned installer warning: Setup.exe is unsigned and may trigger an unknown-publisher or SmartScreen warning.'
}
Say 'Build complete.'
