[CmdletBinding()]
param([switch]$Remove)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$taskName = 'CodexMinder-' + $identity.User.Value
if ($Remove) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Output "Removed sign-in task: $taskName"
    return
}
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
try { $command = if ($key) { $key.GetValue('CodexMinder') } } finally { if ($key) { $key.Dispose() } }
if (!$command -or [IO.Path]::GetFileName($command.Trim([char]34)) -ne 'CodexMinder.exe' -or !(Test-Path -LiteralPath $command.Trim([char]34))) {
    throw 'Install CodexMinder and enable Run at Windows startup before running this repair.'
}
# Embed the small, reviewable launcher as a command. No execution-policy bypass
# or permanent policy change is needed, and the task survives moving this repo.
$launcher = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'signin-launcher.ps1') -Raw
if ($launcher.Contains('"')) { throw 'Launcher contains unsupported double quotes.' }
$arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -Command "& { ' + $launcher + ' }"'
$powershell = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
$action = New-ScheduledTaskAction -Execute $powershell -Argument $arguments -WorkingDirectory $env:LOCALAPPDATA
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity.User.Value
$trigger.Delay = 'PT15S'
$principal = New-ScheduledTaskPrincipal -UserId $identity.User.Value -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 2)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'CodexMinder sign-in fallback. Respects its Run at Windows startup setting and logs launches under LocalAppData/CodexMinder/startup.log.' -Force | Out-Null
# Recreate the startup entry once from the same interactive context that will
# launch the app. An agent host's registry view can differ from that context.
# Restore the ordinary read-only gate afterward so disabling startup still works.
$executableLiteral = $command.Trim([char]34).Replace("'", "''")
$initialize = '$startup = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey(''Software\Microsoft\Windows\CurrentVersion\Run''); try { $startup.SetValue(''CodexMinder'', ([char]34 + ''' + $executableLiteral + ''' + [char]34)) } finally { $startup.Dispose() }; '
$repairArguments = $arguments.Replace('-Command "& { ', ('-Command "& { ' + $initialize))
$repairAction = New-ScheduledTaskAction -Execute $powershell -Argument $repairArguments -WorkingDirectory $env:LOCALAPPDATA
try {
    Set-ScheduledTask -TaskName $taskName -Action $repairAction | Out-Null
    $priorRun = (Get-ScheduledTaskInfo -TaskName $taskName).LastRunTime
    Start-ScheduledTask -TaskName $taskName
    $deadline = (Get-Date).AddSeconds(40)
    do {
        Start-Sleep -Milliseconds 500
        $info = Get-ScheduledTaskInfo -TaskName $taskName
        $taskState = (Get-ScheduledTask -TaskName $taskName).State
        $finished = $info.LastRunTime -ne $priorRun -and $taskState -eq 'Ready'
    } until ($finished -or (Get-Date) -ge $deadline)
    if (!$finished) { throw 'Scheduled launch did not finish within 40 seconds; inspect startup.log and Task Scheduler.' }
    if ($info.LastTaskResult -ne 0) { throw "Scheduled launch failed with result $($info.LastTaskResult); inspect startup.log." }
} finally {
    Set-ScheduledTask -TaskName $taskName -Action $action | Out-Null
}
Write-Output "Registered sign-in task: $taskName"
Write-Output 'Runs 15 seconds after this user signs in, without administrator privileges.'
Write-Output 'The task launcher completed its initial verification successfully.'
