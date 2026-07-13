[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$ExecutablePath,
    [int]$TimeoutMs = 45000
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

# The repository automation session can run at high integrity. Package-manager
# execution deliberately fails closed there, so launch through a temporary interactive
# scheduled task with RunLevel=Limited. The verifier still rejects the evidence unless
# the actual child token is medium-integrity, non-elevated, non-full, and not an admin.
if (-not ('WinForgeNativeTokenVerifier' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class WinForgeNativeTokenVerifier
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenMandatoryLabel { public SidAndAttributes Label; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation { public int TokenIsElevated; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSidToSidW(string value, out IntPtr sid);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr token, int informationClass, out TokenElevation value, int valueLength, out int returnLength);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr token, int informationClass, out int value, int valueLength, out int returnLength);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr token, int informationClass, IntPtr value, int valueLength, out int returnLength);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CheckTokenMembership(IntPtr token, IntPtr sid, out bool isMember);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existing, uint desiredAccess, IntPtr tokenAttributes,
        int impersonationLevel, int tokenType, out IntPtr duplicate);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthority);
    private static void VerifyLimitedMediumToken(IntPtr token)
    {
        int returned;
        TokenElevation elevation;
        if (!GetTokenInformation(token, 20, out elevation, Marshal.SizeOf<TokenElevation>(), out returned))
            throw new Win32Exception();
        if (elevation.TokenIsElevated != 0)
            throw new InvalidOperationException("candidate smoke token is still elevated");

        int elevationType;
        if (!GetTokenInformation(token, 18, out elevationType, sizeof(int), out returned))
            throw new Win32Exception();
        if (elevationType == 2)
            throw new InvalidOperationException("candidate smoke token still has full elevation type");

        if (!GetTokenInformation(token, 25, IntPtr.Zero, 0, out returned) &&
            Marshal.GetLastWin32Error() != 122)
            throw new Win32Exception();
        IntPtr labelBuffer = Marshal.AllocHGlobal(returned);
        IntPtr administratorsSid = IntPtr.Zero, impersonationToken = IntPtr.Zero;
        try
        {
            if (!GetTokenInformation(token, 25, labelBuffer, returned, out returned))
                throw new Win32Exception();
            var label = Marshal.PtrToStructure<TokenMandatoryLabel>(labelBuffer);
            IntPtr countPointer = GetSidSubAuthorityCount(label.Label.Sid);
            if (countPointer == IntPtr.Zero)
                throw new Win32Exception();
            byte count = Marshal.ReadByte(countPointer);
            if (count == 0)
                throw new InvalidOperationException("candidate smoke token has no integrity RID");
            IntPtr ridPointer = GetSidSubAuthority(label.Label.Sid, (uint)(count - 1));
            if (ridPointer == IntPtr.Zero || unchecked((uint)Marshal.ReadInt32(ridPointer)) != 8192u)
                throw new InvalidOperationException("candidate smoke token is not medium integrity");

            if (!ConvertStringSidToSidW("S-1-5-32-544", out administratorsSid))
                throw new Win32Exception();
            const uint TokenQuery = 0x0008;
            const int SecurityImpersonation = 2;
            const int TokenImpersonation = 2;
            if (!DuplicateTokenEx(
                token, TokenQuery, IntPtr.Zero,
                SecurityImpersonation, TokenImpersonation, out impersonationToken))
                throw new Win32Exception();
            bool isAdministrator;
            if (!CheckTokenMembership(impersonationToken, administratorsSid, out isAdministrator))
                throw new Win32Exception();
            if (isAdministrator)
                throw new InvalidOperationException("candidate smoke token retains enabled Administrators membership");
        }
        finally
        {
            if (impersonationToken != IntPtr.Zero) CloseHandle(impersonationToken);
            if (administratorsSid != IntPtr.Zero) LocalFree(administratorsSid);
            Marshal.FreeHGlobal(labelBuffer);
        }
    }

    public static void VerifyProcess(int processId)
    {
        IntPtr process = IntPtr.Zero, token = IntPtr.Zero;
        try
        {
            const uint ProcessQueryLimitedInformation = 0x1000;
            process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero)
                throw new Win32Exception();
            const uint TokenAccess = 0x000A; // duplicate + query
            if (!OpenProcessToken(process, TokenAccess, out token))
                throw new Win32Exception();
            VerifyLimitedMediumToken(token);
        }
        finally
        {
            if (token != IntPtr.Zero) CloseHandle(token);
            if (process != IntPtr.Zero) CloseHandle(process);
        }
    }

}
'@
}

