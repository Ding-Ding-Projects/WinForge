$ErrorActionPreference = "Stop"

$tasks = @(
    "DewEncryption.GUI.Elevated",
    "DewEncryption.CLI.Help.Elevated",
    "DewEncryption.VeraCrypt.Settings.Elevated"
)

foreach ($task in $tasks) {
    if (Get-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $task -Confirm:$false
    }
}

Write-Host "Removed Dew Encryption elevated scheduled tasks."
