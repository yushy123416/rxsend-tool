param([string]$ArtifactPath=(Join-Path (Split-Path $PSScriptRoot) 'dist/RXSendTool.exe'))
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$verifier=Join-Path $projectRoot 'build/VerifyEndpointPrivacy.exe'
New-Item -ItemType Directory -Force (Split-Path $verifier) | Out-Null
& "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo /target:exe "/out:$verifier" (Join-Path $PSScriptRoot 'VerifyEndpointPrivacy.cs')
if($LASTEXITCODE -ne 0){throw 'Privacy verifier compilation failed'}
& $verifier ([IO.Path]::GetFullPath($ArtifactPath))
if($LASTEXITCODE -ne 0){throw 'Release endpoint privacy check failed'}