$script:Passed = 0
$script:Failed = 0

function Assert-True {
    param([bool]$Condition, [string]$Name)
    if ($Condition) {
        $script:Passed++
        Write-Host "PASS $Name" -ForegroundColor Green
    }
    else {
        $script:Failed++
        Write-Host "FAIL $Name" -ForegroundColor Red
    }
}

function Find-ByAutomationId {
    param($Root, [string]$AutomationId)
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Wait-ForElement {
    param($Root, [string]$AutomationId, [int]$WaitMs = $TimeoutMs)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($WaitMs)
    do {
        $element = Find-ByAutomationId -Root $Root -AutomationId $AutomationId
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for automation id '$AutomationId'."
}

function Select-PackageView {
    param($Root, [string]$Name, [string]$ExpectedHeader)
    $picker = Wait-ForElement -Root $Root -AutomationId 'NativePackageViewPicker'
    $expand = [System.Windows.Automation.ExpandCollapsePattern]$picker.GetCurrentPattern(
        [System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    $option = $null
    do {
        $items = $picker.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($item in $items) {
            if ($item.Current.Name.StartsWith($Name, [StringComparison]::Ordinal)) {
                $option = $item
                break
            }
        }
        if (-not $option) { Start-Sleep -Milliseconds 100 }
    } while (-not $option -and [DateTime]::UtcNow -lt $deadline)
    if (-not $option) { throw "Package view '$Name' was not found." }
    $selection = [System.Windows.Automation.SelectionItemPattern]$option.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selection.Select()

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $header = Find-ByAutomationId -Root $Root -AutomationId 'NativePackageResultsHeader'
        if ($header -and $header.Current.Name.StartsWith($ExpectedHeader, [StringComparison]::Ordinal)) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Package view '$Name' did not render header '$ExpectedHeader'."
}

function Wait-ForQueryCompletion {
    param($Root, [string]$Manager)
    $deadline = [DateTime]::UtcNow.AddMilliseconds([Math]::Max($TimeoutMs, 65000))
    do {
        $working = Find-ByAutomationId -Root $Root -AutomationId 'NativePackageWorkingState'
        $managerState = Find-ByAutomationId -Root $Root -AutomationId "NativePackageManagerState_$Manager"
        if (-not $working -and $managerState) { return $managerState }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    return $null
}

function Invoke-ReadOnlyQuery {
    param($Root, [string]$Manager, [string]$Name, [string]$SuccessText = 'Query completed successfully')
    $primary = Wait-ForElement -Root $Root -AutomationId 'NativePackagePrimaryAction'
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while (-not $primary.Current.IsEnabled -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
        $primary = Wait-ForElement -Root $Root -AutomationId 'NativePackagePrimaryAction'
    }
    Assert-True -Condition $primary.Current.IsEnabled -Name "$Name action is enabled at medium integrity"
    if (-not $primary.Current.IsEnabled) { return }
    $invoke = [System.Windows.Automation.InvokePattern]$primary.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    $state = Wait-ForQueryCompletion -Root $Root -Manager $Manager
    Assert-True -Condition ([bool]$state) -Name "$Name exposes an explicit per-engine result"
    if (-not $state) { return }
    $helpText = $state.Current.HelpText
    Assert-True -Condition ($helpText.StartsWith($SuccessText, [StringComparison]::Ordinal)) `
        -Name "$Name succeeds through the native runtime"
}

$process = $null
$taskName = 'WinForgeNativeLiveSmoke_' + [guid]::NewGuid().ToString('N')
$taskRegistered = $false
$taskProcessId = 0
$taskProcessStartTime = $null
$taskProcessHandle = [IntPtr]::Zero
$scheduler = $null
$registeredComTask = $null
try {
    $action = New-ScheduledTaskAction `
        -Execute $exe `
        -Argument '--page "module.packages#updates"' `
        -WorkingDirectory (Split-Path -Parent $exe)
    # A distant trigger satisfies task registration without creating a near-term
    # surprise launch if Windows fails between registration and verified cleanup.
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddYears(1)
    $principal = New-ScheduledTaskPrincipal `
        -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) `
        -LogonType Interactive `
        -RunLevel Limited
    Register-ScheduledTask `
        -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Force | Out-Null
    $taskRegistered = $true
    Start-ScheduledTask -TaskName $taskName

    $scheduler = New-Object -ComObject 'Schedule.Service'
    $scheduler.Connect()
    $registeredComTask = $scheduler.GetFolder('\').GetTask($taskName)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $instances = $registeredComTask.GetInstances(0)
        if ($instances.Count -gt 0) {
            $taskProcessId = [int]$instances.Item(1).EnginePID
            if ($taskProcessId -gt 0) {
                $process = Get-Process -Id $taskProcessId -ErrorAction SilentlyContinue
            }
        }
        if ($process -and $process.Path -eq $exe -and $process.MainWindowHandle -ne 0) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $process -or $process.MainWindowHandle -eq 0) {
        throw 'Least-privilege scheduled native window did not appear.'
    }
    if ($process.Id -ne $taskProcessId -or $process.Path -ne $exe) {
        throw 'Scheduled-task instance PID/path proof did not match the native smoke executable.'
    }
    $taskProcessStartTime = $process.StartTime.ToUniversalTime()
    # Force System.Diagnostics.Process to acquire and retain the exact process
    # object handle before any task stop can make the PID recyclable.
    $taskProcessHandle = $process.Handle
    if ($taskProcessHandle -eq [IntPtr]::Zero) {
        throw 'Could not retain the exact scheduled-task process handle.'
    }
    try {
        [WinForgeNativeTokenVerifier]::VerifyProcess($process.Id)
    }
    catch {
        throw "Normal-integrity live smoke is blocked: Windows returned a token that failed the standard-user proof even for an interactive RunLevel=Limited task. $($_.Exception.Message)"
    }
    Assert-True -Condition $true -Name 'scheduled smoke process has a verified standard-user token'

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    Wait-ForElement -Root $root -AutomationId 'NativePackageReadyState' | Out-Null
    Assert-True -Condition $true -Name 'medium-integrity Package Manager completes engine probes'

    $managerOrder = @('winget', 'scoop', 'choco', 'pip', 'npm', 'dotnet', 'psgallery', 'pwsh7', 'cargo', 'bun', 'vcpkg')
    $selectedManager = $null
    foreach ($manager in $managerOrder) {
        $filter = Wait-ForElement -Root $root -AutomationId "NativePackageManagerFilter_$manager"
        if (-not $filter.Current.IsEnabled) { continue }
        if (-not $selectedManager) {
            $selectedManager = $manager
            continue
        }
        $toggle = [System.Windows.Automation.TogglePattern]$filter.GetCurrentPattern(
            [System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) { $toggle.Toggle() }
    }
    Assert-True -Condition ([bool]$selectedManager) -Name 'medium-integrity probe unlocks a native package engine'
    if (-not $selectedManager) {
        foreach ($manager in $managerOrder) {
            $filter = Find-ByAutomationId -Root $root -AutomationId "NativePackageManagerFilter_$manager"
            if ($filter) {
                Write-Host "PROBE $manager : $($filter.Current.HelpText)"
            }
        }
        throw 'No package engine was available for the live read-only smoke.'
    }

    Invoke-ReadOnlyQuery -Root $root -Manager $selectedManager -Name 'Updates query'

    Select-PackageView -Root $root -Name 'Installed' -ExpectedHeader 'Installed packages'
    Invoke-ReadOnlyQuery -Root $root -Manager $selectedManager -Name 'Installed-packages query'

    Select-PackageView -Root $root -Name 'Sources' -ExpectedHeader 'Package sources'
    Invoke-ReadOnlyQuery -Root $root -Manager $selectedManager -Name 'Source query' `
        -SuccessText 'Source command completed'

    Select-PackageView -Root $root -Name 'Discover' -ExpectedHeader 'Discover packages'
    $search = Wait-ForElement -Root $root -AutomationId 'NativePackageSearchBox'
    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $edit = $search.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $editCondition)
    $valuePattern = $null
    if ($edit -and $edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$valuePattern)) {
        ([System.Windows.Automation.ValuePattern]$valuePattern).SetValue('7zip')
    }
    elseif ($search.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$valuePattern)) {
        ([System.Windows.Automation.ValuePattern]$valuePattern).SetValue('7zip')
    }
    else {
        throw 'Native package search box does not expose ValuePattern.'
    }
    Invoke-ReadOnlyQuery -Root $root -Manager $selectedManager -Name 'Discover query'
}
finally {
    $cleanupErrors = [System.Collections.Generic.List[string]]::new()
    if ($taskRegistered) {
        try {
            Stop-ScheduledTask -TaskName $taskName -ErrorAction Stop
            if ($registeredComTask) {
                $stopDeadline = [DateTime]::UtcNow.AddSeconds(10)
                do {
                    $remainingInstances = $registeredComTask.GetInstances(0).Count
                    if ($remainingInstances -eq 0) { break }
                    Start-Sleep -Milliseconds 100
                } while ([DateTime]::UtcNow -lt $stopDeadline)
                if ($remainingInstances -ne 0) {
                    throw 'scheduled task instance did not stop'
                }
            }
        }
        catch {
            $cleanupErrors.Add("could not stop scheduled task instance: $($_.Exception.Message)")
        }
    }
    if ($process) {
        try {
            if (-not $process.HasExited) {
                if ($process.Id -ne $taskProcessId -or $process.Path -ne $exe -or
                    $process.StartTime.ToUniversalTime() -ne $taskProcessStartTime) {
                    throw 'retained task process identity changed; refusing to terminate by PID'
                }
                $process.Kill()
                if (-not $process.WaitForExit(10000)) {
                    throw "process $taskProcessId did not exit"
                }
            }
        }
        catch {
            $cleanupErrors.Add("could not stop verified task process: $($_.Exception.Message)")
        }
    }
    if ($taskRegistered) {
        try {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop
            if (-not $scheduler) {
                $scheduler = New-Object -ComObject 'Schedule.Service'
                $scheduler.Connect()
            }
            try {
                $null = $scheduler.GetFolder('\').GetTask($taskName)
                throw 'task is still registered after unregistration'
            }
            catch {
                if ($_.Exception.HResult -ne -2147024894) { # HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)
                    throw
                }
            }
        }
        catch {
            $cleanupErrors.Add("could not remove scheduled task: $($_.Exception.Message)")
        }
    }
    if ($process) {
        try {
            $process.Dispose()
            $taskProcessHandle = [IntPtr]::Zero
        }
        catch {
            $cleanupErrors.Add("could not release verified task process handle: $($_.Exception.Message)")
        }
    }
    if ($cleanupErrors.Count -ne 0) {
        throw "Native live-smoke cleanup failed: $($cleanupErrors -join '; ')"
    }
}

Write-Host "`n$script:Passed passed, $script:Failed failed"
if ($script:Failed -ne 0) { exit 1 }
