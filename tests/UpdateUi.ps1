param([switch]$DpiUnaware)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
Push-Location $project
try {
    New-Item -ItemType Directory -Force "$project/build/ui-review" | Out-Null
    $sources=@(Get-ChildItem src -Filter *.cs | ForEach-Object FullName)
    $refs=@('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:System.Numerics.dll','/r:System.Net.Http.dll')
    $manifest=@()
    if(-not $DpiUnaware){$manifest=@("/win32manifest:$project\src\app.manifest")}
    & "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo /target:exe /utf8output /main:RxToolbox.UpdateUiTests "/out:$project\build\UpdateUiTests.exe" "/win32icon:$project\assets\app.ico" "/resource:$project\assets\app.png,RxToolbox.Brand" @manifest @refs @sources "$project\tests\UpdateUiTests.cs"
    if($LASTEXITCODE -ne 0){throw 'Update UI tests compilation failed'}
    & ./build/UpdateUiTests.exe
    if($LASTEXITCODE -ne 0){throw 'Update UI tests failed'}
} finally {Pop-Location}
