param([switch]$Test,[string]$OutputDirectory='dist')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root $OutputDirectory
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Force $dist | Out-Null
New-Item -ItemType Directory -Force (Join-Path $root 'build') | Out-Null
& (Join-Path $root 'build-fullscreen.ps1') -Test:$Test
$overlayResources=@("/resource:$root/build/fullscreen/RxFullscreen.dll,RxToolbox.RxFullscreen.dll","/resource:$root/build/fullscreen/RxFullscreenLoader.exe,RxToolbox.RxFullscreenLoader.exe")
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName)
$refs = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:System.Numerics.dll','/r:System.Net.Http.dll')
& $compiler /nologo /optimize+ /target:winexe /platform:x86 /utf8output /main:RxToolbox.Program "/win32manifest:$root/src/app.manifest" "/win32icon:$root/assets/app.ico" "/resource:$root/assets/app.png,RxToolbox.Brand" "/resource:$root/LICENSE,RxToolbox.LICENSE" "/resource:$root/THIRD-PARTY-NOTICES.md,RxToolbox.NOTICES" "/out:$root/build/RxToolbox.exe" @overlayResources @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Test) {
    $testSource = Join-Path $root 'tests\Tests.cs'
    & $compiler /nologo /optimize+ /target:exe /utf8output /main:RxToolbox.Tests "/out:$root/build/RxToolbox.Tests.exe" @overlayResources @refs @sources $testSource
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & "$root/build/RxToolbox.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
Copy-Item "$root/LICENSE" "$dist/LICENSE" -Force
Copy-Item "$root/THIRD-PARTY-NOTICES.md" "$dist/THIRD-PARTY-NOTICES.md" -Force
Copy-Item "$root/build/RxToolbox.exe" "$dist/RXSendTool.exe" -Force
& "$root/tests/VerifyEndpointPrivacy.ps1" -ArtifactPath "$dist/RXSendTool.exe"
foreach ($legacyName in @('RxToolbox.exe','RxToolbox.Tests.exe')) {
    $legacyFile=Join-Path $dist $legacyName
    if (Test-Path -LiteralPath $legacyFile) {
        New-Item -ItemType Directory -Force "$root/build/legacy-dist" | Out-Null
        Move-Item -LiteralPath $legacyFile -Destination (Join-Path "$root/build/legacy-dist" $legacyName) -Force
    }
}
$releaseVersion=[Reflection.AssemblyName]::GetAssemblyName("$dist/RXSendTool.exe").Version.ToString(3)
Compress-Archive -Path "$dist/RXSendTool.exe","$dist/LICENSE","$dist/THIRD-PARTY-NOTICES.md" -DestinationPath "$dist/RxToolbox-$releaseVersion-Windows.zip" -Force
if (-not ('RxToolbox.BuildShellNotify' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
namespace RxToolbox {
    public static class BuildShellNotify {
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
        public static extern void SHChangeNotify(int change, uint flags, string path, IntPtr unused);
    }
}
'@
}
foreach ($binaryName in @('RXSendTool.exe')) {
    [RxToolbox.BuildShellNotify]::SHChangeNotify(0x2000,0x1005,(Join-Path $dist $binaryName),[IntPtr]::Zero)
}
[RxToolbox.BuildShellNotify]::SHChangeNotify(0x1000,0x1005,$dist,[IntPtr]::Zero)
Get-Item "$dist/RXSendTool.exe" | Select-Object Name,Length
