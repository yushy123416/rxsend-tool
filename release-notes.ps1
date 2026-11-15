param([string]$Version='1.0.2',[string]$Repository='yushy123416/rxsend-tool',[string]$GiteeRepository='ysyyyy/rxsend-tool')
$ErrorActionPreference='Stop'
$readme=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Raw -Encoding UTF8
function Get-ReadmeSection([string]$heading){
    $pattern='(?ms)^## '+[regex]::Escape($heading)+'\r?\n(.*?)(?=^## |\z)'
    $match=[regex]::Match($readme,$pattern)
    if(-not $match.Success){throw "README section missing: $heading"}
    return $match.Groups[1].Value.Trim()
}
$changes=[regex]::Match((Get-ReadmeSection '更新日志'),'(?ms)^### v'+[regex]::Escape($Version)+'(?:[^\r\n]*)\r?\n(.*?)(?=^### |\z)').Groups[1].Value.Trim()
if(-not $changes){throw "README release notes missing: v$Version"}
$requirements=[regex]::Match($readme,'(?m)^> 系统要求：.*$').Value.Trim()
$description=[regex]::Match($readme,'(?m)^\*\*RXSEND.*\*\*\r?$').Value.Trim()
if(-not $description){throw 'README product description missing'}
$body=@"
# RXSend Tool v$Version

$description

## 本次更新

$changes

## 下载

- [RXSendTool.exe · Gitee 镜像](https://gitee.com/$GiteeRepository/raw/main/updates/v$Version/RXSendTool.exe)
- [RXSendTool.exe · GitHub](https://github.com/$Repository/releases/download/v$Version/RXSendTool.exe)
- [Windows 完整发布包](https://github.com/$Repository/releases/download/v$Version/RxToolbox-$Version-Windows.zip)

$requirements

## 升级说明

- 已安装用户可通过程序自动更新，或退出旧版后直接运行新版 EXE。
- 首次启动会迁移原有连接设置与服务器选择；自定义信息接口的加密设置绑定当前 Windows 用户。
- 重复启动时，可选择关闭上一个程序再打开当前版本；旧实例的监听任务与悬浮窗会停止。
- 玩家在线时长为服务器返回的本次连接时长，不代表 Steam 累计游戏时间；服务器未开放玩家查询时无法展示明细。
- 地址加密与混淆用于降低意外暴露和静态提取的便利性，不能阻止运行时分析或网络连接观察，也不会清除旧版本已公开的地址。
- NVIDIA 滤镜仍为实验性功能，配置成功不代表所有驱动、游戏或滤镜组合均可用。

## 文档与许可

- [使用说明](https://github.com/$Repository/blob/v$Version/README.md)
- [更新日志](https://github.com/$Repository/blob/v$Version/README.md#更新日志)
- 项目采用 [GPL-3.0-only](https://github.com/$Repository/blob/v$Version/LICENSE)，第三方来源及许可见 [THIRD-PARTY-NOTICES.md](https://github.com/$Repository/blob/v$Version/THIRD-PARTY-NOTICES.md)。
"@
$folder=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $folder | Out-Null
$output=Join-Path $folder 'release-notes.md'
[IO.File]::WriteAllText($output,$body,[Text.UTF8Encoding]::new($false))
Write-Output $output
