param([switch]$DpiUnaware)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
Push-Location $project
try {
    $sources = @(Get-ChildItem src -Filter *.cs | ForEach-Object FullName)
    $refs = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:System.Numerics.dll','/r:System.Net.Http.dll')
    $manifest = @()
    if (-not $DpiUnaware) { $manifest = @("/win32manifest:$project/src/app.manifest") }
    & "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo /target:exe /utf8output /main:RxToolbox.RenderUi "/out:$project\build\RenderUi.exe" "/resource:$project\assets\app.png,RxToolbox.Brand" @manifest @refs @sources "$project\tests\RenderUi.cs"
    if ($LASTEXITCODE -ne 0) { throw 'UI renderer build failed' }
    & ./build/RenderUi.exe
    if ($LASTEXITCODE -ne 0) { throw 'UI render failed' }
} finally { Pop-Location }
