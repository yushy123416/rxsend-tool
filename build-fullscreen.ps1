param([switch]$Test)
$ErrorActionPreference='Stop'
$out=Join-Path $PSScriptRoot 'build/fullscreen'
New-Item -ItemType Directory -Force $out | Out-Null
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if(!(Test-Path $vswhere)){throw '全屏组件需要 Visual Studio C++ Build Tools 和 Windows SDK'}
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw '未找到 x64 C++ 编译工具'}
$vcvars=Join-Path $vs 'VC/Auxiliary/Build/vcvars64.bat'
$source=Join-Path $PSScriptRoot 'src/fullscreen'
$batch=@"
@echo off
call "$vcvars" >nul
if errorlevel 1 exit /b 1
cd /d "$out"
cl /nologo /O2 /MT /LD /I "$source\minhook\include" "$source\Overlay.cpp" "$source\minhook\src\buffer.c" "$source\minhook\src\hook.c" "$source\minhook\src\trampoline.c" "$source\minhook\src\hde\hde64.c" /link /OUT:RxFullscreen.dll user32.lib d3d9.lib
if errorlevel 1 exit /b 1
cl /nologo /O2 /MT "$source\Load.cpp" /link /OUT:RxFullscreenLoader.exe
"@
$batch | Set-Content (Join-Path $out 'build.cmd') -Encoding Default
& (Join-Path $out 'build.cmd')
if($LASTEXITCODE -ne 0){throw '全屏组件构建失败'}

if($Test){
 $testBatch=@"
@echo off
call "$vcvars" >nul
cd /d "$out"
cl /nologo /O2 /MT "$PSScriptRoot\tests\FullscreenTexture.cpp" /link /OUT:FullscreenTexture.exe user32.lib d3d9.lib
if errorlevel 1 exit /b 1
FullscreenTexture.exe
"@
 $testBatch | Set-Content (Join-Path $out 'test.cmd') -Encoding Default
 & (Join-Path $out 'test.cmd')
 if($LASTEXITCODE -eq 77){Write-Warning 'Direct3D9Ex texture regression skipped: runtime unavailable in this session'}
 elseif($LASTEXITCODE -ne 0){throw 'Direct3D9Ex texture regression failed'}
}
