[CmdletBinding()]
param(
    [string]$Dotnet = 'dotnet',
    [string]$Iscc = 'iscc',
    [string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    [xml]$project = Get-Content -LiteralPath (Join-Path $root 'CodexMinder.csproj')
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version.' }
    if ($ExpectedVersion -and $ExpectedVersion.TrimStart('v') -ne $version) { throw "Tag version does not match project version $version" }
    $publish = Join-Path $root "artifacts/staging/$version"
    $release = Join-Path $root "artifacts/releases/$version"
    New-Item -ItemType Directory -Force -Path $publish, $release | Out-Null
    & $Dotnet build CodexMinder.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $Dotnet run --project tests/CodexMinder.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $Dotnet publish CodexMinder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath LICENSE, THIRD-PARTY-NOTICES.md, README.md, docs/INSTALL.md -Destination $publish

    # Preserve the notices for the exact runtime packages selected by restore.
    $assets = Get-Content -LiteralPath obj/project.assets.json -Raw | ConvertFrom-Json
    $runtimes = @($assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object { $_.name -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64$' })
    if ($runtimes.Count -ne 2) { throw 'Expected both .NET and WindowsDesktop runtime packs in restore metadata.' }
    foreach ($library in $runtimes) {
        $runtimeVersion = $library.version.Split(',')[0].TrimStart('[')
        $found = $false
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $package = Join-Path $folder ($library.name.ToLowerInvariant() + '/' + $runtimeVersion)
            if (!(Test-Path -LiteralPath $package)) { continue }
            $destination = Join-Path $publish ('runtime-notices/' + $library.name + '-' + $runtimeVersion)
            New-Item -ItemType Directory -Force -Path $destination | Out-Null
            foreach ($notice in Get-ChildItem -LiteralPath $package -File | Where-Object { $_.Name -match 'LICENSE|THIRD-PARTY-NOTICES' }) {
                Copy-Item -LiteralPath $notice.FullName -Destination $destination
                $found = $true
            }
            break
        }
        if (!$found) { throw "Could not find runtime notices for $($library.Name)." }
    }
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath (Join-Path $release "CodexMinder-$version-win-x64.zip") -Force
    & $Iscc "/DAppVersion=$version" "/DPublishDir=$publish" "/DReleaseDir=$release" packaging/CodexMinder.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $checksums = Get-ChildItem -LiteralPath $release -File | Where-Object { $_.Extension -in '.exe', '.zip' } | Sort-Object Name | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    }
    [IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'), [string[]]$checksums, [Text.UTF8Encoding]::new($false))
    Write-Output "Release files: $release"
} finally { Pop-Location }
