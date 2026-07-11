param(
    [string]$Python = "python"
)

$ErrorActionPreference = "Stop"

$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 12)

$tasks = @(
    @{
        Name = "DewEncryption.GUI.Elevated"
        Description = "Launch Dew Encryption GUI with normal Windows administrator approval."
        Execute = $Python
        Argument = "-m dew_encryption.gui"
    },
    @{
        Name = "DewEncryption.CLI.Help.Elevated"
        Description = "Launch Dew Encryption CLI help with normal Windows administrator approval."
        Execute = $Python
        Argument = "-m dew_encryption --help"
    },
    @{
        Name = "DewEncryption.VeraCrypt.Settings.Elevated"
        Description = "Show Dew Encryption VeraCrypt settings with normal Windows administrator approval."
        Execute = $Python
        Argument = "-m dew_encryption veracrypt-settings --show"
    }
)

foreach ($task in $tasks) {
    $action = New-ScheduledTaskAction -Execute $task.Execute -Argument $task.Argument
    Register-ScheduledTask `
        -TaskName $task.Name `
        -Action $action `
        -Principal $principal `
        -Settings $settings `
        -Description $task.Description `
        -Force | Out-Null
}

Write-Host "Created Dew Encryption elevated scheduled tasks."
Write-Host "These tasks require normal Windows administrator approval to create and are removable with remove-elevated-tasks.ps1."
