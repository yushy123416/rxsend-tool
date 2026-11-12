param([string]$Version='1.0.0',[string]$Repository='yushy123416/rxsend-tool',[string]$GiteeRepository='ysyyyy/rxsend-tool')
$ErrorActionPreference='Stop'
$readme=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Raw -Encoding UTF8
function Get-ReadmeSection([string]$heading){
    $pattern='(?ms)^## '+[regex]::Escape($heading)+'\r?\n(.*?)(?=^## |\z)'
    $match=[regex]::Match($readme,$pattern)
    if(-not $match.Success){throw "README section missing: $heading"}
    return $match.Groups[1].Value.Trim()
}
$features=Get-ReadmeSection '功能特性'
$usage=Get-ReadmeSection '使用方法'
$license=Get-ReadmeSection '开源协议与致谢'
$license=$license.Replace('](./',"](https://github.com/$Repository/blob/v$Version/")
$requirements=[regex]::Match($readme,'(?m)^> 系统要求：.*$').Value.Trim()
$description=[regex]::Match($readme,'(?m)^\*\*RXSEND.*\*\*$').Value.Trim()
$body=@"
# RXSend Tool v$Version

$description

## 功能特性

$features

## 下载

- [RXSendTool.exe · Gitee 镜像](https://gitee.com/$GiteeRepository/raw/main/updates/v$Version/RXSendTool.exe)
- [RXSendTool.exe · GitHub](https://github.com/$Repository/releases/download/v$Version/RXSendTool.exe)
- [Windows 完整发布包](https://github.com/$Repository/releases/download/v$Version/RxToolbox-$Version-Windows.zip)

$requirements

## 使用方法

$usage

## 开源协议与致谢

$license
"@
$folder=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $folder | Out-Null
$output=Join-Path $folder 'release-notes.md'
[IO.File]::WriteAllText($output,$body,[Text.UTF8Encoding]::new($false))
Write-Output $output
