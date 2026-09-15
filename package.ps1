param([string]$Version='0.2.0',[switch]$Locked)
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid version'}
$declared=([xml](Get-Content (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if($Version -ne $declared){throw 'Release version must match Directory.Build.props'}
& (Join-Path $PSScriptRoot 'build.ps1') -Locked:$Locked
$output=Join-Path $PSScriptRoot 'dist'
$work=Join-Path $PSScriptRoot ('.build/package-'+[Guid]::NewGuid().ToString('N'))
$publish=Join-Path $work 'publish'
& dotnet publish (Join-Path $PSScriptRoot 'desktop/BD2Fishing.Desktop.csproj') -c Release -r win-x64 --self-contained true --no-restore --output $publish "-p:Version=$Version" -p:DebugType=None -p:DebugSymbols=false --nologo
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
$exe=Join-Path $publish 'BD2Fishing.exe'
$check=Join-Path $work 'checks';New-Item -ItemType Directory -Force -Path $check | Out-Null
function RunCheck([string[]]$Arguments){
    $process=Start-Process -FilePath $exe -ArgumentList $Arguments -WorkingDirectory (Get-Location).Path -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(30000)){throw 'Packaged EXE check timed out'}
    if($process.ExitCode -ne 0){throw 'Packaged EXE check failed'}
}
$identityPath=Join-Path $check 'identity.json'
RunCheck @('--identity',('"'+$identityPath+'"'))
$identity=Get-Content $identityPath -Raw | ConvertFrom-Json
if($identity.runtime -ne 'BD2Fishing.Runtime6' -or $identity.compatibility -ne 'local-interface-adaptation' -or $identity.defaultNextCastMs -ne 1000){throw 'Embedded identity differs from release'}
RunCheck @('--smoke',('"'+$check+'"'))
$ui=Get-Content (Join-Path $check 'results.json') -Raw | ConvertFrom-Json
if($ui.status -ne 'pass'){throw 'Packaged UI regression failed'}
New-Item -ItemType Directory -Force -Path $output | Out-Null
$name="BD2Fishing-$Version-win-x64"
$zipPath=Join-Path $output "$name.zip"
$exePath=Join-Path $output "BD2Fishing-$Version.exe"
if((Test-Path $zipPath) -or (Test-Path $exePath)){throw 'Release assets already exist; use a new version or a fresh checkout'}
$bundle=Join-Path $work $name;New-Item -ItemType Directory -Path $bundle | Out-Null
Copy-Item -LiteralPath $exe -Destination $exePath
Copy-Item -LiteralPath $exe -Destination (Join-Path $bundle "BD2Fishing-$Version.exe")
foreach($file in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $bundle}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $bundle -Recurse
Compress-Archive -LiteralPath $bundle -DestinationPath $zipPath -CompressionLevel Optimal
$hashes=@(foreach($file in @($exePath,$zipPath)){"$((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($file))"})
$hashes | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
[ordered]@{version=$Version;runtime=$identity.runtime;compatibility=$identity.compatibility;toolFingerprint=$identity.toolFingerprint;uiAssertions=$ui.assertions.Count;gameLibrariesBundled=$false;clientVersionLock=$false;runtimeVerification='source_implemented_pending_runtime'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'release.json') -Encoding UTF8
Write-Host "Release assets ready: $output"
