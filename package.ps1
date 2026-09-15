param([string]$Version='', [switch]$Locked)
$ErrorActionPreference='Stop'
$declared=([xml](Get-Content (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if(!$Version){$Version=$declared}
if($Version -notmatch '^\d+\.\d+\.\d+$' -or $Version -ne $declared){throw 'Release version must match Directory.Build.props'}
& (Join-Path $PSScriptRoot 'build.ps1') -Locked:$Locked
$destination=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot "dist/v$Version"))
if(Test-Path -LiteralPath $destination){throw 'Release assets already exist; use a fresh checkout'}
$work=Join-Path $PSScriptRoot ('.build/package-'+[Guid]::NewGuid().ToString('N'))
$output=Join-Path $work "assets"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$assets=@();$flavors=@()
foreach($flavor in @('Portable','Lite')){
    $selfContained=if($flavor -eq 'Portable'){'true'}else{'false'}
    $publish=Join-Path $work $flavor
    $buildArtifacts=Join-Path $work ($flavor+'-build')
    $restoreArgs=@();if($Locked){$restoreArgs+='-p:RestoreLockedMode=true'}
    & dotnet publish (Join-Path $PSScriptRoot 'desktop/BD2Fishing.Desktop.csproj') -c Release --self-contained $selfContained "-p:SelfContained=$selfContained" "-p:PublishSelfContained=$selfContained" --artifacts-path $buildArtifacts --output $publish "-p:Version=$Version" "-p:EnableCompressionInSingleFile=$selfContained" -p:DebugType=None -p:DebugSymbols=false @restoreArgs --nologo
    if($LASTEXITCODE -ne 0){throw "Publish failed: $flavor"}
    $exe=Join-Path $publish 'BD2Fishing.exe'
    if(@(Get-ChildItem -LiteralPath $publish -File -Recurse).Count -ne 1){throw "Unexpected sidecar files: $flavor"}
    $check=Join-Path $work "$flavor-checks";New-Item -ItemType Directory -Force -Path $check | Out-Null
    function RunCheck([string[]]$Arguments){
        $process=Start-Process -FilePath $exe -ArgumentList $Arguments -WorkingDirectory (Get-Location).Path -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(30000)){throw "Packaged EXE check timed out: $flavor"}
        if($process.ExitCode -ne 0){throw "Packaged EXE check failed: $flavor"}
    }
    $identityPath=Join-Path $check 'identity.json'
    RunCheck @('--identity',('"'+$identityPath+'"'))
    $identity=Get-Content $identityPath -Raw | ConvertFrom-Json
    if($identity.runtime -ne 'BD2Fishing.Runtime7' -or $identity.compatibility -ne 'local-interface-adaptation' -or $identity.defaultNextCastMs -ne 1000){throw 'Embedded identity differs from release'}
    RunCheck @('--smoke',('"'+$check+'"'))
    $ui=Get-Content (Join-Path $check 'results.json') -Raw | ConvertFrom-Json
    if($ui.status -ne 'pass'){throw 'Packaged UI regression failed'}
    # Check actual host configuration, not just the filename or publish flags.
    $configs=@(Get-ChildItem -LiteralPath (Join-Path $buildArtifacts 'bin') -Recurse -File -Filter 'BD2Fishing.runtimeconfig.json')
    if($configs.Count -ne 1){throw 'Runtime config not unique'}
    $runtimeConfig=Get-Content -LiteralPath $configs[0].FullName -Raw | ConvertFrom-Json
    if($flavor -eq 'Lite' -and !($runtimeConfig.runtimeOptions.frameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App')){throw 'Lite must use installed Desktop Runtime'}
    if($flavor -eq 'Portable' -and !$runtimeConfig.runtimeOptions.includedFrameworks){throw 'Portable must include runtime'}
    $name="BD2Fishing-$Version-$flavor-win-x64"
    $zipPath=Join-Path $output "$name.zip";$exePath=Join-Path $output "$name.exe"
    if((Test-Path $zipPath) -or (Test-Path $exePath)){throw 'Release assets already exist; use a new version or fresh checkout'}
    $bundle=Join-Path $work $name;New-Item -ItemType Directory -Path $bundle | Out-Null
    Copy-Item -LiteralPath $exe -Destination $exePath
    Copy-Item -LiteralPath $exe -Destination (Join-Path $bundle "$name.exe")
    foreach($file in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $bundle}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $bundle -Recurse
    Compress-Archive -LiteralPath $bundle -DestinationPath $zipPath -CompressionLevel Optimal
    $assets+=@($exePath,$zipPath)
    $flavors += [ordered]@{name=$flavor;selfContained=($selfContained -eq 'true');runtimeRequirement=$(if($flavor -eq 'Lite'){'.NET Desktop Runtime 8 x64'}else{'none'});exeBytes=(Get-Item $exePath).Length;uiAssertions=$ui.assertions.Count;toolFingerprint=$identity.toolFingerprint}
}
if($flavors[1].exeBytes -ge $flavors[0].exeBytes){throw 'Lite must be smaller than Portable'}
@(foreach($file in $assets){"$((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($file))"}) | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
[ordered]@{version=$Version;runtime=$identity.runtime;compatibility=$identity.compatibility;flavors=$flavors;gameLibrariesBundled=$false;clientVersionLock=$false;runtimeVerification='source_implemented_pending_runtime'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'release.json') -Encoding UTF8
$repoRoot=[IO.Path]::GetFullPath($PSScriptRoot)+[IO.Path]::DirectorySeparatorChar
if(!([IO.Path]::GetFullPath($output)).StartsWith($repoRoot,[StringComparison]::OrdinalIgnoreCase) -or !$destination.StartsWith($repoRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Output paths outside repository'}
New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
Move-Item -LiteralPath $output -Destination $destination
Write-Host "Release assets ready: $destination"
