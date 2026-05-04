# Registers a Windows Task Scheduler task that runs the Ekub monthly fee
# job on the 1st of every month at 02:00 local time.
#
# Why local Task Scheduler and not the cloud /schedule routine?
#   The Ekub fee job needs to talk to the local Postgres at localhost:5432, which
#   isn't reachable from a remote agent. So this task runs on this machine.
#
# Usage:
#   .\scripts\register-ekub-fees-task.ps1                # register
#   .\scripts\register-ekub-fees-task.ps1 -Unregister    # remove
#   .\scripts\register-ekub-fees-task.ps1 -RunNow        # register and trigger once

[CmdletBinding()]
param(
    [string]$TaskName  = "GoldBank-Ekub-MonthlyFees",
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [switch]$Unregister,
    [switch]$RunNow
)

if ($Unregister) {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Removed scheduled task '$TaskName'."
    } else {
        Write-Host "Task '$TaskName' not registered."
    }
    return
}

$migratorProj = Join-Path $ProjectRoot "server\GoldBank.Migrator\GoldBank.Migrator.csproj"
if (-not (Test-Path $migratorProj)) {
    throw "Migrator project not found at $migratorProj. Are you running from the repo root?"
}

# We invoke `dotnet run` against the migrator project so the build artifacts
# don't have to be persisted between runs. The `--no-restore` flag is omitted
# intentionally — Postgres is fast enough that an extra restore each month is fine.
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$args   = "run --project `"$migratorProj`" -- --apply-ekub-fees"
$action = New-ScheduledTaskAction -Execute $dotnet -Argument $args -WorkingDirectory $ProjectRoot

# 1st of each month at 02:00 local time.
$trigger = New-ScheduledTaskTrigger -Monthly -DaysOfMonth 1 -At 02:00

$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopIfGoingOnBatteries `
    -AllowStartIfOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 30)

$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "GoldBank Ekub: debit the monthly bank fee from each active group's pot. Runs on the 1st of every month." `
    | Out-Null

Write-Host "Registered scheduled task '$TaskName' — runs 02:00 on the 1st of each month."

if ($RunNow) {
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "Triggered '$TaskName' for an immediate run."
}
