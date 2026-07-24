param(
    [string]$Python = "python",
    [string]$Configuration = "Release",
    [ValidateSet("x64")]
    [string]$Platform = "x64",
    [switch]$SkipBuild,
    [switch]$NoExplorerRestart
)

$ErrorActionPreference = "Stop"

function Test-Command($Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function ConvertTo-RelativePath {
    param(
        [string]$Root,
        [string]$Path
    )
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd("\", "/") + "\"
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    $rootUri = [Uri]$rootFull
    $pathUri = [Uri]$pathFull
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace("/", "\")
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$FailureHint
    )
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        if ($FailureHint) {
            throw $FailureHint
        }
        throw "Command failed: $FilePath $($Arguments -join ' ')"
    }
}

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$ContextRoot = Join-Path $ProjectRoot "windows\context-menu"
$BuildRoot = Join-Path $ContextRoot "out\build\$Platform"
$BinRoot = Join-Path $ContextRoot "bin\$Platform"
$DllName = "DewEncryptionExplorerCommand.dll"
$DllPath = Join-Path $BinRoot $DllName
$ManifestTemplate = Join-Path $ContextRoot "package\AppxManifest.template.xml"
$ManifestPath = Join-Path $ContextRoot "AppxManifest.xml"

if (-not (Test-Path $ManifestTemplate)) {
    throw "Missing Windows 11 context menu manifest template: $ManifestTemplate"
}

if (-not $SkipBuild) {
    if (-not (Test-Command cmake)) {
        throw "CMake is required to build the Windows 11 context menu DLL."
    }

    $generator = $env:DEW_CONTEXT_MENU_CMAKE_GENERATOR
    if (-not $generator) {
        $generator = "Visual Studio 17 2022"
    }
    $cmakeConfigureArgs = @("-S", $ContextRoot, "-B", $BuildRoot, "-G", $generator, "-A", $Platform)
    $buildHint = "Could not configure the native Windows 11 context menu DLL. Install Visual Studio 2022 Build Tools with the Desktop development with C++ workload and a Windows 11 SDK, then rerun this script."
    Invoke-Checked -FilePath "cmake" -Arguments $cmakeConfigureArgs -FailureHint $buildHint

    $cmakeBuildArgs = @("--build", $BuildRoot, "--config", $Configuration, "--target", "DewEncryptionExplorerCommand")
    $compileHint = "Could not build the native Windows 11 context menu DLL. Install Visual Studio 2022 Build Tools with C++ tools and the Windows SDK, then rerun this script."
    Invoke-Checked -FilePath "cmake" -Arguments $cmakeBuildArgs -FailureHint $compileHint

    $builtDllCandidates = @(
        (Join-Path $BuildRoot "$Configuration\$DllName"),
        (Join-Path $BuildRoot $DllName)
    )
    $builtDll = $builtDllCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $builtDll) {
        throw "Build completed, but $DllName was not found under $BuildRoot."
    }

    New-Item -ItemType Directory -Path $BinRoot -Force | Out-Null
    Copy-Item -LiteralPath $builtDll -Destination $DllPath -Force
}

if (-not (Test-Path $DllPath)) {
    throw "Missing $DllName at $DllPath. Build it first or rerun without -SkipBuild."
}

$guiExe = Join-Path $ProjectRoot "csharp\DewEncryption.Gui\bin\Debug\net8.0\DewEncryption.Gui.exe"
if (-not (Test-Path $guiExe) -and (Test-Command dotnet)) {
    dotnet build (Join-Path $ProjectRoot "csharp\DewEncryption.Gui\DewEncryption.Gui.csproj")
}
if (-not (Test-Path $guiExe)) {
    throw "Missing C# GUI executable used as package entry point: $guiExe"
}

$resolvedPython = (Get-Command $Python -ErrorAction SilentlyContinue).Source
if (-not $resolvedPython) {
    $resolvedPython = $Python
}

$configKey = "HKCU:\Software\DewEncryption\ContextMenu"
New-Item -Path $configKey -Force | Out-Null
New-ItemProperty -Path $configKey -Name "InstallRoot" -Value $ProjectRoot -PropertyType String -Force | Out-Null
New-ItemProperty -Path $configKey -Name "PythonPath" -Value $resolvedPython -PropertyType String -Force | Out-Null

$appExecutable = ConvertTo-RelativePath -Root $ProjectRoot -Path $guiExe
$dllRelative = ConvertTo-RelativePath -Root $ProjectRoot -Path $DllPath
$manifest = Get-Content -LiteralPath $ManifestTemplate -Raw
$manifest = $manifest.Replace("{{APP_EXECUTABLE}}", $appExecutable).Replace("{{DLL_PATH}}", $dllRelative)
Set-Content -LiteralPath $ManifestPath -Value $manifest -Encoding UTF8

$existing = Get-AppxPackage -Name "CodingMachineEdge.DewEncryption.ContextMenu" -ErrorAction SilentlyContinue
if ($existing) {
    $existing | Remove-AppxPackage
}

Add-AppxPackage -Register $ManifestPath -ExternalLocation $ProjectRoot

if (-not (Get-AppxPackage -Name "CodingMachineEdge.DewEncryption.ContextMenu" -ErrorAction SilentlyContinue)) {
    throw "Windows did not report the Dew Encryption context menu package after registration."
}

if (-not $NoExplorerRestart) {
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Process explorer.exe
}

Write-Host "Installed Windows 11 Explorer context menu package: Dew Encryption"
