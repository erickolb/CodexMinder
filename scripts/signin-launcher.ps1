$ErrorActionPreference = 'Stop'
$logDirectory = Join-Path $env:LOCALAPPDATA 'CodexMinder'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logPath = Join-Path $logDirectory 'startup.log'
function Write-StartupLog([string]$Message) {
    Add-Content -LiteralPath $logPath -Value ('{0:o} {1}' -f [DateTimeOffset]::Now, $Message)
}
try {
    Write-StartupLog 'Sign-in task invoked.'
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    Write-StartupLog ('Startup registry key exists: {0}; CodexMinder value exists: {1}.' -f ($null -ne $key), ($null -ne $key -and $key.GetValueNames() -contains 'CodexMinder'))
    try { $command = if ($key) { $key.GetValue('CodexMinder') } } finally { if ($key) { $key.Dispose() } }
    if (!$command) { Write-StartupLog 'Startup is disabled; no launch attempted.'; exit 0 }
    $executable = $command.Trim([char]34)
    if ([IO.Path]::GetFileName($executable) -ne 'CodexMinder.exe' -or !(Test-Path -LiteralPath $executable)) {
        throw 'Registered CodexMinder executable is missing or invalid.'
    }
    $running = Get-Process CodexMinder -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq [Diagnostics.Process]::GetCurrentProcess().SessionId }
    if ($running) { Write-StartupLog 'CodexMinder is already running in this session.'; exit 0 }
    $process = Start-Process -FilePath $executable -WorkingDirectory ([IO.Path]::GetDirectoryName($executable)) -PassThru
    Start-Sleep -Seconds 5
    $running = Get-Process CodexMinder -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq [Diagnostics.Process]::GetCurrentProcess().SessionId }
    if (!$running) { throw 'CodexMinder exited before launch verification completed.' }
    Write-StartupLog ('CodexMinder is running; PID {0}.' -f ($running.Id -join ', '))
    exit 0
} catch {
    Write-StartupLog ('Launch failed: {0}' -f $_.Exception.Message)
    exit 1
}
