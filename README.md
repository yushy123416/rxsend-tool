<div align="center">

<h1>RXSend Tool</h1>

**RXSEND 轻量游戏工具箱 · 回合计时 · 屏幕准星 · 自动进服**

查看服务器回合状态，自定义悬浮计时与准星，在独立页面监听空位并通过 Steam 发起进服。

[![License](https://img.shields.io/badge/LICENSE-GPL--3.0-blue.svg?style=for-the-badge)](./LICENSE)
[![Release](https://img.shields.io/github/v/release/yushy123416/rxsend-tool?style=for-the-badge)](https://github.com/yushy123416/rxsend-tool/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-blue.svg?style=for-the-badge)](#下载)
[![Built with](https://img.shields.io/badge/built_with-C%23%20%2B%20WinForms-512BD4.svg?style=for-the-badge)](#技术实现)

**简体中文**

</div>

---

## 目录

- [功能特性](#功能特性)
- [工作流程](#工作流程)
- [下载](#下载)
- [使用方法](#使用方法)
- [技术实现](#技术实现)
- [从源码构建](#从源码构建)
- [常见问题](#常见问题)
- [更新日志](#更新日志)
- [开源协议与致谢](#开源协议与致谢)

## 功能特性

1. **回合状态与倒计时：** 同步服务器当前阶段、剩余回合与倒计时，结合 A2S 查询展示人数、地图和延迟；支持时间补偿。

2. **可自由编辑的悬浮计时：** 调整字体、字号、颜色、背景不透明度和圆角，在画布上拖动、缩放或隐藏各个组件。透明度只作用于背景，文字保持清晰；浮窗可独立下拉选服，也可跟随主界面。

3. **独立自动进服：** 单独选择目标服务器，每 5 秒检测一次空位，发现空位后通过 Steam 发起一次连接。支持取消和查询失败重试，与回合计时并行，切换页面不会中断任务。

4. **屏幕准星：** 支持当前 CS2、旧版 CSGO 与无畏契约主准星码导入，可调整形状、颜色、长度、粗细、间距、描边、缩放与偏移，选择显示器并保存预设。

5. **托盘与全局快捷键：** 主窗口默认关闭到托盘，准星鼠标穿透、不抢焦点；通过快捷键控制准星和计时浮窗，托盘可取消自动进服或退出程序。

6. **轻量原生界面：** 使用 C#、WinForms 和 GDI+，采用浅色 Fluent 风格，无 Electron、WebView 或浏览器runtime。

## 工作流程

```text
回合计时：选择服务器 → 同步回合状态 → 主界面 / 悬浮窗倒计时
                                 └→ 自定义字体、背景与组件布局

自动进服：选择目标服务器 → 开始监听空位 → 发现空位 → Steam 发起连接
                                      └→ 可随时取消

屏幕准星：导入准星码 → 实时预览与调整 → 选择显示器 → 开启准星
```

三个功能独立使用，可以同时运行。

## 下载

前往 [Releases](https://github.com/yushy123416/rxsend-tool/releases) 获取已发布版本。暂无发行版时，可以按下文从源码构建。

| 文件 | 用途 |
| --- | --- |
| `RXSendTool.exe` | 单文件程序，直接运行 |
| `RxToolbox-<版本号>-Windows.zip` | 程序与许可证的完整发布包 |

> 系统要求：Windows 10 / 11，.NET Framework 4.8。当前构建为 x86，可在 32 位和 64 位 Windows 上运行；自动进服需要 Steam 可用。

## 使用方法

### 回合计时与悬浮窗

1. 打开“回合计时”，在服务器列表中选择服务器。
2. 点击“开启悬浮计时”，将浮窗拖动到合适位置。
3. 点击“悬浮窗样式”，调整字体、各组件字号、颜色、背景不透明度、显隐与布局，保存后生效。
4. 点击浮窗名称旁的下拉箭头，独立选择服务器；也可以右键打开菜单，选择“跟随主界面选择”。

### 自动进服

1. 打开左侧“自动进服”，选择目标服务器。
2. 点击“开始自动进服”，程序在后台定时检测空位。
3. 发现空位后向 Steam 发送一次进服请求；等待期间可点击“取消自动进服”，或从托盘取消。

自动进服目标独立于回合计时页面。关闭到托盘后任务继续，退出程序后取消，下次启动不会自动恢复。

### 屏幕准星

1. 打开“屏幕准星”，粘贴 CS2、CSGO 或无畏契约准星码并导入。
2. 在预览区确认外观，调整参数、颜色、缩放和偏移。
3. 选择显示器并开启准星，需要时保存为预设。

| 快捷键 | 操作 |
| --- | --- |
| `Ctrl + Alt + F8` | 显示 / 隐藏准星 |
| `Ctrl + Alt + F9` | 显示 / 隐藏回合悬浮窗 |

设置、浮窗布局和准星预设保存在 `%LOCALAPPDATA%\RxToolbox\settings.json`。

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
.\build.ps1 -Test
.\tests\UpdateSmoke.ps1
```

使用 Windows 自带的 .NET Framework C# 编译器，无需下载 .NET SDK 或 NuGet 包。程序输出到 `dist/RXSendTool.exe`，测试和中间产物位于 `build/`。



GitHub Actions 会构建、测试并创建草稿 Release，包含 EXE、发布压缩包和 `update.json`。审阅后发布正式 Release，旧版程序即可检查到更新；标签须与程序集版本一致。

程序优先从 [Gitee 镜像](https://gitee.com/ysyyyy/rxsend-tool) 获取更新清单与程序，失败时回退 GitHub。Gitee 自动拉取 GitHub 仓库中的 `updates/` 正式更新载荷，不依赖 Releases 附件同步。

需要手动生成清单时运行：

```powershell
.\publish-manifest.ps1
```

生成后将 `updates/` 与源码一并提交，再推送版本标签；CI 发布该目录中已校验的同一份 EXE，保证镜像与 GitHub 下载文件的 SHA-256 一致。

## 常见问题


<details>
<summary><b>检测到空位，为什么仍未进入游戏？</b></summary>

自动进服只检测公开人数并向 Steam 发起连接，不会保留空位。Steam 状态、连接期间其他玩家加入，以及服务器验证或预留席位要求都会影响结果。“已向 Steam 发送进服请求”不代表已经进入服务器。

</details>

<details>
<summary><b>游戏里看不到准星或计时浮窗？</b></summary>

优先使用无边框窗口模式。独占全屏或游戏自身的叠加限制可能遮挡外置窗口；快捷键被占用时，可使用主界面或托盘操作。

</details>

<details>
<summary><b>导入准星后，为什么与游戏内不完全一致？</b></summary>

工具显示静态主准星，不模拟开火、移动扩散、后坐力或 ADS 切换。部分旧版尺寸及特殊形状为近似显示，可使用预览区继续调整。

</details>

<details>
<summary><b>检查更新提示“尚未发布可用更新”？</b></summary>

更新源需要正式 Release 中的 `update.json` 和 `RXSendTool.exe`。仅推送源码、创建标签或生成草稿还不够，需要发布 Release；同版本不会提示升级。

</details>

<details>
<summary><b>关闭窗口后，程序为什么还在运行？</b></summary>

默认关闭主窗口会留在系统托盘，浮窗与自动进服任务可以继续运行。需要完全退出时，右键托盘图标选择“退出”。

</details>

## 更新日志

### v1.0.0

- 回合状态、悬浮倒计时与自由布局编辑。
- 多格式准星导入、实时预览和预设保存。
- 独立自动进服页面，与回合计时并行。
- 浅色原生界面、托盘、全局快捷键与 GitHub 更新。

完整变更记录见 [CHANGELOG.md](./CHANGELOG.md)。

## 开源协议与致谢

项目代码采用 **GPL-3.0-only**，完整许可见 [LICENSE](./LICENSE)。

- Counter-Strike 准星码格式参考 [AkiVer / csgo-sharecode](https://github.com/akiver/csgo-sharecode)，保留原 MIT 许可声明。
- 应用图标基于 SCP 基金会标志，原作者 far2，高分辨率版由 Aelanna 制作；图标衍生作品采用 CC BY-SA 3.0。
- 第三方来源、修改与许可说明见 [THIRD-PARTY-NOTICES.md](./THIRD-PARTY-NOTICES.md)。
