using namespace Microsoft.Win32
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'CodexMinder.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$setup = Join-Path $root "artifacts/releases/$version/CodexMinder-$version-win-x64-setup.exe"
$destination = Join-Path $root 'artifacts/install-smoke'
$exe = Join-Path $destination 'CodexMinder.exe'
$runKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
$priorStartup = $null

function Invoke-Setup([string]$Tasks) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/DIR="' + $destination + '"'), ('/TASKS="' + $Tasks + '"'), ('/LOG="' + (Join-Path $root 'artifacts/installer-test.log') + '"'))
    $process = Start-Process -FilePath $setup -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Setup failed: $($process.ExitCode)" }
}
function Startup-Value {
    $key = [Registry]::CurrentUser.OpenSubKey($runKeyPath)
    try { return $key.GetValue('CodexMinder') } finally { $key.Dispose() }
}
function Assert([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    Write-Output "PASS $Message"
}

$key = [Registry]::CurrentUser.CreateSubKey($runKeyPath)
try { $priorStartup = $key.GetValue('CodexMinder'); $key.DeleteValue('CodexMinder', $false) } finally { $key.Dispose() }
try {
    Invoke-Setup ''
    Assert (Test-Path -LiteralPath $exe) 'Per-user installation produces executable'
    Assert (Test-Path -LiteralPath (Join-Path $destination 'installed.marker')) 'Installer startup-policy marker exists'
    Assert ($null -eq (Startup-Value)) 'Startup opt-out respected'
    $app = Start-Process -FilePath $exe -ArgumentList '--demo' -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    Assert (!$app.HasExited) 'Installed app stays running'
    Invoke-Setup 'startup'
    $app.WaitForExit(10000) | Out-Null
    Assert $app.HasExited 'Upgrade gracefully stops running installed app'
    Assert ((Startup-Value) -eq ('"' + $exe + '"')) 'Startup opt-in points at installed executable'
    Invoke-Setup ''
    Assert ($null -eq (Startup-Value)) 'Upgrade can turn startup off'
    $app = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    Assert (!$app.HasExited) 'Installed app launches normally'
    Assert ($null -eq (Startup-Value)) 'First launch does not reverse installer startup opt-out'
    $uninstaller = Start-Process -FilePath (Join-Path $destination 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -PassThru -Wait
    Assert ($uninstaller.ExitCode -eq 0) 'Uninstall succeeds while widget is running'
    $app.WaitForExit(10000) | Out-Null
    Assert $app.HasExited 'Uninstall gracefully stops widget'
    Assert (!(Test-Path -LiteralPath $exe)) 'Uninstall removes executable'
    Assert ($null -eq (Startup-Value)) 'Uninstall removes startup entry'
    Assert (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CodexMinder/settings.json')) 'Uninstall preserves user preferences'
} finally {
    if (Test-Path -LiteralPath $exe) { Start-Process -FilePath $exe -ArgumentList '--exit' -WindowStyle Hidden -Wait }
    $key = [Registry]::CurrentUser.CreateSubKey($runKeyPath)
    try { if ($null -ne $priorStartup) { $key.SetValue('CodexMinder', $priorStartup) } else { $key.DeleteValue('CodexMinder', $false) } } finally { $key.Dispose() }
}
