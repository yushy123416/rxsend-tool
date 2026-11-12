$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$testRoot = Join-Path $projectRoot 'build\update-smoke'
$stage = Join-Path $env:LOCALAPPDATA ('RxToolbox\updates\smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $testRoot,$stage | Out-Null
$stub = @'
using System;
using System.IO;
using System.Reflection;
[assembly: AssemblyVersion("1.1.0.0")]
class Fixture { static void Main() { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"started.txt"),"1.1.0"); } }
'@
$stubPath=Join-Path $testRoot 'Fixture.cs'
[IO.File]::WriteAllText($stubPath,$stub)
$payload=Join-Path $stage 'RxToolbox.exe'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:winexe "/out:$payload" $stubPath
if($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed' }
$target=Join-Path $testRoot 'RxToolbox.exe'
$helper=Join-Path $stage 'RxToolbox.updater.exe'
Copy-Item (Join-Path $projectRoot 'dist\RXSendTool.exe') $target -Force
Copy-Item (Join-Path $projectRoot 'dist\RXSendTool.exe') $helper -Force
$hash=(Get-FileHash $payload -Algorithm SHA256).Hash
$marker=Join-Path $testRoot 'started.txt'
if(Test-Path $marker){Remove-Item -LiteralPath $marker}
$argsText='--apply-update "{0}" 2147483647 "{1}" {2}' -f $target,$payload,$hash
$process=Start-Process -FilePath $helper -ArgumentList $argsText -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(10000)){throw 'Updater timed out'}
for($i=0;$i -lt 30 -and -not(Test-Path $marker);$i++){Start-Sleep -Milliseconds 100}
if((Get-FileHash $target -Algorithm SHA256).Hash -ne $hash){throw 'Installed hash mismatch'}
if(-not(Test-Path $marker) -or (Get-Content $marker) -ne '1.1.0'){throw 'Updated program did not start'}
'PASS updater staged replacement, installed hash, restart marker'
