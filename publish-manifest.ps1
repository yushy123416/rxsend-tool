param(
    [string]$Repository='yushy123416/rxsend-tool',
    [string]$GiteeRepository='ysyyyy/rxsend-tool',
    [string[]]$Mirrors=@(),
    [switch]$UsePrepared
)
$ErrorActionPreference='Stop'
foreach($repoName in @($Repository,$GiteeRepository)){
    if($repoName -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'){throw 'Expected owner/repo'}
}
$exe=Join-Path $PSScriptRoot 'dist\RXSendTool.exe'
$version=[Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString(3)
$updates=Join-Path $PSScriptRoot 'updates'
$preparedExe=Join-Path $updates "v$version\RXSendTool.exe"
$preparedManifest=Join-Path $updates 'update.json'
if($UsePrepared){
    $manifest=Get-Content -LiteralPath $preparedManifest -Raw | ConvertFrom-Json
    if($manifest.version -ne $version){throw 'Prepared update version differs from build'}
    $preparedHash=(Get-FileHash -LiteralPath $preparedExe -Algorithm SHA256).Hash
    if($preparedHash -ine $manifest.sha256){throw 'Prepared update SHA-256 differs'}
    $identity=[Reflection.AssemblyName]::GetAssemblyName($preparedExe)
    if($identity.Name -ne 'RxToolbox' -or $identity.Version.ToString(3) -ne $version){throw 'Prepared update identity differs'}
    Copy-Item -LiteralPath $preparedExe -Destination $exe -Force
    Copy-Item -LiteralPath $preparedManifest -Destination (Join-Path $PSScriptRoot 'dist\update.json') -Force
    Compress-Archive -Path $exe,(Join-Path $PSScriptRoot 'dist\LICENSE'),(Join-Path $PSScriptRoot 'dist\THIRD-PARTY-NOTICES.md') -DestinationPath (Join-Path $PSScriptRoot "dist\RxToolbox-$version-Windows.zip") -Force
    Write-Output "Staged immutable v$version payload from updates/"
    return
}
$githubUrl="https://github.com/$Repository/releases/download/v$version/RXSendTool.exe"
$manifest=[ordered]@{
    version=$version
    url="https://gitee.com/$GiteeRepository/raw/main/updates/v$version/RXSendTool.exe"
    sha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    mirrors=@(@($githubUrl)+$Mirrors | Select-Object -Unique)
    notes="RXSend Tool $version"
}
New-Item -ItemType Directory -Force (Split-Path $preparedExe) | Out-Null
Copy-Item -LiteralPath $exe -Destination $preparedExe -Force
$json=($manifest|ConvertTo-Json -Depth 4).Replace([Environment]::NewLine,[string][char]10)
foreach($target in @((Join-Path $PSScriptRoot 'dist\update.json'),$preparedManifest)){
    [IO.File]::WriteAllText($target,$json,[Text.UTF8Encoding]::new($false))
}
Write-Output "Prepared Gitee-primary / GitHub-fallback update v$version"
