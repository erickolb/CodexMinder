[CmdletBinding()]
param([string]$Destination = (Join-Path (Split-Path $PSScriptRoot -Parent) '.tools/inno'))
$ErrorActionPreference = 'Stop'
# Pinned upstream release and its published SHA-256 digest.
$version = '6.7.3'
$expected = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
$download = Join-Path $env:TEMP "codexminder-innosetup-$version.exe"
Invoke-WebRequest "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-$version.exe" -OutFile $download
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expected) { throw 'Inno Setup checksum mismatch.' }
$install = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', ('/DIR="' + $Destination + '"')) -WindowStyle Hidden -PassThru -Wait
if ($install.ExitCode -ne 0) { throw "Inno Setup installation failed: $($install.ExitCode)" }
Write-Output (Join-Path $Destination 'ISCC.exe')
